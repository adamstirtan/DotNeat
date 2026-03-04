using DotNeat;
using DotNeat.Runner.Persistence;

namespace DotNeat.Runner.Experiments;

public sealed class CartPoleExperiment(int seed = 12345) : IExperiment
{
    private readonly int _seed = seed;

    public string Name => "cartpole";

    public void Run()
    {
        const int maxSteps = 500;
        const int trials = 5;

        CartPoleFitnessEvaluator evaluator = new(maxSteps: maxSteps, trials: trials, seed: _seed);

        Guid inputPos = Guid.NewGuid();
        Guid inputVel = Guid.NewGuid();
        Guid inputAngle = Guid.NewGuid();
        Guid inputAngularVel = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        EvolutionOptions options = new(
            PopulationSize: 150,
            GenerationCount: 300,
            CompatibilityThreshold: 3.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.75,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.8,
                AddConnectionMutationProbability: 0.1,
                AddNodeMutationProbability: 0.05,
                ToggleConnectionMutationProbability: 0.05,
                WeightPerturbChance: 0.9,
                WeightPerturbScale: 0.5,
                WeightResetMin: -2,
                WeightResetMax: 2),
            InitialGenomeFactory: (rng, tracker) =>
                CreateGenome(rng, tracker, inputPos, inputVel, inputAngle, inputAngularVel, output),
            Seed: _seed);

        EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);

        Console.WriteLine($"Cart-Pole Experiment | Seed: {_seed} | MaxSteps: {maxSteps} | Trials: {trials}");
        Console.WriteLine($"Solving criterion: fitness >= {maxSteps + 1} (average {maxSteps} steps survived)");
        Console.WriteLine();
        Console.WriteLine("Generation | BestFitness | AvgFitness | Species | AvgComplexity | ChampSteps");

        SqliteExperimentRunPersistence persistence = new();
        EvolutionRunContext runContext = new(
            ExperimentName: Name,
            Seed: _seed,
            ConfigJson: ExperimentConfigSerializer.Serialize(options, new { MaxSteps = maxSteps, Trials = trials }));

        EvolutionResult result = orchestrator.Run(
            onGenerationCompleted: metrics =>
            {
                if (metrics.Generation % 10 == 0 || metrics.Generation == options.GenerationCount - 1)
                {
                    double champSteps = metrics.BestFitness - 1.0;
                    Console.WriteLine(
                        $"{metrics.Generation,10} | {metrics.BestFitness,11:F2} | {metrics.AverageFitness,10:F2} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2} | {champSteps,10:F1}");
                }
            },
            runPersistence: persistence,
            runContext: runContext);

        Console.WriteLine();
        double bestSteps = result.BestFitness - 1.0;
        bool solved = result.BestFitness >= maxSteps + 1;
        Console.WriteLine($"Best fitness: {result.BestFitness:F2} (avg {bestSteps:F1} steps survived)");
        Console.WriteLine(solved ? $"SOLVED in {result.History.Count} generations!" : "Did not reach solving criterion.");
        Console.WriteLine($"Results database: {persistence.DatabasePath}");
    }

    private static Genome CreateGenome(
        Random rng,
        InnovationTracker tracker,
        Guid inputPos,
        Guid inputVel,
        Guid inputAngle,
        Guid inputAngularVel,
        Guid output)
    {
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(inputPos, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputVel, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputAngle, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputAngularVel, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        int innov0 = tracker.GetOrCreateConnectionInnovation(inputPos, output);
        int innov1 = tracker.GetOrCreateConnectionInnovation(inputVel, output);
        int innov2 = tracker.GetOrCreateConnectionInnovation(inputAngle, output);
        int innov3 = tracker.GetOrCreateConnectionInnovation(inputAngularVel, output);

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputPos, output, NextWeight(rng), true, innov0));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputVel, output, NextWeight(rng), true, innov1));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputAngle, output, NextWeight(rng), true, innov2));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputAngularVel, output, NextWeight(rng), true, innov3));

        return genome;
    }

    private static double NextWeight(Random rng)
    {
        return -1d + (2d * rng.NextDouble());
    }
}
