namespace DotNeat;

public class ConnectionGene(
    Guid geneId,
    Guid inputNodeId,
    Guid outputNodeId,
    double weight,
    bool enabled,
    int innovationNumber)
    : Gene(geneId)
{
    public Guid InputNodeId { get; } = inputNodeId;

    public Guid OutputNodeId { get; } = outputNodeId;

    public double Weight { get; set; } = weight;

    public bool Enabled { get; set; } = enabled;

    public int InnovationNumber { get; } = innovationNumber;
}
