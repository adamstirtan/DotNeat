namespace DotNeat.Tests;

[TestClass]
public sealed class CartPoleFitnessEvaluatorTests
{
    [TestMethod]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenMaxStepsIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CartPoleFitnessEvaluator(maxSteps: 0));
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenTrialsIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CartPoleFitnessEvaluator(trials: 0));
    }

    [TestMethod]
    public void Evaluate_ThrowsArgumentNullException_ForNullGenome()
    {
        CartPoleFitnessEvaluator evaluator = new();

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenInputCountIsNotFour()
    {
        CartPoleFitnessEvaluator evaluator = new();
        Genome genome = CreateMinimalGenome(inputCount: 2, outputCount: 1);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenOutputCountIsNotOne()
    {
        CartPoleFitnessEvaluator evaluator = new();
        Genome genome = CreateMinimalGenome(inputCount: 4, outputCount: 2);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    [TestMethod]
    public void Evaluate_ReturnsAtLeastOne_ForAnyController()
    {
        CartPoleFitnessEvaluator evaluator = new(maxSteps: 500, trials: 3);
        Genome genome = CreateCartPoleGenome();

        double fitness = evaluator.Evaluate(genome);

        Assert.IsGreaterThanOrEqualTo(1.0, fitness, $"Fitness should be at least 1.0, was {fitness}.");
    }

    [TestMethod]
    public void Evaluate_ReturnsMoreThanOne_ForStrongAngleController()
    {
        // A genome with a large positive weight on the pole-angle input pushes right
        // when the pole tilts right, which should keep it balanced longer than random.
        const int maxSteps = 500;
        CartPoleFitnessEvaluator evaluator = new(maxSteps: maxSteps, trials: 1);
        Genome genome = CreateStrongAngleController();

        double fitness = evaluator.Evaluate(genome);

        Assert.IsGreaterThan(1.0, fitness, $"A strong angle controller should survive > 0 steps; fitness was {fitness}.");
    }

    [TestMethod]
    public void PhysicsStep_CartMovesInForceDirection()
    {
        // Starting from rest with a positive force, the cart velocity should become positive
        (double x, double xDot, double _, double _) = CartPoleFitnessEvaluator.PhysicsStep(0, 0, 0, 0, force: 10.0);

        Assert.IsGreaterThan(0.0, xDot, $"Cart velocity should be positive after rightward force; was {xDot}.");
        Assert.IsGreaterThanOrEqualTo(0.0, x, $"Cart position should be non-negative after one rightward step; was {x}.");
    }

    [TestMethod]
    public void PhysicsStep_PoleAcceleratesUnderGravity()
    {
        // A positively tilted pole with no force should gain positive angular velocity
        (double _, double _, double _, double thetaDot) = CartPoleFitnessEvaluator.PhysicsStep(0, 0, 0.1, 0, force: 0);

        Assert.IsGreaterThan(0.0, thetaDot, $"Angular velocity should increase for a positively tilted pole; was {thetaDot}.");
    }

    [TestMethod]
    public void MaxSteps_And_Trials_ReturnConfiguredValues()
    {
        CartPoleFitnessEvaluator evaluator = new(maxSteps: 1000, trials: 3);

        Assert.AreEqual(1000, evaluator.MaxSteps);
        Assert.AreEqual(3, evaluator.Trials);
    }

    // Creates a genome with the given number of fully-connected inputs and outputs
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
                genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inId, outId, 1.0, true, innovation));
            }
        }

        return genome;
    }

    // Creates a valid 4-input, 1-output cart-pole genome with unit weights
    private static Genome CreateCartPoleGenome()
    {
        return CreateMinimalGenome(inputCount: 4, outputCount: 1);
    }

    // Creates a cart-pole genome with a strong weight on the pole-angle input (index 2),
    // biasing the network to push in the direction the pole is leaning.
    private static Genome CreateStrongAngleController()
    {
        InnovationTracker tracker = new();
        Genome genome = new();

        Guid inputPos = Guid.NewGuid();
        Guid inputVel = Guid.NewGuid();
        Guid inputAngle = Guid.NewGuid();
        Guid inputAngularVel = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        genome.Nodes.Add(new NodeGene(inputPos, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputVel, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputAngle, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(inputAngularVel, NodeType.Input, new ReluActivationFunction(), 0));
        genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), 0));

        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputPos, output, 0.0, true,
            tracker.GetOrCreateConnectionInnovation(inputPos, output)));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputVel, output, 0.0, true,
            tracker.GetOrCreateConnectionInnovation(inputVel, output)));
        // Large positive weight on angle: sigmoid output will be > 0.5 when pole tilts right
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputAngle, output, 50.0, true,
            tracker.GetOrCreateConnectionInnovation(inputAngle, output)));
        genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputAngularVel, output, 0.0, true,
            tracker.GetOrCreateConnectionInnovation(inputAngularVel, output)));

        return genome;
    }
}
