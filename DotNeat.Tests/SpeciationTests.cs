using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class SpeciationTests
{
    [TestMethod]
    public void CompatibilityDistance_ComputesExpectedValue()
    {
        (Genome a, Genome b, _, _, _) = CreateDistanceGenomes();

        double distance = Speciation.CompatibilityDistance(a, b, c1: 1d, c2: 1d, c3: 0.4d);

        Assert.AreEqual(3.2d, distance, 1e-12);
    }

    [TestMethod]
    public void GroupIntoSpecies_GroupsSimilarGenomesTogether()
    {
        (Genome g1, Genome g2, Genome g3) = CreateGroupingGenomes();

        IReadOnlyList<Species> species = Speciation.GroupIntoSpecies(
            [g1, g2, g3],
            compatibilityThreshold: 0.6,
            c1: 1,
            c2: 1,
            c3: 1);

        Assert.HasCount(2, species);

        Species groupWithG1 = species.Single(s => s.Members.Any(m => m.GenomeId == g1.GenomeId));
        Assert.IsTrue(groupWithG1.Members.Any(m => m.GenomeId == g2.GenomeId));
        Assert.IsFalse(groupWithG1.Members.Any(m => m.GenomeId == g3.GenomeId));
    }

    [TestMethod]
    public void ShareFitnessWithinSpecies_DividesBySpeciesSize()
    {
        (Genome g1, Genome g2, Genome g3) = CreateGroupingGenomes();

        IReadOnlyList<Species> species = Speciation.GroupIntoSpecies(
            [g1, g2, g3],
            compatibilityThreshold: 0.6,
            c1: 1,
            c2: 1,
            c3: 1);

        IReadOnlyDictionary<Guid, double> rawFitness = new Dictionary<Guid, double>
        {
            [g1.GenomeId] = 6d,
            [g2.GenomeId] = 4d,
            [g3.GenomeId] = 5d,
        };

        IReadOnlyDictionary<Guid, double> shared = Speciation.ShareFitnessWithinSpecies(species, rawFitness);

        Assert.AreEqual(3d, shared[g1.GenomeId], 1e-12);
        Assert.AreEqual(2d, shared[g2.GenomeId], 1e-12);
        Assert.AreEqual(5d, shared[g3.GenomeId], 1e-12);
    }

    private static (Genome a, Genome b, Guid input, Guid hidden1, Guid output) CreateDistanceGenomes()
    {
        Guid input = Guid.NewGuid();
        Guid hidden1 = Guid.NewGuid();
        Guid hidden2 = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome a = new();
        a.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        a.Nodes.Add(new NodeGene(hidden1, NodeType.Hidden, new ReluActivationFunction(), 0));
        a.Nodes.Add(new NodeGene(hidden2, NodeType.Hidden, new ReluActivationFunction(), 0));
        a.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        a.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden1, 1.0, true, 1));
        a.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden1, output, 1.0, true, 2));

        Genome b = new();
        b.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        b.Nodes.Add(new NodeGene(hidden1, NodeType.Hidden, new ReluActivationFunction(), 0));
        b.Nodes.Add(new NodeGene(hidden2, NodeType.Hidden, new ReluActivationFunction(), 0));
        b.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        b.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden1, 1.5, true, 1));
        b.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden2, 1.0, true, 3));
        b.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden2, output, 1.0, true, 4));

        return (a, b, input, hidden1, output);
    }

    private static (Genome g1, Genome g2, Genome g3) CreateGroupingGenomes()
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();

        Genome g1 = new();
        g1.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        g1.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        g1.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, 1.0, true, 1));

        Genome g2 = new();
        g2.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        g2.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        g2.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, 1.2, true, 1));

        Genome g3 = new();
        g3.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        g3.Nodes.Add(new NodeGene(hidden, NodeType.Hidden, new ReluActivationFunction(), 0));
        g3.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        g3.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden, 2.0, true, 2));
        g3.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden, output, 2.0, true, 3));

        return (g1, g2, g3);
    }
}
