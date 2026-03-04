namespace DotNeat.Runner.Experiments;

public interface IExperiment
{
    string Name { get; }

    void Run();
}
