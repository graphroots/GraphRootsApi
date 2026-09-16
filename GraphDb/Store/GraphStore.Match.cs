using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public sealed partial class GraphStore
{
    public async Task<PageResult<SubgraphMatch>> MatchSubgraph(
        SubgraphPattern pattern,
        MatchScope? scope,
        MatchOptions? options,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var unscoped = scope == null ||
            (scope.VersionId == null && string.IsNullOrEmpty(scope.DocumentId) && string.IsNullOrEmpty(scope.Origin));
        if (unscoped && page.First == null)
            throw GraphStoreException.LimitRequired("Corpus-wide matchSubgraph requires first.");

        var matchPage = new PageRequest
        {
            First = page.First,
            After = page.After,
            DefaultSize = 20,
            MaxSize = SubgraphMatcher.MaxMatchResults,
            IncludeTotalCount = page.IncludeTotalCount,
        };
        var take = matchPage.Take();
        var compiled = SubgraphMatcher.Compile(pattern, scope, options, matchPage.After, take + 1);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SubgraphMatcher.MatchTimeout);
        var token = timeout.Token;

        try
        {
            var matches = new List<SubgraphMatch>();
            await _db.QueryRecords(compiled.Cypher, compiled.Parameters, record =>
            {
                matches.Add(MapMatch(record, compiled));
                return Task.CompletedTask;
            }, token);

            var total = 0;
            if (matchPage.IncludeTotalCount)
            {
                var counted = SubgraphMatcher.Compile(pattern, scope, options, null, take + 1);
                await _db.QueryRecords(counted.CountCypher, counted.Parameters, record =>
                {
                    total = (int)(record.Int64("Total") ?? 0);
                    return Task.CompletedTask;
                }, token);
            }

            var hasNext = matches.Count > take;
            if (hasNext)
                matches.RemoveAt(matches.Count - 1);

            var cursors = matches.Select(match => MatchCursor(match, compiled.OrderNodeKeys)).ToList();
            return new PageResult<SubgraphMatch>
            {
                Items = matches,
                Cursors = cursors,
                TotalCount = total,
                StartCursor = cursors.Count == 0 ? null : cursors[0],
                EndCursor = cursors.Count == 0 ? null : cursors[^1],
                HasNextPage = hasNext,
                HasPreviousPage = matchPage.After != null,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw GraphStoreException.MatchTimeout($"matchSubgraph exceeded {SubgraphMatcher.MatchTimeout.TotalSeconds:0}s.");
        }
    }

    static string MatchCursor(SubgraphMatch match, IReadOnlyList<string> orderKeys)
    {
        var parts = new List<string>();
        foreach (var key in orderKeys)
        {
            var node = match.Nodes.FirstOrDefault(n => n.Key == key)?.Node;
            parts.Add(node?.VersionId.ToString() ?? "");
            parts.Add(node?.NodeId ?? "");
        }
        return StoreCursor.Encode(parts.ToArray());
    }

    static SubgraphMatch MapMatch(IQueryRecord record, CompiledSubgraphQuery compiled)
    {
        var nodes = compiled.NodeKeys
            .Select(key => new NodeBinding { Key = key, Node = record.Node<Node>($"node_{key}") ?? new Node() })
            .ToList();
        var ports = compiled.PortKeys
            .Select(key => new PortBinding { Key = key, Port = record.Node<Port>($"port_{key}") ?? new Port() })
            .ToList();
        var edges = compiled.EdgeKeys
            .Select(key => new EdgeBinding { Key = key, Edge = record.Relationship<Edge>($"edge_{key}") ?? new Edge() })
            .ToList();
        var connections = compiled.ConnectionKeys.Select(key => new ConnectionRealization
        {
            Key = key,
            Ports = record.Nodes<Port>($"conn_{key}_ports"),
            Edges = record.Relationships<Edge>($"conn_{key}_edges"),
        }).ToList();

        return new SubgraphMatch
        {
            Document = record.Node<Document>("Document"),
            Nodes = nodes,
            Ports = ports,
            Edges = edges,
            Connections = connections,
        };
    }
}
