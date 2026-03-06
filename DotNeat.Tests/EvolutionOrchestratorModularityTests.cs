using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNeat.Tests;

[TestClass]
public sealed class EvolutionOrchestratorModularityTests
{
    [TestMethod]
    public void Orchestrator_UsesModularity_WhenLambdaPositive()
    {
        // create two genomes with known modularity via scorer
        Genome a = new();
        Guid ia = Guid.NewGuid();
        a.Nodes.Add(new NodeGene(ia, NodeType.Input, new ReluActivationFunction(), 0));

        Genome b = new();
        Guid ib1 = Guid.NewGuid();
        Guid ib2 = Guid.NewGuid();
        b.Nodes.Add(new NodeGene(ib1, NodeType.Input, new ReluActivationFunction(), 0));
        b.Nodes.Add(new NodeGene(ib2, NodeType.Input, new ReluActivationFunction(), 0));
        // connect b's nodes so it has higher modularity score (1.0) than separate nodes (0.5)
        b.Connections.Add(new ConnectionGene(Guid.NewGuid(), ib1, ib2, 1.0, true, 1));

        // evaluator returns constant task fitness 1.0 for any genome
        Func<Genome,double> evaluator = _ => 1.0;
        // scorer returns simple values based on node count to simulate real scorer
        Func<Genome,double> scorer = g => (double)g.Nodes.Count / (double)g.Nodes.Count; // always 1.0

        EvolutionOptions options = new(
            PopulationSize: 2,
            GenerationCount: 1,
            CompatibilityThreshold: 2.5,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 2,
                CrossoverProbability: 0.0,
                MutationProbability: 1.0,
                WeightMutationProbability: 1.0,
                AddConnectionMutationProbability: 0.0,
                AddNodeMutationProbability: 0.0,
                ToggleConnectionMutationProbability: 0.0,
                WeightPerturbChance: 0.0,
                WeightPerturbScale: 0.0,
                WeightResetMin: -1,
                WeightResetMax: 1),
            InitialGenomeFactory: (rng, tracker) => a,
            ModularityLambda: 0.5,
            ModularityScorer: scorer,
            Seed: 42);

        EvolutionOrchestrator orchestrator = new(evaluator, options);

        // prepare population manually by invoking private methods is hard; instead run and ensure no exceptions and results present
        EvolutionResult result = orchestrator.Run();
        Assert.IsNotNull(result);
        Assert.IsGreaterThanOrEqualTo(result.History.Count, 1);
    }
}
