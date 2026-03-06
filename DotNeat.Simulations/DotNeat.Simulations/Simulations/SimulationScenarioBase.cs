using System.Collections.Generic;
using DotNeat;

namespace DotNeat.Simulations.Experiments;

public abstract class SimulationScenarioBase
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract string ComplexityLabel { get; }

    public SimulationTrace EvaluateGenome(Genome genome, int seed, bool captureFrames)
    {
        Random evaluationRandom = CreateEvaluationRandom(genome, seed);
        return RunSimulation(genome, evaluationRandom, captureFrames);
    }

    public abstract EvolutionOptions CreateEvolutionOptions(int seed);

    public abstract Genome CreateInitialGenome(Random rng, InnovationTracker tracker);

    protected abstract SimulationTrace RunSimulation(Genome genome, Random evaluationRandom, bool captureFrames);

    protected static Random CreateEvaluationRandom(Genome genome, int seed)
    {
        byte[] buffer = genome.GenomeId.ToByteArray();
        int genomeSeed = BitConverter.ToInt32(buffer, 0);
        unchecked
        {
            return new Random(seed + genomeSeed);
        }
    }

    protected static double Normalize(double value, double min, double max)
    {
        if (double.IsNaN(value))
        {
            return 0d;
        }

        return Math.Clamp((value - min) / (max - min), 0d, 1d);
    }

    protected static Genome CreateFullyConnectedGenome(
        Random rng,
        InnovationTracker tracker,
        IReadOnlyList<Guid> inputs,
        IReadOnlyList<Guid> outputs)
    {
        Genome genome = new();

        foreach (Guid inputId in inputs)
        {
            genome.Nodes.Add(new NodeGene(inputId, NodeType.Input, new ReluActivationFunction(), bias: 0));
        }

        foreach (Guid outputId in outputs)
        {
            genome.Nodes.Add(new NodeGene(outputId, NodeType.Output, new SigmoidActivationFunction(), bias: NextBias(rng)));
            foreach (Guid inputId in inputs)
            {
                int innovation = tracker.GetOrCreateConnectionInnovation(inputId, outputId);
                genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputId, outputId, NextWeight(rng), true, innovation));
            }
        }

        return genome;
    }

    private static double NextWeight(Random rng)
    {
        return -1d + (rng.NextDouble() * 2d);
    }

    private static double NextBias(Random rng)
    {
        return -0.5d + rng.NextDouble();
    }
}
