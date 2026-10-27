using System;
using System.Collections.Generic;
using System.Linq;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class DocumentComparerTest
{
    static Node N(string id, string? nick = null, float? x = 10, float? y = 20, string? hash = null) => new()
    {
        NodeId = id,
        NickName = nick,
        X = x,
        Y = y,
        Extensions = hash == null ? null : new Dictionary<string, object?> { ["gh.stateHash"] = hash },
    };

    static Port P(string id, string? name = null, string? direction = "In") => new() { PortId = id, Name = name, Direction = direction };

    static Edge E(string source, string target) => new() { SourcePortId = source, TargetPortId = target };

    static DocumentGraphResult G(IEnumerable<Node> nodes, IEnumerable<(string NodeId, Port Port)>? ports = null, IEnumerable<Edge>? edges = null)
    {
        var portList = (ports ?? []).ToList();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nodeId, port) in portList)
            map[port.PortId] = nodeId;
        return new DocumentGraphResult
        {
            Nodes = nodes.ToList(),
            Ports = portList.Select(p => p.Port).ToList(),
            Edges = (edges ?? []).ToList(),
            PortIdToNodeId = map,
        };
    }

    static DocumentGraphResult Sample() => G(
        [N("a", "A", hash: "h1"), N("b", "B", hash: "h2")],
        [("a", P("a-out", "Out", "Out")), ("b", P("b-in", "In"))],
        [E("a-out", "b-in")]);

    static List<NodeFieldChange> OnlyChanges(DocumentComparison c)
    {
        Assert.AreEqual(1, c.ChangedNodes.Count);
        return c.ChangedNodes[0].Changes.ToList();
    }

    [TestMethod]
    public void IdenticalGraphs_HaveNoChanges()
    {
        var c = DocumentComparer.Compare(Sample(), Sample());
        Assert.IsFalse(c.HasChanges);
        Assert.AreEqual(0, c.ChangedNodes.Count + c.AddedNodes.Count + c.RemovedNodes.Count + c.AddedPorts.Count
            + c.RemovedPorts.Count + c.AddedEdges.Count + c.RemovedEdges.Count);
    }

    [TestMethod]
    public void AddedAndRemovedNodes_AreReported()
    {
        var before = G([N("a"), N("gone")]);
        var after = G([N("a"), N("new")]);
        var c = DocumentComparer.Compare(before, after);
        Assert.IsTrue(c.HasChanges);
        CollectionAssert.AreEqual(new[] { "new" }, c.AddedNodes.Select(n => n.NodeId).ToArray());
        CollectionAssert.AreEqual(new[] { "gone" }, c.RemovedNodes.Select(n => n.NodeId).ToArray());
        Assert.AreEqual(0, c.ChangedNodes.Count);
    }

    [TestMethod]
    public void NickNameChange_IsReported_AndNameIsNotCompared()
    {
        var before = G([new Node { NodeId = "a", Name = "Slider", NickName = "x" }]);
        var after = G([new Node { NodeId = "a", Name = "Renamed", NickName = "y" }]);
        var change = OnlyChanges(DocumentComparer.Compare(before, after)).Single();
        Assert.AreEqual(NodeChangeKind.NickName, change.Kind);
        Assert.AreEqual("x", change.Before);
        Assert.AreEqual("y", change.After);
    }

    [TestMethod]
    public void NullAndEmptyNickName_AreTheSame()
    {
        Assert.IsFalse(DocumentComparer.Compare(G([N("a", nick: null)]), G([N("a", nick: "")])).HasChanges);
    }

    [TestMethod]
    public void Position_MovedBeyondToleranceIsReported_WithinToleranceIsNot()
    {
        Assert.IsFalse(DocumentComparer.Compare(G([N("a", x: 10, y: 20)]), G([N("a", x: 10.4f, y: 20)])).HasChanges);

        var change = OnlyChanges(DocumentComparer.Compare(G([N("a", x: 10, y: 20)]), G([N("a", x: 12, y: 20)]))).Single();
        Assert.AreEqual(NodeChangeKind.Position, change.Kind);
        var after = (Dictionary<string, object?>)change.After!;
        Assert.AreEqual(12f, after["x"]);
        Assert.AreEqual(20f, after["y"]);
    }

    [TestMethod]
    public void Position_GainingOrLosingCoordinates_IsReportedWithNull()
    {
        var gained = OnlyChanges(DocumentComparer.Compare(G([N("a", x: null, y: null)]), G([N("a", x: 1, y: 2)]))).Single();
        Assert.AreEqual(NodeChangeKind.Position, gained.Kind);
        Assert.IsNull(gained.Before);
        Assert.IsNotNull(gained.After);

        var lost = OnlyChanges(DocumentComparer.Compare(G([N("a", x: 1, y: 2)]), G([N("a", x: null, y: null)]))).Single();
        Assert.AreEqual(NodeChangeKind.Position, lost.Kind);
        Assert.IsNotNull(lost.Before);
        Assert.IsNull(lost.After);

        Assert.IsFalse(DocumentComparer.Compare(G([N("a", x: null, y: null)]), G([N("a", x: null, y: null)])).HasChanges);
    }

    [TestMethod]
    public void Extension_ChangedAddedAndRemoved_AreReportedByKey()
    {
        var before = G([new Node { NodeId = "a", Extensions = new Dictionary<string, object?> { ["gh.stateHash"] = "h1", ["gh.gone"] = 1L } }]);
        var after = G([new Node { NodeId = "a", Extensions = new Dictionary<string, object?> { ["gh.stateHash"] = "h2", ["gh.new"] = "v" } }]);
        var changes = OnlyChanges(DocumentComparer.Compare(before, after));
        CollectionAssert.AreEqual(new[] { "gh.gone", "gh.new", "gh.stateHash" }, changes.Select(c => c.Key).ToArray());
        Assert.IsTrue(changes.All(c => c.Kind == NodeChangeKind.Extension));
        Assert.AreEqual(1L, changes[0].Before);
        Assert.IsNull(changes[0].After);
        Assert.IsNull(changes[1].Before);
        Assert.AreEqual("v", changes[1].After);
        Assert.AreEqual("h1", changes[2].Before);
        Assert.AreEqual("h2", changes[2].After);
    }

    [TestMethod]
    public void Extension_ArraysCompareByContent()
    {
        static DocumentGraphResult With(params object?[] values) =>
            G([new Node { NodeId = "a", Extensions = new Dictionary<string, object?> { ["k"] = new List<object?>(values) } }]);
        Assert.IsFalse(DocumentComparer.Compare(With(1L, 2L), With(1L, 2L)).HasChanges);
        Assert.IsTrue(DocumentComparer.Compare(With(1L, 2L), With(1L, 3L)).HasChanges);
    }

    [TestMethod]
    public void PortsAddedAndRemoved_ShowAsPortsChangeOnTheNode()
    {
        var before = G([N("a")], [("a", P("p1")), ("a", P("p2"))]);
        var after = G([N("a")], [("a", P("p1")), ("a", P("p3"))]);
        var c = DocumentComparer.Compare(before, after);
        var change = OnlyChanges(c).Single();
        Assert.AreEqual(NodeChangeKind.Ports, change.Kind);
        CollectionAssert.AreEqual(new[] { "p1", "p2" }, ((List<string>)change.Before!).ToArray());
        CollectionAssert.AreEqual(new[] { "p1", "p3" }, ((List<string>)change.After!).ToArray());
        CollectionAssert.AreEqual(new[] { "p3" }, c.AddedPorts.Select(p => p.PortId).ToArray());
        CollectionAssert.AreEqual(new[] { "p2" }, c.RemovedPorts.Select(p => p.PortId).ToArray());
    }

    [TestMethod]
    public void PortRename_IsReportedWithThePortIdAsKey()
    {
        var before = G([N("a")], [("a", P("p1", "Radius"))]);
        var after = G([N("a")], [("a", P("p1", "R"))]);
        var c = DocumentComparer.Compare(before, after);
        Assert.IsTrue(c.HasChanges);
        var change = OnlyChanges(c).Single();
        Assert.AreEqual(NodeChangeKind.PortName, change.Kind);
        Assert.AreEqual("p1", change.Key);
        Assert.AreEqual("Radius", change.Before);
        Assert.AreEqual("R", change.After);
    }

    [TestMethod]
    public void PortDirectionChange_IsReported()
    {
        var before = G([N("a")], [("a", P("p1", "X", "In"))]);
        var after = G([N("a")], [("a", P("p1", "X", "Both"))]);
        var change = OnlyChanges(DocumentComparer.Compare(before, after)).Single();
        Assert.AreEqual(NodeChangeKind.PortDirection, change.Kind);
        Assert.AreEqual("p1", change.Key);
        Assert.AreEqual("In", change.Before);
        Assert.AreEqual("Both", change.After);
    }

    [TestMethod]
    public void EdgesAddedAndRemoved_AreReported()
    {
        var before = G([N("a")], edges: [E("p1", "p2"), E("p1", "p3")]);
        var after = G([N("a")], edges: [E("p1", "p2"), E("p4", "p5")]);
        var c = DocumentComparer.Compare(before, after);
        Assert.IsTrue(c.HasChanges);
        Assert.AreEqual("p4", c.AddedEdges.Single().SourcePortId);
        Assert.AreEqual("p3", c.RemovedEdges.Single().TargetPortId);
    }

    [TestMethod]
    public void DuplicateEdgesAndNodes_DoNotThrow_AndCountOnce()
    {
        var dup = G([N("a"), N("a")], [("a", P("p1")), ("a", P("p1"))], [E("p1", "p2"), E("p1", "p2")]);
        var c = DocumentComparer.Compare(dup, G([N("a")], [("a", P("p1"))], [E("p1", "p2")]));
        Assert.IsFalse(c.HasChanges);

        var added = DocumentComparer.Compare(G([]), dup);
        Assert.AreEqual(1, added.AddedEdges.Count);
        Assert.AreEqual(1, added.AddedNodes.Count);
        Assert.AreEqual(1, added.AddedPorts.Count);
    }

    [TestMethod]
    public void PortIdsDifferingOnlyByCase_AreTheSamePortAndEdge()
    {
        var before = G([N("a")], [("a", P("ABC-1")), ("a", P("abc-1"))], [E("ABC-1", "X"), E("abc-1", "x")]);
        var after = G([N("a")], [("a", P("abc-1"))], [E("abc-1", "x")]);
        var c = DocumentComparer.Compare(before, after);
        Assert.IsFalse(c.HasChanges);
        Assert.AreEqual(0, c.AddedPorts.Count + c.RemovedPorts.Count + c.AddedEdges.Count + c.RemovedEdges.Count);
    }

    [TestMethod]
    public void Output_IsOrderedDeterministically_RegardlessOfInputOrder()
    {
        var before = G([N("m", hash: "1"), N("a", hash: "1"), N("z", hash: "1")], edges: [E("q", "r"), E("a", "b")]);
        var after = G([N("z", hash: "2"), N("a", hash: "2"), N("m", hash: "2"), N("y"), N("b")], edges: [E("x", "y"), E("c", "d")]);
        var forward = DocumentComparer.Compare(before, after);
        var reversed = DocumentComparer.Compare(
            G(before.Nodes.Reverse(), edges: before.Edges.Reverse()),
            G(after.Nodes.Reverse(), edges: after.Edges.Reverse()));

        foreach (var c in new[] { forward, reversed })
        {
            CollectionAssert.AreEqual(new[] { "a", "m", "z" }, c.ChangedNodes.Select(n => n.After.NodeId).ToArray());
            CollectionAssert.AreEqual(new[] { "b", "y" }, c.AddedNodes.Select(n => n.NodeId).ToArray());
            CollectionAssert.AreEqual(new[] { "c", "x" }, c.AddedEdges.Select(e => e.SourcePortId).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "q" }, c.RemovedEdges.Select(e => e.SourcePortId).ToArray());
        }
    }
}
