namespace DotNeat;

public static class GenomeCrossover
{
    public static Genome Crossover(Genome parentA, double fitnessA, Genome parentB, double fitnessB, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        parentA.Validate();
        parentB.Validate();

        Random rng = random ?? new Random(12345);
        const double fitterMatchSelectionBias = 0.75;
        const double disabledGeneChance = 0.75;

        bool equalFitness = Math.Abs(fitnessA - fitnessB) < 1e-12;
        Genome fitterParent;
        Genome otherParent;

        if (equalFitness)
        {
            if (rng.NextDouble() < 0.5)
            {
                fitterParent = parentA;
                otherParent = parentB;
            }
            else
            {
                fitterParent = parentB;
                otherParent = parentA;
            }
        }
        else if (fitnessA > fitnessB)
        {
            fitterParent = parentA;
            otherParent = parentB;
        }
        else
        {
            fitterParent = parentB;
            otherParent = parentA;
        }

        Genome child = new();

        Dictionary<int, ConnectionGene> fitterConnections = fitterParent.Connections.ToDictionary(c => c.InnovationNumber);
        Dictionary<int, ConnectionGene> otherConnections = otherParent.Connections.ToDictionary(c => c.InnovationNumber);

        List<int> allInnovations = [.. fitterConnections.Keys.Union(otherConnections.Keys).OrderBy(x => x)];

        HashSet<Guid> requiredNodeIds = [];
        List<ConnectionGene> childConnections = [];

        foreach (int innovation in allInnovations)
        {
            bool inFitter = fitterConnections.TryGetValue(innovation, out ConnectionGene? fitterGene);
            bool inOther = otherConnections.TryGetValue(innovation, out ConnectionGene? otherGene);

            ConnectionGene? chosen = null;

            if (inFitter && inOther)
            {
                ConnectionGene selected = equalFitness
                    ? (rng.NextDouble() < 0.5 ? fitterGene! : otherGene!)
                    : (rng.NextDouble() < fitterMatchSelectionBias ? fitterGene! : otherGene!);

                bool eitherDisabled = !fitterGene!.Enabled || !otherGene!.Enabled;
                bool enabled = eitherDisabled ? rng.NextDouble() >= disabledGeneChance : selected.Enabled;

                chosen = new ConnectionGene(
                    Guid.NewGuid(),
                    selected.InputNodeId,
                    selected.OutputNodeId,
                    selected.Weight,
                    enabled,
                    selected.InnovationNumber);
            }
            else if (inFitter)
            {
                ConnectionGene gene = fitterGene!;
                chosen = new ConnectionGene(
                    Guid.NewGuid(),
                    gene.InputNodeId,
                    gene.OutputNodeId,
                    gene.Weight,
                    gene.Enabled,
                    gene.InnovationNumber);
            }
            else if (equalFitness && inOther)
            {
                ConnectionGene gene = otherGene!;
                chosen = new ConnectionGene(
                    Guid.NewGuid(),
                    gene.InputNodeId,
                    gene.OutputNodeId,
                    gene.Weight,
                    gene.Enabled,
                    gene.InnovationNumber);
            }

            if (chosen is null)
            {
                continue;
            }

            childConnections.Add(chosen);
            _ = requiredNodeIds.Add(chosen.InputNodeId);
            _ = requiredNodeIds.Add(chosen.OutputNodeId);
        }

        foreach (NodeGene node in fitterParent.Nodes.Where(n => n.NodeType is NodeType.Input or NodeType.Output))
        {
            _ = requiredNodeIds.Add(node.GeneId);
        }

        if (equalFitness)
        {
            foreach (NodeGene node in otherParent.Nodes.Where(n => n.NodeType is NodeType.Input or NodeType.Output))
            {
                _ = requiredNodeIds.Add(node.GeneId);
            }
        }

        Dictionary<Guid, NodeGene> fitterNodes = fitterParent.Nodes.ToDictionary(n => n.GeneId);
        Dictionary<Guid, NodeGene> otherNodes = otherParent.Nodes.ToDictionary(n => n.GeneId);

        foreach (Guid nodeId in requiredNodeIds)
        {
            NodeGene sourceNode;

            if (fitterNodes.TryGetValue(nodeId, out NodeGene? fitterNode) && otherNodes.TryGetValue(nodeId, out NodeGene? otherNode))
            {
                sourceNode = equalFitness
                    ? (rng.NextDouble() < 0.5 ? fitterNode : otherNode)
                    : (rng.NextDouble() < fitterMatchSelectionBias ? fitterNode : otherNode);
            }
            else
            {
                sourceNode = fitterNodes.TryGetValue(nodeId, out fitterNode) ? fitterNode : otherNodes[nodeId];
            }

            child.Nodes.Add(new NodeGene(
                sourceNode.GeneId,
                sourceNode.NodeType,
                sourceNode.ActivationFunction,
                sourceNode.Bias));
        }

        foreach (ConnectionGene connection in childConnections)
        {
            child.Connections.Add(connection);
        }

        child.Validate();
        return child;
    }
}
