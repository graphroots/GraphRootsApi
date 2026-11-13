using System;

namespace GraphRoots.GraphDb;

/// <summary>
/// Limits that are part of the store contract (documented in docs/graphql). Every backend enforces the same values
/// so clients see the same errors whichever storage is configured.
/// </summary>
public static class StoreLimits
{
    /// <summary>Nodes + ports + edges in a dense document read or an import snapshot (<c>RESULT_TOO_LARGE</c>).</summary>
    public const int MaxDenseGraphEntities = 100_000;

    /// <summary>Pattern size limits for <c>matchSubgraph</c> (<c>PATTERN_TOO_LARGE</c>).</summary>
    public const int MaxPatternNodes = 16;

    public const int MaxPatternPorts = 32;

    /// <summary>Applies to pattern edges (mode 1) and connections (mode 2).</summary>
    public const int MaxPatternEdges = 32;

    /// <summary>Upper bound for <c>maxHops</c> on dataflow walks, paths, history walks, and pattern connections.</summary>
    public const int MaxHops = 8;

    /// <summary>Maximum <c>first</c> for <c>matchSubgraph</c>.</summary>
    public const int MaxMatchResults = 100;

    /// <summary>Matcher budget (<c>MATCH_TIMEOUT</c>).</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Total operations in one <c>applyGraphPatch</c>.</summary>
    public const int MaxPatchOperations = 500;

    public const int MaxPatchCreateNodes = 200;

    public const int MaxPatchCreatePorts = 400;

    public const int MaxPatchCreateEdges = 400;
}
