namespace DotNeat;

public static class ModularityScorer
{
    // Simple heuristic modularity score: number of connected components (considering enabled connections)
    // normalized by node count. Higher is more modular (more separate modules).
    // This is intentionally cheap and deterministic.
    public static double Score(Genome genome, double weightThreshold = 0.01)
    {
        if (genome is null) throw new ArgumentNullException(nameof(genome));

        int n = genome.Nodes.Count;
        if (n == 0) return 0d;

        // build adjacency
        Dictionary<Guid, List<Guid>> adj = new();
        foreach (NodeGene node in genome.Nodes)
        {
            adj[node.GeneId] = new List<Guid>();
        }

        foreach (ConnectionGene conn in genome.Connections)
        {
            if (!conn.Enabled) continue;
            if (Math.Abs(conn.Weight) < weightThreshold) continue;

            if (adj.ContainsKey(conn.InputNodeId) && adj.ContainsKey(conn.OutputNodeId))
            {
                adj[conn.InputNodeId].Add(conn.OutputNodeId);
                adj[conn.OutputNodeId].Add(conn.InputNodeId);
            }
        }

        HashSet<Guid> visited = new();
        int components = 0;

        foreach (Guid id in adj.Keys)
        {
            if (visited.Contains(id)) continue;

            components++;
            // BFS
            Queue<Guid> q = new();
            q.Enqueue(id);
            visited.Add(id);

            while (q.Count > 0)
            {
                Guid cur = q.Dequeue();
                foreach (Guid nb in adj[cur])
                {
                    if (!visited.Contains(nb))
                    {
                        visited.Add(nb);
                        q.Enqueue(nb);
                    }
                }
            }
        }

        // normalize: more components -> higher score, range (1/n .. 1)
        double score = (double)components / (double)n;
        return score;
    }
}
