using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNeat.Tests;

[TestClass]
public sealed class ModularityScorerTests
{
    [TestMethod]
    public void Score_ReturnsZero_ForEmptyGenome()
    {
        Genome g = new();
        double s = ModularityScorer.Score(g);
        Assert.AreEqual(0d, s);
    }

    [TestMethod]
    public void Score_SingleNode_ReturnsOne()
    {
        Genome g = new();
        Guid id = Guid.NewGuid();
        g.Nodes.Add(new NodeGene(id, NodeType.Input, new ReluActivationFunction(), 0));
        double s = ModularityScorer.Score(g);
        Assert.AreEqual(1d, s);
    }

    [TestMethod]
    public void Score_TwoDisconnectedNodes_ReturnsOne()
    {
        Genome g = new();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        g.Nodes.Add(new NodeGene(a, NodeType.Input, new ReluActivationFunction(), 0));
        g.Nodes.Add(new NodeGene(b, NodeType.Input, new ReluActivationFunction(), 0));
        double s = ModularityScorer.Score(g);
        Assert.AreEqual(1d, s);
    }

    [TestMethod]
    public void Score_TwoConnectedNodes_AboveThreshold_ReturnsHalf()
    {
        Genome g = new();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        g.Nodes.Add(new NodeGene(a, NodeType.Input, new ReluActivationFunction(), 0));
        g.Nodes.Add(new NodeGene(b, NodeType.Input, new ReluActivationFunction(), 0));
        g.Connections.Add(new ConnectionGene(Guid.NewGuid(), a, b, 0.5, true, 1));
        double s = ModularityScorer.Score(g, weightThreshold: 0.01);
        Assert.AreEqual(0.5d, s);
    }

    [TestMethod]
    public void Score_TwoConnectedNodes_BelowThreshold_TreatedAsDisconnected()
    {
        Genome g = new();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        g.Nodes.Add(new NodeGene(a, NodeType.Input, new ReluActivationFunction(), 0));
        g.Nodes.Add(new NodeGene(b, NodeType.Input, new ReluActivationFunction(), 0));
        g.Connections.Add(new ConnectionGene(Guid.NewGuid(), a, b, 0.0001, true, 1));
        double s = ModularityScorer.Score(g, weightThreshold: 0.01);
        Assert.AreEqual(1d, s);
    }
}
