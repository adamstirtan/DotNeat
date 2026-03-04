namespace DotNeat;

public class Genome
{
    public Guid GenomeId { get; } = Guid.NewGuid();
    public List<NodeGene> Nodes { get; } = [];
    public List<ConnectionGene> Connections { get; } = [];

    public IReadOnlyList<string> GetValidationErrors(bool allowCycles = false)
    {
        List<string> errors = [];

        HashSet<Guid> nodeIds = [];
        foreach (NodeGene node in Nodes)
        {
            if (!nodeIds.Add(node.GeneId))
            {
                errors.Add($"Duplicate node id detected: {node.GeneId}.");
            }

            if (node.ActivationFunction is null)
            {
                errors.Add($"Node {node.GeneId} has a null activation function.");
            }
        }

        HashSet<Guid> connectionIds = [];
        HashSet<(Guid input, Guid output)> directedEdges = [];

        foreach (ConnectionGene connection in Connections)
        {
            if (!connectionIds.Add(connection.GeneId))
            {
                errors.Add($"Duplicate connection id detected: {connection.GeneId}.");
            }

            if (!nodeIds.Contains(connection.InputNodeId))
            {
                errors.Add($"Connection {connection.GeneId} references missing input node {connection.InputNodeId}.");
            }

            if (!nodeIds.Contains(connection.OutputNodeId))
            {
                errors.Add($"Connection {connection.GeneId} references missing output node {connection.OutputNodeId}.");
            }

            if (connection.InputNodeId == connection.OutputNodeId)
            {
                errors.Add($"Self-loop detected on node {connection.InputNodeId} in connection {connection.GeneId}.");
            }

            if (!directedEdges.Add((connection.InputNodeId, connection.OutputNodeId)))
            {
                errors.Add($"Duplicate directed connection detected: {connection.InputNodeId} -> {connection.OutputNodeId}.");
            }
        }

        if (!allowCycles && HasCycle(nodeIds, Connections))
        {
            errors.Add("Cycle detected in enabled connections.");
        }

        return errors;
    }

    public void Validate(bool allowCycles = false)
    {
        IReadOnlyList<string> errors = GetValidationErrors(allowCycles);

        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Genome {GenomeId} is invalid:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", errors)}");
    }

    private static bool HasCycle(HashSet<Guid> nodeIds, IEnumerable<ConnectionGene> connections)
    {
        Dictionary<Guid, int> indegree = [];
        Dictionary<Guid, List<Guid>> adjacency = [];

        foreach (Guid nodeId in nodeIds)
        {
            indegree[nodeId] = 0;
            adjacency[nodeId] = [];
        }

        foreach (ConnectionGene connection in connections)
        {
            if (!connection.Enabled)
            {
                continue;
            }

            if (!nodeIds.Contains(connection.InputNodeId) || !nodeIds.Contains(connection.OutputNodeId))
            {
                continue;
            }

            if (connection.InputNodeId == connection.OutputNodeId)
            {
                continue;
            }

            adjacency[connection.InputNodeId].Add(connection.OutputNodeId);
            indegree[connection.OutputNodeId]++;
        }

        Queue<Guid> queue = new(indegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        int visitedCount = 0;

        while (queue.Count > 0)
        {
            Guid current = queue.Dequeue();
            visitedCount++;

            foreach (Guid target in adjacency[current])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                {
                    queue.Enqueue(target);
                }
            }
        }

        return visitedCount != nodeIds.Count;
    }
}
