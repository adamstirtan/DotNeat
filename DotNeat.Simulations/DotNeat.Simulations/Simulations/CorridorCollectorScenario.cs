using System.Linq;
using DotNeat;

namespace DotNeat.Simulations.Experiments;

public sealed class CorridorCollectorScenario : SimulationScenarioBase
{
    private const int StepCount = 220;
    private const int TokenPoolSize = 4;
    private const double MoveSpeed = 0.045;
    private const double CollectionRadius = 0.045;

    private readonly Guid _inputPosition = Guid.NewGuid();
    private readonly Guid _inputTargetDelta = Guid.NewGuid();
    private readonly Guid _inputProgress = Guid.NewGuid();
    private readonly Guid _outputMovement = Guid.NewGuid();

    public override string Id => "corridor-collector";
    public override string Title => "Corridor Collector";
    public override string Description => "Agents patrol a one-dimensional track and race to grab tokens before time runs out.";
    public override string ComplexityLabel => "Simple";

    public override EvolutionOptions CreateEvolutionOptions(int seed)
    {
        return new(
            PopulationSize: 40,
            GenerationCount: 30,
            CompatibilityThreshold: 2.5,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.7,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.6,
                AddConnectionMutationProbability: 0.2,
                AddNodeMutationProbability: 0.15,
                ToggleConnectionMutationProbability: 0.05,
                WeightPerturbChance: 0.9,
                WeightPerturbScale: 0.4,
                WeightResetMin: -1,
                WeightResetMax: 1,
                BiasMutationChance: 0.7,
                BiasPerturbChance: 0.9,
                BiasPerturbScale: 0.3,
                BiasResetMin: -1,
                BiasResetMax: 1),
            InitialGenomeFactory: CreateInitialGenome,
            Seed: seed);
    }

    public override Genome CreateInitialGenome(Random rng, InnovationTracker tracker)
    {
        return CreateFullyConnectedGenome(
            rng,
            tracker,
            inputs: new[] { _inputPosition, _inputTargetDelta, _inputProgress },
            outputs: new[] { _outputMovement });
    }

    protected override SimulationTrace RunSimulation(Genome genome, Random evaluationRandom, bool captureFrames)
    {
        NeuralNetwork network = NeuralNetwork.FromGenome(genome);
        double agentPosition = 0.5;
        List<double> tokens = Enumerable.Range(0, TokenPoolSize).Select(_ => evaluationRandom.NextDouble()).ToList();
        int tokensCollected = 0;
        double closenessSum = 0d;
        List<SimulationFrame>? frames = captureFrames ? new() : null;

        for (int step = 0; step < StepCount; step++)
        {
            double closestToken = tokens.MinBy(token => Math.Abs(token - agentPosition));
            double delta = closestToken - agentPosition;
            double normalizedDelta = Math.Clamp(delta * 2d, -1d, 1d);
            double progress = (double)tokensCollected / TokenPoolSize;

            Dictionary<Guid, double> inputs = new()
            {
                [_inputPosition] = agentPosition,
                [_inputTargetDelta] = (normalizedDelta + 1d) / 2d,
                [_inputProgress] = progress,
            };

            IReadOnlyDictionary<Guid, double> outputs = network.Forward(inputs);
            double movement = (outputs[_outputMovement] - 0.5d) * 2d;
            agentPosition = Math.Clamp(agentPosition + movement * MoveSpeed, 0d, 1d);

            double distance = Math.Abs(delta);
            closenessSum += 1d - Math.Clamp(distance * 3d, 0d, 1d);

            for (int i = 0; i < tokens.Count; i++)
            {
                if (Math.Abs(agentPosition - tokens[i]) <= CollectionRadius)
                {
                    tokensCollected++;
                    tokens[i] = evaluationRandom.NextDouble();
                }
            }

            if (captureFrames)
            {
                List<SimulationActor> actors = new()
                {
                    new SimulationActor("Agent", agentPosition, 0.5, "Collector", "#1f77b4", 10d)
                };

                List<SimulationEntity> entities = tokens
                    .Select((value, index) => new SimulationEntity(value, 0.65, $"Token {index + 1}", "#f4b400", 8d))
                    .ToList();

                frames!.Add(new SimulationFrame(
                    step,
                    actors,
                    entities,
                    $"Tokens: {tokensCollected}"));
            }
        }

        double fitness = tokensCollected * 12d + (closenessSum / StepCount) * 6d;
        string summary = $"Tokens collected: {tokensCollected}";
        IReadOnlyList<SimulationFrame> finalFrames = frames is not null ? frames : Array.Empty<SimulationFrame>();
        return new SimulationTrace(fitness, finalFrames, summary);
    }

}
