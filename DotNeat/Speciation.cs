namespace DotNeat;

public sealed record Species(
    int SpeciesId,
    Genome Representative,
    IReadOnlyList<Genome> Members);

public static class Speciation
{
    public static double CompatibilityDistance(
        Genome a,
        Genome b,
        double c1,
        double c2,
        double c3)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        a.Validate();
        b.Validate();

        Dictionary<int, ConnectionGene> aByInnovation = a.Connections.ToDictionary(c => c.InnovationNumber);
        Dictionary<int, ConnectionGene> bByInnovation = b.Connections.ToDictionary(c => c.InnovationNumber);

        if (aByInnovation.Count == 0 && bByInnovation.Count == 0)
        {
            return 0d;
        }

        int maxA = aByInnovation.Count > 0 ? aByInnovation.Keys.Max() : 0;
        int maxB = bByInnovation.Count > 0 ? bByInnovation.Keys.Max() : 0;

        int matchingCount = 0;
        double weightDiffSum = 0d;

        int disjoint = 0;
        int excess = 0;

        foreach (int innovation in aByInnovation.Keys.Union(bByInnovation.Keys))
        {
            bool inA = aByInnovation.TryGetValue(innovation, out ConnectionGene? geneA);
            bool inB = bByInnovation.TryGetValue(innovation, out ConnectionGene? geneB);

            if (inA && inB)
            {
                matchingCount++;
                weightDiffSum += Math.Abs(geneA!.Weight - geneB!.Weight);
                continue;
            }

            if (inA)
            {
                if (innovation > maxB)
                {
                    excess++;
                }
                else
                {
                    disjoint++;
                }

                continue;
            }

            if (innovation > maxA)
            {
                excess++;
            }
            else
            {
                disjoint++;
            }
        }

        double avgWeightDiff = matchingCount > 0 ? weightDiffSum / matchingCount : 0d;

        return (c1 * excess) + (c2 * disjoint) + (c3 * avgWeightDiff);
    }

    public static IReadOnlyList<Species> GroupIntoSpecies(
        IReadOnlyList<Genome> genomes,
        double compatibilityThreshold,
        double c1,
        double c2,
        double c3)
    {
        ArgumentNullException.ThrowIfNull(genomes);

        if (compatibilityThreshold < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(compatibilityThreshold), "compatibilityThreshold must be >= 0.");
        }

        List<Species> species = [];
        int nextSpeciesId = 1;

        foreach (Genome genome in genomes)
        {
            bool assigned = false;

            for (int i = 0; i < species.Count; i++)
            {
                Species current = species[i];
                double distance = CompatibilityDistance(
                    genome,
                    current.Representative,
                    c1,
                    c2,
                    c3);

                if (distance > compatibilityThreshold)
                {
                    continue;
                }

                List<Genome> updatedMembers = [.. current.Members, genome];
                species[i] = current with { Members = updatedMembers };
                assigned = true;
                break;
            }

            if (assigned)
            {
                continue;
            }

            species.Add(new Species(nextSpeciesId++, genome, [genome]));
        }

        return species;
    }

    public static IReadOnlyDictionary<Guid, double> ShareFitnessWithinSpecies(
        IReadOnlyList<Species> species,
        IReadOnlyDictionary<Guid, double> rawFitnessByGenomeId)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(rawFitnessByGenomeId);

        Dictionary<Guid, double> sharedFitness = [];

        foreach (Species group in species)
        {
            int count = group.Members.Count;
            if (count <= 0)
            {
                continue;
            }

            foreach (Genome member in group.Members)
            {
                if (!rawFitnessByGenomeId.TryGetValue(member.GenomeId, out double rawFitness))
                {
                    throw new InvalidOperationException($"Missing raw fitness for genome {member.GenomeId}.");
                }

                sharedFitness[member.GenomeId] = rawFitness / count;
            }
        }

        return sharedFitness;
    }
}
