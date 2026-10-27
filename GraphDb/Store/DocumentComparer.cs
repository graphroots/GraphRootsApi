using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GraphRoots.GraphDb;

/// <summary>
/// Compares two document graphs. Tool-neutral: core fields are compared directly and every extension
/// key generically, so a tool-specific state key (e.g. <c>gh.stateHash</c>) surfaces as an
/// <see cref="NodeChangeKind.Extension"/> change for clients to interpret.
/// </summary>
/// <remarks>
/// Origin: GrasshopperDigestor (GraphHop, https://github.com/graphhop/GrasshopperDigestor). The version
/// comparison generalised here (<c>SharedRhino/GhVersionDiff.cs</c>) is Konrad Zaremba's modification in his
/// copy of that project (https://github.com/KonradZaremba/GrasshopperDigestor).
/// </remarks>
public static class DocumentComparer
{
    public const double DefaultPositionTolerance = 0.5;

    public static DocumentComparison Compare(DocumentGraphResult before, DocumentGraphResult after, double positionTolerance = DefaultPositionTolerance)
    {
        // Stored graphs can be odd but valid (repeated edges, port ids differing only by case), so every index
        // keeps the first entry for a key instead of throwing. Port ids are case-insensitive throughout.
        var beforeNodes = Index(before.Nodes, n => n.NodeId, StringComparer.Ordinal);
        var afterNodes = Index(after.Nodes, n => n.NodeId, StringComparer.Ordinal);
        var beforePorts = Index(before.Ports, p => p.PortId, PortIds);
        var afterPorts = Index(after.Ports, p => p.PortId, PortIds);
        var beforeEdges = Index(before.Edges, EdgeKey, PortIds);
        var afterEdges = Index(after.Edges, EdgeKey, PortIds);
        var beforePortsByNode = PortsByNode(before, beforePorts);
        var afterPortsByNode = PortsByNode(after, afterPorts);

        var changed = new List<NodeChange>();
        foreach (var node in afterNodes.Values.OrderBy(n => n.NodeId, StringComparer.Ordinal))
        {
            if (!beforeNodes.TryGetValue(node.NodeId, out var old))
                continue;
            var changes = CompareNode(
                old,
                node,
                beforePortsByNode.GetValueOrDefault(old.NodeId) ?? [],
                afterPortsByNode.GetValueOrDefault(node.NodeId) ?? [],
                positionTolerance);
            if (changes.Count > 0)
                changed.Add(new NodeChange { Before = old, After = node, Changes = changes });
        }

        return new DocumentComparison
        {
            AddedNodes = Missing(afterNodes, beforeNodes, n => n.NodeId),
            RemovedNodes = Missing(beforeNodes, afterNodes, n => n.NodeId),
            ChangedNodes = changed,
            AddedPorts = Missing(afterPorts, beforePorts, p => p.PortId),
            RemovedPorts = Missing(beforePorts, afterPorts, p => p.PortId),
            AddedEdges = Missing(afterEdges, beforeEdges, EdgeKey),
            RemovedEdges = Missing(beforeEdges, afterEdges, EdgeKey),
        };
    }

    static List<NodeFieldChange> CompareNode(Node a, Node b, List<Port> portsBefore, List<Port> portsAfter, double positionTolerance)
    {
        var changes = new List<NodeFieldChange>();
        void Add(NodeChangeKind kind, object? before, object? after, string? key = null) =>
            changes.Add(new NodeFieldChange { Kind = kind, Key = key, Before = before, After = after });

        if (!string.Equals(a.TypeId, b.TypeId, StringComparison.Ordinal))
            Add(NodeChangeKind.Type, a.TypeId, b.TypeId);
        if (Differs(a.NickName, b.NickName))
            Add(NodeChangeKind.NickName, a.NickName, b.NickName);
        if (PositionChanged(a, b, positionTolerance))
            Add(NodeChangeKind.Position, Position(a), Position(b));
        if ((a.Locked ?? false) != (b.Locked ?? false))
            Add(NodeChangeKind.Locked, a.Locked ?? false, b.Locked ?? false);
        if (Differs(a.Source, b.Source))
            Add(NodeChangeKind.Source, a.Source, b.Source);
        if (Differs(a.Text, b.Text))
            Add(NodeChangeKind.Text, a.Text, b.Text);

        var idsBefore = portsBefore.Select(p => p.PortId).ToList();
        var idsAfter = portsAfter.Select(p => p.PortId).ToList();
        if (!idsBefore.SequenceEqual(idsAfter, PortIds))
            Add(NodeChangeKind.Ports, idsBefore, idsAfter);

        // Ports present on both sides: a rename or a direction change is invisible to the id sets above. Lists are id-sorted.
        var oldPorts = portsBefore.ToDictionary(p => p.PortId, PortIds);
        foreach (var port in portsAfter)
        {
            if (!oldPorts.TryGetValue(port.PortId, out var old))
                continue;
            if (Differs(old.Name, port.Name))
                Add(NodeChangeKind.PortName, old.Name, port.Name, port.PortId);
            if (Differs(old.Direction, port.Direction))
                Add(NodeChangeKind.PortDirection, old.Direction, port.Direction, port.PortId);
        }

        var keys = (a.Extensions?.Keys ?? []).Union(b.Extensions?.Keys ?? [], StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var before = ExtensionValue(a, key);
            var after = ExtensionValue(b, key);
            if (!ValuesEqual(before, after))
                Add(NodeChangeKind.Extension, before, after, key);
        }
        return changes;
    }

    // Optional text fields: missing and empty are the same.
    static bool Differs(string? a, string? b) => !string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

    // A node with only one coordinate is treated as having no position.
    static bool HasPosition(Node n) => n.X != null && n.Y != null;

    static bool PositionChanged(Node a, Node b, double tolerance)
    {
        if (HasPosition(a) != HasPosition(b))
            return true;
        return HasPosition(a)
            && (Math.Abs(a.X!.Value - b.X!.Value) > tolerance || Math.Abs(a.Y!.Value - b.Y!.Value) > tolerance);
    }

    static Dictionary<string, object?>? Position(Node n) =>
        HasPosition(n) ? new() { ["x"] = n.X!.Value, ["y"] = n.Y!.Value } : null;

    static object? ExtensionValue(Node node, string key) =>
        node.Extensions != null && node.Extensions.TryGetValue(key, out var value) ? value : null;

    static Dictionary<string, List<Port>> PortsByNode(DocumentGraphResult graph, IReadOnlyDictionary<string, Port> portsById) =>
        portsById.Values
            .Where(p => graph.PortIdToNodeId.ContainsKey(p.PortId))
            .GroupBy(p => graph.PortIdToNodeId[p.PortId], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.PortId, PortIds).ToList(), StringComparer.Ordinal);

    // Extension values are scalars or arrays of scalars (graph-model.md), read back as driver lists.
    static bool ValuesEqual(object? a, object? b)
    {
        if (a is IEnumerable listA and not string && b is IEnumerable listB and not string)
            return listA.Cast<object?>().SequenceEqual(listB.Cast<object?>());
        return Equals(a, b);
    }

    static Dictionary<string, T> Index<T>(IEnumerable<T> items, Func<T, string> key, StringComparer comparer)
    {
        var index = new Dictionary<string, T>(comparer);
        foreach (var item in items)
            index.TryAdd(key(item), item);
        return index;
    }

    static List<T> Missing<T>(IReadOnlyDictionary<string, T> items, IReadOnlyDictionary<string, T> other, Func<T, string> key) =>
        items.Values.Where(item => !other.ContainsKey(key(item))).OrderBy(key, StringComparer.Ordinal).ToList();

    static StringComparer PortIds => StringComparer.OrdinalIgnoreCase;

    static string EdgeKey(Edge edge) => $"{edge.SourcePortId}\u001f{edge.TargetPortId}";
}
