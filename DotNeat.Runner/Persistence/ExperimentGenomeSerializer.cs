using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNeat.Runner.Persistence;

public static class ExperimentGenomeSerializer
{
    public static string Serialize(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        GenomeSnapshot snapshot = new(
            GenomeId: genome.GenomeId,
            Nodes:
            [
                .. genome.Nodes.Select(n => new NodeSnapshot(
                    n.GeneId,
                    n.NodeType,
                    n.ActivationFunction.Name,
                    n.Bias))
            ],
            Connections:
            [
                .. genome.Connections.Select(c => new ConnectionSnapshot(
                    c.GeneId,
                    c.InputNodeId,
                    c.OutputNodeId,
                    c.Weight,
                    c.Enabled,
                    c.InnovationNumber))
            ]);

        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        return JsonSerializer.Serialize(snapshot, options);
    }

    private sealed record GenomeSnapshot(
        Guid GenomeId,
        IReadOnlyList<NodeSnapshot> Nodes,
        IReadOnlyList<ConnectionSnapshot> Connections);

    private sealed record NodeSnapshot(
        Guid NodeId,
        NodeType NodeType,
        string ActivationFunction,
        double Bias);

    private sealed record ConnectionSnapshot(
        Guid ConnectionId,
        Guid InputNodeId,
        Guid OutputNodeId,
        double Weight,
        bool Enabled,
        int InnovationNumber);
}
