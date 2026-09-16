using System.Collections.Generic;
using System.Linq;

namespace GraphRoots.GraphDb;

public static class GraphPatchValidator
{
    public const int MaxOperations = 500;
    public const int MaxCreateNodes = 200;
    public const int MaxCreatePorts = 400;
    public const int MaxCreateEdges = 400;

    public static void Validate(GraphPatch patch)
    {
        if (string.IsNullOrWhiteSpace(patch.DocumentId))
            throw GraphStoreException.InvalidArgument("documentId is required.");

        var createNodes = patch.CreateNodes ?? [];
        var updateNodes = patch.UpdateNodes ?? [];
        var deleteNodes = patch.DeleteNodes ?? [];
        var createPorts = patch.CreatePorts ?? [];
        var updatePorts = patch.UpdatePorts ?? [];
        var deletePorts = patch.DeletePorts ?? [];
        var createEdges = patch.CreateEdges ?? [];
        var deleteEdges = patch.DeleteEdges ?? [];
        var addToGroups = patch.AddToGroups ?? [];
        var removeFromGroups = patch.RemoveFromGroups ?? [];
        var setExtensions = patch.SetExtensions ?? [];

        var total =
            createNodes.Count + updateNodes.Count + deleteNodes.Count +
            createPorts.Count + updatePorts.Count + deletePorts.Count +
            createEdges.Count + deleteEdges.Count +
            addToGroups.Count + removeFromGroups.Count + setExtensions.Count;
        if (total > MaxOperations)
            throw GraphStoreException.InvalidArgument($"Patch exceeds {MaxOperations} operations.");
        if (createNodes.Count > MaxCreateNodes)
            throw GraphStoreException.InvalidArgument($"createNodes exceeds {MaxCreateNodes}.");
        if (createPorts.Count > MaxCreatePorts)
            throw GraphStoreException.InvalidArgument($"createPorts exceeds {MaxCreatePorts}.");
        if (createEdges.Count > MaxCreateEdges)
            throw GraphStoreException.InvalidArgument($"createEdges exceeds {MaxCreateEdges}.");

        RequireUnique(createNodes.Select(n => n.NodeId), "createNodes.nodeId");
        RequireUnique(updateNodes.Select(n => n.NodeId), "updateNodes.nodeId");
        RequireUnique(deleteNodes, "deleteNodes");
        RequireUnique(createPorts.Select(p => p.PortId), "createPorts.portId");
        RequireUnique(updatePorts.Select(p => p.PortId), "updatePorts.portId");
        RequireUnique(deletePorts, "deletePorts");
        RequireUnique(createEdges.Select(e => EdgeKey(e.SourcePortId, e.TargetPortId)), "createEdges");
        RequireUnique(deleteEdges.Select(e => EdgeKey(e.SourcePortId, e.TargetPortId)), "deleteEdges");

        RejectOverlap(createNodes.Select(n => n.NodeId), updateNodes.Select(n => n.NodeId), "createNodes", "updateNodes");
        RejectOverlap(createNodes.Select(n => n.NodeId), deleteNodes, "createNodes", "deleteNodes");
        RejectOverlap(updateNodes.Select(n => n.NodeId), deleteNodes, "updateNodes", "deleteNodes");
        RejectOverlap(createPorts.Select(p => p.PortId), updatePorts.Select(p => p.PortId), "createPorts", "updatePorts");
        RejectOverlap(createPorts.Select(p => p.PortId), deletePorts, "createPorts", "deletePorts");
        RejectOverlap(updatePorts.Select(p => p.PortId), deletePorts, "updatePorts", "deletePorts");
        RejectOverlap(
            createEdges.Select(e => EdgeKey(e.SourcePortId, e.TargetPortId)),
            deleteEdges.Select(e => EdgeKey(e.SourcePortId, e.TargetPortId)),
            "createEdges",
            "deleteEdges");

        foreach (var node in createNodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
                throw GraphStoreException.InvalidArgument("createNodes.nodeId is required.");
        }

        foreach (var port in createPorts)
        {
            if (string.IsNullOrWhiteSpace(port.PortId) || string.IsNullOrWhiteSpace(port.NodeId))
                throw GraphStoreException.InvalidArgument("createPorts requires portId and nodeId.");
        }

        foreach (var edge in createEdges)
        {
            if (string.Equals(edge.SourcePortId, edge.TargetPortId, System.StringComparison.Ordinal))
                throw GraphStoreException.InvalidArgument("createEdges cannot wire a port to itself.");
        }

        foreach (var op in addToGroups.Concat(removeFromGroups))
        {
            if (string.IsNullOrWhiteSpace(op.MemberNodeId) || string.IsNullOrWhiteSpace(op.GroupNodeId))
                throw GraphStoreException.InvalidArgument("group membership requires memberNodeId and groupNodeId.");
            if (string.Equals(op.MemberNodeId, op.GroupNodeId, System.StringComparison.Ordinal))
                throw GraphStoreException.InvalidArgument("A node cannot be a member of itself.");
        }

        foreach (var op in setExtensions)
        {
            if (op.Entries.Count == 0)
                throw GraphStoreException.InvalidArgument("setExtensions.entries must not be empty.");
            if (op.Entity == StoreEntityKind.Edge &&
                (string.IsNullOrWhiteSpace(op.SourcePortId) || string.IsNullOrWhiteSpace(op.TargetPortId)))
                throw GraphStoreException.InvalidArgument("setExtensions for EDGE requires sourcePortId and targetPortId.");
            if (op.Entity != StoreEntityKind.Edge && string.IsNullOrWhiteSpace(op.Id))
                throw GraphStoreException.InvalidArgument("setExtensions.id is required.");
            foreach (var entry in op.Entries)
                DbExtensionProperties.ValidateKey(entry.Key);
        }
    }

    static string EdgeKey(string source, string target) => $"{source}\u001f{target}";

    static void RequireUnique(IEnumerable<string> values, string label)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw GraphStoreException.InvalidArgument($"{label} contains an empty id.");
            if (!seen.Add(value))
                throw GraphStoreException.InvalidArgument($"Duplicate {label} '{value}'.");
        }
    }

    static void RejectOverlap(IEnumerable<string> left, IEnumerable<string> right, string leftLabel, string rightLabel)
    {
        var set = new HashSet<string>(left, System.StringComparer.Ordinal);
        foreach (var value in right)
        {
            if (set.Contains(value))
                throw GraphStoreException.InvalidArgument($"{leftLabel} and {rightLabel} both target '{value}'.");
        }
    }
}
