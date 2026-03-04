using DotNeat;
using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Experiments;

public sealed class XorExperiment(int seed = 31337) : IExperiment
{
    private readonly int _seed = seed;

    public string Name => "xor";

    public void Run()
    {
        XorFitnessEvaluator evaluator = new(_seed);

        Guid inputA = Guid.NewGuid();
        Guid inputB = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        EvolutionOptions options = new(
            PopulationSize: 100,
            GenerationCount: 40,
            CompatibilityThreshold: 2.5,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.75,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.7,
                AddConnectionMutationProbability: 0.15,
                AddNodeMutationProbability: 0.1,
                ToggleConnectionMutationProbability: 0.05,
                WeightPerturbChance: 0.9,
                WeightPerturbScale: 0.4,
                WeightResetMin: -2,
                WeightResetMax: 2),
            InitialGenomeFactory: (rng, tracker) => CreateGenome(rng, tracker, inputA, inputB, output),
            Seed: _seed);

        Console.WriteLine($"Seed: {_seed}");
        Console.WriteLine("Generation | BestFitness | AvgFitness | Species | AvgComplexity");

        SqliteExperimentRunPersistence persistence = new();
        EvolutionRunContext runContext = new(
            ExperimentName: Name,
            Seed: _seed,
            ConfigJson: ExperimentConfigSerializer.Serialize(options));

        EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);
        EvolutionResult result = orchestrator.Run(
            onGenerationCompleted: metrics =>
            {
                if (metrics.Generation % 5 == 0 || metrics.Generation == options.GenerationCount - 1)
                {
                    Console.WriteLine($"{metrics.Generation,10} | {metrics.BestFitness,11:F6} | {metrics.AverageFitness,10:F6} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2}");
                }
            },
            runPersistence: persistence,
            runContext: runContext);

        Console.WriteLine();
        Console.WriteLine($"Best fitness: {result.BestFitness:F6} / 4.000000");
        Console.WriteLine($"Results database: {persistence.DatabasePath}");
    }

    private static Genome CreateGenome(Random rng, InnovationTracker tracker, Guid inputA, Guid inputB, Guid output)
    {
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(inputA, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputB, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        int innovationA = tracker.GetOrCreateConnectionInnovation(inputA, output);
        int innovationB = tracker.GetOrCreateConnectionInnovation(inputB, output);

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputA, output, NextWeight(rng), true, innovationA));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputB, output, NextWeight(rng), true, innovationB));

        return genome;
    }

    private static double NextWeight(Random rng)
    {
        return -1d + (2d * rng.NextDouble());
    }
}
