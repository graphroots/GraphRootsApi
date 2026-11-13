using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GraphRoots.GraphDb.Neo4j;

public static class SubgraphMatcher
{
    public static CompiledSubgraphQuery Compile(
        SubgraphPattern pattern,
        MatchScope? scope,
        MatchOptions? options,
        string? afterCursor,
        int limit)
    {
        if (limit < 1)
            throw GraphStoreException.InvalidArgument("limit must be >= 1.");

        var normalized = SubgraphPatternValidator.Normalize(pattern);
        var mode = normalized.Mode;
        var nodes = normalized.Nodes;
        var ports = normalized.Ports;
        var edges = normalized.Edges;
        var connections = normalized.Connections;

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
            cypher.AppendLine($"OPTIONAL MATCH (scopeDoc)-[:NESTS*0..{StoreLimits.MaxHops}]->(nestedDoc:Document)");
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
            ExtensionKeys.ValidateKey(ext.Key);
            var keyName = $"{varName}_extKey{i}";
            var valueName = $"{varName}_ext{i}";
            parameters[keyName] = ext.Key;
            parameters[valueName] = ext.EqualsValue == null ? null : ExtensionKeys.NormalizeValue(ext.EqualsValue);
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
