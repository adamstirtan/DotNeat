namespace DotNeat;

public sealed class NeuralNetwork
{
    private readonly IReadOnlyDictionary<Guid, NodeGene> _nodesById;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<ConnectionGene>> _incomingConnectionsByNodeId;

    private NeuralNetwork(
        IReadOnlyDictionary<Guid, NodeGene> nodesById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ConnectionGene>> incomingConnectionsByNodeId,
        IReadOnlyList<Guid> topologicalOrderNodeIds,
        IReadOnlyList<Guid> inputNodeIds,
        IReadOnlyList<Guid> outputNodeIds)
    {
        _nodesById = nodesById;
        _incomingConnectionsByNodeId = incomingConnectionsByNodeId;
        TopologicalOrderNodeIds = topologicalOrderNodeIds;

        InputNodeIds = inputNodeIds;
        OutputNodeIds = outputNodeIds;
    }

    public IReadOnlyList<Guid> InputNodeIds { get; }

    public IReadOnlyList<Guid> OutputNodeIds { get; }

    public IReadOnlyList<Guid> TopologicalOrderNodeIds { get; }

    public static NeuralNetwork FromGenome(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        genome.Validate();

        Dictionary<Guid, NodeGene> nodesById = genome.Nodes.ToDictionary(node => node.GeneId);
        List<ConnectionGene> enabledConnections = [.. genome.Connections.Where(connection => connection.Enabled)];

        Dictionary<Guid, int> indegree = [];
        Dictionary<Guid, List<Guid>> adjacency = [];
        Dictionary<Guid, List<ConnectionGene>> incomingConnectionsByNodeId = [];

        foreach (Guid nodeId in nodesById.Keys)
        {
            indegree[nodeId] = 0;
            adjacency[nodeId] = [];
            incomingConnectionsByNodeId[nodeId] = [];
        }

        foreach (ConnectionGene connection in enabledConnections)
        {
            indegree[connection.OutputNodeId]++;
            adjacency[connection.InputNodeId].Add(connection.OutputNodeId);
            incomingConnectionsByNodeId[connection.OutputNodeId].Add(connection);
        }

        Queue<Guid> queue = new(indegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        List<Guid> topologicalOrder = [];

        while (queue.Count > 0)
        {
            Guid current = queue.Dequeue();
            topologicalOrder.Add(current);

            foreach (Guid next in adjacency[current])
            {
                indegree[next]--;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (topologicalOrder.Count != nodesById.Count)
        {
            throw new InvalidOperationException("Genome contains cycles in enabled connections.");
        }

        Dictionary<Guid, IReadOnlyList<ConnectionGene>> incomingReadonly = incomingConnectionsByNodeId
            .ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<ConnectionGene>)kvp.Value);

        List<Guid> inputNodeIds = [.. genome.Nodes.Where(node => node.NodeType == NodeType.Input).Select(node => node.GeneId)];
        List<Guid> outputNodeIds = [.. genome.Nodes.Where(node => node.NodeType == NodeType.Output).Select(node => node.GeneId)];

        return new NeuralNetwork(nodesById, incomingReadonly, topologicalOrder, inputNodeIds, outputNodeIds);
    }

    public IReadOnlyDictionary<Guid, double> Forward(IReadOnlyDictionary<Guid, double> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        Dictionary<Guid, double> values = [];

        foreach (Guid nodeId in TopologicalOrderNodeIds)
        {
            NodeGene node = _nodesById[nodeId];

            if (node.NodeType == NodeType.Input)
            {
                if (!inputs.TryGetValue(nodeId, out double inputValue))
                {
                    throw new InvalidOperationException($"Missing input value for node {nodeId}.");
                }

                values[nodeId] = inputValue;
                continue;
            }

            double weightedSum = node.Bias;
            foreach (ConnectionGene connection in _incomingConnectionsByNodeId[nodeId])
            {
                weightedSum += values[connection.InputNodeId] * connection.Weight;
            }

            values[nodeId] = node.ActivationFunction.Activate(weightedSum);
        }

        return OutputNodeIds.ToDictionary(nodeId => nodeId, nodeId => values[nodeId]);
    }
}
