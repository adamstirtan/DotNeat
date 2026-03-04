namespace DotNeat;

public class NodeGene(
    Guid geneId,
    NodeType nodeType,
    ActivationFunction activationFunction,
    double bias)
    : Gene(geneId)
{
    public NodeType NodeType { get; } = nodeType;

    public ActivationFunction ActivationFunction { get; set; } = activationFunction;

    public double Bias { get; set; } = bias;
}
