namespace DotNeat;

/// <summary>
/// Fitness evaluator for a simple top-down car driving task.
/// The car must navigate from a fixed start position to a configurable goal inside a
/// rectangular arena using five forward-facing raycast sensors and a goal compass.
/// </summary>
/// <remarks>
/// Network topology requirements:
/// <list type="bullet">
///   <item><description>Inputs (7): rays[0–4] (left→right, normalized [0,1]) | goal angle / π ([-1,1]) | goal distance ([0,1])</description></item>
///   <item><description>Outputs (2): steering (0=hard-left, 1=hard-right) | throttle (0–1)</description></item>
/// </list>
/// Fitness:
/// <list type="bullet">
///   <item><description>Goal not reached: <c>progressRatio × MaxSteps</c> where progressRatio ∈ [0,1]</description></item>
///   <item><description>Goal reached at step s: <c>MaxSteps + (MaxSteps − s) + 1</c></description></item>
/// </list>
/// </remarks>
public sealed class CarFitnessEvaluator
{
    // ── Arena ──────────────────────────────────────────────────────────────────
    /// <summary>Width of the rectangular arena in world units.</summary>
    public const double ArenaWidth = 800.0;

    /// <summary>Height of the rectangular arena in world units.</summary>
    public const double ArenaHeight = 600.0;

    // ── Physics ────────────────────────────────────────────────────────────────
    /// <summary>Maximum car speed in world units per second.</summary>
    public const double MaxSpeed = 150.0;

    private const double MaxSteering = 2.5;  // radians per second
    private const double SpeedDecay = 0.05;  // friction coefficient

    /// <summary>Simulation time step in seconds.</summary>
    public const double TimeStep = 0.05;

    // ── Sensors ────────────────────────────────────────────────────────────────
    /// <summary>Number of raycast sensors.</summary>
    public const int NumRays = 5;

    /// <summary>Maximum raycast length in world units.</summary>
    public const double MaxRayLength = 250.0;

    // ── Goal ──────────────────────────────────────────────────────────────────
    /// <summary>Distance at which the car is considered to have reached the goal.</summary>
    public const double GoalRadius = 35.0;

    // ── Default positions ─────────────────────────────────────────────────────
    /// <summary>Default car start X position.</summary>
    public const double DefaultStartX = 80.0;

    /// <summary>Default car start Y position.</summary>
    public const double DefaultStartY = 300.0;

    /// <summary>Default goal X position.</summary>
    public const double DefaultGoalX = 700.0;

    /// <summary>Default goal Y position.</summary>
    public const double DefaultGoalY = 300.0;

    private readonly double _goalX;
    private readonly double _goalY;
    private readonly double _startX;
    private readonly double _startY;
    private readonly int _maxSteps;

    /// <summary>
    /// Initializes a new <see cref="CarFitnessEvaluator"/>.
    /// </summary>
    /// <param name="goalX">Goal X coordinate. Defaults to <see cref="DefaultGoalX"/>.</param>
    /// <param name="goalY">Goal Y coordinate. Defaults to <see cref="DefaultGoalY"/>.</param>
    /// <param name="startX">Car start X coordinate. Defaults to <see cref="DefaultStartX"/>.</param>
    /// <param name="startY">Car start Y coordinate. Defaults to <see cref="DefaultStartY"/>.</param>
    /// <param name="maxSteps">Maximum simulation steps per episode. Must be &gt;= 1.</param>
    public CarFitnessEvaluator(
        double goalX = DefaultGoalX,
        double goalY = DefaultGoalY,
        double startX = DefaultStartX,
        double startY = DefaultStartY,
        int maxSteps = 400)
    {
        if (maxSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "maxSteps must be >= 1.");
        }

        _goalX = goalX;
        _goalY = goalY;
        _startX = startX;
        _startY = startY;
        _maxSteps = maxSteps;
    }

    /// <summary>Gets the maximum number of simulation steps per episode.</summary>
    public int MaxSteps => _maxSteps;

    /// <summary>Gets the goal X coordinate used by this evaluator instance.</summary>
    public double GoalX => _goalX;

    /// <summary>Gets the goal Y coordinate used by this evaluator instance.</summary>
    public double GoalY => _goalY;

    /// <summary>
    /// Evaluates a genome on the car driving task and returns its fitness.
    /// </summary>
    /// <param name="genome">The genome to evaluate. Must have exactly 7 inputs and 2 outputs.</param>
    /// <returns>
    /// A value in <c>[0, MaxSteps]</c> when the goal is not reached; or
    /// <c>MaxSteps + (MaxSteps − stepReached) + 1</c> when the goal is reached (always &gt; MaxSteps).
    /// </returns>
    public double Evaluate(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        if (network.InputNodeIds.Count != 7)
        {
            throw new InvalidOperationException("Car benchmark requires exactly 7 input nodes.");
        }

        if (network.OutputNodeIds.Count != 2)
        {
            throw new InvalidOperationException("Car benchmark requires exactly 2 output nodes.");
        }

        return RunEpisode(network, _goalX, _goalY, _startX, _startY, _maxSteps);
    }

    /// <summary>
    /// Runs a single episode with the given network and goal, returning the fitness score.
    /// </summary>
    public static double RunEpisode(
        NeuralNetwork network,
        double goalX,
        double goalY,
        double startX = DefaultStartX,
        double startY = DefaultStartY,
        int maxSteps = 400)
    {
        double x = startX;
        double y = startY;
        double heading = 0.0;
        double speed = 0.0;

        double initialDist = Euclidean(x, y, goalX, goalY);

        if (initialDist < 1e-6)
        {
            return (maxSteps * 2) + 1;
        }

        double bestDist = initialDist;

        IReadOnlyList<Guid> inputIds = network.InputNodeIds;
        Guid steeringId = network.OutputNodeIds[0];
        Guid throttleId = network.OutputNodeIds[1];

        for (int step = 0; step < maxSteps; step++)
        {
            double[] rays = CastRays(x, y, heading);
            double goalAngle = NormalizeAngle(Math.Atan2(goalY - y, goalX - x) - heading);
            double goalDist = Math.Clamp(Euclidean(x, y, goalX, goalY) / (ArenaWidth * 0.8), 0.0, 1.0);

            Dictionary<Guid, double> inputMap = new(7)
            {
                [inputIds[0]] = rays[0],
                [inputIds[1]] = rays[1],
                [inputIds[2]] = rays[2],
                [inputIds[3]] = rays[3],
                [inputIds[4]] = rays[4],
                [inputIds[5]] = goalAngle / Math.PI,
                [inputIds[6]] = goalDist,
            };

            IReadOnlyDictionary<Guid, double> outputMap = network.Forward(inputMap);
            double steeringOutput = outputMap[steeringId];
            double throttleOutput = outputMap[throttleId];

            (x, y, heading, speed) = PhysicsStep(x, y, heading, speed, steeringOutput, throttleOutput);

            double dist = Euclidean(x, y, goalX, goalY);

            if (dist < bestDist)
            {
                bestDist = dist;
            }

            if (dist < GoalRadius)
            {
                return maxSteps + (maxSteps - step) + 1;
            }

            if (x < 0 || x > ArenaWidth || y < 0 || y > ArenaHeight)
            {
                break;
            }
        }

        double progressRatio = Math.Clamp(1.0 - (bestDist / initialDist), 0.0, 1.0);
        return progressRatio * maxSteps;
    }

    /// <summary>
    /// Advances the car state by one timestep using kinematic equations.
    /// </summary>
    /// <param name="steeringOutput">Network steering output in [0,1]. 0 = hard-left, 1 = hard-right.</param>
    /// <param name="throttleOutput">Network throttle output in [0,1].</param>
    public static (double x, double y, double heading, double speed) PhysicsStep(
        double x,
        double y,
        double heading,
        double speed,
        double steeringOutput,
        double throttleOutput)
    {
        double steeringDelta = (steeringOutput * 2.0 - 1.0) * MaxSteering * TimeStep;
        double newHeading = heading + steeringDelta;

        double newSpeed = Math.Clamp(
            speed + (throttleOutput * MaxSpeed * TimeStep) - (speed * SpeedDecay),
            0.0,
            MaxSpeed);

        double newX = x + (Math.Cos(newHeading) * newSpeed * TimeStep);
        double newY = y + (Math.Sin(newHeading) * newSpeed * TimeStep);

        return (newX, newY, newHeading, newSpeed);
    }

    /// <summary>
    /// Casts five rays from the car's position (left, left-front, front, right-front, right)
    /// and returns normalized distances in [0,1] (1 = no wall within <see cref="MaxRayLength"/>).
    /// </summary>
    public static double[] CastRays(double x, double y, double heading)
    {
        double[] angles = [-Math.PI / 2.0, -Math.PI / 4.0, 0.0, Math.PI / 4.0, Math.PI / 2.0];
        double[] result = new double[NumRays];

        for (int i = 0; i < NumRays; i++)
        {
            result[i] = CastRay(x, y, heading + angles[i]) / MaxRayLength;
        }

        return result;
    }

    private static double CastRay(double x, double y, double angle)
    {
        double dx = Math.Cos(angle);
        double dy = Math.Sin(angle);
        double t = MaxRayLength;

        // Left wall (x = 0)
        if (dx < -1e-9)
        {
            double tHit = -x / dx;
            double hitY = y + (tHit * dy);
            if (tHit > 0 && tHit < t && hitY >= 0 && hitY <= ArenaHeight)
            {
                t = tHit;
            }
        }

        // Right wall (x = ArenaWidth)
        if (dx > 1e-9)
        {
            double tHit = (ArenaWidth - x) / dx;
            double hitY = y + (tHit * dy);
            if (tHit > 0 && tHit < t && hitY >= 0 && hitY <= ArenaHeight)
            {
                t = tHit;
            }
        }

        // Top wall (y = 0)
        if (dy < -1e-9)
        {
            double tHit = -y / dy;
            double hitX = x + (tHit * dx);
            if (tHit > 0 && tHit < t && hitX >= 0 && hitX <= ArenaWidth)
            {
                t = tHit;
            }
        }

        // Bottom wall (y = ArenaHeight)
        if (dy > 1e-9)
        {
            double tHit = (ArenaHeight - y) / dy;
            double hitX = x + (tHit * dx);
            if (tHit > 0 && tHit < t && hitX >= 0 && hitX <= ArenaWidth)
            {
                t = tHit;
            }
        }

        return t;
    }

    private static double Euclidean(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2.0 * Math.PI;
        }

        while (angle < -Math.PI)
        {
            angle += 2.0 * Math.PI;
        }

        return angle;
    }
}
