namespace DotNeat.Tests;

[TestClass]
public sealed class Mux6FitnessEvaluatorTests
{
    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenInputCountIsNotSix()
    {
        Mux6FitnessEvaluator evaluator = new();
        Genome genome = CreateGenome(inputCount: 2, outputCount: 1, weight: 1);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    [TestMethod]
    public void Evaluate_ThrowsInvalidOperationException_WhenOutputCountIsNotOne()
    {
        Mux6FitnessEvaluator evaluator = new();
        Genome genome = CreateGenome(inputCount: 6, outputCount: 2, weight: 1);

        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(genome));
    }

    [TestMethod]
    public void Evaluate_ReturnsFitnessInExpectedRange()
    {
        Mux6FitnessEvaluator evaluator = new();
        Genome genome = CreateGenome(inputCount: 6, outputCount: 1, weight: 0);

        double fitness = evaluator.Evaluate(genome);

        Assert.IsGreaterThanOrEqualTo(0d, fitness);
        Assert.IsLessThanOrEqualTo(64d, fitness);
    }

    [TestMethod]
    public void Evaluate_ConstantZeroClassifier_GetsExpectedScore()
    {
        Mux6FitnessEvaluator evaluator = new();
        Genome genome = CreateGenome(inputCount: 6, outputCount: 1, weight: 0);

        // With zero weights/bias and sigmoid output thresholded at 0.5,
        // classifier predicts 1 for all rows. Exactly half rows are 1 in MUX-6.
        double fitness = evaluator.Evaluate(genome);

        Assert.AreEqual(32d, fitness, 1e-12);
    }

    private static Genome CreateGenome(int inputCount, int outputCount, double weight)
    {
        InnovationTracker tracker = new();
        Genome genome = new();

        List<Guid> inputIds = [];
        for (int i = 0; i < inputCount; i++)
        {
            Guid inputId = Guid.NewGuid();
            inputIds.Add(inputId);
            genome.Nodes.Add(new NodeGene(inputId, NodeType.Input, new ReluActivationFunction(), 0));
        }

        for (int o = 0; o < outputCount; o++)
        {
            Guid outputId = Guid.NewGuid();
            genome.Nodes.Add(new NodeGene(outputId, NodeType.Output, new SigmoidActivationFunction(), 0));

            foreach (Guid inputId in inputIds)
            {
                int innovation = tracker.GetOrCreateConnectionInnovation(inputId, outputId);
                genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, outputId, weight, true, innovation));
            }
        }

        return genome;
    }
}
