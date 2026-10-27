using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// Structural difference between two document versions (see <c>DocumentComparer</c>). Nodes are matched
/// by <see cref="Node.NodeId"/>, ports by <see cref="Port.PortId"/> and edges by their port pair. Lists are
/// ordered by those keys.
/// </summary>
public sealed class DocumentComparison
{
    public IReadOnlyList<Node> AddedNodes { get; init; } = [];

    public IReadOnlyList<Node> RemovedNodes { get; init; } = [];

    public IReadOnlyList<NodeChange> ChangedNodes { get; init; } = [];

    public IReadOnlyList<Port> AddedPorts { get; init; } = [];

    public IReadOnlyList<Port> RemovedPorts { get; init; } = [];

    public IReadOnlyList<Edge> AddedEdges { get; init; } = [];

    public IReadOnlyList<Edge> RemovedEdges { get; init; } = [];

    /// <summary>Port changes on a surviving node show up as a <see cref="NodeChangeKind.Ports"/> change on it.</summary>
    public bool HasChanges =>
        AddedNodes.Count + RemovedNodes.Count + ChangedNodes.Count + AddedEdges.Count + RemovedEdges.Count > 0;
}

/// <summary>A node present in both versions with at least one changed field.</summary>
public sealed class NodeChange
{
    public required Node Before { get; init; }

    public required Node After { get; init; }

    public IReadOnlyList<NodeFieldChange> Changes { get; init; } = [];
}

/// <summary>One changed field and its values in the older and newer version.</summary>
public sealed class NodeFieldChange
{
    public NodeChangeKind Kind { get; init; }

    /// <summary>Extension key for <see cref="NodeChangeKind.Extension"/>, port id for the port kinds; null for core fields.</summary>
    public string? Key { get; init; }

    public object? Before { get; init; }

    public object? After { get; init; }
}

public enum NodeChangeKind
{
    /// <summary><see cref="Node.TypeId"/>.</summary>
    Type,

    NickName,

    /// <summary><see cref="Node.X"/>/<see cref="Node.Y"/> moved by more than the tolerance, or appeared or disappeared; values are <c>{ x, y }</c>, null when absent.</summary>
    Position,

    Locked,

    Source,

    Text,

    /// <summary>The node's set of port ids; values are the sorted ids.</summary>
    Ports,

    /// <summary>A port present on both sides was renamed; <see cref="NodeFieldChange.Key"/> is the port id, values are the names.</summary>
    PortName,

    /// <summary>A port present on both sides changed direction; <see cref="NodeFieldChange.Key"/> is the port id, values are the directions.</summary>
    PortDirection,

    /// <summary>An extension value was added, removed or changed; <see cref="NodeFieldChange.Key"/> names it.</summary>
    Extension,
}
