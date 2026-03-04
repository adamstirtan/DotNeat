using DotNeat;

ActivationFunction sigmoid = new SigmoidActivationFunction();

NodeGene nodeGene = new(Guid.NewGuid(), NodeType.Input, sigmoid, 1);
