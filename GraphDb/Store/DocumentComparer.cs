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
        var beforeNodes = before.Nodes.ToDictionary(n => n.NodeId, StringComparer.Ordinal);
        var afterNodes = after.Nodes.ToDictionary(n => n.NodeId, StringComparer.Ordinal);
        var beforePorts = before.Ports.ToDictionary(p => p.PortId, StringComparer.OrdinalIgnoreCase);
        var afterPorts = after.Ports.ToDictionary(p => p.PortId, StringComparer.OrdinalIgnoreCase);
        var beforeEdges = before.Edges.ToDictionary(EdgeKey);
        var afterEdges = after.Edges.ToDictionary(EdgeKey);
        var beforePortsByNode = PortIdsByNode(before);
        var afterPortsByNode = PortIdsByNode(after);

        var changed = new List<NodeChange>();
        foreach (var node in after.Nodes.OrderBy(n => n.NodeId, StringComparer.Ordinal))
        {
            if (!beforeNodes.TryGetValue(node.NodeId, out var old))
                continue;
            var changes = CompareNode(old, node, beforePortsByNode, afterPortsByNode, positionTolerance);
            if (changes.Count > 0)
                changed.Add(new NodeChange { Before = old, After = node, Changes = changes });
        }

        return new DocumentComparison
        {
            AddedNodes = Missing(after.Nodes, beforeNodes, n => n.NodeId),
            RemovedNodes = Missing(before.Nodes, afterNodes, n => n.NodeId),
            ChangedNodes = changed,
            AddedPorts = Missing(after.Ports, beforePorts, p => p.PortId),
            RemovedPorts = Missing(before.Ports, afterPorts, p => p.PortId),
            AddedEdges = Missing(after.Edges, beforeEdges, EdgeKey),
            RemovedEdges = Missing(before.Edges, afterEdges, EdgeKey),
        };
    }

    static List<NodeFieldChange> CompareNode(
        Node a,
        Node b,
        IReadOnlyDictionary<string, List<string>> beforePortsByNode,
        IReadOnlyDictionary<string, List<string>> afterPortsByNode,
        double positionTolerance)
    {
        var changes = new List<NodeFieldChange>();
        void Add(NodeChangeKind kind, object? before, object? after, string? key = null) =>
            changes.Add(new NodeFieldChange { Kind = kind, Key = key, Before = before, After = after });

        if (!string.Equals(a.TypeId, b.TypeId, StringComparison.Ordinal))
            Add(NodeChangeKind.Type, a.TypeId, b.TypeId);
        if (!string.Equals(a.NickName ?? "", b.NickName ?? "", StringComparison.Ordinal))
            Add(NodeChangeKind.NickName, a.NickName, b.NickName);
        if (a.X is float ax && a.Y is float ay && b.X is float bx && b.Y is float by
            && (Math.Abs(ax - bx) > positionTolerance || Math.Abs(ay - by) > positionTolerance))
            Add(NodeChangeKind.Position, Position(ax, ay), Position(bx, by));
        if ((a.Locked ?? false) != (b.Locked ?? false))
            Add(NodeChangeKind.Locked, a.Locked ?? false, b.Locked ?? false);
        if (!string.Equals(a.Source ?? "", b.Source ?? "", StringComparison.Ordinal))
            Add(NodeChangeKind.Source, a.Source, b.Source);
        if (!string.Equals(a.Text ?? "", b.Text ?? "", StringComparison.Ordinal))
            Add(NodeChangeKind.Text, a.Text, b.Text);

        var portsBefore = beforePortsByNode.GetValueOrDefault(a.NodeId) ?? [];
        var portsAfter = afterPortsByNode.GetValueOrDefault(b.NodeId) ?? [];
        if (!portsBefore.SequenceEqual(portsAfter, StringComparer.OrdinalIgnoreCase))
            Add(NodeChangeKind.Ports, portsBefore, portsAfter);

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

    static Dictionary<string, object?> Position(float x, float y) => new() { ["x"] = x, ["y"] = y };

    static object? ExtensionValue(Node node, string key) =>
        node.Extensions != null && node.Extensions.TryGetValue(key, out var value) ? value : null;

    static Dictionary<string, List<string>> PortIdsByNode(DocumentGraphResult graph) =>
        graph.Ports
            .Where(p => graph.PortIdToNodeId.ContainsKey(p.PortId))
            .GroupBy(p => graph.PortIdToNodeId[p.PortId], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(p => p.PortId).Order(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

    // Extension values are scalars or arrays of scalars (graph-model.md), read back as driver lists.
    static bool ValuesEqual(object? a, object? b)
    {
        if (a is IEnumerable listA and not string && b is IEnumerable listB and not string)
            return listA.Cast<object?>().SequenceEqual(listB.Cast<object?>());
        return Equals(a, b);
    }

    static List<T> Missing<T>(IEnumerable<T> items, IReadOnlyDictionary<string, T> other, Func<T, string> key) =>
        items.Where(item => !other.ContainsKey(key(item))).OrderBy(key, StringComparer.Ordinal).ToList();

    static string EdgeKey(Edge edge) => $"{edge.SourcePortId}>{edge.TargetPortId}";
}
