using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Experiments;

public sealed class ModularityExperiment(int seed = 31337) : IExperiment
{
    private readonly int _seed = seed;

    public string Name => "modularity";

    public void Run()
    {
        // default to XOR task for quick demo; sweep lambda values
        double[] lambdas = new double[] { 0.0, 0.01, 0.1, 0.5, 1.0 };

        Console.WriteLine($"Seed: {_seed}");
        Console.WriteLine("Lambda | Generation | BestFitness | AvgFitness | Species | AvgComplexity | Modularity");

        foreach (double lambda in lambdas)
        {
            XorFitnessEvaluator evaluator = new(_seed);

            Guid inputA = Guid.NewGuid();
            Guid inputB = Guid.NewGuid();
            Guid output = Guid.NewGuid();

            EvolutionOptions options = new(
                PopulationSize: 150,
                GenerationCount: 60,
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
                ModularityLambda: lambda,
                ModularityScorer: ModularityScorer.Score,
                Seed: _seed);

            SqliteExperimentRunPersistence persistence = new();
            EvolutionRunContext runContext = new(
                ExperimentName: Name,
                Seed: _seed,
                ConfigJson: ExperimentConfigSerializer.Serialize(options));

            EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);
            EvolutionResult result = orchestrator.Run(
                onGenerationCompleted: metrics =>
                {
                    if (metrics.Generation % 10 == 0 || metrics.Generation == options.GenerationCount - 1)
                    {
                        double modularity = ModularityScorer.Score(result.BestGenome);
                        Console.WriteLine($"{lambda,6} | {metrics.Generation,10} | {metrics.BestFitness,11:F6} | {metrics.AverageFitness,10:F6} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2} | {modularity,10:F4}");
                    }
                },
                runPersistence: persistence,
                runContext: runContext);

            Console.WriteLine();
            Console.WriteLine($"Lambda {lambda} - Best fitness: {result.BestFitness:F6} / 4.000000");
            Console.WriteLine($"Results database: {persistence.DatabasePath}");
            Console.WriteLine(new string('-', 80));
        }
    }

    private static Genome CreateGenome(Random rng, InnovationTracker tracker, Guid inputA, Guid inputB, Guid output)
    {
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(inputA, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputB, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), NextBias(rng)));

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

    private static double NextBias(Random rng)
    {
        return -1d + (2d * rng.NextDouble());
    }
}
