using System.Linq;
using DotNeat;

namespace DotNeat.Simulations.Experiments;

public sealed class ObstacleNavigatorScenario : SimulationScenarioBase
{
    private const int StepCount = 260;
    private const double StepScale = 0.035;
    private const double GoalRadius = 0.04;
    private const double ObstacleRadius = 0.07;
    private const int ObstacleCount = 3;

    private readonly Guid _inputAgentX = Guid.NewGuid();
    private readonly Guid _inputAgentY = Guid.NewGuid();
    private readonly Guid _inputGoalDx = Guid.NewGuid();
    private readonly Guid _inputGoalDy = Guid.NewGuid();
    private readonly Guid _inputObstacleDx = Guid.NewGuid();
    private readonly Guid _inputObstacleDy = Guid.NewGuid();
    private readonly Guid _outputMoveX = Guid.NewGuid();
    private readonly Guid _outputMoveY = Guid.NewGuid();

    public override string Id => "obstacle-navigator";
    public override string Title => "Obstacle Navigator";
    public override string Description => "A single agent navigates a cluttered arena to reach a goal while dodging moving obstacles.";
    public override string ComplexityLabel => "Medium";

    public override EvolutionOptions CreateEvolutionOptions(int seed)
    {
        return new(
            PopulationSize: 60,
            GenerationCount: 35,
            CompatibilityThreshold: 3.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.5,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.75,
                MutationProbability: 0.88,
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
            inputs: new[] { _inputAgentX, _inputAgentY, _inputGoalDx, _inputGoalDy, _inputObstacleDx, _inputObstacleDy },
            outputs: new[] { _outputMoveX, _outputMoveY });
    }

    protected override SimulationTrace RunSimulation(Genome genome, Random evaluationRandom, bool captureFrames)
    {
        NeuralNetwork network = NeuralNetwork.FromGenome(genome);
        double agentX = 0.1;
        double agentY = 0.1;
        double goalX = 0.5 + evaluationRandom.NextDouble() * 0.4;
        double goalY = 0.5 + evaluationRandom.NextDouble() * 0.4;

        List<(double angleOffset, double radius)> obstacles = Enumerable.Range(0, ObstacleCount)
            .Select(offset => (angleOffset: evaluationRandom.NextDouble() * Math.PI * 2, radius: 0.2 + evaluationRandom.NextDouble() * 0.25))
            .ToList();

        int collisionCount = 0;
        bool reachedGoal = false;
        double closenessSum = 0d;
        List<SimulationFrame>? frames = captureFrames ? new() : null;

        for (int step = 0; step < StepCount; step++)
        {
            List<SimulationEntity> obstacleEntities = new();
            double minObstacleDistance = double.MaxValue;
            double nearestObstacleDx = 0d;
            double nearestObstacleDy = 0d;

            for (int index = 0; index < obstacles.Count; index++)
            {
                (double angleOffset, double radius) = obstacles[index];
                double theta = angleOffset + step * 0.05;
                double obsX = 0.5 + Math.Cos(theta) * radius;
                double obsY = 0.5 + Math.Sin(theta) * radius;

                double dx = obsX - agentX;
                double dy = obsY - agentY;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance < minObstacleDistance)
                {
                    minObstacleDistance = distance;
                    nearestObstacleDx = Math.Clamp(dx * 2d, -1d, 1d);
                    nearestObstacleDy = Math.Clamp(dy * 2d, -1d, 1d);
                }

                if (captureFrames)
                {
                    obstacleEntities.Add(new SimulationEntity(obsX, obsY, $"Obstacle {index + 1}", "#d62728", 12d));
                }

                if (distance < ObstacleRadius)
                {
                    collisionCount++;
                }
            }

            double goalDx = goalX - agentX;
            double goalDy = goalY - agentY;
            double goalDistance = Math.Sqrt(goalDx * goalDx + goalDy * goalDy);
            closenessSum += 1d - Math.Clamp(goalDistance * 1.5d, 0d, 1d);

            if (goalDistance < GoalRadius)
            {
                reachedGoal = true;
                goalDx = 0d;
                goalDy = 0d;
            }

            Dictionary<Guid, double> inputs = new()
            {
                [_inputAgentX] = agentX,
                [_inputAgentY] = agentY,
                [_inputGoalDx] = Normalize(goalDx, -1d, 1d),
                [_inputGoalDy] = Normalize(goalDy, -1d, 1d),
                [_inputObstacleDx] = (nearestObstacleDx + 1d) / 2d,
                [_inputObstacleDy] = (nearestObstacleDy + 1d) / 2d,
            };

            IReadOnlyDictionary<Guid, double> outputs = network.Forward(inputs);
            double moveX = (outputs[_outputMoveX] - 0.5d) * 2d;
            double moveY = (outputs[_outputMoveY] - 0.5d) * 2d;
            agentX = Math.Clamp(agentX + moveX * StepScale, 0d, 1d);
            agentY = Math.Clamp(agentY + moveY * StepScale, 0d, 1d);

                if (captureFrames)
                {
                    List<SimulationActor> actors = new()
                    {
                    new SimulationActor("Agent", agentX, agentY, "Navigator", "#2ca02c", 10d),
                    new SimulationActor("Goal", goalX, goalY, "Goal", "#9467bd", 12d),
                };

                List<SimulationEntity> entities = new()
                {
                    new SimulationEntity(goalX, goalY, "Goal", "#9467bd", 14d)
                };

                entities.AddRange(obstacleEntities);

                frames!.Add(new SimulationFrame(
                    step,
                    actors,
                    entities,
                    reachedGoal
                        ? "Goal reached!"
                        : $"Distance: {goalDistance:F2}, Collisions: {collisionCount}"));
            }

            if (reachedGoal)
            {
                break;
            }
        }

        double fitness = (reachedGoal ? 400d : 0d) + (closenessSum / StepCount) * 40d - collisionCount * 12d;
        string summary = reachedGoal ? "Reached the goal!" : "Goal still missing.";
        IReadOnlyList<SimulationFrame> finalFrames = frames is not null ? frames : Array.Empty<SimulationFrame>();
        return new SimulationTrace(fitness, finalFrames, summary);
    }

}
