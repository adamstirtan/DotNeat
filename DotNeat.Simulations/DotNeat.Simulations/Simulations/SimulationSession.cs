using System.Collections.Generic;
using System.Threading.Tasks;
using DotNeat;

namespace DotNeat.Simulations.Experiments;

public sealed class SimulationSession
{
    private readonly SimulationScenarioBase _scenario;
    private readonly List<GenerationMetrics> _history = new();

    public SimulationSession(SimulationScenarioBase scenario)
    {
        _scenario = scenario;
    }

    public IReadOnlyList<GenerationMetrics> History => _history;

    public SimulationTrace? LatestTrace { get; private set; }

    public bool IsRunning { get; private set; }

    public string StatusMessage { get; private set; } = "Idle";

    public async Task RunAsync(int seed, Func<Task>? notifyStateChanged = null)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        StatusMessage = "Running...";
        _history.Clear();
        LatestTrace = null;

        Notify(notifyStateChanged);

        try
        {
            await Task.Run(() =>
            {
                EvolutionOptions options = _scenario.CreateEvolutionOptions(seed);
                EvolutionOrchestrator orchestrator = new(
                    genome => _scenario.EvaluateGenome(genome, seed, captureFrames: false).Fitness,
                    options);

                orchestrator.Run(
                    onGenerationCompleted: metrics =>
                    {
                        _history.Add(metrics);
                        Notify(notifyStateChanged);
                    },
                    onGenerationChampionCaptured: (_, champion) =>
                    {
                        LatestTrace = _scenario.EvaluateGenome(champion, seed, captureFrames: true);
                        Notify(notifyStateChanged);
                    });
            });

            StatusMessage = "Completed";
        }
        catch (Exception ex)
        {
            StatusMessage = "Simulation failed. See console for details.";
            Console.Error.WriteLine(ex);
        }
        finally
        {
            IsRunning = false;
            Notify(notifyStateChanged);
        }
    }

    private static void Notify(Func<Task>? notifyStateChanged)
    {
        // Invoke the UI notification asynchronously and do not block the caller.
        // Blocking here (e.g. via GetAwaiter().GetResult) can deadlock in single-threaded
        // environments such as Blazor WebAssembly because the UI callback runs on the
        // synchronization context that the caller may be occupying. Fire-and-forget
        // the task so background work can continue and the UI update will be scheduled.
        _ = notifyStateChanged?.Invoke();
    }
}
