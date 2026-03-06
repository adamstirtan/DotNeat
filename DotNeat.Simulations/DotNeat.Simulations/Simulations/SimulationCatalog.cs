using System;

namespace DotNeat.Simulations.Experiments;

public sealed record SimulationCatalogEntry(
    string Id,
    string Title,
    string Description,
    string Complexity,
    string Route);

public static class SimulationCatalog
{
    public static IReadOnlyList<SimulationCatalogEntry> Entries { get; } = new[]
    {
        new SimulationCatalogEntry(
            Id: "corridor-collector",
            Title: "Corridor Collector",
            Description: "A linear collector that races to pick up tokens along a track.",
            Complexity: "Simple",
            Route: "/simulations/corridor-collector"),
        new SimulationCatalogEntry(
            Id: "obstacle-navigator",
            Title: "Obstacle Navigator",
            Description: "Dodge dynamic obstacles to reach the distant goal.",
            Complexity: "Medium",
            Route: "/simulations/obstacle-navigator"),
        new SimulationCatalogEntry(
            Id: "resource-arena",
            Title: "Resource Arena",
            Description: "Multi-agent competition for scarce resources with collisions and scoring.",
            Complexity: "Complex",
            Route: "/simulations/resource-arena"),
    };

    public static SimulationScenarioBase CreateScenario(string id) => id switch
    {
        "corridor-collector" => new CorridorCollectorScenario(),
        "obstacle-navigator" => new ObstacleNavigatorScenario(),
        "resource-arena" => new ResourceArenaScenario(),
        _ => throw new InvalidOperationException($"Unknown simulation id: {id}"),
    };
}
