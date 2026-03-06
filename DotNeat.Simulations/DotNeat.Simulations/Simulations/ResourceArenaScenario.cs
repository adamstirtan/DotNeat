using System.Collections.Generic;
using System.Linq;
using DotNeat;

namespace DotNeat.Simulations.Experiments;

public sealed class ResourceArenaScenario : SimulationScenarioBase
{
    private const int StepCount = 320;
    private const int ResourcePool = 6;
    private const double MoveScale = 0.035;
    private const double ResourceRadius = 0.05;
    private const double OpponentResourceRadius = 0.06;
    private const double OpponentCollisionRadius = 0.06;
    private const double OpponentSpeed = 0.028;

    private readonly Guid _inputAgentX = Guid.NewGuid();
    private readonly Guid _inputAgentY = Guid.NewGuid();
    private readonly Guid _inputResourceDx = Guid.NewGuid();
    private readonly Guid _inputResourceDy = Guid.NewGuid();
    private readonly Guid _inputOpponentDx = Guid.NewGuid();
    private readonly Guid _inputOpponentDy = Guid.NewGuid();
    private readonly Guid _inputResourceRatio = Guid.NewGuid();
    private readonly Guid _outputMoveX = Guid.NewGuid();
    private readonly Guid _outputMoveY = Guid.NewGuid();
    private readonly Guid _outputAggression = Guid.NewGuid();

    public override string Id => "resource-arena";
    public override string Title => "Resource Arena";
    public override string Description => "Two agents fight for limited resources in a shared arena while avoiding collisions.";
    public override string ComplexityLabel => "Complex";

    public override EvolutionOptions CreateEvolutionOptions(int seed)
    {
        return new(
            PopulationSize: 80,
            GenerationCount: 40,
            CompatibilityThreshold: 3.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.5,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.8,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.55,
                AddConnectionMutationProbability: 0.25,
                AddNodeMutationProbability: 0.15,
                ToggleConnectionMutationProbability: 0.05),
            InitialGenomeFactory: CreateInitialGenome,
            Seed: seed);
    }

    public override Genome CreateInitialGenome(Random rng, InnovationTracker tracker)
    {
        return CreateFullyConnectedGenome(
            rng,
            tracker,
            inputs: new[] { _inputAgentX, _inputAgentY, _inputResourceDx, _inputResourceDy, _inputOpponentDx, _inputOpponentDy, _inputResourceRatio },
            outputs: new[] { _outputMoveX, _outputMoveY, _outputAggression });
    }

    protected override SimulationTrace RunSimulation(Genome genome, Random evaluationRandom, bool captureFrames)
    {
        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        double agentX = 0.2;
        double agentY = 0.2;
        double opponentX = 0.8;
        double opponentY = 0.8;

        List<(double X, double Y)> resources = Enumerable.Range(0, ResourcePool)
            .Select(_ => (X: evaluationRandom.NextDouble(), Y: evaluationRandom.NextDouble()))
            .ToList();

        double opponentTargetX = evaluationRandom.NextDouble();
        double opponentTargetY = evaluationRandom.NextDouble();
        int oppositionTargetDuration = evaluationRandom.Next(20, 60);

        int resourcesCollected = 0;
        int opponentCollected = 0;
        int collisionCount = 0;
        double distanceSum = 0d;
        List<SimulationFrame>? frames = captureFrames ? new() : null;

        for (int step = 0; step < StepCount; step++)
        {
            if (step % oppositionTargetDuration == 0)
            {
                opponentTargetX = evaluationRandom.NextDouble();
                opponentTargetY = evaluationRandom.NextDouble();
                oppositionTargetDuration = evaluationRandom.Next(20, 60);
            }

            int closestResourceIndex = -1;
            double closestResourceDistance = double.MaxValue;
            double closestDx = 0d;
            double closestDy = 0d;

            for (int i = 0; i < resources.Count; i++)
            {
                double dx = resources[i].X - agentX;
                double dy = resources[i].Y - agentY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < closestResourceDistance)
                {
                    closestResourceDistance = distance;
                    closestResourceIndex = i;
                    closestDx = dx;
                    closestDy = dy;
                }
            }

            distanceSum += double.IsFinite(closestResourceDistance) ? closestResourceDistance : 1d;

            int opponentClosestIndex = -1;
            double opponentClosestDistance = double.MaxValue;

            for (int i = 0; i < resources.Count; i++)
            {
                double dx = resources[i].X - opponentX;
                double dy = resources[i].Y - opponentY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < opponentClosestDistance)
                {
                    opponentClosestDistance = distance;
                    opponentClosestIndex = i;
                }
            }

            double resourceRatio = resourcesCollected / (double)ResourcePool;
            double opponentDx = opponentX - agentX;
            double opponentDy = opponentY - agentY;

            Dictionary<Guid, double> inputs = new()
            {
                [_inputAgentX] = agentX,
                [_inputAgentY] = agentY,
                [_inputResourceDx] = (closestDx + 1d) / 2d,
                [_inputResourceDy] = (closestDy + 1d) / 2d,
                [_inputOpponentDx] = (opponentDx + 1d) / 2d,
                [_inputOpponentDy] = (opponentDy + 1d) / 2d,
                [_inputResourceRatio] = Math.Clamp(resourceRatio, 0d, 1d),
            };

            IReadOnlyDictionary<Guid, double> outputs = network.Forward(inputs);
            double moveX = (outputs[_outputMoveX] - 0.5d) * 2d;
            double moveY = (outputs[_outputMoveY] - 0.5d) * 2d;
            double aggression = outputs[_outputAggression];
            double speed = MoveScale + aggression * 0.03;

            agentX = Math.Clamp(agentX + moveX * speed, 0d, 1d);
            agentY = Math.Clamp(agentY + moveY * speed, 0d, 1d);

            double opponentDxToTarget = opponentTargetX - opponentX;
            double opponentDyToTarget = opponentTargetY - opponentY;
            double opponentTargetDistance = Math.Sqrt(opponentDxToTarget * opponentDxToTarget + opponentDyToTarget * opponentDyToTarget);

            if (opponentTargetDistance > 0)
            {
                opponentX = Math.Clamp(opponentX + (opponentDxToTarget / opponentTargetDistance) * OpponentSpeed, 0d, 1d);
                opponentY = Math.Clamp(opponentY + (opponentDyToTarget / opponentTargetDistance) * OpponentSpeed, 0d, 1d);
            }

            if (closestResourceIndex >= 0 && closestResourceDistance < ResourceRadius)
            {
                resourcesCollected++;
                resources[closestResourceIndex] = (evaluationRandom.NextDouble(), evaluationRandom.NextDouble());
                closestResourceDistance = double.MaxValue;
            }

            if (opponentClosestIndex >= 0 && opponentClosestDistance < OpponentResourceRadius)
            {
                opponentCollected++;
                resources[opponentClosestIndex] = (evaluationRandom.NextDouble(), evaluationRandom.NextDouble());
            }

            double agentOpponentDistance = Math.Sqrt(opponentDx * opponentDx + opponentDy * opponentDy);
            if (agentOpponentDistance < OpponentCollisionRadius)
            {
                collisionCount++;
            }

            if (captureFrames)
            {
                List<SimulationActor> actors = new()
                {
                    new SimulationActor("Agent", agentX, agentY, "Collector", "#1f77b4", 10d),
                    new SimulationActor("Opponent", opponentX, opponentY, "Opponent", "#ff7f0e", 10d),
                };

                List<SimulationEntity> entities = resources
                    .Select((resource, index) => new SimulationEntity(resource.X, resource.Y, $"Resource {index + 1}", "#f4b400", 8d))
                    .ToList();

                frames!.Add(new SimulationFrame(
                    step,
                    actors,
                    entities,
                    $"Agent: {resourcesCollected}, Opponent: {opponentCollected}, Collisions: {collisionCount}"));
            }
        }

        double averageDistance = distanceSum / StepCount;
        double closenessBonus = 1d - Math.Clamp(averageDistance, 0d, 1d);
        double fitness = resourcesCollected * 22d - opponentCollected * 5d - collisionCount * 6d + closenessBonus * 18d;
        string summary = $"Resources gathered: {resourcesCollected}, Shielded collisions: {collisionCount}";
        IReadOnlyList<SimulationFrame> finalFrames = frames is not null ? frames : Array.Empty<SimulationFrame>();
        return new SimulationTrace(fitness, finalFrames, summary);
    }

}
