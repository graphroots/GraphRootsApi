using System.Collections.Generic;

namespace GraphRoots.GraphDb.Neo4j;

/// <summary>
/// A <see cref="SubgraphPattern"/> compiled to parameterized Cypher by <see cref="SubgraphMatcher"/>.
/// </summary>
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
