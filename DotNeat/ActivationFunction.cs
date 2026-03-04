namespace DotNeat;

public abstract class ActivationFunction(string name)
{
    public string Name { get; } = name;

    public abstract double Activate(double x);

    public abstract double Derivative(double x);
}

public sealed class SigmoidActivationFunction : ActivationFunction
{
    public SigmoidActivationFunction()
        : base("Sigmoid")
    { }

    public override double Activate(double x)
    {
        return 1d / (1d + Math.Exp(-x));
    }

    public override double Derivative(double x)
    {
        double sigmoid = Activate(x);
        return sigmoid * (1d - sigmoid);
    }
}

public sealed class ReluActivationFunction : ActivationFunction
{
    public ReluActivationFunction()
        : base("ReLU")
    {
    }

    public override double Activate(double x)
    {
        return x > 0d ? x : 0d;
    }

    public override double Derivative(double x)
    {
        return x > 0d ? 1d : 0d;
    }
}

