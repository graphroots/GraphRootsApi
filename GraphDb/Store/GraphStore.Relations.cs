using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public sealed partial class GraphStore
{
    public async Task<DocumentGraphResult> GetDocumentGraph(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var vid = versionId.ToString();
        var nodes = new List<Node>();
        var ports = new List<Port>();
        var edges = new List<Edge>();
        var portToNode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await _db.QueryRecords(
            @"MATCH (d:Document {DocumentId: $documentId, VersionId: $versionId})-[:CONTAINS]->(n:Node)
OPTIONAL MATCH (n)-[:HAS_PORT]->(p:Port)
OPTIONAL MATCH (p)-[e:EDGE]->(:Port)
WITH collect(DISTINCT n) AS nodes, collect(DISTINCT p) AS ports, collect(DISTINCT e) AS edges,
     [pair IN collect(DISTINCT CASE WHEN p IS NULL THEN null ELSE [n.NodeId, p.PortId] END) WHERE pair IS NOT NULL] AS pairs
RETURN nodes, ports, edges, pairs",
            new { documentId, versionId = vid },
            record =>
            {
                nodes.AddRange(record.Nodes<Node>("nodes"));
                ports.AddRange(record.Nodes<Port>("ports").Where(p => p.PortId.Length > 0));
                edges.AddRange(record.Relationships<Edge>("edges").Where(e => e.SourcePortId.Length > 0));
                if (record["pairs"] is System.Collections.IEnumerable pairs)
                {
                    foreach (var item in pairs)
                    {
                        if (item is System.Collections.IList list && list.Count >= 2 && list[0] is string nodeId && list[1] is string portId)
                            portToNode[portId] = nodeId;
                    }
                }
                return Task.CompletedTask;
            },
            cancellationToken);

        var total = nodes.Count + ports.Count + edges.Count;
        if (total > MaxDenseGraphEntities)
            throw GraphStoreException.ResultTooLarge($"Document graph has {total} entities; maximum is {MaxDenseGraphEntities}.");

        return new DocumentGraphResult
        {
            Nodes = nodes,
            Ports = ports,
            Edges = edges,
            PortIdToNodeId = portToNode,
        };
    }

    public async Task<DocumentStatsResult> GetDocumentStats(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var vid = versionId.ToString();
        var counts = await _db.RunQuery<StatsCountRow>(
            @"MATCH (d:Document {DocumentId: $documentId, VersionId: $versionId})-[:CONTAINS]->(n:Node)
OPTIONAL MATCH (n)-[:HAS_PORT]->(p:Port)
OPTIONAL MATCH (p)-[e:EDGE]->(:Port)
RETURN count(DISTINCT n) AS NodeCount, count(DISTINCT p) AS PortCount, count(DISTINCT e) AS EdgeCount",
            new { documentId, versionId = vid },
            cancellationToken);

        var byKind = await _db.RunQuery<BucketRow>(
            @"MATCH (:Document {DocumentId: $documentId, VersionId: $versionId})-[:CONTAINS]->(n:Node)
WHERE n.Kind IS NOT NULL RETURN n.Kind AS Key, count(*) AS Count",
            new { documentId, versionId = vid },
            cancellationToken);
        var byType = await _db.RunQuery<BucketRow>(
            @"MATCH (:Document {DocumentId: $documentId, VersionId: $versionId})-[:CONTAINS]->(n:Node)
WHERE n.TypeId IS NOT NULL RETURN n.TypeId AS Key, count(*) AS Count",
            new { documentId, versionId = vid },
            cancellationToken);

        var row = counts.FirstOrDefault();
        return new DocumentStatsResult
        {
            NodeCount = (int)(row?.NodeCount ?? 0),
            PortCount = (int)(row?.PortCount ?? 0),
            EdgeCount = (int)(row?.EdgeCount ?? 0),
            CountsByKind = byKind.Where(b => b.Key != null).Select(b => (b.Key!, (int)b.Count)).ToList(),
            CountsByTypeId = byType.Where(b => b.Key != null).Select(b => (b.Key!, (int)b.Count)).ToList(),
        };
    }

    public async Task<IReadOnlyList<Document>> GetNestedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var map = await GetNestedDocumentsByKeys([(documentId, versionId)], cancellationToken);
        return map[(documentId, versionId)];
    }

    public async Task<IReadOnlyList<Document>> GetParentDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var map = await GetParentDocumentsByKeys([(documentId, versionId)], cancellationToken);
        return map[(documentId, versionId)];
    }

    public async Task<IReadOnlyList<Document>> GetBasedOn(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var map = await GetBasedOnByKeys([(documentId, versionId)], cancellationToken);
        return map[(documentId, versionId)];
    }

    public async Task<IReadOnlyList<Document>> GetDerivedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var map = await GetDerivedDocumentsByKeys([(documentId, versionId)], cancellationToken);
        return map[(documentId, versionId)];
    }

    public async Task<IReadOnlyList<LibraryVersion>> GetLibrariesForDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var map = await GetLibrariesForDocuments([(documentId, versionId)], cancellationToken);
        return map[(documentId, versionId)];
    }

    public async Task<IReadOnlyList<LibraryVersion>> GetLibraryVersions(string origin, string libraryId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<LibraryVersion>>(
            "MATCH (:Library {Origin: $origin, LibraryId: $libraryId})-[:HAS_VERSION]->(n:LibraryVersion) RETURN n AS Item",
            new { origin, libraryId },
            cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<IReadOnlyList<Node>> GetGroups(Guid versionId, string nodeId, CancellationToken cancellationToken = default)
    {
        var map = await GetGroupsByNodes([(versionId, nodeId)], cancellationToken);
        return map[(versionId, nodeId)];
    }

    public async Task<IReadOnlyList<Node>> GetMembers(Guid versionId, string nodeId, CancellationToken cancellationToken = default)
    {
        var map = await GetMembersByNodes([(versionId, nodeId)], cancellationToken);
        return map[(versionId, nodeId)];
    }

    public async Task<IReadOnlyList<Node>> GetIsolatedNodes(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Node>>(
            $@"MATCH (:Document {{DocumentId: $documentId, VersionId: $versionId}})-[:CONTAINS]->(n:Node)
WHERE NOT (n)-[:HAS_PORT]->(:Port)-[:EDGE]-()
  AND NOT (n)-[:HAS_PORT]->(:Port)<-[:EDGE]-()
  AND (n.Kind IS NULL OR NOT n.Kind IN ['{NodeKinds.Group}', '{NodeKinds.Annotation}', '{NodeKinds.Cluster}'])
RETURN n AS Item",
            new { documentId, versionId = versionId.ToString() },
            cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<IReadOnlyList<LibraryVersion>> GetDefinedBy(string origin, string typeId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<LibraryVersion>>(
            "MATCH (n:LibraryVersion)-[:DEFINES]->(:NodeType {Origin: $origin, TypeId: $typeId}) RETURN n AS Item",
            new { origin, typeId },
            cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<IReadOnlyList<NodeType>> GetNodeTypesForLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<NodeType>>(
            @"MATCH (lv:LibraryVersion {Origin: $origin, LibraryId: $libraryId, Version: $version})-[:DEFINES]->(n:NodeType)
WHERE ($assemblyVersion IS NULL AND lv.AssemblyVersion IS NULL) OR lv.AssemblyVersion = $assemblyVersion
RETURN n AS Item",
            new { origin, libraryId, version, assemblyVersion },
            cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<Document?> GetClusterDocument(Guid versionId, string nodeId, CancellationToken cancellationToken = default)
    {
        var node = await GetNode(versionId, nodeId, cancellationToken);
        if (node?.Extensions == null ||
            !node.Extensions.TryGetValue(WellKnownExtensionKeys.ClusterVersionId, out var raw) ||
            raw is not string text ||
            !Guid.TryParse(text, out var clusterVersion))
            return null;

        Document? nested = null;
        await _db.QueryRecords(
            @"MATCH (parent:Document)-[:CONTAINS]->(:Node {VersionId: $versionId, NodeId: $nodeId})
MATCH (parent)-[:NESTS]->(nested:Document {VersionId: $clusterVersion})
RETURN nested AS Item",
            new { versionId = versionId.ToString(), nodeId, clusterVersion = clusterVersion.ToString() },
            record =>
            {
                nested = record.Node<Document>("Item");
                return Task.CompletedTask;
            },
            cancellationToken);
        return nested;
    }

    public async Task<IReadOnlyList<Node>> Traverse(
        Guid versionId,
        string nodeId,
        DataflowDirection direction,
        int minHops,
        int maxHops,
        CancellationToken cancellationToken = default)
    {
        ValidateHops(minHops, maxHops);
        var vid = versionId.ToString();
        string query;
        if (minHops == 1 && maxHops == 1)
        {
            query = direction == DataflowDirection.Outgoing
                ? @"MATCH (:Node {VersionId: $versionId, NodeId: $nodeId})-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(n:Node)
RETURN DISTINCT n AS Item"
                : @"MATCH (n:Node)-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(:Node {VersionId: $versionId, NodeId: $nodeId})
RETURN DISTINCT n AS Item";
        }
        else
        {
            var qpp = direction == DataflowDirection.Outgoing
                ? $"((:Node)-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(:Node)){{{minHops},{maxHops}}}"
                : $"((:Node)-[:HAS_PORT]->(:Port)<-[:EDGE]-(:Port)<-[:HAS_PORT]-(:Node)){{{minHops},{maxHops}}}";
            query = $@"MATCH (start:Node {{VersionId: $versionId, NodeId: $nodeId}})
MATCH (start){qpp}(n:Node)
RETURN DISTINCT n AS Item";
        }

        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Node>>(query, new { versionId = vid, nodeId }, cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<IReadOnlyList<Document>> TraverseHistory(
        string documentId,
        Guid versionId,
        HistoryDirection direction,
        int minHops,
        int maxHops,
        CancellationToken cancellationToken = default)
    {
        ValidateHops(minHops, maxHops);
        var vid = versionId.ToString();
        string query;
        if (minHops == 1 && maxHops == 1)
        {
            query = direction == HistoryDirection.Ancestors
                ? @"MATCH (:Document {DocumentId: $documentId, VersionId: $versionId})-[:BASED_ON]->(n:Document)
RETURN DISTINCT n AS Item"
                : @"MATCH (n:Document)-[:BASED_ON]->(:Document {DocumentId: $documentId, VersionId: $versionId})
RETURN DISTINCT n AS Item";
        }
        else
        {
            var rel = direction == HistoryDirection.Ancestors
                ? $"-[:BASED_ON*{minHops}..{maxHops}]->"
                : $"<-[:BASED_ON*{minHops}..{maxHops}]-";
            query = $@"MATCH (start:Document {{DocumentId: $documentId, VersionId: $versionId}})
MATCH (start){rel}(n:Document)
RETURN DISTINCT n AS Item";
        }

        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Document>>(query, new { documentId, versionId = vid }, cancellationToken);
        return rows.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public async Task<DataflowPathResult?> FindPath(string documentId, Guid versionId, string fromNodeId, string toNodeId, int maxHops, CancellationToken cancellationToken = default)
    {
        if (maxHops < 1 || maxHops > SubgraphMatcher.MaxHops)
            throw GraphStoreException.InvalidArgument($"maxHops must be between 1 and {SubgraphMatcher.MaxHops}.");

        DataflowPathResult? path = null;
        await _db.QueryRecords(
            $@"MATCH (doc:Document {{DocumentId: $documentId, VersionId: $versionId}})
MATCH (doc)-[:CONTAINS]->(a:Node {{NodeId: $fromNodeId}})
MATCH (doc)-[:CONTAINS]->(b:Node {{NodeId: $toNodeId}})
MATCH p = (a)((:Node)-[:HAS_PORT]->(:Port)-[:EDGE]->(:Port)<-[:HAS_PORT]-(:Node)){{1,{maxHops}}}(b)
WITH p LIMIT 1
RETURN [n IN nodes(p) WHERE n:Node | n] AS nodes,
       [n IN nodes(p) WHERE n:Port | n] AS ports,
       [r IN relationships(p) WHERE type(r) = 'EDGE' | r] AS edges",
            new { documentId, versionId = versionId.ToString(), fromNodeId, toNodeId },
            record =>
            {
                path = new DataflowPathResult
                {
                    Nodes = record.Nodes<Node>("nodes"),
                    Ports = record.Nodes<Port>("ports"),
                    Edges = record.Relationships<Edge>("edges"),
                };
                return Task.CompletedTask;
            },
            cancellationToken);
        return path;
    }

    static void ValidateHops(int minHops, int maxHops)
    {
        if (minHops < 1 || maxHops < minHops || maxHops > SubgraphMatcher.MaxHops)
            throw GraphStoreException.InvalidArgument($"Hops must satisfy 1 <= minHops <= maxHops <= {SubgraphMatcher.MaxHops}.");
    }

    sealed class StatsCountRow
    {
        public long NodeCount { get; set; }
        public long PortCount { get; set; }
        public long EdgeCount { get; set; }
    }

    sealed class BucketRow
    {
        public string? Key { get; set; }
        public long Count { get; set; }
    }
}
