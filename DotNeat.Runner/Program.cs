using DotNeat;

// Select experiment: "cartpole" or default to "xor"
string experiment = args.Length > 0 ? args[0].ToLowerInvariant() : "xor";

if (experiment == "cartpole")
{
    RunCartPoleExperiment();
}
else
{
    RunXorExperiment();
}

static void RunXorExperiment()
{
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
        InitialGenomeFactory: (rng, tracker) => CreateXorGenome(rng, tracker, inputA, inputB, output),
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
}

static void RunCartPoleExperiment()
{
    const int seed = 12345;
    const int maxSteps = 500;
    const int trials = 5;

    CartPoleFitnessEvaluator evaluator = new(maxSteps: maxSteps, trials: trials);

    // 4 input nodes: cart position, cart velocity, pole angle, pole angular velocity
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
            CreateCartPoleGenome(rng, tracker, inputPos, inputVel, inputAngle, inputAngularVel, output),
        Seed: seed);

    EvolutionOrchestrator orchestrator = new(evaluator.Evaluate, options);

    Console.WriteLine($"Cart-Pole Experiment | Seed: {seed} | MaxSteps: {maxSteps} | Trials: {trials}");
    Console.WriteLine($"Solving criterion: fitness >= {maxSteps + 1} (average {maxSteps} steps survived)");
    Console.WriteLine();
    Console.WriteLine("Generation | BestFitness | Species | AvgComplexity | ChampSteps");

    EvolutionResult result = orchestrator.Run();

    foreach (GenerationMetrics metrics in result.History)
    {
        if (metrics.Generation % 10 == 0 || metrics.Generation == result.History.Count - 1)
        {
            // Champion steps = BestFitness - 1 (reverse the +1 offset)
            double champSteps = metrics.BestFitness - 1.0;
            Console.WriteLine(
                $"{metrics.Generation,10} | {metrics.BestFitness,11:F2} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2} | {champSteps,10:F1}");
        }
    }

    Console.WriteLine();
    double bestSteps = result.BestFitness - 1.0;
    bool solved = result.BestFitness >= maxSteps + 1;
    Console.WriteLine($"Best fitness: {result.BestFitness:F2} (avg {bestSteps:F1} steps survived)");
    Console.WriteLine(solved ? $"SOLVED in {result.History.Count} generations!" : "Did not reach solving criterion.");
}

static Genome CreateXorGenome(Random rng, InnovationTracker tracker, Guid inputA, Guid inputB, Guid output)
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

static Genome CreateCartPoleGenome(
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

static double NextWeight(Random rng)
{
    return -1d + (2d * rng.NextDouble());
}
