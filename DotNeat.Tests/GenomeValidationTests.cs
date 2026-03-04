using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class GenomeValidationTests
{
    [TestMethod]
    public void Validate_DoesNotThrow_ForValidGenome()
    {
        Genome genome = CreateBaseGenome();

        genome.Validate();
    }

    [TestMethod]
    public void GetValidationErrors_ReturnsError_ForDuplicateNodeId()
    {
        Guid duplicateNodeId = Guid.NewGuid();
        Genome genome = new();
        genome.Nodes.Add(new NodeGene(duplicateNodeId, NodeType.Input, new SigmoidActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(duplicateNodeId, NodeType.Hidden, new ReluActivationFunction(), 0));

        IReadOnlyList<string> errors = genome.GetValidationErrors();

        CollectionAssert.Contains(errors.ToList(), $"Duplicate node id detected: {duplicateNodeId}.");
    }

    [TestMethod]
    public void GetValidationErrors_ReturnsError_ForMissingConnectionEndpoint()
    {
        Genome genome = new();
        Guid inputId = Guid.NewGuid();
        Guid missingOutputId = Guid.NewGuid();

        genome.Nodes.Add(new NodeGene(inputId, NodeType.Input, new SigmoidActivationFunction(), 0));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, missingOutputId, 1, true, 1));

        IReadOnlyList<string> errors = genome.GetValidationErrors();

        Assert.IsTrue(errors.Any(e => e.Contains("references missing output node")));
    }

    [TestMethod]
    public void GetValidationErrors_ReturnsError_ForCycle_WhenCyclesNotAllowed()
    {
        Genome genome = new();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        genome.Nodes.Add(new NodeGene(a, NodeType.Hidden, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(b, NodeType.Hidden, new ReluActivationFunction(), 0));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), a, b, 1, true, 1));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), b, a, 1, true, 2));

        IReadOnlyList<string> errors = genome.GetValidationErrors();

        CollectionAssert.Contains(errors.ToList(), "Cycle detected in enabled connections.");
    }

    [TestMethod]
    public void GetValidationErrors_DoesNotReturnCycleError_WhenCyclesAllowed()
    {
        Genome genome = new();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        genome.Nodes.Add(new NodeGene(a, NodeType.Hidden, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(b, NodeType.Hidden, new ReluActivationFunction(), 0));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), a, b, 1, true, 1));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), b, a, 1, true, 2));

        IReadOnlyList<string> errors = genome.GetValidationErrors(allowCycles: true);

        Assert.IsFalse(errors.Contains("Cycle detected in enabled connections."));
    }

    private static Genome CreateBaseGenome()
    {
        Genome genome = new();
        Guid inputId = Guid.NewGuid();
        Guid outputId = Guid.NewGuid();

        genome.Nodes.Add(new NodeGene(inputId, NodeType.Input, new SigmoidActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(outputId, NodeType.Output, new ReluActivationFunction(), 0));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, outputId, 0.5, true, 1));

        return genome;
    }
}
