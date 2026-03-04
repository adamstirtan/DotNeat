namespace DotNeat.Tests;

[TestClass]
public sealed class ReproductionPipelineTests
{
    [TestMethod]
    public void Reproduce_KeepsSpeciesElites()
    {
        (Genome aBest, Genome aOther) = CreateTwoMemberSpeciesGenomes(3.0, 1.0);
        (Genome bBest, Genome bOther) = CreateTwoMemberSpeciesGenomes(2.5, 0.5);

        Species speciesA = new(1, aBest, [aBest, aOther]);
        Species speciesB = new(2, bBest, [bBest, bOther]);

        IReadOnlyDictionary<Guid, double> fitness = new Dictionary<Guid, double>
        {
            [aBest.GenomeId] = 10,
            [aOther.GenomeId] = 2,
            [bBest.GenomeId] = 8,
            [bOther.GenomeId] = 1,
        };

        IReadOnlyList<Genome> next = ReproductionPipeline.Reproduce(
            [speciesA, speciesB],
            fitness,
            offspringCount: 2,
            innovationTracker: new InnovationTracker(),
            options: new ReproductionOptions(ElitesPerSpecies: 1, CrossoverProbability: 0, MutationProbability: 0),
            random: new Random(1));

        Assert.HasCount(2, next);
        Assert.IsTrue(next.Any(g => g.GenomeId == aBest.GenomeId));
        Assert.IsTrue(next.Any(g => g.GenomeId == bBest.GenomeId));
    }

    [TestMethod]
    public void Reproduce_AllocatesOffspring_ByAdjustedFitness()
    {
        Genome strong = CreateSingleGenomeWithWeight(10);
        Genome weak = CreateSingleGenomeWithWeight(-10);

        Species speciesStrong = new(1, strong, [strong]);
        Species speciesWeak = new(2, weak, [weak]);

        IReadOnlyDictionary<Guid, double> fitness = new Dictionary<Guid, double>
        {
            [strong.GenomeId] = 10,
            [weak.GenomeId] = 2,
        };

        IReadOnlyList<Genome> next = ReproductionPipeline.Reproduce(
            [speciesStrong, speciesWeak],
            fitness,
            offspringCount: 12,
            innovationTracker: new InnovationTracker(),
            options: new ReproductionOptions(ElitesPerSpecies: 0, CrossoverProbability: 0, MutationProbability: 0),
            random: new Random(2));

        int strongCount = next.Count(g => g.Connections.Single().Weight > 0);
        int weakCount = next.Count(g => g.Connections.Single().Weight < 0);

        Assert.AreEqual(10, strongCount);
        Assert.AreEqual(2, weakCount);
    }

    [TestMethod]
    public void Reproduce_AppliesMutationProbability()
    {
        Genome parent = CreateSingleGenomeWithWeight(0);
        Species species = new(1, parent, [parent]);

        IReadOnlyDictionary<Guid, double> fitness = new Dictionary<Guid, double>
        {
            [parent.GenomeId] = 1,
        };

        ReproductionOptions options = new(
            ElitesPerSpecies: 0,
            CrossoverProbability: 0,
            MutationProbability: 1,
            WeightMutationProbability: 1,
            AddConnectionMutationProbability: 0,
            AddNodeMutationProbability: 0,
            ToggleConnectionMutationProbability: 0,
            WeightPerturbChance: 1,
            WeightPerturbScale: 0.5);

        IReadOnlyList<Genome> next = ReproductionPipeline.Reproduce(
            [species],
            fitness,
            offspringCount: 1,
            innovationTracker: new InnovationTracker(),
            options: options,
            random: new Random(3));

        Assert.HasCount(1, next);
        Assert.AreNotEqual(0d, next[0].Connections.Single().Weight);
    }

    private static (Genome best, Genome other) CreateTwoMemberSpeciesGenomes(double bestWeight, double otherWeight)
    {
        return (CreateSingleGenomeWithWeight(bestWeight), CreateSingleGenomeWithWeight(otherWeight));
    }

    private static Genome CreateSingleGenomeWithWeight(double weight)
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome genome = new();
        genome.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, weight, true, 1));
        return genome;
    }
}
