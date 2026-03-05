using Microsoft.VisualStudio.TestTools.UnitTesting;
using DotNeat.Runner.Persistence;
using System.Text.Json;

namespace DotNeat.Tests;

[TestClass]
public class ExperimentGenomeSerializerTests
{
    [TestMethod]
    public void Serialize_outputs_expected_json_structure()
    {
        var genome = new Genome();
        var nodeIn = new NodeGene(Guid.NewGuid(), NodeType.Input, new SigmoidActivationFunction(), 0.1);
        var nodeOut = new NodeGene(Guid.NewGuid(), NodeType.Output, new ReluActivationFunction(), -0.2);
        genome.Nodes.Add(nodeIn);
        genome.Nodes.Add(nodeOut);

        var conn = new ConnectionGene(Guid.NewGuid(), nodeIn.GeneId, nodeOut.GeneId, 0.75, true, 7);
        genome.Connections.Add(conn);

        // ExperimentGenomeSerializer is internal; call it via reflection from the runner assembly
        var asm = System.Reflection.Assembly.Load("DotNeat.Runner");
        var serType = asm.GetType("DotNeat.Runner.Persistence.ExperimentGenomeSerializer");
        var serializeMethod = serType!.GetMethod("Serialize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
        string json = (string)serializeMethod.Invoke(null, new object[] { genome })!;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.IsTrue(root.TryGetProperty("GenomeId", out var gid));
        Assert.IsTrue(root.TryGetProperty("Nodes", out var nodes));
        Assert.IsTrue(root.TryGetProperty("Connections", out var conns));

        Assert.IsTrue(nodes.GetArrayLength() == 2);
        Assert.IsTrue(conns.GetArrayLength() == 1);

        var firstNode = nodes[0];
        Assert.AreEqual(nodeIn.GeneId.ToString("D"), firstNode.GetProperty("NodeId").GetString());
        Assert.AreEqual("Input", firstNode.GetProperty("NodeType").GetString());
        Assert.AreEqual("Sigmoid", firstNode.GetProperty("ActivationFunction").GetString());
        Assert.AreEqual(0.1, firstNode.GetProperty("Bias").GetDouble(), 1e-9);

        var firstConn = conns[0];
        Assert.AreEqual(conn.GeneId.ToString("D"), firstConn.GetProperty("ConnectionId").GetString());
        Assert.AreEqual(conn.InputNodeId.ToString("D"), firstConn.GetProperty("InputNodeId").GetString());
        Assert.AreEqual(conn.OutputNodeId.ToString("D"), firstConn.GetProperty("OutputNodeId").GetString());
        Assert.AreEqual(0.75, firstConn.GetProperty("Weight").GetDouble(), 1e-9);
        Assert.IsTrue(firstConn.GetProperty("Enabled").GetBoolean());
        Assert.AreEqual(7, firstConn.GetProperty("InnovationNumber").GetInt32());
    }
}
