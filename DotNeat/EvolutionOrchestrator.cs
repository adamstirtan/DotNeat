namespace DotNeat;

public sealed record EvolutionOptions(
    int PopulationSize,
    int GenerationCount,
    double CompatibilityThreshold,
    double C1,
    double C2,
    double C3,
    ReproductionOptions Reproduction,
    Func<Random, InnovationTracker, Genome> InitialGenomeFactory,
    int Seed = 12345)
{
    public void Validate()
    {
        if (PopulationSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(PopulationSize), "PopulationSize must be >= 1.");
        }

        if (GenerationCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(GenerationCount), "GenerationCount must be >= 1.");
        }

        if (CompatibilityThreshold < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(CompatibilityThreshold), "CompatibilityThreshold must be >= 0.");
        }

        ArgumentNullException.ThrowIfNull(Reproduction);
        Reproduction.Validate();
        ArgumentNullException.ThrowIfNull(InitialGenomeFactory);
    }
}

public sealed record GenerationMetrics(
    int Generation,
    double BestFitness,
    double AverageFitness,
    int SpeciesCount,
    double AverageComplexity);

public sealed record EvolutionResult(
    Genome BestGenome,
    double BestFitness,
    IReadOnlyList<GenerationMetrics> History,
    IReadOnlyList<Genome> FinalPopulation);

public sealed class EvolutionOrchestrator(Func<Genome, double> evaluate, EvolutionOptions options)
{
    private readonly Func<Genome, double> _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    private readonly EvolutionOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public EvolutionResult Run(
        Action<GenerationMetrics>? onGenerationCompleted = null,
        Action<GenerationMetrics, Genome>? onGenerationChampionCaptured = null)
    {
        _options.Validate();

        Random rng = new(_options.Seed);
        InnovationTracker innovationTracker = new();

        List<Genome> population = [];
        for (int i = 0; i < _options.PopulationSize; i++)
        {
            Genome genome = _options.InitialGenomeFactory(rng, innovationTracker);
            genome.Validate();
            population.Add(genome);
        }

        List<GenerationMetrics> history = [];

        Genome? bestGenome = null;
        double bestFitness = double.MinValue;

        for (int generation = 0; generation < _options.GenerationCount; generation++)
        {
            Dictionary<Guid, double> rawFitness = EvaluatePopulationFitness(population);

            Genome generationBest = population[0];
            double generationBestFitness = double.MinValue;
            double totalFitness = 0d;
            double totalComplexity = 0d;

            foreach (Genome genome in population)
            {
                double fitness = rawFitness[genome.GenomeId];
                totalFitness += fitness;
                totalComplexity += ComputeComplexity(genome);

                if (fitness > generationBestFitness)
                {
                    generationBest = genome;
                    generationBestFitness = fitness;
                }
            }

            if (generationBestFitness > bestFitness || bestGenome is null)
            {
                bestFitness = generationBestFitness;
                bestGenome = CloneGenome(generationBest);
            }

            IReadOnlyList<Species> species = Speciation.GroupIntoSpecies(
                population,
                _options.CompatibilityThreshold,
                _options.C1,
                _options.C2,
                _options.C3);

            double averageFitness = totalFitness / population.Count;
            double averageComplexity = totalComplexity / population.Count;
            GenerationMetrics metrics = new(generation, generationBestFitness, averageFitness, species.Count, averageComplexity);
            history.Add(metrics);
            onGenerationCompleted?.Invoke(metrics);

            if (onGenerationChampionCaptured is not null)
            {
                onGenerationChampionCaptured(metrics, CloneGenome(generationBest));
            }

            if (generation == _options.GenerationCount - 1)
            {
                break;
            }

            IReadOnlyList<Genome> nextPopulation = ReproductionPipeline.Reproduce(
                species,
                rawFitness,
                _options.PopulationSize,
                innovationTracker,
                _options.Reproduction,
                rng);

            population = [.. nextPopulation];
        }

        return new EvolutionResult(
            bestGenome!,
            bestFitness,
            history,
            population);
    }

    private static double ComputeComplexity(Genome genome)
    {
        int enabledConnections = genome.Connections.Count(c => c.Enabled);
        return genome.Nodes.Count + enabledConnections;
    }

    private Dictionary<Guid, double> EvaluatePopulationFitness(IReadOnlyList<Genome> population)
    {
        int count = population.Count;
        double[] fitnessValues = new double[count];

        Parallel.For(0, count, i =>
        {
            fitnessValues[i] = _evaluate(population[i]);
        });

        Dictionary<Guid, double> fitnessByGenomeId = new(count);
        for (int i = 0; i < count; i++)
        {
            fitnessByGenomeId[population[i].GenomeId] = fitnessValues[i];
        }

        return fitnessByGenomeId;
    }

    private static Genome CloneGenome(Genome source)
    {
        Genome clone = new();

        foreach (NodeGene node in source.Nodes)
        {
            clone.Nodes.Add(new NodeGene(node.GeneId, node.NodeType, node.ActivationFunction, node.Bias));
        }

        foreach (ConnectionGene connection in source.Connections)
        {
            clone.Connections.Add(new ConnectionGene(
                Guid.NewGuid(),
                connection.InputNodeId,
                connection.OutputNodeId,
                connection.Weight,
                connection.Enabled,
                connection.InnovationNumber));
        }

        return clone;
    }
}
