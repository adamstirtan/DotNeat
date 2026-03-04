namespace DotNeat;

public sealed class XorFitnessEvaluator
{
    private static readonly (double x1, double x2, double expected)[] XorCases =
    [
        (0d, 0d, 0d),
        (0d, 1d, 1d),
        (1d, 0d, 1d),
        (1d, 1d, 0d),
    ];

    private readonly Random _random;

    public XorFitnessEvaluator(int seed = 12345)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int Seed { get; }

    public double NextDeterministicWeight(double minValue = -1d, double maxValue = 1d)
    {
        if (maxValue <= minValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "maxValue must be greater than minValue.");
        }

        return minValue + (_random.NextDouble() * (maxValue - minValue));
    }

    public double Evaluate(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        if (network.InputNodeIds.Count != 2)
        {
            throw new InvalidOperationException("XOR benchmark requires exactly 2 input nodes.");
        }

        if (network.OutputNodeIds.Count != 1)
        {
            throw new InvalidOperationException("XOR benchmark requires exactly 1 output node.");
        }

        Guid inputA = network.InputNodeIds[0];
        Guid inputB = network.InputNodeIds[1];
        Guid output = network.OutputNodeIds[0];

        double totalSquaredError = 0d;

        foreach ((double x1, double x2, double expected) in XorCases)
        {
            IReadOnlyDictionary<Guid, double> outputs = network.Forward(new Dictionary<Guid, double>
            {
                [inputA] = x1,
                [inputB] = x2,
            });

            double error = expected - outputs[output];
            totalSquaredError += error * error;
        }

        double fitness = XorCases.Length - totalSquaredError;
        return Math.Max(0d, fitness);
    }
}
