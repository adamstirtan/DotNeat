using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace DotNeat.Runner.Net;

/// <summary>
/// Minimal HTTP + WebSocket server that:
/// <list type="bullet">
///   <item><description>Serves <c>index.html</c> from an embedded resource on <c>GET /</c>.</description></item>
///   <item><description>Accepts WebSocket connections on <c>/ws</c> and broadcasts JSON frames to all connected clients.</description></item>
///   <item><description>Reads <c>{"type":"setGoal","x":…,"y":…}</c> messages from clients and forwards them via the <paramref name="onGoalChanged"/> callback.</description></item>
/// </list>
/// Start with <see cref="Start"/>, enqueue messages with <see cref="EnqueueBroadcast"/>, and dispose when done.
/// </summary>
public sealed class LocalServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Action<double, double>? _onGoalChanged;
    private readonly Channel<string> _broadcastChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<WebSocket> _sockets = [];
    private readonly object _socketsLock = new();

    /// <summary>
    /// Initializes a new <see cref="LocalServer"/>.
    /// </summary>
    /// <param name="onGoalChanged">
    /// Optional callback invoked (from a background thread) when a client sends a
    /// <c>setGoal</c> message. Arguments are the new goal X and Y coordinates.
    /// </param>
    /// <param name="port">TCP port to listen on (default 5000).</param>
    public LocalServer(Action<double, double>? onGoalChanged = null, int port = 5000)
    {
        _onGoalChanged = onGoalChanged;
        _broadcastChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>Starts the HTTP listener and background processing tasks.</summary>
    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
        _ = Task.Run(BroadcastLoopAsync);
    }

    /// <summary>
    /// Enqueues a JSON string to be broadcast to all connected WebSocket clients.
    /// This method is thread-safe and non-blocking.
    /// </summary>
    public void EnqueueBroadcast(string json) => _broadcastChannel.Writer.TryWrite(json);

    /// <inheritdoc/>
    public void Dispose()
    {
        _broadcastChannel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException) { }

        _cts.Dispose();

        lock (_socketsLock)
        {
            foreach (WebSocket ws in _sockets)
            {
                try { ws.Dispose(); }
                catch { }
            }

            _sockets.Clear();
        }
    }

    // ── Accept loop ────────────────────────────────────────────────────────────

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                HttpListenerContext ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleContextAsync(ctx));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsCtx =
                    await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                await HandleWebSocketAsync(wsCtx.WebSocket).ConfigureAwait(false);
            }
            else
            {
                ServeHtml(ctx);
            }
        }
        catch { }
    }

    // ── HTTP ──────────────────────────────────────────────────────────────────

    private static void ServeHtml(HttpListenerContext ctx)
    {
        try
        {
            string? path = ctx.Request.Url?.AbsolutePath;
            if (path is "/" or "/index.html")
            {
                byte[]? content = LoadIndexHtml();
                if (content is not null)
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    ctx.Response.ContentLength64 = content.Length;
                    ctx.Response.OutputStream.Write(content);
                }
                else
                {
                    ctx.Response.StatusCode = 503;
                }
            }
            else
            {
                ctx.Response.StatusCode = 404;
            }
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    private static byte[]? LoadIndexHtml()
    {
        string resourceName = "DotNeat.Runner.www.index.html";
        using Stream? stream = typeof(LocalServer).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // ── WebSocket ─────────────────────────────────────────────────────────────

    private async Task HandleWebSocketAsync(WebSocket socket)
    {
        lock (_socketsLock)
        {
            _sockets.Add(socket);
        }

        byte[] buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result =
                    await socket.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleClientMessage(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            lock (_socketsLock)
            {
                _sockets.Remove(socket);
            }
        }
    }

    private void HandleClientMessage(string json)
    {
        if (_onGoalChanged is null)
        {
            return;
        }

        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
            System.Text.Json.JsonElement root = doc.RootElement;

            if (root.TryGetProperty("type", out System.Text.Json.JsonElement typeEl) &&
                typeEl.GetString() == "setGoal" &&
                root.TryGetProperty("x", out System.Text.Json.JsonElement xEl) &&
                root.TryGetProperty("y", out System.Text.Json.JsonElement yEl))
            {
                _onGoalChanged(xEl.GetDouble(), yEl.GetDouble());
            }
        }
        catch { }
    }

    // ── Broadcast loop ────────────────────────────────────────────────────────

    private async Task BroadcastLoopAsync()
    {
        await foreach (string json in _broadcastChannel.Reader.ReadAllAsync(_cts.Token)
            .ConfigureAwait(false))
        {
            await BroadcastAsync(json).ConfigureAwait(false);
        }
    }

    private async Task BroadcastAsync(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        List<WebSocket> dead = [];

        List<WebSocket> snapshot;
        lock (_socketsLock)
        {
            snapshot = [.. _sockets];
        }

        foreach (WebSocket socket in snapshot)
        {
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(data),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    dead.Add(socket);
                }
            }
            catch
            {
                dead.Add(socket);
            }
        }

        if (dead.Count > 0)
        {
            lock (_socketsLock)
            {
                foreach (WebSocket ws in dead)
                {
                    _sockets.Remove(ws);
                }
            }
        }
    }
}
