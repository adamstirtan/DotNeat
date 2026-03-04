namespace DotNeat.Tests;

[TestClass]
public sealed class GenomeMutatorTests
{
    [TestMethod]
    public void MutateWeights_PerturbsWeights_WhenPerturbChanceIsOne()
    {
        Genome genome = CreateSingleConnectionGenome(weight: 0.5);
        GenomeMutator mutator = new(new InnovationTracker(), new Random(1));

        double originalWeight = genome.Connections[0].Weight;

        bool mutated = mutator.MutateWeights(genome, perturbChance: 1d, perturbScale: 0.1);

        Assert.IsTrue(mutated);
        Assert.AreNotEqual(originalWeight, genome.Connections[0].Weight);
    }

    [TestMethod]
    public void MutateWeights_ResetsWeights_WhenPerturbChanceIsZero()
    {
        Genome genome = CreateSingleConnectionGenome(weight: 0.5);
        GenomeMutator mutator = new(new InnovationTracker(), new Random(2));

        bool mutated = mutator.MutateWeights(genome, perturbChance: 0d, resetMin: -2d, resetMax: -1d);

        Assert.IsTrue(mutated);
        Assert.IsTrue(genome.Connections[0].Weight is >= -2d and <= -1d);
    }

    [TestMethod]
    public void MutateAddConnection_AddsNewConnection_WhenCandidateExists()
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome genome = new();
        genome.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        GenomeMutator mutator = new(new InnovationTracker(), new Random(3));

        bool added = mutator.MutateAddConnection(genome);

        Assert.IsTrue(added);
        Assert.HasCount(1, genome.Connections);
        Assert.AreEqual(input, genome.Connections[0].InputNodeId);
        Assert.AreEqual(output, genome.Connections[0].OutputNodeId);
    }

    [TestMethod]
    public void MutateAddNode_SplitsEnabledConnection()
    {
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome genome = new();
        genome.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, output, 0.75, true, 1));

        InnovationTracker tracker = new();
        GenomeMutator mutator = new(tracker, new Random(4));

        bool added = mutator.MutateAddNode(genome);

        Assert.IsTrue(added);
        Assert.IsFalse(genome.Connections[0].Enabled);
        Assert.HasCount(3, genome.Connections);

        NodeSplitInnovation split = tracker.GetOrCreateNodeSplitInnovation(1);

        Assert.IsTrue(genome.Nodes.Any(n => n.GeneId == split.NewNodeId));
        Assert.IsTrue(genome.Connections.Any(c => c.InputNodeId == input && c.OutputNodeId == split.NewNodeId && c.Enabled));
        Assert.IsTrue(genome.Connections.Any(c => c.InputNodeId == split.NewNodeId && c.OutputNodeId == output && c.Enabled && Math.Abs(c.Weight - 0.75) < 1e-12));
    }

    [TestMethod]
    public void MutateToggleConnection_TogglesEnabledFlag()
    {
        Genome genome = CreateSingleConnectionGenome(weight: 1d);
        GenomeMutator mutator = new(new InnovationTracker(), new Random(5));

        bool first = mutator.MutateToggleConnection(genome);
        bool second = mutator.MutateToggleConnection(genome);

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        Assert.IsTrue(genome.Connections[0].Enabled);
    }

    [TestMethod]
    public void MutateToggleConnection_DoesNotEnableCycle()
    {
        Guid input = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome genome = new();
        genome.Nodes.Add(new NodeGene(input, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(hidden, NodeType.Hidden, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), input, hidden, 1, true, 1));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden, output, 1, true, 2));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), output, input, 1, false, 3));

        GenomeMutator mutator = new(new InnovationTracker(), new Random(7));

        for (int i = 0; i < 20; i++)
        {
            _ = mutator.MutateToggleConnection(genome);
            Assert.IsFalse(genome.GetValidationErrors().Contains("Cycle detected in enabled connections."));
        }
    }

    private static Genome CreateSingleConnectionGenome(double weight)
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
