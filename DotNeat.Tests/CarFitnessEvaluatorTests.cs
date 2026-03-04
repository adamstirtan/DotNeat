namespace DotNeat.Tests;

[TestClass]
public sealed class CarFitnessEvaluatorTests
{
    // ── Constructor validation ────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenMaxStepsIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CarFitnessEvaluator(maxSteps: 0));
    }

    [TestMethod]
    public void MaxSteps_ReturnsConfiguredValue()
    {
        CarFitnessEvaluator evaluator = new(maxSteps: 200);
        Assert.AreEqual(200, evaluator.MaxSteps);
    }

    [TestMethod]
    public void GoalXY_ReturnConfiguredValues()
    {
        CarFitnessEvaluator evaluator = new(goalX: 500.0, goalY: 250.0);
        Assert.AreEqual(500.0, evaluator.GoalX);
        Assert.AreEqual(250.0, evaluator.GoalY);
    }

    // ── Evaluate input validation ─────────────────────────────────────────────

    [TestMethod]
    public void Evaluate_ThrowsArgumentNullException_ForNullGenome()
    {
        CarFitnessEvaluator evaluator = new();
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenInputCountIsNot7()
    {
        CarFitnessEvaluator evaluator = new();
        Genome genome = CreateMinimalGenome(inputCount: 4, outputCount: 2);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenOutputCountIsNot2()
    {
        CarFitnessEvaluator evaluator = new();
        Genome genome = CreateMinimalGenome(inputCount: 7, outputCount: 1);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    // ── Fitness properties ────────────────────────────────────────────────────

    [TestMethod]
    public void Evaluate_ReturnsNonNegativeFitness_ForAnyController()
    {
        CarFitnessEvaluator evaluator = new(maxSteps: 50);
        Genome genome = CreateCarGenome();

        double fitness = evaluator.Evaluate(genome);

        Assert.IsGreaterThanOrEqualTo(0.0, fitness, $"Fitness should be non-negative; was {fitness}.");
    }

    [TestMethod]
    public void Evaluate_FitnessAboveMaxSteps_WhenGoalReached()
    {
        // Place start and goal very close together so any controller reaches it
        const int maxSteps = 200;
        CarFitnessEvaluator evaluator = new(
            goalX: CarFitnessEvaluator.DefaultStartX + CarFitnessEvaluator.GoalRadius * 0.5,
            goalY: CarFitnessEvaluator.DefaultStartY,
            maxSteps: maxSteps);

        // Any genome — the car already starts inside the goal radius
        Genome genome = CreateCarGenome();
        double fitness = evaluator.Evaluate(genome);

        // When start is inside the goal the evaluator should return the "already at goal" score
        Assert.IsGreaterThan(maxSteps, fitness,
            $"Fitness should exceed maxSteps when starting on the goal; was {fitness}.");
    }

    [TestMethod]
    public void Evaluate_FitnessAtMostTwoTimesMaxSteps_PlusOffset()
    {
        const int maxSteps = 100;
        CarFitnessEvaluator evaluator = new(maxSteps: maxSteps);
        Genome genome = CreateCarGenome();

        double fitness = evaluator.Evaluate(genome);

        // Maximum possible fitness is 2*maxSteps + maxSteps + 1 (reached at step 0 from offset start)
        double absoluteMax = (maxSteps * 2) + maxSteps + 1;
        Assert.IsGreaterThanOrEqualTo(fitness, absoluteMax,
            $"Fitness {fitness} exceeds theoretical maximum {absoluteMax}.");
    }

    // ── Physics step ─────────────────────────────────────────────────────────

    [TestMethod]
    public void PhysicsStep_SpeedIncreasesWithFullThrottle()
    {
        (double _, double _, double _, double speed) =
            CarFitnessEvaluator.PhysicsStep(0, 0, 0, 0, steeringOutput: 0.5, throttleOutput: 1.0);

        Assert.IsGreaterThan(0.0, speed, $"Speed should increase with full throttle; was {speed}.");
    }

    [TestMethod]
    public void PhysicsStep_CarMovesEastWithZeroSteering()
    {
        // heading=0, full throttle, neutral steering → moves in +X direction
        double x = 100, y = 300;
        (double newX, double newY, double _, double _) =
            CarFitnessEvaluator.PhysicsStep(x, y, 0.0, CarFitnessEvaluator.MaxSpeed, 0.5, 1.0);

        Assert.IsGreaterThan(x, newX, $"Car should move east from x={x}; newX was {newX}.");
        Assert.AreEqual(y, newY, 0.01, $"Car should not drift north/south; newY was {newY}.");
    }

    [TestMethod]
    public void PhysicsStep_SteeringLeftDecreasesHeading()
    {
        // Full left steering (output=0) should produce negative heading delta
        (double _, double _, double newHeading, double _) =
            CarFitnessEvaluator.PhysicsStep(400, 300, 0, 60.0, steeringOutput: 0.0, throttleOutput: 0.5);

        Assert.IsGreaterThan(newHeading, 0.0,
            $"Full-left steering should yield negative heading; was {newHeading}.");
    }

    // ── Raycasting ────────────────────────────────────────────────────────────

    [TestMethod]
    public void CastRays_ReturnsExactlyFiveValues()
    {
        double[] rays = CarFitnessEvaluator.CastRays(400, 300, 0);
        Assert.HasCount(CarFitnessEvaluator.NumRays, rays);
    }

    [TestMethod]
    public void CastRays_AllValuesInZeroToOne()
    {
        double[] rays = CarFitnessEvaluator.CastRays(400, 300, 0);

        foreach (double r in rays)
        {
            Assert.IsGreaterThanOrEqualTo(0.0, r, $"Ray value {r} is below 0.");
            Assert.IsGreaterThanOrEqualTo(r, 1.0, $"Ray value {r} exceeds 1.");
        }
    }

    [TestMethod]
    public void CastRays_FrontRayReturnsSmallerValue_WhenNearRightWall()
    {
        // Car positioned very close to the right wall facing right (heading=0)
        // The front ray should hit quickly (small normalized distance)
        double[] raysClose = CarFitnessEvaluator.CastRays(
            CarFitnessEvaluator.ArenaWidth - 10, 300, 0);

        double[] raysFar = CarFitnessEvaluator.CastRays(
            CarFitnessEvaluator.ArenaWidth / 2, 300, 0);

        // Front ray index = 2
        Assert.IsGreaterThan(raysClose[2], raysFar[2],
            $"Front ray near wall ({raysClose[2]:F3}) should be shorter than far ray ({raysFar[2]:F3}).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Genome CreateMinimalGenome(int inputCount, int outputCount)
    {
        InnovationTracker tracker = new();
        Genome genome = new();

        List<Guid> inputs = [];
        for (int i = 0; i < inputCount; i++)
        {
            Guid id = Guid.NewGuid();
            inputs.Add(id);
            genome.Nodes.Add(new NodeGene(id, NodeType.Input, new ReluActivationFunction(), 0));
        }

        for (int o = 0; o < outputCount; o++)
        {
            Guid outId = Guid.NewGuid();
            genome.Nodes.Add(new NodeGene(outId, NodeType.Output, new SigmoidActivationFunction(), 0));

            foreach (Guid inId in inputs)
            {
                int innovation = tracker.GetOrCreateConnectionInnovation(inId, outId);
                genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inId, outId, 0.5, true, innovation));
            }
        }

        return genome;
    }

    private static Genome CreateCarGenome() => CreateMinimalGenome(inputCount: 7, outputCount: 2);
}
