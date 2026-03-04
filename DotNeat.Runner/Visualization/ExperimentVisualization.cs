using DotNeat;
using System.Text;

namespace DotNeat.Runner.Visualization;

public static class ExperimentVisualization
{
    public static string WriteEvolutionReport(
        string experimentName,
        int seed,
        IReadOnlyList<GenerationMetrics> history,
        double? goalFitness = null,
        string? goalLabel = null,
        Func<GenerationMetrics, double>? additionalMetricSelector = null,
        string? additionalMetricLabel = null)
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
        File.WriteAllText(htmlPath, BuildHtml(experimentName, seed, history, goalFitness, goalLabel, additionalMetricSelector, additionalMetricLabel));

        return htmlPath;
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
        string? additionalMetricLabel)
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
</body>
</html>
""";
    }

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
