using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class ActivationFunctionTests
{
    [TestMethod]
    public void Sigmoid_Activate_ReturnsExpectedValueAtZero()
    {
        ActivationFunction activation = new SigmoidActivationFunction();

        double result = activation.Activate(0);

        Assert.AreEqual(0.5, result, 1e-12);
    }

    [TestMethod]
    public void Sigmoid_Derivative_ReturnsExpectedValueAtZero()
    {
        ActivationFunction activation = new SigmoidActivationFunction();

        double result = activation.Derivative(0);

        Assert.AreEqual(0.25, result, 1e-12);
    }

    [TestMethod]
    public void Relu_Activate_And_Derivative_ReturnExpectedValues()
    {
        ActivationFunction activation = new ReluActivationFunction();

        Assert.AreEqual(0, activation.Activate(-2), 0);
        Assert.AreEqual(0, activation.Activate(0), 0);
        Assert.AreEqual(3, activation.Activate(3), 0);

        Assert.AreEqual(0, activation.Derivative(-2), 0);
        Assert.AreEqual(0, activation.Derivative(0), 0);
        Assert.AreEqual(1, activation.Derivative(3), 0);
    }
}
