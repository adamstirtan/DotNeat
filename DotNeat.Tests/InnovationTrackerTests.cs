using DotNeat;

namespace DotNeat.Tests;

[TestClass]
public sealed class InnovationTrackerTests
{
    [TestMethod]
    public void GetOrCreateConnectionInnovation_ReturnsSameInnovation_ForSameConnection()
    {
        InnovationTracker tracker = new();
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        int first = tracker.GetOrCreateConnectionInnovation(input, output);
        int second = tracker.GetOrCreateConnectionInnovation(input, output);

        Assert.AreEqual(first, second);
        Assert.AreEqual(2, tracker.PeekNextInnovationNumber());
    }

    [TestMethod]
    public void GetOrCreateConnectionInnovation_ReturnsDifferentInnovations_ForDifferentConnections()
    {
        InnovationTracker tracker = new();

        int first = tracker.GetOrCreateConnectionInnovation(Guid.NewGuid(), Guid.NewGuid());
        int second = tracker.GetOrCreateConnectionInnovation(Guid.NewGuid(), Guid.NewGuid());

        Assert.AreEqual(1, first);
        Assert.AreEqual(2, second);
        Assert.AreEqual(3, tracker.PeekNextInnovationNumber());
    }

    [TestMethod]
    public void GetOrCreateNodeSplitInnovation_ReturnsSameSplit_ForSameConnectionInnovation()
    {
        InnovationTracker tracker = new();
        Guid expectedNodeId = Guid.NewGuid();

        NodeSplitInnovation first = tracker.GetOrCreateNodeSplitInnovation(7, () => expectedNodeId);
        NodeSplitInnovation second = tracker.GetOrCreateNodeSplitInnovation(7, () => Guid.NewGuid());

        Assert.AreEqual(first, second);
        Assert.AreEqual(expectedNodeId, first.NewNodeId);
        Assert.AreEqual(1, first.InputConnectionInnovationNumber);
        Assert.AreEqual(2, first.OutputConnectionInnovationNumber);
    }

    [TestMethod]
    public void RegisterConnectionInnovation_UpdatesNextInnovationNumber()
    {
        InnovationTracker tracker = new();
        Guid input = Guid.NewGuid();
        Guid output = Guid.NewGuid();

        tracker.RegisterConnectionInnovation(input, output, 15);

        Assert.AreEqual(16, tracker.PeekNextInnovationNumber());
        Assert.AreEqual(15, tracker.GetOrCreateConnectionInnovation(input, output));
    }

    [TestMethod]
    public void RegisterConnectionInnovation_ThrowsOnConflictingMappings()
    {
        InnovationTracker tracker = new();
        Guid inputA = Guid.NewGuid();
        Guid outputA = Guid.NewGuid();
        Guid inputB = Guid.NewGuid();
        Guid outputB = Guid.NewGuid();

        tracker.RegisterConnectionInnovation(inputA, outputA, 4);

        Assert.Throws<InvalidOperationException>(() =>
            tracker.RegisterConnectionInnovation(inputA, outputA, 5));

        Assert.Throws<InvalidOperationException>(() =>
            tracker.RegisterConnectionInnovation(inputB, outputB, 4));
    }
}
