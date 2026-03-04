using DotNeat;

const int seed = 12345;

XorFitnessEvaluator evaluator = new(seed);
Genome genome = CreateKnownXorGenome();

double fitness = evaluator.Evaluate(genome);

Console.WriteLine($"Seed: {seed}");
Console.WriteLine($"XOR fitness: {fitness:F6} / 4.000000");

static Genome CreateKnownXorGenome()
{
    Guid inputA = Guid.NewGuid();
    Guid inputB = Guid.NewGuid();
    Guid hiddenOr = Guid.NewGuid();
    Guid hiddenNand = Guid.NewGuid();
    Guid output = Guid.NewGuid();

    Genome genome = new();
    genome.Nodes.Add(new NodeGene(inputA, NodeType.Input, new ReluActivationFunction(), 0));
    genome.Nodes.Add(new NodeGene(inputB, NodeType.Input, new ReluActivationFunction(), 0));
    genome.Nodes.Add(new NodeGene(hiddenOr, NodeType.Hidden, new SigmoidActivationFunction(), -10));
    genome.Nodes.Add(new NodeGene(hiddenNand, NodeType.Hidden, new SigmoidActivationFunction(), 30));
    genome.Nodes.Add(new NodeGene(output, NodeType.Output, new SigmoidActivationFunction(), -30));

    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputA, hiddenOr, 20, true, 1));
    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputB, hiddenOr, 20, true, 2));

    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputA, hiddenNand, -20, true, 3));
    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), inputB, hiddenNand, -20, true, 4));

    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), hiddenOr, output, 20, true, 5));
    genome.Connections.Add(new ConnectionGene(Guid.NewGuid(), hiddenNand, output, 20, true, 6));

    return genome;
}
