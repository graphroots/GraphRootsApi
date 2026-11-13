using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GraphRoots.GraphDb;

/// <summary>
/// A pattern that passed <see cref="SubgraphPatternValidator.Normalize"/>: mode detected, references checked,
/// and every edge / connection carrying a key (explicit or generated <c>edge_i</c> / <c>conn_i</c>).
/// </summary>
public sealed class NormalizedSubgraphPattern
{
    public SubgraphPatternMode Mode { get; init; }

    public IReadOnlyList<PatternNode> Nodes { get; init; } = [];

    public IReadOnlyList<PatternPort> Ports { get; init; } = [];

    public IReadOnlyList<PatternEdge> Edges { get; init; } = [];

    public IReadOnlyList<PatternConnection> Connections { get; init; } = [];
}

/// <summary>
/// Backend-neutral validation of <c>matchSubgraph</c> patterns (docs/graphql/subgraph-matching.md).
/// Backends compile the <see cref="NormalizedSubgraphPattern"/> into their own query form.
/// </summary>
public static class SubgraphPatternValidator
{
    static readonly Regex KeyPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static SubgraphPatternMode DetectMode(SubgraphPattern pattern)
    {
        var hasEdges = pattern.Edges is { Count: > 0 };
        var hasConnections = pattern.Connections is { Count: > 0 };
        if (hasEdges && hasConnections)
            throw GraphStoreException.InvalidPattern("Specify edges or connections, not both.");
        if (hasEdges)
            return SubgraphPatternMode.PortExplicit;
        if (hasConnections)
            return SubgraphPatternMode.NodeConnection;
        return SubgraphPatternMode.Instances;
    }

    public static void Validate(SubgraphPattern pattern) => Normalize(pattern);

    public static NormalizedSubgraphPattern Normalize(SubgraphPattern pattern)
    {
        if (pattern.Nodes == null || pattern.Nodes.Count == 0)
            throw GraphStoreException.InvalidPattern("Pattern must include at least one node.");
        if (pattern.Nodes.Count > StoreLimits.MaxPatternNodes)
            throw GraphStoreException.PatternTooLarge($"Pattern may have at most {StoreLimits.MaxPatternNodes} nodes.");

        var mode = DetectMode(pattern);
        var nodes = pattern.Nodes.ToList();
        var ports = (pattern.Ports ?? []).ToList();
        var edges = (pattern.Edges ?? []).ToList();
        var connections = (pattern.Connections ?? []).ToList();

        if (mode == SubgraphPatternMode.PortExplicit)
        {
            if (ports.Count == 0)
                throw GraphStoreException.InvalidPattern("Port-explicit mode requires ports.");
            if (ports.Count > StoreLimits.MaxPatternPorts)
                throw GraphStoreException.PatternTooLarge($"Pattern may have at most {StoreLimits.MaxPatternPorts} ports.");
            if (edges.Count > StoreLimits.MaxPatternEdges)
                throw GraphStoreException.PatternTooLarge($"Pattern may have at most {StoreLimits.MaxPatternEdges} edges.");
        }
        else if (ports.Count > 0)
            throw GraphStoreException.InvalidPattern("Ports are only valid in port-explicit mode.");

        if (mode == SubgraphPatternMode.NodeConnection && connections.Count > StoreLimits.MaxPatternEdges)
            throw GraphStoreException.PatternTooLarge($"Pattern may have at most {StoreLimits.MaxPatternEdges} connections.");

        var allKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            AddKey(allKeys, node.Key, "node");
            foreach (var ext in node.Extensions ?? [])
            {
                ExtensionKeys.ValidateKey(ext.Key);
                if (ext.EqualsValue != null)
                    ExtensionKeys.NormalizeValue(ext.EqualsValue);
            }
        }

        var nodeKeys = nodes.Select(n => n.Key).ToHashSet(StringComparer.Ordinal);
        var portKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in ports)
        {
            AddKey(allKeys, port.Key, "port");
            portKeys.Add(port.Key);
            if (!nodeKeys.Contains(port.Node))
                throw GraphStoreException.InvalidPattern($"Port '{port.Key}' references unknown node '{port.Node}'.");
        }

        for (var i = 0; i < edges.Count; i++)
        {
            var key = edges[i].Key ?? UniqueGenerated(allKeys, "edge", i);
            edges[i] = new PatternEdge { Key = key, SourcePort = edges[i].SourcePort, TargetPort = edges[i].TargetPort };
            AddKey(allKeys, key, "edge");
            if (!portKeys.Contains(edges[i].SourcePort))
                throw GraphStoreException.InvalidPattern($"Edge references unknown source port '{edges[i].SourcePort}'.");
            if (!portKeys.Contains(edges[i].TargetPort))
                throw GraphStoreException.InvalidPattern($"Edge references unknown target port '{edges[i].TargetPort}'.");
        }

        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            var key = connection.Key ?? UniqueGenerated(allKeys, "conn", i);
            connections[i] = new PatternConnection
            {
                Key = key,
                SourceNode = connection.SourceNode,
                TargetNode = connection.TargetNode,
                SourcePortName = connection.SourcePortName,
                TargetPortName = connection.TargetPortName,
                SourceDirection = connection.SourceDirection,
                TargetDirection = connection.TargetDirection,
                MinHops = connection.MinHops,
                MaxHops = connection.MaxHops,
            };
            AddKey(allKeys, key, "connection");
            if (!nodeKeys.Contains(connection.SourceNode))
                throw GraphStoreException.InvalidPattern($"Connection references unknown source node '{connection.SourceNode}'.");
            if (!nodeKeys.Contains(connection.TargetNode))
                throw GraphStoreException.InvalidPattern($"Connection references unknown target node '{connection.TargetNode}'.");
            if (connection.MinHops < 1 || connection.MaxHops < connection.MinHops || connection.MaxHops > StoreLimits.MaxHops)
                throw GraphStoreException.InvalidArgument($"Connection hops must satisfy 1 <= minHops <= maxHops <= {StoreLimits.MaxHops}.");
        }

        return new NormalizedSubgraphPattern
        {
            Mode = mode,
            Nodes = nodes,
            Ports = ports,
            Edges = edges,
            Connections = connections,
        };
    }

    static void AddKey(HashSet<string> allKeys, string key, string kind)
    {
        if (!KeyPattern.IsMatch(key))
            throw GraphStoreException.InvalidPattern($"Pattern {kind} key '{key}' must match {KeyPattern}.");
        if (!allKeys.Add(key))
            throw GraphStoreException.InvalidPattern($"Duplicate pattern key '{key}'.");
    }

    static string UniqueGenerated(HashSet<string> allKeys, string prefix, int index)
    {
        var key = $"{prefix}_{index}";
        var n = index;
        while (allKeys.Contains(key))
        {
            n++;
            key = $"{prefix}_{n}";
        }
        return key;
    }
}
