using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GraphRoots.GraphDb;

public static class SubgraphMatcher
{
    public const int MaxPatternNodes = 16;
    public const int MaxPatternPorts = 32;
    public const int MaxPatternEdges = 32;
    public const int MaxHops = 8;
    public const int MaxMatchResults = 100;
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

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

    public static void Validate(SubgraphPattern pattern)
    {
        Compile(pattern, null, null, null, 1);
    }

    public static CompiledSubgraphQuery Compile(
        SubgraphPattern pattern,
        MatchScope? scope,
        MatchOptions? options,
        string? afterCursor,
        int limit)
    {
        if (pattern.Nodes == null || pattern.Nodes.Count == 0)
            throw GraphStoreException.InvalidPattern("Pattern must include at least one node.");
        if (pattern.Nodes.Count > MaxPatternNodes)
            throw GraphStoreException.PatternTooLarge($"Pattern may have at most {MaxPatternNodes} nodes.");
        if (limit < 1)
            throw GraphStoreException.InvalidArgument("limit must be >= 1.");

        var mode = DetectMode(pattern);
        var nodes = pattern.Nodes.ToList();
        var ports = (pattern.Ports ?? []).ToList();
        var edges = (pattern.Edges ?? []).ToList();
        var connections = (pattern.Connections ?? []).ToList();

        if (mode == SubgraphPatternMode.PortExplicit)
        {
            if (ports.Count == 0)
                throw GraphStoreException.InvalidPattern("Port-explicit mode requires ports.");
            if (ports.Count > MaxPatternPorts)
                throw GraphStoreException.PatternTooLarge($"Pattern may have at most {MaxPatternPorts} ports.");
            if (edges.Count > MaxPatternEdges)
                throw GraphStoreException.PatternTooLarge($"Pattern may have at most {MaxPatternEdges} edges.");
        }
        else if (ports.Count > 0)
            throw GraphStoreException.InvalidPattern("Ports are only valid in port-explicit mode.");

        if (mode == SubgraphPatternMode.NodeConnection && connections.Count > MaxPatternEdges)
            throw GraphStoreException.PatternTooLarge($"Pattern may have at most {MaxPatternEdges} connections.");

        var allKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
            AddKey(allKeys, node.Key, "node");

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
            if (connection.MinHops < 1 || connection.MaxHops < connection.MinHops || connection.MaxHops > MaxHops)
                throw GraphStoreException.InvalidArgument($"Connection hops must satisfy 1 <= minHops <= maxHops <= {MaxHops}.");
        }

        options ??= new MatchOptions();
        var orderedNodes = nodes
            .Select((node, index) => (node, index))
            .OrderBy(t => Selectivity(t.node))
            .ThenBy(t => t.index)
            .Select(t => t.node)
            .ToList();

        var nodeVar = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < orderedNodes.Count; i++)
            nodeVar[orderedNodes[i].Key] = $"n{i}";

        var portVar = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < ports.Count; i++)
            portVar[ports[i].Key] = $"p{i}";

        var edgeVar = new List<string>();
        var edgeKeys = new List<string>();
        for (var i = 0; i < edges.Count; i++)
        {
            edgeVar.Add($"e{i}");
            edgeKeys.Add(edges[i].Key!);
        }

        var connectionKeys = connections.Select(c => c.Key!).ToList();
        var cypher = new StringBuilder();
        var parameters = new Dictionary<string, object?> { ["limit"] = limit };
        AppendScopePreamble(cypher, parameters, scope);

        foreach (var node in orderedNodes)
        {
            var v = nodeVar[node.Key];
            cypher.AppendLine($"MATCH ({v}:Node)");
            var predicates = NodePredicates(v, node, parameters, scope);
            if (predicates.Count > 0)
                cypher.AppendLine("WHERE " + string.Join(" AND ", predicates));
        }

        if (mode == SubgraphPatternMode.PortExplicit)
        {
            foreach (var port in ports)
            {
                var pv = portVar[port.Key];
                var nv = nodeVar[port.Node];
                cypher.AppendLine($"MATCH ({nv})-[:HAS_PORT]->({pv}:Port)");
                var predicates = PortPredicates(pv, port, parameters);
                if (predicates.Count > 0)
                    cypher.AppendLine("WHERE " + string.Join(" AND ", predicates));
            }

            for (var i = 0; i < edges.Count; i++)
            {
                var ev = edgeVar[i];
                var src = portVar[edges[i].SourcePort];
                var tgt = portVar[edges[i].TargetPort];
                cypher.AppendLine($"MATCH ({src})-[{ev}:EDGE]->({tgt})");
            }
        }
        var extra = new List<string>();
        if (mode == SubgraphPatternMode.NodeConnection)
        {
            for (var i = 0; i < connections.Count; i++)
                extra.AddRange(AppendConnection(cypher, parameters, connections[i], i, nodeVar));
        }

        if (options.Injective)
        {
            extra.AddRange(InjectivePairs(nodeVar.Values));
            if (mode == SubgraphPatternMode.PortExplicit)
                extra.AddRange(InjectivePairs(portVar.Values));
        }

        if (options.Induced && mode == SubgraphPatternMode.PortExplicit && portVar.Count > 0)
            extra.Add(InducedPortsClause(portVar.Values.ToList(), edges, portVar));
        if (options.Induced && mode == SubgraphPatternMode.NodeConnection && nodeVar.Count > 0)
            extra.Add(InducedNodesClause(orderedNodes, nodeVar, connections));

        if (extra.Count > 0)
            cypher.AppendLine("WITH * WHERE " + string.Join(" AND ", extra));

        var cursorPredicate = MatchCursorPredicate(parameters, afterCursor, orderedNodes, nodeVar);
        if (cursorPredicate != null)
            cypher.AppendLine($"FILTER WHERE {cursorPredicate}");

        var firstNodeVar = nodeVar[orderedNodes[0].Key];
        cypher.AppendLine($"OPTIONAL MATCH (matchDoc:Document)-[:CONTAINS]->({firstNodeVar})");

        var returnItems = new List<string> { "matchDoc AS Document" };
        foreach (var node in nodes)
            returnItems.Add($"{nodeVar[node.Key]} AS node_{node.Key}");
        foreach (var port in ports)
            returnItems.Add($"{portVar[port.Key]} AS port_{port.Key}");
        for (var i = 0; i < edges.Count; i++)
            returnItems.Add($"{edgeVar[i]} AS edge_{edgeKeys[i]}");
        for (var i = 0; i < connections.Count; i++)
        {
            returnItems.Add($"cports{i} AS conn_{connectionKeys[i]}_ports");
            returnItems.Add($"cedges{i} AS conn_{connectionKeys[i]}_edges");
        }

        var matchBody = cypher.ToString();
        var orderParts = orderedNodes.Select(n => $"{nodeVar[n.Key]}.VersionId, {nodeVar[n.Key]}.NodeId");
        var orderBy = "ORDER BY " + string.Join(", ", orderParts);
        return new CompiledSubgraphQuery
        {
            Cypher = matchBody + "RETURN " + string.Join(", ", returnItems) + "\n" + orderBy + "\nLIMIT $limit",
            CountCypher = matchBody + "RETURN count(*) AS Total",
            Parameters = parameters,
            Mode = mode,
            NodeKeys = nodes.Select(n => n.Key).ToList(),
            PortKeys = ports.Select(p => p.Key).ToList(),
            EdgeKeys = edgeKeys,
            ConnectionKeys = connectionKeys,
            OrderNodeKeys = orderedNodes.Select(n => n.Key).ToList(),
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

    static int Selectivity(PatternNode node)
    {
        if (!string.IsNullOrEmpty(node.TypeId))
            return 0;
        if (!string.IsNullOrEmpty(node.Kind))
            return 1;
        if (!string.IsNullOrEmpty(node.Name) || !string.IsNullOrEmpty(node.NickName))
            return 2;
        return 3;
    }

    static void AppendScopePreamble(StringBuilder cypher, Dictionary<string, object?> parameters, MatchScope? scope)
    {
        if (scope == null)
            return;

        if (scope.VersionId is Guid versionId && string.IsNullOrEmpty(scope.DocumentId) && !scope.IncludeNested)
        {
            parameters["scopeVersionId"] = versionId.ToString();
            return;
        }

        if (string.IsNullOrEmpty(scope.DocumentId) && scope.VersionId == null)
            return;

        cypher.Append("MATCH (scopeDoc:Document)");
        var preds = new List<string>();
        if (!string.IsNullOrEmpty(scope.DocumentId))
        {
            parameters["scopeDocumentId"] = scope.DocumentId;
            preds.Add("scopeDoc.DocumentId = $scopeDocumentId");
        }
        if (scope.VersionId is Guid vid)
        {
            parameters["scopeVersionId"] = vid.ToString();
            preds.Add("scopeDoc.VersionId = $scopeVersionId");
        }
        if (preds.Count > 0)
            cypher.Append(" WHERE ").Append(string.Join(" AND ", preds));
        cypher.AppendLine();

        if (scope.IncludeNested)
        {
            cypher.AppendLine($"OPTIONAL MATCH (scopeDoc)-[:NESTS*0..{MaxHops}]->(nestedDoc:Document)");
            cypher.AppendLine("WITH collect(DISTINCT nestedDoc.VersionId) AS scopeVersionIds");
        }
        else
            cypher.AppendLine("WITH collect(DISTINCT scopeDoc.VersionId) AS scopeVersionIds");
    }

    static List<string> NodePredicates(string varName, PatternNode node, Dictionary<string, object?> parameters, MatchScope? scope)
    {
        var predicates = new List<string>();
        if (scope?.VersionId is Guid && string.IsNullOrEmpty(scope.DocumentId) && scope is { IncludeNested: false })
            predicates.Add($"{varName}.VersionId = $scopeVersionId");
        else if (scope != null && (!string.IsNullOrEmpty(scope.DocumentId) || scope.IncludeNested && scope.VersionId != null))
            predicates.Add($"{varName}.VersionId IN scopeVersionIds");

        GraphOrigins.AddPredicate(predicates, parameters, varName, scope?.Origin, "scopeOrigin");

        AddEq(predicates, parameters, varName, "TypeId", node.TypeId);
        AddEq(predicates, parameters, varName, "Kind", node.Kind);
        AddEq(predicates, parameters, varName, "Name", node.Name);
        AddEq(predicates, parameters, varName, "NickName", node.NickName);
        AddEq(predicates, parameters, varName, "Language", node.Language);
        GraphOrigins.AddPredicate(predicates, parameters, varName, node.Origin, $"{varName}_Origin");
        if (node.Locked is bool locked)
        {
            var pname = $"{varName}_Locked";
            parameters[pname] = locked;
            predicates.Add($"{varName}.Locked = ${pname}");
        }
        AddExtensions(predicates, parameters, varName, node.Extensions);
        return predicates;
    }

    static List<string> PortPredicates(string varName, PatternPort port, Dictionary<string, object?> parameters)
    {
        var predicates = new List<string>();
        AddEq(predicates, parameters, varName, "Name", port.Name);
        AddEq(predicates, parameters, varName, "Access", port.Access);
        if (!string.IsNullOrEmpty(port.Direction))
            predicates.Add(DirectionPredicate(varName, port.Direction, parameters));
        return predicates;
    }

    static List<string> AppendConnection(
        StringBuilder cypher,
        Dictionary<string, object?> parameters,
        PatternConnection connection,
        int index,
        Dictionary<string, string> nodeVar)
    {
        var src = nodeVar[connection.SourceNode];
        var tgt = nodeVar[connection.TargetNode];
        cypher.AppendLine(
            $"MATCH p{index} = ({src})((:Node)-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(:Node)){{{connection.MinHops},{connection.MaxHops}}}({tgt})");
        cypher.AppendLine(
            $"WITH *, [x IN nodes(p{index}) WHERE x:Port][0] AS cs{index}, [x IN nodes(p{index}) WHERE x:Port][-1] AS ct{index}, [x IN nodes(p{index}) WHERE x:Port] AS cports{index}, [r IN relationships(p{index}) WHERE type(r) = 'EDGE'] AS cedges{index}");
        var predicates = new List<string>();
        if (!string.IsNullOrEmpty(connection.SourceDirection))
            predicates.Add(DirectionPredicate($"cs{index}", connection.SourceDirection, parameters));
        if (!string.IsNullOrEmpty(connection.TargetDirection))
            predicates.Add(DirectionPredicate($"ct{index}", connection.TargetDirection, parameters));
        AddEq(predicates, parameters, $"cs{index}", "Name", connection.SourcePortName);
        AddEq(predicates, parameters, $"ct{index}", "Name", connection.TargetPortName);
        return predicates;
    }

    static string? MatchCursorPredicate(
        Dictionary<string, object?> parameters,
        string? afterCursor,
        IReadOnlyList<PatternNode> orderedNodes,
        Dictionary<string, string> nodeVar)
    {
        if (afterCursor == null)
            return null;
        var keys = StoreCursor.Decode(afterCursor);
        if (keys.Length != orderedNodes.Count * 2)
            throw GraphStoreException.InvalidArgument("Invalid match cursor.");

        var comparisons = new List<string>();
        for (var i = 0; i < orderedNodes.Count; i++)
        {
            var v = nodeVar[orderedNodes[i].Key];
            var versionParam = $"cVer{i}";
            var idParam = $"cId{i}";
            parameters[versionParam] = keys[i * 2];
            parameters[idParam] = keys[i * 2 + 1];
            var prefix = new List<string>();
            for (var j = 0; j < i; j++)
            {
                var pv = nodeVar[orderedNodes[j].Key];
                prefix.Add($"{pv}.VersionId = $cVer{j} AND {pv}.NodeId = $cId{j}");
            }
            var current = $"({v}.VersionId > ${versionParam} OR ({v}.VersionId = ${versionParam} AND {v}.NodeId > ${idParam}))";
            comparisons.Add(prefix.Count == 0 ? current : "(" + string.Join(" AND ", prefix) + " AND " + current + ")");
        }
        return "(" + string.Join(" OR ", comparisons) + ")";
    }

    static string DirectionPredicate(string varName, string direction, Dictionary<string, object?> parameters)
    {
        var pname = $"{varName}_Direction";
        if (string.Equals(direction, PortDirections.Out, StringComparison.OrdinalIgnoreCase))
        {
            parameters[pname] = new[] { PortDirections.Out, PortDirections.Both };
            return $"{varName}.Direction IN ${pname}";
        }
        if (string.Equals(direction, PortDirections.In, StringComparison.OrdinalIgnoreCase))
        {
            parameters[pname] = new[] { PortDirections.In, PortDirections.Both };
            return $"{varName}.Direction IN ${pname}";
        }
        parameters[pname] = direction;
        return $"{varName}.Direction = ${pname}";
    }

    static void AddEq(List<string> predicates, Dictionary<string, object?> parameters, string varName, string property, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        var pname = $"{varName}_{property}";
        parameters[pname] = value;
        predicates.Add($"{varName}.{property} = ${pname}");
    }

    static void AddExtensions(List<string> predicates, Dictionary<string, object?> parameters, string varName, IReadOnlyList<ExtensionPredicate>? extensions)
    {
        if (extensions == null)
            return;
        for (var i = 0; i < extensions.Count; i++)
        {
            var ext = extensions[i];
            DbExtensionProperties.ValidateKey(ext.Key);
            var keyName = $"{varName}_extKey{i}";
            var valueName = $"{varName}_ext{i}";
            parameters[keyName] = ext.Key;
            parameters[valueName] = ext.EqualsValue == null ? null : DbExtensionProperties.NormalizeValue(ext.EqualsValue);
            predicates.Add($"{DbExtensionProperties.CypherDynamicProperty(varName, keyName)} = ${valueName}");
        }
    }

    static IEnumerable<string> InjectivePairs(IEnumerable<string> vars)
    {
        var list = vars.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
                yield return $"elementId({list[i]}) <> elementId({list[j]})";
        }
    }

    static string InducedPortsClause(
        IReadOnlyList<string> portVars,
        IReadOnlyList<PatternEdge> edges,
        Dictionary<string, string> portVar)
    {
        var allowed = new List<string>();
        foreach (var edge in edges)
        {
            var src = portVar[edge.SourcePort];
            var tgt = portVar[edge.TargetPort];
            allowed.Add($"(elementId(startNode(extraE)) = elementId({src}) AND elementId(endNode(extraE)) = elementId({tgt}))");
        }

        var inSet = string.Join(", ", portVars);
        var allowedClause = allowed.Count == 0 ? "false" : string.Join(" OR ", allowed);
        return $@"NOT EXISTS {{ MATCH (extraS:Port)-[extraE:EDGE]->(extraT:Port) WHERE extraS IN [{inSet}] AND extraT IN [{inSet}] AND NOT ({allowedClause}) }}";
    }

    static string InducedNodesClause(
        IReadOnlyList<PatternNode> nodes,
        Dictionary<string, string> nodeVar,
        IReadOnlyList<PatternConnection> connections)
    {
        var allowed = new List<string>();
        foreach (var connection in connections)
        {
            var src = nodeVar[connection.SourceNode];
            var tgt = nodeVar[connection.TargetNode];
            allowed.Add($"(elementId(extraA) = elementId({src}) AND elementId(extraB) = elementId({tgt}))");
        }

        var inSet = string.Join(", ", nodes.Select(n => nodeVar[n.Key]));
        var allowedClause = allowed.Count == 0 ? "false" : string.Join(" OR ", allowed);
        return $@"NOT EXISTS {{ MATCH (extraA:Node)-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(extraB:Node) WHERE extraA IN [{inSet}] AND extraB IN [{inSet}] AND NOT ({allowedClause}) }}";
    }
}
