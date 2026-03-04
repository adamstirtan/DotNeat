using DotNeat;

const int seed = 12345;

XorFitnessEvaluator evaluator = new(seed);

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
    InitialGenomeFactory: (rng, tracker) => CreateInitialGenome(rng, tracker, inputA, inputB, output),
    Seed: seed);

EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);
EvolutionResult result = orchestrator.Run();

Console.WriteLine($"Seed: {seed}");
Console.WriteLine("Generation | BestFitness | Species | AvgComplexity");

foreach (GenerationMetrics metrics in result.History)
{
    if (metrics.Generation % 5 == 0 || metrics.Generation == result.History.Count - 1)
    {
        Console.WriteLine($"{metrics.Generation,10} | {metrics.BestFitness,11:F6} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2}");
    }
}

Console.WriteLine();
Console.WriteLine($"Best fitness: {result.BestFitness:F6} / 4.000000");

static Genome CreateInitialGenome(Random rng, InnovationTracker tracker, Guid inputA, Guid inputB, Guid output)
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

static double NextWeight(Random rng)
{
    return -1d + (2d * rng.NextDouble());
}
