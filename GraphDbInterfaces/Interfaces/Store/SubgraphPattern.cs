using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

public enum SubgraphPatternMode
{
    Instances,
    PortExplicit,
    NodeConnection,
}

public sealed class PatternNode
{
    public string Key { get; init; } = "";

    public string? TypeId { get; init; }

    public string? Kind { get; init; }

    public string? Name { get; init; }

    public string? NickName { get; init; }

    public bool? Locked { get; init; }

    public string? Language { get; init; }

    public string? Origin { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class PatternPort
{
    public string Key { get; init; } = "";

    public string Node { get; init; } = "";

    public string? Name { get; init; }

    public string? Direction { get; init; }

    public string? Access { get; init; }
}

public sealed class PatternEdge
{
    public string? Key { get; init; }

    public string SourcePort { get; init; } = "";

    public string TargetPort { get; init; } = "";
}

public sealed class PatternConnection
{
    public string? Key { get; init; }

    public string SourceNode { get; init; } = "";

    public string TargetNode { get; init; } = "";

    public string? SourcePortName { get; init; }

    public string? TargetPortName { get; init; }

    public string? SourceDirection { get; init; }

    public string? TargetDirection { get; init; }

    public int MinHops { get; init; } = 1;

    public int MaxHops { get; init; } = 1;
}

public sealed class SubgraphPattern
{
    public IReadOnlyList<PatternNode> Nodes { get; init; } = [];

    public IReadOnlyList<PatternPort>? Ports { get; init; }

    public IReadOnlyList<PatternEdge>? Edges { get; init; }

    public IReadOnlyList<PatternConnection>? Connections { get; init; }
}

public sealed class MatchScope
{
    public Guid? VersionId { get; init; }

    public string? DocumentId { get; init; }

    public string? Origin { get; init; }

    public bool IncludeNested { get; init; }
}

public sealed class MatchOptions
{
    public bool Injective { get; init; } = true;

    public bool Induced { get; init; }
}

public sealed class NodeBinding
{
    public string Key { get; init; } = "";

    public Node Node { get; init; } = new();
}

public sealed class PortBinding
{
    public string Key { get; init; } = "";

    public Port Port { get; init; } = new();
}

public sealed class EdgeBinding
{
    public string Key { get; init; } = "";

    public Edge Edge { get; init; } = new();
}

public sealed class ConnectionRealization
{
    public string? Key { get; init; }

    public IReadOnlyList<Port> Ports { get; init; } = [];

    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

public sealed class SubgraphMatch
{
    public Document? Document { get; init; }

    public IReadOnlyList<NodeBinding> Nodes { get; init; } = [];

    public IReadOnlyList<PortBinding> Ports { get; init; } = [];

    public IReadOnlyList<EdgeBinding> Edges { get; init; } = [];

    public IReadOnlyList<ConnectionRealization> Connections { get; init; } = [];
}

public sealed class CompiledSubgraphQuery
{
    public string Cypher { get; init; } = "";

    public string CountCypher { get; init; } = "";

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new Dictionary<string, object?>();

    public SubgraphPatternMode Mode { get; init; }

    public IReadOnlyList<string> NodeKeys { get; init; } = [];

    public IReadOnlyList<string> PortKeys { get; init; } = [];

    public IReadOnlyList<string> EdgeKeys { get; init; } = [];

    public IReadOnlyList<string> ConnectionKeys { get; init; } = [];

    public IReadOnlyList<string> OrderNodeKeys { get; init; } = [];
}
