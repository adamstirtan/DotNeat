namespace DotNeat;

public sealed class Mux6FitnessEvaluator
{
    private static readonly (double[] inputs, double expected)[] Cases = BuildCases();

    public double Evaluate(Genome genome)
    {
        ArgumentNullException.ThrowIfNull(genome);

        NeuralNetwork network = NeuralNetwork.FromGenome(genome);

        if (network.InputNodeIds.Count != 6)
        {
            throw new InvalidOperationException("MUX-6 benchmark requires exactly 6 input nodes.");
        }

        if (network.OutputNodeIds.Count != 1)
        {
            throw new InvalidOperationException("MUX-6 benchmark requires exactly 1 output node.");
        }

        Guid outputId = network.OutputNodeIds[0];
        double correct = 0;

        foreach ((double[] inputs, double expected) in Cases)
        {
            Dictionary<Guid, double> byNodeId = new(6);
            for (int i = 0; i < 6; i++)
            {
                byNodeId[network.InputNodeIds[i]] = inputs[i];
            }

            IReadOnlyDictionary<Guid, double> outputs = network.Forward(byNodeId);
            double predicted = outputs[outputId] >= 0.5 ? 1d : 0d;

            if (Math.Abs(predicted - expected) < 1e-12)
            {
                correct++;
            }
        }

        return correct;
    }

    private static (double[] inputs, double expected)[] BuildCases()
    {
        List<(double[] inputs, double expected)> cases = [];

        for (int bits = 0; bits < 64; bits++)
        {
            double[] input = new double[6];
            for (int i = 0; i < 6; i++)
            {
                input[i] = ((bits >> i) & 1) == 1 ? 1d : 0d;
            }

            int address = ((int)input[0]) | (((int)input[1]) << 1);
            double expected = input[2 + address];
            cases.Add((input, expected));
        }

        return [.. cases];
    }
}
