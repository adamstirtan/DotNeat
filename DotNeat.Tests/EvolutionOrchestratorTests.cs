using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class EvolutionOrchestratorTests
{
    [TestMethod]
    public void Run_ExecutesFullGenerationLoop_AndTracksHistory()
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        EvolutionOptions options = new(
            PopulationSize: 12,
            GenerationCount: 5,
            CompatibilityThreshold: 1.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                CrossoverProbability: 0.5,
                MutationProbability: 1,
                WeightMutationProbability: 1,
                AddConnectionMutationProbability: 0,
                AddNodeMutationProbability: 0,
                ToggleConnectionMutationProbability: 0),
            InitialGenomeFactory: (rng, tracker) => CreateInitialGenome(rng, tracker, input, output),
            Seed: 123);

        EvolutionOrchestrator orchestrator = new(EvaluateByWeight, options);

        EvolutionResult result = orchestrator.Run();

        Assert.HasCount(5, result.History);
        Assert.HasCount(12, result.FinalPopulation);
        Assert.IsGreaterThan(double.MinValue, result.BestFitness);
        Assert.IsTrue(result.History.All(h => h.SpeciesCount >= 1));
        Assert.IsTrue(result.History.All(h => h.AverageComplexity >= 3d));
    }

    private static double EvaluateByWeight(Genome genome)
    {
        return genome.Connections.Sum(c => c.Weight);
    }

    private static Genome CreateInitialGenome(Random rng, InnovationTracker tracker, Guid input, Guid output)
    {
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        int innovation = tracker.GetOrCreateConnectionInnovation(input, output);
        double weight = -1d + (2d * rng.NextDouble());

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, weight, true, innovation));
        return genome;
    }
}
