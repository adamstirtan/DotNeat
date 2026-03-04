namespace DotNeat.Tests;

[TestClass]
public sealed class NeuralNetworkExecutionTests
{
    [TestMethod]
    public void FromGenome_ComputesForwardPass()
    {
        Guid inputA = Guid.NewGuid();
        Guid inputB = Guid.NewGuid();
        Guid hidden = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        Genome genome = new();
        genome.Nodes.Add(new NodeGene(inputA, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputB, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(hidden, NodeType.Hidden, new ReluActivationFunction(), 0.5));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), -1));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputA, hidden, 2, true, 1));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputB, hidden, -1, true, 2));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputA, output, 0.25, true, 3));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), hidden, output, 1.5, true, 4));

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        IReadOnlyDictionary<Guid, double> outputs = network.Forward(new Dictionary<Guid, double>
        {
            [inputA] = 3,
            [inputB] = 1,
        });

        Assert.HasCount(1, outputs);
        Assert.AreEqual(1d / (1d + Math.Exp(-8d)), outputs[output], 1e-12);
    }

    [TestMethod]
    public void FromGenome_ProducesTopologicalOrder()
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

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        IReadOnlyList<Guid> order = network.TopologicalOrderNodeIds;
        List<Guid> orderList = [.. order];

        int inputIndex = orderList.IndexOf(input);
        int hiddenIndex = orderList.IndexOf(hidden);
        int outputIndex = orderList.IndexOf(output);

        Assert.IsGreaterThanOrEqualTo(0, inputIndex);
        Assert.IsGreaterThan(inputIndex, hiddenIndex);
        Assert.IsGreaterThan(hiddenIndex, outputIndex);
    }
}
