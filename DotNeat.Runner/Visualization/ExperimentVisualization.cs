using DotNeat;
using System.Text;
using System.Text.Json;

namespace DotNeat.Runner.Visualization;

public sealed record NetworkSnapshot(int Generation, Genome Genome);

public static class ExperimentVisualization
{
    public static string WriteEvolutionReport(
        string experimentName,
        int seed,
        IReadOnlyList<GenerationMetrics> history,
        double? goalFitness = null,
        string? goalLabel = null,
        Func<GenerationMetrics, double>? additionalMetricSelector = null,
        string? additionalMetricLabel = null,
        IReadOnlyList<NetworkSnapshot>? networkSnapshots = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentName);
        ArgumentNullException.ThrowIfNull(history);

        if (history.Count == 0)
        {
            throw new InvalidOperationException("Cannot build visualization report for empty history.");
        }

        string visualizationRoot = ResolveVisualizationRoot();
        string root = Path.Combine(visualizationRoot, experimentName);
        string runDir = Path.Combine(root, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-seed{seed}");
        Directory.CreateDirectory(runDir);

        string csvPath = Path.Combine(runDir, "history.csv");
        File.WriteAllText(csvPath, BuildCsv(history, additionalMetricSelector, additionalMetricLabel));

        string htmlPath = Path.Combine(runDir, "report.html");
        File.WriteAllText(htmlPath, BuildHtml(experimentName, seed, history, goalFitness, goalLabel, additionalMetricSelector, additionalMetricLabel, networkSnapshots));

        return htmlPath;
    }

    public static HashSet<int> SelectSnapshotGenerations(int generationCount, int desiredSnapshotCount = 5)
    {
        if (generationCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(generationCount), "generationCount must be >= 1.");
        }

        int count = Math.Max(2, desiredSnapshotCount);
        HashSet<int> result = [0, generationCount - 1];

        if (generationCount == 1)
        {
            return result;
        }

        for (int i = 1; i < count - 1; i++)
        {
            int generation = (int)Math.Round(i * (generationCount - 1d) / (count - 1d));
            result.Add(generation);
        }

        return result;
    }

    private static string BuildCsv(
        IReadOnlyList<GenerationMetrics> history,
        Func<GenerationMetrics, double>? additionalMetricSelector,
        string? additionalMetricLabel)
    {
        StringBuilder sb = new();
        sb.Append("Generation,BestFitness,AverageFitness,SpeciesCount,AverageComplexity");

        bool hasAdditional = additionalMetricSelector is not null && !string.IsNullOrWhiteSpace(additionalMetricLabel);
        if (hasAdditional)
        {
            sb.Append(',').Append(additionalMetricLabel);
        }

        sb.AppendLine();

        foreach (GenerationMetrics m in history)
        {
            sb.Append(m.Generation).Append(',')
                .Append(m.BestFitness.ToString("F6")).Append(',')
                .Append(m.AverageFitness.ToString("F6")).Append(',')
                .Append(m.SpeciesCount).Append(',')
                .Append(m.AverageComplexity.ToString("F6"));

            if (hasAdditional)
            {
                sb.Append(',').Append(additionalMetricSelector!(m).ToString("F6"));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ResolveVisualizationRoot()
    {
        string? repoRoot = FindRepositoryRoot(AppContext.BaseDirectory)
            ?? FindRepositoryRoot(Environment.CurrentDirectory);

        string basePath = repoRoot ?? Environment.CurrentDirectory;
        return Path.Combine(basePath, "artifacts", "visualizations");
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(startPath));
        if (!directory.Exists)
        {
            return null;
        }

        while (directory is not null)
        {
            string fullName = directory.FullName;
            bool hasGit = Directory.Exists(Path.Combine(fullName, ".git"));
            bool hasSolution = File.Exists(Path.Combine(fullName, "DotNeat.slnx"));

            if (hasGit || hasSolution)
            {
                return fullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string BuildHtml(
        string experimentName,
        int seed,
        IReadOnlyList<GenerationMetrics> history,
        double? goalFitness,
        string? goalLabel,
        Func<GenerationMetrics, double>? additionalMetricSelector,
        string? additionalMetricLabel,
        IReadOnlyList<NetworkSnapshot>? networkSnapshots)
    {
        List<(int x, double y)> best = [.. history.Select(h => (h.Generation, h.BestFitness))];
        List<(int x, double y)> avg = [.. history.Select(h => (h.Generation, h.AverageFitness))];
        List<(int x, double y)> species = [.. history.Select(h => (h.Generation, (double)h.SpeciesCount))];
        List<(int x, double y)> complexity = [.. history.Select(h => (h.Generation, h.AverageComplexity))];

        string fitnessChart = BuildLineChartSvg(
            "Fitness over generations",
            [
                ("Best fitness", "#2e7d32", best),
                ("Average fitness", "#1565c0", avg),
            ],
            goalFitness,
            goalLabel);

        string speciesChart = BuildLineChartSvg(
            "Species count",
            [("Species", "#6a1b9a", species)]);

        string complexityChart = BuildLineChartSvg(
            "Average complexity",
            [("Complexity", "#ef6c00", complexity)]);

        string additionalSection = string.Empty;
        if (additionalMetricSelector is not null && !string.IsNullOrWhiteSpace(additionalMetricLabel))
        {
            List<(int x, double y)> additional = [.. history.Select(h => (h.Generation, additionalMetricSelector(h)))];
            additionalSection = BuildLineChartSvg(
                additionalMetricLabel,
                [(additionalMetricLabel, "#00897b", additional)]);
        }

        string networkSection = BuildNetworkSection(networkSnapshots);

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>{{experimentName}} evolution report</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }
    h1 { margin-bottom: 4px; }
    .meta { color: #6b7280; margin-bottom: 16px; }
    .chart { margin: 16px 0 28px 0; border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px; }
    .caption { font-weight: 600; margin-bottom: 8px; }
    .legend { font-size: 12px; color: #4b5563; margin-top: 6px; }
  </style>
</head>
<body>
  <h1>{{experimentName}} evolution visualization</h1>
  <div class="meta">Seed: {{seed}} | Generations: {{history.Count}}</div>

  <div class="chart">
    <div class="caption">Fitness progression</div>
    {{fitnessChart}}
  </div>

  <div class="chart">
    <div class="caption">Speciation behavior</div>
    {{speciesChart}}
  </div>

  <div class="chart">
    <div class="caption">Structural growth</div>
    {{complexityChart}}
  </div>

  {{(string.IsNullOrEmpty(additionalSection) ? string.Empty : $"<div class=\"chart\"><div class=\"caption\">Task-specific progress</div>{additionalSection}</div>")}}

  {{networkSection}}
</body>
</html>
""";
    }

    private static string BuildNetworkSection(IReadOnlyList<NetworkSnapshot>? networkSnapshots)
    {
        if (networkSnapshots is null || networkSnapshots.Count == 0)
        {
            return string.Empty;
        }

        List<NetworkSnapshotDto> snapshots =
        [
            .. networkSnapshots
                .OrderBy(s => s.Generation)
                .Select(ToDto)
        ];

        string snapshotJson = JsonSerializer.Serialize(
            snapshots,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        StringBuilder canvases = new();
        for (int i = 0; i < snapshots.Count; i++)
        {
            canvases.AppendLine($"<div style=\"margin: 10px 0;\"><canvas id=\"networkCanvas{i}\" width=\"900\" height=\"420\" style=\"border:1px solid #e5e7eb;border-radius:6px;width:100%;height:auto;\"></canvas></div>");
        }

        return $$"""
<div class="chart">
  <div class="caption">Network structure snapshots (champion genomes)</div>
  <div class="meta">Inputs are blue, hidden are amber, outputs are green. Edge color encodes weight sign (green positive, red negative).</div>
  {{canvases}}
</div>
<script>
(() => {
  const snapshots = {{snapshotJson}};

  function drawSnapshot(canvasId, snapshot) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;
    const pad = 32;
    ctx.clearRect(0, 0, width, height);

    const nodes = snapshot.nodes;
    const edges = snapshot.connections.filter(c => c.enabled);

    const nodeMap = new Map(nodes.map(n => [n.id, n]));
    const outgoing = new Map();
    const indegree = new Map();
    const level = new Map();

    for (const n of nodes) {
      outgoing.set(n.id, []);
      indegree.set(n.id, 0);
      level.set(n.id, n.nodeType === 'Input' ? 0 : 1);
    }

    for (const e of edges) {
      if (!outgoing.has(e.inputNodeId) || !indegree.has(e.outputNodeId)) continue;
      outgoing.get(e.inputNodeId).push(e.outputNodeId);
      indegree.set(e.outputNodeId, indegree.get(e.outputNodeId) + 1);
    }

    const queue = [];
    for (const [id, d] of indegree.entries()) {
      if (d === 0) queue.push(id);
    }

    while (queue.length > 0) {
      const current = queue.shift();
      const currentLevel = level.get(current) ?? 0;
      for (const next of outgoing.get(current) ?? []) {
        level.set(next, Math.max(level.get(next) ?? 0, currentLevel + 1));
        indegree.set(next, indegree.get(next) - 1);
        if (indegree.get(next) === 0) queue.push(next);
      }
    }

    let maxLevel = 0;
    for (const l of level.values()) maxLevel = Math.max(maxLevel, l);
    for (const n of nodes) {
      if (n.nodeType === 'Output') level.set(n.id, maxLevel + 1);
    }
    maxLevel += 1;

    const byLevel = new Map();
    for (const n of nodes) {
      const l = level.get(n.id) ?? 0;
      if (!byLevel.has(l)) byLevel.set(l, []);
      byLevel.get(l).push(n);
    }

    const pos = new Map();
    for (let l = 0; l <= maxLevel; l++) {
      const group = byLevel.get(l) ?? [];
      const x = pad + (maxLevel === 0 ? 0.5 : l / maxLevel) * (width - (2 * pad));
      for (let i = 0; i < group.length; i++) {
        const y = pad + ((i + 1) / (group.length + 1)) * (height - (2 * pad));
        pos.set(group[i].id, { x, y });
      }
    }

    for (const e of edges) {
      const a = pos.get(e.inputNodeId);
      const b = pos.get(e.outputNodeId);
      if (!a || !b) continue;
      const strength = Math.min(1, Math.abs(e.weight) / 3);
      const alpha = 0.25 + (0.65 * strength);
      ctx.strokeStyle = e.weight >= 0 ? `rgba(46,125,50,${alpha.toFixed(3)})` : `rgba(198,40,40,${alpha.toFixed(3)})`;
      ctx.lineWidth = 1 + (2 * strength);
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(b.x, b.y);
      ctx.stroke();
    }

    for (const n of nodes) {
      const p = pos.get(n.id);
      if (!p) continue;
      const color = n.nodeType === 'Input'
        ? '#1565c0'
        : (n.nodeType === 'Output' ? '#2e7d32' : '#ef6c00');
      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.arc(p.x, p.y, 9, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = '#111827';
      ctx.lineWidth = 1;
      ctx.stroke();
    }

    ctx.fillStyle = '#111827';
    ctx.font = 'bold 13px Segoe UI, Arial';
    ctx.fillText(`Generation ${snapshot.generation} | Nodes: ${nodes.length} | Enabled connections: ${edges.length}`, 16, 20);
  }

  snapshots.forEach((snapshot, i) => drawSnapshot(`networkCanvas${i}`, snapshot));
})();
</script>
""";
    }

    private static NetworkSnapshotDto ToDto(NetworkSnapshot snapshot)
    {
        List<NodeSnapshotDto> nodes =
        [
            .. snapshot.Genome.Nodes.Select(n => new NodeSnapshotDto(
                n.GeneId.ToString("D"),
                n.NodeType.ToString()))
        ];

        List<ConnectionSnapshotDto> connections =
        [
            .. snapshot.Genome.Connections.Select(c => new ConnectionSnapshotDto(
                c.InputNodeId.ToString("D"),
                c.OutputNodeId.ToString("D"),
                c.Weight,
                c.Enabled))
        ];

        return new NetworkSnapshotDto(snapshot.Generation, nodes, connections);
    }

    private sealed record NodeSnapshotDto(string Id, string NodeType);

    private sealed record ConnectionSnapshotDto(string InputNodeId, string OutputNodeId, double Weight, bool Enabled);

    private sealed record NetworkSnapshotDto(
        int Generation,
        IReadOnlyList<NodeSnapshotDto> Nodes,
        IReadOnlyList<ConnectionSnapshotDto> Connections);

    private static string BuildLineChartSvg(
        string title,
        IReadOnlyList<(string name, string color, List<(int x, double y)> points)> series,
        double? horizontalLine = null,
        string? horizontalLabel = null)
    {
        const int width = 920;
        const int height = 250;
        const int left = 50;
        const int right = 20;
        const int top = 20;
        const int bottom = 35;

        int xMin = series.Min(s => s.points.First().x);
        int xMax = series.Max(s => s.points.Last().x);

        double yMin = series.Min(s => s.points.Min(p => p.y));
        double yMax = series.Max(s => s.points.Max(p => p.y));

        if (horizontalLine.HasValue)
        {
            yMin = Math.Min(yMin, horizontalLine.Value);
            yMax = Math.Max(yMax, horizontalLine.Value);
        }

        if (Math.Abs(yMax - yMin) < 1e-9)
        {
            yMax = yMin + 1d;
        }

        double yPad = (yMax - yMin) * 0.1;
        yMin -= yPad;
        yMax += yPad;

        double PlotX(int x)
        {
            if (xMax == xMin)
            {
                return left;
            }

            return left + (double)(x - xMin) / (xMax - xMin) * (width - left - right);
        }

        double PlotY(double y)
        {
            return top + (yMax - y) / (yMax - yMin) * (height - top - bottom);
        }

        StringBuilder sb = new();
        sb.AppendLine($"<svg viewBox=\"0 0 {width} {height}\" width=\"100%\" height=\"260\" role=\"img\" aria-label=\"{title}\">");
        sb.AppendLine($"<line x1=\"{left}\" y1=\"{height - bottom}\" x2=\"{width - right}\" y2=\"{height - bottom}\" stroke=\"#9ca3af\" />");
        sb.AppendLine($"<line x1=\"{left}\" y1=\"{top}\" x2=\"{left}\" y2=\"{height - bottom}\" stroke=\"#9ca3af\" />");

        if (horizontalLine.HasValue)
        {
            double y = PlotY(horizontalLine.Value);
            sb.AppendLine($"<line x1=\"{left}\" y1=\"{y:F2}\" x2=\"{width - right}\" y2=\"{y:F2}\" stroke=\"#ef4444\" stroke-dasharray=\"6,4\" />");
            if (!string.IsNullOrWhiteSpace(horizontalLabel))
            {
                sb.AppendLine($"<text x=\"{left + 6}\" y=\"{y - 6:F2}\" font-size=\"11\" fill=\"#ef4444\">{horizontalLabel}</text>");
            }
        }

        foreach ((string name, string color, List<(int x, double y)> points) in series)
        {
            string polyline = string.Join(' ', points.Select(p => $"{PlotX(p.x):F2},{PlotY(p.y):F2}"));
            sb.AppendLine($"<polyline fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" points=\"{polyline}\" />");
        }

        for (int i = 0; i < series.Count; i++)
        {
            int y = height - 10 - (i * 14);
            sb.AppendLine($"<rect x=\"{left + 6}\" y=\"{y - 9}\" width=\"10\" height=\"10\" fill=\"{series[i].color}\" />");
            sb.AppendLine($"<text x=\"{left + 22}\" y=\"{y}\" font-size=\"11\" fill=\"#374151\">{series[i].name}</text>");
        }

        sb.AppendLine($"<text x=\"{left}\" y=\"{height - 6}\" font-size=\"11\" fill=\"#6b7280\">Gen {xMin}</text>");
        sb.AppendLine($"<text x=\"{width - right - 55}\" y=\"{height - 6}\" font-size=\"11\" fill=\"#6b7280\">Gen {xMax}</text>");
        sb.AppendLine($"<text x=\"{left + 4}\" y=\"{top + 12}\" font-size=\"11\" fill=\"#6b7280\">{yMax:F2}</text>");
        sb.AppendLine($"<text x=\"{left + 4}\" y=\"{height - bottom - 4}\" font-size=\"11\" fill=\"#6b7280\">{yMin:F2}</text>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
