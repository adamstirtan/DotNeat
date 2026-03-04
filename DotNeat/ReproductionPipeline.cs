namespace DotNeat;

public sealed record ReproductionOptions(
    int ElitesPerSpecies = 1,
    int TournamentSize = 3,
    double CrossoverProbability = 0.75,
    double MutationProbability = 0.8,
    double WeightMutationProbability = 0.7,
    double AddConnectionMutationProbability = 0.15,
    double AddNodeMutationProbability = 0.1,
    double ToggleConnectionMutationProbability = 0.05,
    double WeightPerturbChance = 0.9,
    double WeightPerturbScale = 0.5,
    double WeightResetMin = -1d,
    double WeightResetMax = 1d,
    double BiasMutationChance = 1d,
    double BiasPerturbChance = 0.9,
    double BiasPerturbScale = 0.5,
    double BiasResetMin = -1d,
    double BiasResetMax = 1d,
    double NewNodeBiasMin = -1d,
    double NewNodeBiasMax = 1d)
{
    public void Validate()
    {
        if (ElitesPerSpecies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ElitesPerSpecies), "ElitesPerSpecies must be >= 0.");
        }

        if (TournamentSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(TournamentSize), "TournamentSize must be >= 1.");
        }

        ValidateProbability(CrossoverProbability, nameof(CrossoverProbability));
        ValidateProbability(MutationProbability, nameof(MutationProbability));
        ValidateProbability(WeightMutationProbability, nameof(WeightMutationProbability));
        ValidateProbability(AddConnectionMutationProbability, nameof(AddConnectionMutationProbability));
        ValidateProbability(AddNodeMutationProbability, nameof(AddNodeMutationProbability));
        ValidateProbability(ToggleConnectionMutationProbability, nameof(ToggleConnectionMutationProbability));
        ValidateProbability(WeightPerturbChance, nameof(WeightPerturbChance));
        ValidateProbability(BiasMutationChance, nameof(BiasMutationChance));
        ValidateProbability(BiasPerturbChance, nameof(BiasPerturbChance));

        if (WeightResetMax <= WeightResetMin)
        {
            throw new ArgumentOutOfRangeException(nameof(WeightResetMax), "WeightResetMax must be greater than WeightResetMin.");
        }

        if (BiasResetMax <= BiasResetMin)
        {
            throw new ArgumentOutOfRangeException(nameof(BiasResetMax), "BiasResetMax must be greater than BiasResetMin.");
        }

        if (NewNodeBiasMax <= NewNodeBiasMin)
        {
            throw new ArgumentOutOfRangeException(nameof(NewNodeBiasMax), "NewNodeBiasMax must be greater than NewNodeBiasMin.");
        }

        double operatorProbabilitySum = WeightMutationProbability
            + AddConnectionMutationProbability
            + AddNodeMutationProbability
            + ToggleConnectionMutationProbability;

        if (Math.Abs(operatorProbabilitySum - 1d) > 1e-12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WeightMutationProbability),
                "Mutation operator probabilities must sum to 1.");
        }
    }

    private static void ValidateProbability(double value, string paramName)
    {
        if (value is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be in [0, 1].");
        }
    }
}

public static class ReproductionPipeline
{
    public static IReadOnlyList<Genome> Reproduce(
        IReadOnlyList<Species> species,
        IReadOnlyDictionary<Guid, double> rawFitnessByGenomeId,
        int offspringCount,
        InnovationTracker innovationTracker,
        ReproductionOptions? options = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(rawFitnessByGenomeId);
        ArgumentNullException.ThrowIfNull(innovationTracker);

        if (offspringCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offspringCount), "offspringCount must be >= 0.");
        }

        ReproductionOptions config = options ?? new ReproductionOptions();
        config.Validate();

        Random rng = random ?? new Random(12345);
        GenomeMutator mutator = new(innovationTracker, rng);

        List<Species> usableSpecies = [.. species.Where(s => s.Members.Count > 0)];
        if (usableSpecies.Count == 0 || offspringCount == 0)
        {
            return [];
        }

        IReadOnlyDictionary<Guid, double> adjustedFitness = Speciation.ShareFitnessWithinSpecies(usableSpecies, rawFitnessByGenomeId);

        List<Genome> nextGeneration = [];

        foreach (Species group in usableSpecies)
        {
            int elitesToKeep = Math.Min(config.ElitesPerSpecies, group.Members.Count);
            if (elitesToKeep == 0)
            {
                continue;
            }

            IEnumerable<Genome> elites = group.Members
                .OrderByDescending(g => rawFitnessByGenomeId[g.GenomeId])
                .Take(elitesToKeep);

            foreach (Genome elite in elites)
            {
                if (nextGeneration.Count >= offspringCount)
                {
                    return nextGeneration;
                }

                nextGeneration.Add(elite);
            }
        }

        int remainingOffspring = offspringCount - nextGeneration.Count;
        if (remainingOffspring <= 0)
        {
            return nextGeneration;
        }

        Dictionary<int, int> allocations = AllocateOffspringCounts(usableSpecies, adjustedFitness, remainingOffspring);

        foreach (Species group in usableSpecies)
        {
            if (!allocations.TryGetValue(group.SpeciesId, out int allocation) || allocation <= 0)
            {
                continue;
            }

            for (int i = 0; i < allocation; i++)
            {
                Genome parentA = SelectParent(group, rawFitnessByGenomeId, config.TournamentSize, rng);
                double parentAFitness = rawFitnessByGenomeId[parentA.GenomeId];

                Genome child;

                if (group.Members.Count > 1 && rng.NextDouble() < config.CrossoverProbability)
                {
                    Genome parentB = SelectParent(group, rawFitnessByGenomeId, config.TournamentSize, rng);
                    double parentBFitness = rawFitnessByGenomeId[parentB.GenomeId];

                    try
                    {
                        child = GenomeCrossover.Crossover(parentA, parentAFitness, parentB, parentBFitness, rng);
                    }
                    catch (InvalidOperationException)
                    {
                        child = CloneGenome(parentA);
                    }
                }
                else
                {
                    child = CloneGenome(parentA);
                }

                if (rng.NextDouble() < config.MutationProbability)
                {
                    ApplyMutation(child, mutator, config, rng);
                }

                if (!IsGenomeValid(child))
                {
                    child = CloneGenome(parentA);
                }

                nextGeneration.Add(child);
                if (nextGeneration.Count >= offspringCount)
                {
                    return nextGeneration;
                }
            }
        }

        return nextGeneration;
    }

    private static bool IsGenomeValid(Genome genome)
    {
        return genome.GetValidationErrors().Count == 0;
    }

    private static Dictionary<int, int> AllocateOffspringCounts(
        IReadOnlyList<Species> species,
        IReadOnlyDictionary<Guid, double> adjustedFitness,
        int offspringSlots)
    {
        Dictionary<int, int> allocations = species.ToDictionary(s => s.SpeciesId, _ => 0);

        if (offspringSlots <= 0)
        {
            return allocations;
        }

        Dictionary<int, double> speciesFitness = species.ToDictionary(
            s => s.SpeciesId,
            s => s.Members.Sum(member => adjustedFitness[member.GenomeId]));

        double totalFitness = speciesFitness.Values.Sum();

        if (totalFitness <= 0d)
        {
            for (int i = 0; i < offspringSlots; i++)
            {
                Species target = species[i % species.Count];
                allocations[target.SpeciesId]++;
            }

            return allocations;
        }

        List<(int speciesId, double remainder)> remainders = [];
        int assigned = 0;

        foreach ((int speciesId, double fitness) in speciesFitness)
        {
            double expected = fitness / totalFitness * offspringSlots;
            int baseCount = (int)Math.Floor(expected);
            allocations[speciesId] = baseCount;
            assigned += baseCount;
            remainders.Add((speciesId, expected - baseCount));
        }

        int left = offspringSlots - assigned;
        foreach ((int speciesId, _) in remainders.OrderByDescending(r => r.remainder).Take(left))
        {
            allocations[speciesId]++;
        }

        return allocations;
    }

    private static Genome SelectParent(
        Species species,
        IReadOnlyDictionary<Guid, double> rawFitnessByGenomeId,
        int tournamentSize,
        Random rng)
    {
        Genome best = species.Members[rng.Next(species.Members.Count)];
        double bestFitness = rawFitnessByGenomeId[best.GenomeId];

        for (int i = 1; i < tournamentSize; i++)
        {
            Genome candidate = species.Members[rng.Next(species.Members.Count)];
            double candidateFitness = rawFitnessByGenomeId[candidate.GenomeId];

            if (candidateFitness <= bestFitness)
            {
                continue;
            }

            best = candidate;
            bestFitness = candidateFitness;
        }

        return best;
    }

    private static void ApplyMutation(Genome genome, GenomeMutator mutator, ReproductionOptions config, Random rng)
    {
        double roll = rng.NextDouble();

        if (roll < config.WeightMutationProbability)
        {
            bool weightMutated = mutator.MutateWeights(
                genome,
                config.WeightPerturbChance,
                config.WeightPerturbScale,
                config.WeightResetMin,
                config.WeightResetMax);

            bool biasMutated = mutator.MutateBiases(
                genome,
                config.BiasMutationChance,
                config.BiasPerturbChance,
                config.BiasPerturbScale,
                config.BiasResetMin,
                config.BiasResetMax);

            if (!weightMutated && !biasMutated)
            {
                _ = mutator.MutateToggleConnection(genome);
            }

            return;
        }

        roll -= config.WeightMutationProbability;
        if (roll < config.AddConnectionMutationProbability)
        {
            if (!mutator.MutateAddConnection(genome))
            {
                _ = mutator.MutateWeights(genome, config.WeightPerturbChance, config.WeightPerturbScale, config.WeightResetMin, config.WeightResetMax);
            }

            return;
        }

        roll -= config.AddConnectionMutationProbability;
        if (roll < config.AddNodeMutationProbability)
        {
            if (!mutator.MutateAddNode(genome, config.NewNodeBiasMin, config.NewNodeBiasMax))
            {
                _ = mutator.MutateWeights(genome, config.WeightPerturbChance, config.WeightPerturbScale, config.WeightResetMin, config.WeightResetMax);
                _ = mutator.MutateBiases(genome, config.BiasMutationChance, config.BiasPerturbChance, config.BiasPerturbScale, config.BiasResetMin, config.BiasResetMax);
            }

            return;
        }

        if (!mutator.MutateToggleConnection(genome))
        {
            _ = mutator.MutateWeights(genome, config.WeightPerturbChance, config.WeightPerturbScale, config.WeightResetMin, config.WeightResetMax);
            _ = mutator.MutateBiases(genome, config.BiasMutationChance, config.BiasPerturbChance, config.BiasPerturbScale, config.BiasResetMin, config.BiasResetMax);
        }
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
