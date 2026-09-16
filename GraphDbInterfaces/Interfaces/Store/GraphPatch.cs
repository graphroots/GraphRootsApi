using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

public enum StoreEntityKind
{
    Document,
    Node,
    Port,
    Edge,
}

public sealed class GraphPatch
{
    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }

    public IReadOnlyList<CreateNodeOp>? CreateNodes { get; init; }

    public IReadOnlyList<UpdateNodeOp>? UpdateNodes { get; init; }

    public IReadOnlyList<string>? DeleteNodes { get; init; }

    public IReadOnlyList<CreatePortOp>? CreatePorts { get; init; }

    public IReadOnlyList<UpdatePortOp>? UpdatePorts { get; init; }

    public IReadOnlyList<string>? DeletePorts { get; init; }

    public IReadOnlyList<CreateEdgeOp>? CreateEdges { get; init; }

    public IReadOnlyList<DeleteEdgeOp>? DeleteEdges { get; init; }

    public IReadOnlyList<GroupMembershipOp>? AddToGroups { get; init; }

    public IReadOnlyList<GroupMembershipOp>? RemoveFromGroups { get; init; }

    public IReadOnlyList<SetExtensionsOp>? SetExtensions { get; init; }
}

public sealed class CreateNodeOp
{
    public string NodeId { get; init; } = "";

    public string? Origin { get; init; }

    public string? TypeId { get; init; }

    public string? Name { get; init; }

    public string? NickName { get; init; }

    public string? Kind { get; init; }

    public bool? Locked { get; init; }

    public float? X { get; init; }

    public float? Y { get; init; }

    public string? Source { get; init; }

    public string? Language { get; init; }

    public string? Text { get; init; }
}

public sealed class UpdateNodeOp
{
    public string NodeId { get; init; } = "";

    public OptionalSet<string?> TypeId { get; init; }

    public OptionalSet<string?> Name { get; init; }

    public OptionalSet<string?> NickName { get; init; }

    public OptionalSet<string?> Kind { get; init; }

    public OptionalSet<bool?> Locked { get; init; }

    public OptionalSet<float?> X { get; init; }

    public OptionalSet<float?> Y { get; init; }

    public OptionalSet<string?> Source { get; init; }

    public OptionalSet<string?> Language { get; init; }

    public OptionalSet<string?> Text { get; init; }
}

public sealed class CreatePortOp
{
    public string PortId { get; init; } = "";

    public string NodeId { get; init; } = "";

    public string? Name { get; init; }

    public string? Direction { get; init; }

    public string? Access { get; init; }
}

public sealed class UpdatePortOp
{
    public string PortId { get; init; } = "";

    public OptionalSet<string?> Name { get; init; }

    public OptionalSet<string?> Direction { get; init; }

    public OptionalSet<string?> Access { get; init; }
}

public sealed class CreateEdgeOp
{
    public string SourcePortId { get; init; } = "";

    public string TargetPortId { get; init; } = "";

    public string? SourceName { get; init; }

    public string? TargetName { get; init; }
}

public sealed class DeleteEdgeOp
{
    public string SourcePortId { get; init; } = "";

    public string TargetPortId { get; init; } = "";
}

public sealed class GroupMembershipOp
{
    public string MemberNodeId { get; init; } = "";

    public string GroupNodeId { get; init; } = "";
}

public sealed class SetExtensionsOp
{
    public StoreEntityKind Entity { get; init; }

    public string Id { get; init; } = "";

    public string? SourcePortId { get; init; }

    public string? TargetPortId { get; init; }

    public IReadOnlyList<ExtensionPredicate> Entries { get; init; } = [];
}

public sealed class GraphPatchResult
{
    public Document Document { get; init; } = new();

    public IReadOnlyList<string> CreatedNodeIds { get; init; } = [];

    public IReadOnlyList<string> DeletedNodeIds { get; init; } = [];

    public IReadOnlyList<string> CreatedPortIds { get; init; } = [];

    public IReadOnlyList<string> DeletedPortIds { get; init; } = [];
}

public sealed class DocumentGraphResult
{
    public IReadOnlyList<Node> Nodes { get; init; } = [];

    public IReadOnlyList<Port> Ports { get; init; } = [];

    public IReadOnlyList<Edge> Edges { get; init; } = [];

    public IReadOnlyDictionary<string, string> PortIdToNodeId { get; init; } =
        new Dictionary<string, string>();
}

public sealed class DocumentStatsResult
{
    public int NodeCount { get; init; }

    public int PortCount { get; init; }

    public int EdgeCount { get; init; }

    public IReadOnlyList<(string Key, int Count)> CountsByKind { get; init; } = [];

    public IReadOnlyList<(string Key, int Count)> CountsByTypeId { get; init; } = [];
}

public sealed class DataflowPathResult
{
    public IReadOnlyList<Node> Nodes { get; init; } = [];

    public IReadOnlyList<Port> Ports { get; init; } = [];

    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

public enum DataflowDirection
{
    Outgoing,
    Incoming,
}

public enum HistoryDirection
{
    Ancestors,
    Descendants,
}

public sealed class CommitDocumentResult
{
    public Document Document { get; init; } = new();

    public Document WorkingDocument { get; init; } = new();
}
