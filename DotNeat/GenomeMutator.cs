namespace DotNeat;

public sealed class GenomeMutator(InnovationTracker innovationTracker, Random? random = null)
{
    private readonly InnovationTracker _innovationTracker = innovationTracker ?? throw new ArgumentNullException(nameof(innovationTracker));
    private readonly Random _random = random ?? new Random(12345);

    public bool MutateWeights(
        Genome genome,
        double perturbChance = 0.9,
        double perturbScale = 0.5,
        double resetMin = -1d,
        double resetMax = 1d)
    {
        ArgumentNullException.ThrowIfNull(genome);

        if (perturbChance < 0d || perturbChance > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(perturbChance), "perturbChance must be in [0, 1].");
        }

        if (perturbScale < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(perturbScale), "perturbScale must be >= 0.");
        }

        if (resetMax <= resetMin)
        {
            throw new ArgumentOutOfRangeException(nameof(resetMax), "resetMax must be greater than resetMin.");
        }

        if (genome.Connections.Count == 0)
        {
            return false;
        }

        foreach (ConnectionGene connection in genome.Connections)
        {
            if (_random.NextDouble() < perturbChance)
            {
                double delta = NextInRange(-perturbScale, perturbScale);
                connection.Weight += delta;
            }
            else
            {
                connection.Weight = NextInRange(resetMin, resetMax);
            }
        }

        return true;
    }

    public bool MutateAddConnection(Genome genome, double minWeight = -1d, double maxWeight = 1d)
    {
        ArgumentNullException.ThrowIfNull(genome);

        if (maxWeight <= minWeight)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWeight), "maxWeight must be greater than minWeight.");
        }

        if (genome.Nodes.Count < 2)
        {
            return false;
        }

        genome.Validate();

        IReadOnlyList<Guid> topologicalOrder = GetTopologicalOrder(genome);
        Dictionary<Guid, int> orderIndex = topologicalOrder
            .Select((nodeId, index) => (nodeId, index))
            .ToDictionary(x => x.nodeId, x => x.index);

        HashSet<(Guid input, Guid output)> existingEdges = [.. genome.Connections.Select(c => (c.InputNodeId, c.OutputNodeId))];
        List<(Guid input, Guid output)> candidates = [];

        foreach (NodeGene source in genome.Nodes)
        {
            if (source.NodeType == NodeType.Output)
            {
                continue;
            }

            foreach (NodeGene target in genome.Nodes)
            {
                if (target.NodeType == NodeType.Input)
                {
                    continue;
                }

                if (source.GeneId == target.GeneId)
                {
                    continue;
                }

                if (orderIndex[source.GeneId] >= orderIndex[target.GeneId])
                {
                    continue;
                }

                if (existingEdges.Contains((source.GeneId, target.GeneId)))
                {
                    continue;
                }

                candidates.Add((source.GeneId, target.GeneId));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        (Guid input, Guid output) chosen = candidates[_random.Next(candidates.Count)];
        int innovation = _innovationTracker.GetOrCreateConnectionInnovation(chosen.input, chosen.output);
        double weight = NextInRange(minWeight, maxWeight);

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), chosen.input, chosen.output, weight, true, innovation));
        return true;
    }

    public bool MutateAddNode(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        List<ConnectionGene> enabledConnections = [.. genome.Connections.Where(c => c.Enabled)];
        if (enabledConnections.Count == 0)
        {
            return false;
        }

        ConnectionGene splitConnection = enabledConnections[_random.Next(enabledConnections.Count)];
        splitConnection.Enabled = false;

        NodeSplitInnovation splitInnovation = _innovationTracker.GetOrCreateNodeSplitInnovation(splitConnection.InnovationNumber);

        if (genome.Nodes.All(n => n.GeneId != splitInnovation.NewNodeId))
        {
            genome.Nodes.Add(new NodeGene(splitInnovation.NewNodeId, NodeType.Hidden, new ReluActivationFunction(), 0d));
        }

        UpsertConnection(
            genome,
            splitConnection.InputNodeId,
            splitInnovation.NewNodeId,
            1d,
            splitInnovation.InputConnectionInnovationNumber);

        UpsertConnection(
            genome,
            splitInnovation.NewNodeId,
            splitConnection.OutputNodeId,
            splitConnection.Weight,
            splitInnovation.OutputConnectionInnovationNumber);

        return true;
    }

    public bool MutateToggleConnection(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        if (genome.Connections.Count == 0)
        {
            return false;
        }

        ConnectionGene connection = genome.Connections[_random.Next(genome.Connections.Count)];
        connection.Enabled = !connection.Enabled;
        return true;
    }

    private void UpsertConnection(Genome genome, Guid inputNodeId, Guid outputNodeId, double weight, int innovationNumber)
    {
        ConnectionGene? existing = genome.Connections.FirstOrDefault(c => c.InputNodeId == inputNodeId && c.OutputNodeId == outputNodeId);

        if (existing is not null)
        {
            existing.Enabled = true;
            existing.Weight = weight;
            return;
        }

        genome.Connections.Add(new ConnectionGene(
            Guid.NewGuid(),
            inputNodeId,
            outputNodeId,
            weight,
            true,
            innovationNumber));
    }

    private static IReadOnlyList<Guid> GetTopologicalOrder(Genome genome)
    {
        Dictionary<Guid, int> indegree = genome.Nodes.ToDictionary(n => n.GeneId, _ => 0);
        Dictionary<Guid, List<Guid>> adjacency = genome.Nodes.ToDictionary(n => n.GeneId, _ => new List<Guid>());

        foreach (ConnectionGene connection in genome.Connections.Where(c => c.Enabled))
        {
            indegree[connection.OutputNodeId]++;
            adjacency[connection.InputNodeId].Add(connection.OutputNodeId);
        }

        Queue<Guid> queue = new(indegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        List<Guid> ordered = [];

        while (queue.Count > 0)
        {
            Guid current = queue.Dequeue();
            ordered.Add(current);

            foreach (Guid next in adjacency[current])
            {
                indegree[next]--;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        if (ordered.Count != indegree.Count)
        {
            throw new InvalidOperationException("Genome contains cycles in enabled connections.");
        }

        return ordered;
    }

    private double NextInRange(double min, double max)
    {
        return min + ((max - min) * _random.NextDouble());
    }
}
