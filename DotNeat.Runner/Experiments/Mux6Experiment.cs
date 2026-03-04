using DotNeat;
using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Experiments;

public sealed class Mux6Experiment : IExperiment
{
    private readonly int _seed;

    public Mux6Experiment(int seed = 12345)
    {
        _seed = seed;
    }

    public string Name => "mux6";

    public void Run()
    {
        Mux6FitnessEvaluator evaluator = new();

        Guid a0 = Guid.NewGuid();
        Guid a1 = Guid.NewGuid();
        Guid d0 = Guid.NewGuid();
        Guid d1 = Guid.NewGuid();
        Guid d2 = Guid.NewGuid();
        Guid d3 = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        EvolutionOptions options = new(
            PopulationSize: 200,
            GenerationCount: 200,
            CompatibilityThreshold: 3.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.75,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.75,
                AddConnectionMutationProbability: 0.15,
                AddNodeMutationProbability: 0.05,
                ToggleConnectionMutationProbability: 0.05,
                WeightPerturbChance: 0.9,
                WeightPerturbScale: 0.5,
                WeightResetMin: -2,
                WeightResetMax: 2),
            InitialGenomeFactory: (rng, tracker) =>
                CreateGenome(rng, tracker, a0, a1, d0, d1, d2, d3, output),
            Seed: _seed);

        EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);

        Console.WriteLine($"MUX-6 Experiment | Seed: {_seed}");
        Console.WriteLine("Task: 2-bit address + 4-bit data (64 cases), max fitness = 64");
        Console.WriteLine();
        Console.WriteLine("Generation | BestFitness | AvgFitness | Species | AvgComplexity");

        SqliteExperimentRunPersistence persistence = new();
        EvolutionRunContext runContext = new(
            ExperimentName: Name,
            Seed: _seed,
            ConfigJson: ExperimentConfigSerializer.Serialize(options));

        EvolutionResult result = orchestrator.Run(
            onGenerationCompleted: metrics =>
            {
                if (metrics.Generation % 10 == 0 || metrics.Generation == options.GenerationCount - 1)
                {
                    Console.WriteLine(
                        $"{metrics.Generation,10} | {metrics.BestFitness,11:F2} | {metrics.AverageFitness,10:F2} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2}");
                }
            },
            runPersistence: persistence,
            runContext: runContext);

        Console.WriteLine();
        bool solved = result.BestFitness >= 64d;
        Console.WriteLine($"Best fitness: {result.BestFitness:F2} / 64.00");
        Console.WriteLine(solved ? "SOLVED." : "Did not fully solve benchmark.");
        Console.WriteLine($"Results database: {persistence.DatabasePath}");
    }

    private static Genome CreateGenome(
        Random rng,
        InnovationTracker tracker,
        Guid a0,
        Guid a1,
        Guid d0,
        Guid d1,
        Guid d2,
        Guid d3,
        Guid output)
    {
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(a0, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(a1, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(d0, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(d1, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(d2, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(d3, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), NextBias(rng)));

        Guid[] inputs = [a0, a1, d0, d1, d2, d3];

        foreach (Guid input in inputs)
        {
            int innovation = tracker.GetOrCreateConnectionInnovation(input, output);
            genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, NextWeight(rng), true, innovation));
        }

        return genome;
    }

    private static double NextWeight(Random rng)
    {
        return -1d + (2d * rng.NextDouble());
    }

    private static double NextBias(Random rng)
    {
        return -1d + (2d * rng.NextDouble());
    }
}
