using System.Text.Json;
using DotNeat;
using DotNeat.Runner.Net;
using DotNeat.Runner.Sim;
using DotNeat.Runner.Visualization;

namespace DotNeat.Runner.Experiments;

/// <summary>
/// Interactive top-down car driving experiment.
/// Evolves NEAT controllers that steer a kinematic car to a user-specified goal
/// inside a rectangular arena.  A browser UI is served at <c>http://localhost:5000</c>
/// showing the champion's trajectory, sensor rays, fitness sparklines, and a
/// click-to-set-goal control.
/// </summary>
public sealed class CarExperiment(int seed = 12345) : IExperiment
{
    private readonly int _seed = seed;

    // Mutable goal protected by a lock (updated from the WebSocket background thread)
    private double _goalX = CarFitnessEvaluator.DefaultGoalX;
    private double _goalY = CarFitnessEvaluator.DefaultGoalY;
    private readonly object _goalLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public string Name => "car";

    /// <inheritdoc/>
    public void Run()
    {
        const int maxSteps = 400;

        LocalServer? server = null;
        try
        {
            server = new LocalServer(
                onGoalChanged: (x, y) =>
                {
                    lock (_goalLock)
                    {
                        _goalX = Math.Clamp(x, 0, CarFitnessEvaluator.ArenaWidth);
                        _goalY = Math.Clamp(y, 0, CarFitnessEvaluator.ArenaHeight);
                    }

                    Console.WriteLine($"Goal updated → ({x:F0}, {y:F0})");
                });

            server.Start();
            Console.WriteLine("Car experiment UI → http://localhost:5000");
            Console.WriteLine("Open this URL in a browser to watch evolution and click to move the goal.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not start local server ({ex.Message}). Continuing without UI.");
            server?.Dispose();
            server = null;
        }

        // Build genome node IDs (7 inputs, 2 outputs)
        Guid[] inputIds = [
            Guid.NewGuid(), // ray 0  – left
            Guid.NewGuid(), // ray 1  – left-front
            Guid.NewGuid(), // ray 2  – front
            Guid.NewGuid(), // ray 3  – right-front
            Guid.NewGuid(), // ray 4  – right
            Guid.NewGuid(), // goal angle
            Guid.NewGuid(), // goal distance
        ];

        Guid steeringId = Guid.NewGuid();
        Guid throttleId = Guid.NewGuid();

        EvolutionOptions options = new(
            PopulationSize: 150,
            GenerationCount: 200,
            CompatibilityThreshold: 3.0,
            C1: 1.0,
            C2: 1.0,
            C3: 0.4,
            Reproduction: new ReproductionOptions(
                ElitesPerSpecies: 1,
                TournamentSize: 3,
                CrossoverProbability: 0.75,
                MutationProbability: 0.9,
                WeightMutationProbability: 0.75,
                AddConnectionMutationProbability: 0.10,
                AddNodeMutationProbability: 0.10,
                ToggleConnectionMutationProbability: 0.05,
                WeightPerturbChance: 0.9,
                WeightPerturbScale: 0.5,
                WeightResetMin: -2,
                WeightResetMax: 2),
            InitialGenomeFactory: (rng, tracker) =>
                CreateGenome(rng, tracker, inputIds, steeringId, throttleId),
            Seed: _seed);

        Func<Genome, double> evaluate = genome =>
        {
            double gx, gy;
            lock (_goalLock)
            {
                gx = _goalX;
                gy = _goalY;
            }

            return new CarFitnessEvaluator(
                goalX: gx,
                goalY: gy,
                maxSteps: maxSteps).Evaluate(genome);
        };

        Console.WriteLine($"Car Experiment | Seed: {_seed} | MaxSteps: {maxSteps} | Pop: {options.PopulationSize}");
        Console.WriteLine($"Solving criterion: fitness > {maxSteps} (goal reached)");
        Console.WriteLine();
        Console.WriteLine("Generation | BestFitness | AvgFitness | Species | AvgComplexity");

        HashSet<int> snapshotGens =
            ExperimentVisualization.SelectSnapshotGenerations(options.GenerationCount, desiredSnapshotCount: 5);
        List<NetworkSnapshot> snapshots = [];

        EvolutionOrchestrator orchestrator = new(evaluate, options);
        EvolutionResult result = orchestrator.Run(
            onGenerationCompleted: metrics =>
            {
                if (metrics.Generation % 10 == 0 || metrics.Generation == options.GenerationCount - 1)
                {
                    Console.WriteLine(
                        $"{metrics.Generation,10} | {metrics.BestFitness,11:F2} | " +
                        $"{metrics.AverageFitness,10:F2} | {metrics.SpeciesCount,7} | {metrics.AverageComplexity,13:F2}");
                }
            },
            onGenerationChampionCaptured: (metrics, champion) =>
            {
                if (snapshotGens.Contains(metrics.Generation))
                {
                    snapshots.Add(new NetworkSnapshot(metrics.Generation, champion));
                }

                if (server is not null)
                {
                    StreamChampion(server, metrics, champion);
                }
            });

        server?.Dispose();

        Console.WriteLine();
        bool solved = result.BestFitness > maxSteps;
        Console.WriteLine($"Best fitness: {result.BestFitness:F2}" +
            (solved ? $"  ✓ SOLVED in {result.History.Count} generations!" : "  (goal not reached)"));

        string reportPath = ExperimentVisualization.WriteEvolutionReport(
            experimentName: Name,
            seed: _seed,
            history: result.History,
            goalFitness: maxSteps + 1,
            goalLabel: "Goal reached",
            networkSnapshots: snapshots);

        Console.WriteLine($"Visualization report: {reportPath}");
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    private void StreamChampion(LocalServer server, GenerationMetrics metrics, Genome champion)
    {
        double gx, gy;
        lock (_goalLock)
        {
            gx = _goalX;
            gy = _goalY;
        }

        try
        {
            NeuralNetwork network = NeuralNetwork.FromGenome(champion);
            IReadOnlyList<CarFrame> frames = CarSimulator.RecordEpisode(network, gx, gy);

            string json = BuildGenerationMessage(metrics, gx, gy, frames);
            server.EnqueueBroadcast(json);
        }
        catch
        {
            // Swallow errors so a bad champion doesn't crash the experiment
        }
    }

    private static string BuildGenerationMessage(
        GenerationMetrics metrics,
        double goalX,
        double goalY,
        IReadOnlyList<CarFrame> frames)
    {
        GenerationMessageDto dto = new(
            Type: "generation",
            Generation: metrics.Generation,
            BestFitness: metrics.BestFitness,
            AvgFitness: metrics.AverageFitness,
            Species: metrics.SpeciesCount,
            Complexity: metrics.AverageComplexity,
            GoalX: goalX,
            GoalY: goalY,
            Trajectory: [.. frames.Select(f => new FrameDto(
                X: f.X,
                Y: f.Y,
                Heading: f.Heading,
                Speed: f.Speed,
                Sensors: f.Sensors,
                Steering: f.Steering,
                Throttle: f.Throttle))]);

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // ── Genome factory ────────────────────────────────────────────────────────

    private static Genome CreateGenome(
        Random rng,
        InnovationTracker tracker,
        Guid[] inputIds,
        Guid steeringId,
        Guid throttleId)
    {
        Genome genome = new();

        foreach (Guid id in inputIds)
        {
            genome.Nodes.Add(new NodeGene(id, NodeType.Input, new ReluActivationFunction(), 0));
        }

        genome.Nodes.Add(new NodeGene(steeringId, NodeType.Output, new SigmoidActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(throttleId, NodeType.Output, new SigmoidActivationFunction(), 0));

        // Connect every input to both outputs with small random weights
        foreach (Guid inputId in inputIds)
        {
            int innov0 = tracker.GetOrCreateConnectionInnovation(inputId, steeringId);
            int innov1 = tracker.GetOrCreateConnectionInnovation(inputId, throttleId);
            genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, steeringId, NextWeight(rng), true, innov0));
            genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, throttleId, NextWeight(rng), true, innov1));
        }

        return genome;
    }

    private static double NextWeight(Random rng) => -1.0 + (2.0 * rng.NextDouble());

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record FrameDto(
        double X,
        double Y,
        double Heading,
        double Speed,
        double[] Sensors,
        double Steering,
        double Throttle);

    private sealed record GenerationMessageDto(
        string Type,
        int Generation,
        double BestFitness,
        double AvgFitness,
        int Species,
        double Complexity,
        double GoalX,
        double GoalY,
        FrameDto[] Trajectory);
}
