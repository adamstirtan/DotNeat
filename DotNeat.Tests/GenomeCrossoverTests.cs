using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class GenomeCrossoverTests
{
    [TestMethod]
    public void Crossover_MatchingGenes_BiasesTowardFitterParent()
    {
        (Genome fitter, Genome other, _, _) = CreateParentsWithSharedInnovation();

        Genome child = GenomeCrossover.Crossover(fitter, fitnessA: 10, other, fitnessB: 1, new Random(1));

        ConnectionGene childConnection = child.Connections.Single(c => c.InnovationNumber == 1);
        Assert.AreEqual(2.0, childConnection.Weight, 0d);
    }

    [TestMethod]
    public void Crossover_DisjointAndExcessGenes_ComeFromFitterParent()
    {
        (Genome fitter, Genome other, Guid input, Guid output) = CreateParentsWithSharedInnovation();

        Guid hidden = Guid.NewGuid();
        fitter.Nodes.Add(new NodeGene(hidden, NodeType.Hidden, new ReluActivationFunction(), 0));
        fitter.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden, 5.0, true, 2));
        fitter.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden, output, 5.0, true, 3));

        Genome child = GenomeCrossover.Crossover(fitter, fitnessA: 5, other, fitnessB: 2, new Random(2));

        Assert.IsTrue(child.Connections.Any(c => c.InnovationNumber == 2));
        Assert.IsTrue(child.Connections.Any(c => c.InnovationNumber == 3));
    }

    [TestMethod]
    public void Crossover_EqualFitness_IncludesDisjointGenesFromBothParents()
    {
        (Genome parentA, Genome parentB, Guid input, Guid output) = CreateParentsWithSharedInnovation();

        Guid hiddenA = Guid.NewGuid();
        Guid hiddenB = Guid.NewGuid();

        parentA.Nodes.Add(new NodeGene(hiddenA, NodeType.Hidden, new ReluActivationFunction(), 0));
        parentB.Nodes.Add(new NodeGene(hiddenB, NodeType.Hidden, new ReluActivationFunction(), 0));

        parentA.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hiddenA, 1.5, true, 2));
        parentA.Connections.Add(new ConnectionGene(Guid.NewGuid(), hiddenA, output, 1.5, true, 3));

        parentB.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hiddenB, -1.5, true, 4));
        parentB.Connections.Add(new ConnectionGene(Guid.NewGuid(), hiddenB, output, -1.5, true, 5));

        Genome child = GenomeCrossover.Crossover(parentA, fitnessA: 3, parentB, fitnessB: 3, new Random(3));

        Assert.IsTrue(child.Connections.Any(c => c.InnovationNumber == 2 || c.InnovationNumber == 3));
        Assert.IsTrue(child.Connections.Any(c => c.InnovationNumber == 4 || c.InnovationNumber == 5));
    }

    private static (Genome fitter, Genome other, Guid input, Guid output) CreateParentsWithSharedInnovation()
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome fitter = new();
        fitter.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        fitter.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        fitter.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, 2.0, true, 1));

        Genome other = new();
        other.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        other.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        other.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, -3.0, true, 1));

        return (fitter, other, input, output);
    }
}
