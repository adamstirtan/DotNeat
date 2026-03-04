namespace DotNeat;

/// <summary>
/// Fitness evaluator for the classic single-pole (cart-pole) balancing task.
/// </summary>
/// <remarks>
/// The cart-pole environment uses simplified Euler-integrated dynamics matching
/// the original NEAT paper and OpenAI Gym CartPole-v1 parameters. The genome
/// requires exactly 4 input nodes (cart position, cart velocity, pole angle,
/// pole angular velocity) and 1 output node (force direction: output &gt; 0.5
/// applies a positive force; otherwise a negative force).
///
/// Inputs are normalized to approximately [-1, 1] before being fed into the
/// network. Fitness is the average number of timesteps survived across all
/// trials, plus 1 (to avoid zero fitness).
/// </remarks>
public sealed class CartPoleFitnessEvaluator
{
    // Physics constants matching OpenAI Gym CartPole-v1
    private const double Gravity = 9.8;
    private const double MassCart = 1.0;
    private const double MassPole = 0.1;
    private const double HalfPoleLength = 0.5;
    private const double ForceMagnitude = 10.0;
    private const double TimeStep = 0.02;

    // Episode termination thresholds
    private const double MaxCartPosition = 2.4;
    private const double MaxPoleAngleRadians = 12.0 * Math.PI / 180.0;

    // Normalization denominators for scaling state to [-1, 1]
    private const double MaxCartVelocity = 3.0;
    private const double MaxPoleAngularVelocity = 3.5;

    private readonly int _maxSteps;
    private readonly int _trials;

    /// <summary>
    /// Initializes a new <see cref="CartPoleFitnessEvaluator"/>.
    /// </summary>
    /// <param name="maxSteps">Maximum timesteps per episode. Default is 500.</param>
    /// <param name="trials">Number of independent trials per genome. Default is 5.</param>
    public CartPoleFitnessEvaluator(int maxSteps = 500, int trials = 5)
    {
        if (maxSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "maxSteps must be >= 1.");
        }

        if (trials < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(trials), "trials must be >= 1.");
        }

        _maxSteps = maxSteps;
        _trials = trials;
    }

    /// <summary>Gets the maximum number of timesteps per episode.</summary>
    public int MaxSteps => _maxSteps;

    /// <summary>Gets the number of independent trials used per evaluation.</summary>
    public int Trials => _trials;

    /// <summary>
    /// Evaluates a genome on the cart-pole task.
    /// </summary>
    /// <param name="genome">The genome to evaluate.</param>
    /// <returns>
    /// Average timesteps survived over all trials, plus 1. A perfectly
    /// balancing controller returns <c>MaxSteps + 1</c>.
    /// </returns>
    public double Evaluate(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        if (network.InputNodeIds.Count != 4)
        {
            throw new InvalidOperationException("Cart-pole benchmark requires exactly 4 input nodes.");
        }

        if (network.OutputNodeIds.Count != 1)
        {
            throw new InvalidOperationException("Cart-pole benchmark requires exactly 1 output node.");
        }

        double totalSteps = 0.0;

        for (int trial = 0; trial < _trials; trial++)
        {
            totalSteps += RunEpisode(network, trial);
        }

        return (totalSteps / _trials) + 1.0;
    }

    private int RunEpisode(NeuralNetwork network, int trial)
    {
        // Vary starting conditions slightly across trials for robustness
        double x = (trial % 2 == 0) ? 0.0 : 0.05;
        double xDot = 0.0;
        double theta = (trial % 2 == 0) ? 0.01 : -0.01;
        double thetaDot = 0.0;

        Guid input0 = network.InputNodeIds[0];
        Guid input1 = network.InputNodeIds[1];
        Guid input2 = network.InputNodeIds[2];
        Guid input3 = network.InputNodeIds[3];
        Guid outputId = network.OutputNodeIds[0];

        for (int step = 0; step < _maxSteps; step++)
        {
            // Normalize state to [-1, 1] to avoid input saturation
            Dictionary<Guid, double> inputs = new()
            {
                [input0] = Math.Clamp(x / MaxCartPosition, -1.0, 1.0),
                [input1] = Math.Clamp(xDot / MaxCartVelocity, -1.0, 1.0),
                [input2] = Math.Clamp(theta / MaxPoleAngleRadians, -1.0, 1.0),
                [input3] = Math.Clamp(thetaDot / MaxPoleAngularVelocity, -1.0, 1.0),
            };

            IReadOnlyDictionary<Guid, double> outputs = network.Forward(inputs);

            // Output > 0.5 → push right (+force); otherwise push left (−force)
            double force = outputs[outputId] > 0.5 ? ForceMagnitude : -ForceMagnitude;

            (x, xDot, theta, thetaDot) = PhysicsStep(x, xDot, theta, thetaDot, force);

            if (Math.Abs(x) > MaxCartPosition || Math.Abs(theta) > MaxPoleAngleRadians)
            {
                return step;
            }
        }

        return _maxSteps;
    }

    /// <summary>
    /// Advances the cart-pole state by one timestep using Euler integration.
    /// </summary>
    public static (double x, double xDot, double theta, double thetaDot) PhysicsStep(
        double x, double xDot, double theta, double thetaDot, double force)
    {
        double totalMass = MassCart + MassPole;
        double poleMassLength = MassPole * HalfPoleLength;
        double cosTheta = Math.Cos(theta);
        double sinTheta = Math.Sin(theta);

        double temp = (force + poleMassLength * thetaDot * thetaDot * sinTheta) / totalMass;
        double thetaAcc = (Gravity * sinTheta - cosTheta * temp)
            / (HalfPoleLength * (4.0 / 3.0 - MassPole * cosTheta * cosTheta / totalMass));
        double xAcc = temp - poleMassLength * thetaAcc * cosTheta / totalMass;

        return (
            x + TimeStep * xDot,
            xDot + TimeStep * xAcc,
            theta + TimeStep * thetaDot,
            thetaDot + TimeStep * thetaAcc);
    }
}
