namespace DotNeat.Simulations.Experiments;

public sealed record SimulationActor(string Id, double X, double Y, string Label, string Color, double Size = 8d);

public sealed record SimulationEntity(double X, double Y, string Label, string Color, double Size = 6d);

public sealed record SimulationFrame(
    int Step,
    IReadOnlyList<SimulationActor> Actors,
    IReadOnlyList<SimulationEntity> Entities,
    string? Message = null);

public sealed record SimulationTrace(
    double Fitness,
    IReadOnlyList<SimulationFrame> Frames,
    string Summary);
