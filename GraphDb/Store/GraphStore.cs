using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public sealed partial class GraphStore : IGraphStore
{
    public const int MaxDenseGraphEntities = 100_000;

    readonly IDbOperations _db;

    public GraphStore(IDbOperations dbOperations)
    {
        _db = dbOperations;
    }

    public Task<Document?> GetDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        return _db.GetNode(new Document { DocumentId = documentId, VersionId = versionId }, cancellationToken);
    }

    public Task<Node?> GetNode(Guid versionId, string nodeId, CancellationToken cancellationToken = default)
    {
        return _db.GetNode(new Node { VersionId = versionId, NodeId = nodeId }, cancellationToken);
    }

    public Task<Port?> GetPort(Guid versionId, string portId, CancellationToken cancellationToken = default)
    {
        return _db.GetNode(new Port { VersionId = versionId, PortId = portId }, cancellationToken);
    }

    public async Task<Edge?> GetEdge(Guid versionId, string sourcePortId, string targetPortId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Edge>>(
            "MATCH (:Port {VersionId: $versionId, PortId: $sourcePortId})-[e:EDGE {VersionId: $versionId, SourcePortId: $sourcePortId, TargetPortId: $targetPortId}]->(:Port) RETURN e AS Item LIMIT 1",
            new { versionId = versionId.ToString(), sourcePortId, targetPortId },
            cancellationToken);
        return rows.Count == 0 ? null : rows[0].Item;
    }

    public Task<NodeType?> GetNodeType(string origin, string typeId, CancellationToken cancellationToken = default)
    {
        return _db.GetNode(new NodeType { Origin = origin, TypeId = typeId }, cancellationToken);
    }

    public Task<Library?> GetLibrary(string origin, string libraryId, CancellationToken cancellationToken = default)
    {
        return _db.GetNode(new Library { Origin = origin, LibraryId = libraryId }, cancellationToken);
    }

    public Task<LibraryVersion?> GetLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default)
    {
        if (assemblyVersion != null)
        {
            return _db.GetNode(new LibraryVersion
            {
                Origin = origin,
                LibraryId = libraryId,
                Version = version,
                AssemblyVersion = assemblyVersion,
            }, cancellationToken);
        }

        return FirstLibraryVersion(origin, libraryId, version, cancellationToken);
    }

    async Task<LibraryVersion?> FirstLibraryVersion(string origin, string libraryId, string version, CancellationToken cancellationToken)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<LibraryVersion>>(
            @"MATCH (n:LibraryVersion {Origin: $origin, LibraryId: $libraryId, Version: $version})
RETURN n AS Item
ORDER BY n.AssemblyVersion ASC
LIMIT 1",
            new { origin, libraryId, version },
            cancellationToken);
        return rows.Count == 0 ? null : rows[0].Item;
    }

    public async Task<IReadOnlyList<Document?>> GetDocuments(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
            return [];
        var found = await _db.GetNodes(keys.Select(k => new Document { DocumentId = k.DocumentId, VersionId = k.VersionId }), cancellationToken);
        var map = found.ToDictionary(d => (d.DocumentId, d.VersionId));
        return keys.Select(k => map.TryGetValue((k.DocumentId, k.VersionId), out var d) ? d : null).ToList();
    }

    public async Task<IReadOnlyList<Node?>> GetNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
            return [];
        var found = await _db.GetNodes(keys.Select(k => new Node { VersionId = k.VersionId, NodeId = k.NodeId }), cancellationToken);
        var map = found.ToDictionary(n => (n.VersionId, n.NodeId), StringPairComparer);
        return keys.Select(k => map.TryGetValue((k.VersionId, k.NodeId), out var n) ? n : null).ToList();
    }

    public async Task<IReadOnlyList<Port?>> GetPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
            return [];
        var found = await _db.GetNodes(keys.Select(k => new Port { VersionId = k.VersionId, PortId = k.PortId }), cancellationToken);
        var map = found.ToDictionary(p => (p.VersionId, p.PortId), StringPairComparer);
        return keys.Select(k => map.TryGetValue((k.VersionId, k.PortId), out var p) ? p : null).ToList();
    }

    static readonly IEqualityComparer<(Guid, string)> StringPairComparer = EqualityComparer<(Guid, string)>.Default;

    public async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Port>>> GetPortsByNodes(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken = default)
    {
        var result = keys.ToDictionary(k => k, _ => (IReadOnlyList<Port>)[], StringPairComparer);
        if (keys.Count == 0)
            return result;

        var rows = new List<(Guid VersionId, string NodeId, Port Port)>();
        await _db.QueryRecords(
            @"UNWIND $keys AS key
MATCH (n:Node {VersionId: key.VersionId, NodeId: key.NodeId})-[:HAS_PORT]->(p:Port)
RETURN key.VersionId AS VersionId, key.NodeId AS NodeId, p AS Item",
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.NodeId }).ToList() },
            record =>
            {
                var port = record.Node<Port>("Item");
                var version = Guid.Parse((string)record["VersionId"]!);
                var nodeId = (string)record["NodeId"]!;
                if (port != null)
                    rows.Add((version, nodeId, port));
                return Task.CompletedTask;
            },
            cancellationToken);

        foreach (var group in rows.GroupBy(r => (r.VersionId, r.NodeId)))
            result[group.Key] = group.Select(r => r.Port).ToList();
        return result;
    }

    public async Task<IReadOnlyDictionary<(Guid VersionId, string PortId), string?>> GetNodeIdsByPorts(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken = default)
    {
        var result = keys.ToDictionary(k => k, _ => (string?)null, StringPairComparer);
        if (keys.Count == 0)
            return result;

        await _db.QueryRecords(
            @"UNWIND $keys AS key
MATCH (n:Node {VersionId: key.VersionId})-[:HAS_PORT]->(p:Port {VersionId: key.VersionId, PortId: key.PortId})
RETURN key.VersionId AS VersionId, key.PortId AS PortId, n.NodeId AS NodeId",
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.PortId }).ToList() },
            record =>
            {
                var version = Guid.Parse((string)record["VersionId"]!);
                var portId = (string)record["PortId"]!;
                result[(version, portId)] = (string?)record["NodeId"];
                return Task.CompletedTask;
            },
            cancellationToken);
        return result;
    }

    public Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetOutgoingEdgesByPorts(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken = default)
    {
        return GetEdgesByPorts(keys, outgoing: true, cancellationToken);
    }

    public Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetIncomingEdgesByPorts(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken = default)
    {
        return GetEdgesByPorts(keys, outgoing: false, cancellationToken);
    }

    async Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetEdgesByPorts(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        bool outgoing,
        CancellationToken cancellationToken)
    {
        var result = keys.ToDictionary(k => k, _ => (IReadOnlyList<Edge>)[], StringPairComparer);
        if (keys.Count == 0)
            return result;

        var pattern = outgoing
            ? "MATCH (p:Port {VersionId: key.VersionId, PortId: key.PortId})-[e:EDGE]->(:Port)"
            : "MATCH (:Port)-[e:EDGE]->(p:Port {VersionId: key.VersionId, PortId: key.PortId})";
        var rows = new List<(Guid VersionId, string PortId, Edge Edge)>();
        await _db.QueryRecords(
            $@"UNWIND $keys AS key
{pattern}
RETURN key.VersionId AS VersionId, key.PortId AS PortId, e AS Item",
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.PortId }).ToList() },
            record =>
            {
                var edge = record.Relationship<Edge>("Item");
                if (edge != null)
                    rows.Add((Guid.Parse((string)record["VersionId"]!), (string)record["PortId"]!, edge));
                return Task.CompletedTask;
            },
            cancellationToken);

        foreach (var group in rows.GroupBy(r => (r.VersionId, r.PortId)))
            result[group.Key] = group.Select(r => r.Edge).ToList();
        return result;
    }

    public async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), Document?>> GetDocumentsForNodes(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken = default)
    {
        var result = keys.ToDictionary(k => k, _ => (Document?)null, StringPairComparer);
        if (keys.Count == 0)
            return result;
        await _db.QueryRecords(
            @"UNWIND $keys AS key
MATCH (d:Document)-[:CONTAINS]->(:Node {VersionId: key.VersionId, NodeId: key.NodeId})
RETURN key.VersionId AS VersionId, key.NodeId AS NodeId, d AS Item",
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.NodeId }).ToList() },
            record =>
            {
                result[(Guid.Parse((string)record["VersionId"]!), (string)record["NodeId"]!)] = record.Node<Document>("Item");
                return Task.CompletedTask;
            },
            cancellationToken);
        return result;
    }

    public async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), NodeType?>> GetNodeTypesForNodes(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken = default)
    {
        var result = keys.ToDictionary(k => k, _ => (NodeType?)null, StringPairComparer);
        if (keys.Count == 0)
            return result;
        await _db.QueryRecords(
            @"UNWIND $keys AS key
MATCH (t:NodeType)-[:HAS_INSTANCE]->(:Node {VersionId: key.VersionId, NodeId: key.NodeId})
RETURN key.VersionId AS VersionId, key.NodeId AS NodeId, t AS Item",
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.NodeId }).ToList() },
            record =>
            {
                result[(Guid.Parse((string)record["VersionId"]!), (string)record["NodeId"]!)] = record.Node<NodeType>("Item");
                return Task.CompletedTask;
            },
            cancellationToken);
        return result;
    }

    public async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetGroupsByNodes(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken = default)
    {
        return await RelatedNodesByKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (:Node {VersionId: key.VersionId, NodeId: key.NodeId})-[:MEMBER_OF]->(n:Node)
RETURN key.VersionId AS VersionId, key.NodeId AS NodeId, n AS Item",
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetMembersByNodes(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken = default)
    {
        return await RelatedNodesByKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (n:Node)-[:MEMBER_OF]->(:Node {VersionId: key.VersionId, NodeId: key.NodeId})
RETURN key.VersionId AS VersionId, key.NodeId AS NodeId, n AS Item",
            cancellationToken);
    }

    async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> RelatedNodesByKeys(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        string query,
        CancellationToken cancellationToken)
    {
        var result = keys.ToDictionary(k => k, _ => (IReadOnlyList<Node>)[], StringPairComparer);
        if (keys.Count == 0)
            return result;
        var rows = new List<(Guid VersionId, string NodeId, Node Node)>();
        await _db.QueryRecords(
            query,
            new { keys = keys.Select(k => new { VersionId = k.VersionId.ToString(), k.NodeId }).ToList() },
            record =>
            {
                var node = record.Node<Node>("Item");
                if (node != null)
                    rows.Add((Guid.Parse((string)record["VersionId"]!), (string)record["NodeId"]!, node));
                return Task.CompletedTask;
            },
            cancellationToken);
        foreach (var group in rows.GroupBy(r => (r.VersionId, r.NodeId)))
            result[group.Key] = group.Select(r => r.Node).ToList();
        return result;
    }

    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetNestedDocumentsByKeys(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken = default)
    {
        return DocumentsByDocumentKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (:Document {DocumentId: key.DocumentId, VersionId: key.VersionId})-[:NESTS]->(n:Document)
RETURN key.DocumentId AS DocumentId, key.VersionId AS VersionId, n AS Item",
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetParentDocumentsByKeys(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken = default)
    {
        return DocumentsByDocumentKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (n:Document)-[:NESTS]->(:Document {DocumentId: key.DocumentId, VersionId: key.VersionId})
RETURN key.DocumentId AS DocumentId, key.VersionId AS VersionId, n AS Item",
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetBasedOnByKeys(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken = default)
    {
        return DocumentsByDocumentKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (:Document {DocumentId: key.DocumentId, VersionId: key.VersionId})-[:BASED_ON]->(n:Document)
RETURN key.DocumentId AS DocumentId, key.VersionId AS VersionId, n AS Item",
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetDerivedDocumentsByKeys(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken = default)
    {
        return DocumentsByDocumentKeys(
            keys,
            @"UNWIND $keys AS key
MATCH (n:Document)-[:BASED_ON]->(:Document {DocumentId: key.DocumentId, VersionId: key.VersionId})
RETURN key.DocumentId AS DocumentId, key.VersionId AS VersionId, n AS Item",
            cancellationToken);
    }

    async Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> DocumentsByDocumentKeys(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        string query,
        CancellationToken cancellationToken)
    {
        var result = keys.ToDictionary(k => k, _ => (IReadOnlyList<Document>)[]);
        if (keys.Count == 0)
            return result;
        var rows = new List<(string DocumentId, Guid VersionId, Document Document)>();
        await _db.QueryRecords(
            query,
            new { keys = keys.Select(k => new { k.DocumentId, VersionId = k.VersionId.ToString() }).ToList() },
            record =>
            {
                var document = record.Node<Document>("Item");
                if (document != null)
                    rows.Add(((string)record["DocumentId"]!, Guid.Parse((string)record["VersionId"]!), document));
                return Task.CompletedTask;
            },
            cancellationToken);
        foreach (var group in rows.GroupBy(r => (r.DocumentId, r.VersionId)))
            result[group.Key] = group.Select(r => r.Document).ToList();
        return result;
    }

    public async Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>> GetLibrariesForDocuments(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken = default)
    {
        var result = keys.ToDictionary(k => k, _ => (IReadOnlyList<LibraryVersion>)[]);
        if (keys.Count == 0)
            return result;
        var rows = new List<(string DocumentId, Guid VersionId, LibraryVersion Version)>();
        await _db.QueryRecords(
            @"UNWIND $keys AS key
MATCH (n:LibraryVersion)-[:USED_BY]->(:Document {DocumentId: key.DocumentId, VersionId: key.VersionId})
RETURN key.DocumentId AS DocumentId, key.VersionId AS VersionId, n AS Item",
            new { keys = keys.Select(k => new { k.DocumentId, VersionId = k.VersionId.ToString() }).ToList() },
            record =>
            {
                var version = record.Node<LibraryVersion>("Item");
                if (version != null)
                    rows.Add(((string)record["DocumentId"]!, Guid.Parse((string)record["VersionId"]!), version));
                return Task.CompletedTask;
            },
            cancellationToken);
        foreach (var group in rows.GroupBy(r => (r.DocumentId, r.VersionId)))
            result[group.Key] = group.Select(r => r.Version).ToList();
        return result;
    }

    public Task<PageResult<Document>> ListDocuments(DocumentFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        ApplyDocumentFilter("n", filter, clauses);
        string match;
        if (filter?.LibraryId != null)
        {
            match = "MATCH (lv:LibraryVersion {LibraryId: $libraryId})-[:USED_BY]->(n:Document)";
            clauses.Parameters.TryAdd("libraryId", filter.LibraryId);
            clauses.Origin("lv", filter.Origin);
            if (!string.IsNullOrEmpty(filter.LibraryVersion))
                clauses.Eq("lv", "Version", filter.LibraryVersion);
            if (filter.LibraryAssemblyVersion != null)
                clauses.Eq("lv", "AssemblyVersion", filter.LibraryAssemblyVersion);
        }
        else
            match = "MATCH (n:Document)";

        var (primary, dir) = StoreSort.Resolve(StoreSort.Documents, page.Sort, "documentId");
        var orderBy = StoreSort.OrderBy(primary, dir, "n.DocumentId", "n.VersionId");
        return Page<Document>(
            match,
            clauses,
            orderBy,
            n => StoreKeyset.Encode(primary, n, n.DocumentId, n.VersionId.ToString()),
            page,
            cancellationToken,
            c => StoreKeyset.ApplyAfter(c, page.After, 3, primary, dir, "n.DocumentId", "n.VersionId"));
    }

    public Task<PageResult<Node>> ListNodes(NodeFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        ApplyNodeFilter("n", filter, clauses);
        string match;
        var hasInstance = filter != null && GraphOrigins.IsKnown(filter.Origin) && !string.IsNullOrEmpty(filter.TypeId);
        if (hasInstance)
        {
            clauses.Parameters.TryAdd("instanceOrigin", filter!.Origin);
            clauses.Parameters.TryAdd("instanceTypeId", filter.TypeId);
            if (!string.IsNullOrEmpty(filter.DocumentId))
            {
                match = "MATCH (t:NodeType {Origin: $instanceOrigin, TypeId: $instanceTypeId})-[:HAS_INSTANCE]->(n:Node)<-[:CONTAINS]-(d:Document {DocumentId: $documentId})";
                clauses.Parameters.TryAdd("documentId", filter.DocumentId);
                if (filter.VersionId is Guid versionId)
                    clauses.Eq("d", "VersionId", versionId.ToString());
            }
            else
                match = "MATCH (t:NodeType {Origin: $instanceOrigin, TypeId: $instanceTypeId})-[:HAS_INSTANCE]->(n:Node)";
        }
        else if (!string.IsNullOrEmpty(filter?.DocumentId))
        {
            match = "MATCH (d:Document {DocumentId: $documentId})-[:CONTAINS]->(n:Node)";
            clauses.Parameters.TryAdd("documentId", filter.DocumentId);
            if (filter.VersionId is Guid versionId)
                clauses.Eq("d", "VersionId", versionId.ToString());
        }
        else
            match = "MATCH (n:Node)";

        var (primary, dir) = StoreSort.Resolve(StoreSort.Nodes, page.Sort, "nodeId");
        var orderBy = StoreSort.OrderBy(primary, dir, "n.VersionId", "n.NodeId");
        return Page<Node>(
            match,
            clauses,
            orderBy,
            n => StoreKeyset.Encode(primary, n, n.VersionId.ToString(), n.NodeId),
            page,
            cancellationToken,
            c => StoreKeyset.ApplyAfter(c, page.After, 3, primary, dir, "n.VersionId", "n.NodeId"));
    }

    public Task<PageResult<Port>> ListPorts(PortFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        ApplyPortFilter("n", filter, clauses);
        string match;
        if (!string.IsNullOrEmpty(filter?.NodeId))
        {
            if (filter.VersionId is not Guid versionId)
                throw GraphStoreException.InvalidArgument("ports filtered by nodeId require versionId.");
            match = "MATCH (:Node {VersionId: $nodeVersionId, NodeId: $nodeId})-[:HAS_PORT]->(n:Port)";
            clauses.Parameters.TryAdd("nodeVersionId", versionId.ToString());
            clauses.Parameters.TryAdd("nodeId", filter.NodeId);
        }
        else
            match = "MATCH (n:Port)";

        var (primary, dir) = StoreSort.Resolve(StoreSort.Ports, page.Sort, "portId");
        var orderBy = StoreSort.OrderBy(primary, dir, "n.VersionId", "n.PortId");
        return Page<Port>(
            match,
            clauses,
            orderBy,
            n => StoreKeyset.Encode(primary, n, n.VersionId.ToString(), n.PortId),
            page,
            cancellationToken,
            c => StoreKeyset.ApplyAfter(c, page.After, 3, primary, dir, "n.VersionId", "n.PortId"));
    }

    public async Task<PageResult<Edge>> ListEdges(EdgeFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        clauses.Eq("e", "VersionId", filter?.VersionId?.ToString());
        clauses.Eq("e", "SourcePortId", filter?.SourcePortId);
        clauses.Eq("e", "TargetPortId", filter?.TargetPortId);
        clauses.Extensions("e", filter?.Extensions);
        var take = page.Take();
        var (primary, dir) = StoreSort.Resolve(StoreSort.Edges, page.Sort, "versionId");
        var orderBy = StoreSort.OrderBy(primary, dir, "e.VersionId", "e.SourcePortId", "e.TargetPortId");
        var total = 0;
        if (page.IncludeTotalCount)
        {
            var countWhere = clauses.WherePrefix();
            var totalRows = await _db.RunQuery<CountRow>(
                $"MATCH ()-[e:EDGE]->() {countWhere} RETURN count(e) AS Total",
                clauses.Parameters,
                cancellationToken);
            total = (int)(totalRows.FirstOrDefault()?.Total ?? 0);
        }

        StoreKeyset.ApplyAfter(clauses, page.After, 4, primary, dir, "e.VersionId", "e.SourcePortId", "e.TargetPortId");
        var parameters = new Dictionary<string, object?>(clauses.Parameters) { ["limit"] = take + 1 };
        var where = clauses.WherePrefix();
        var items = new List<Edge>();
        await _db.QueryRecords(
            $"MATCH ()-[e:EDGE]->() {where} RETURN e AS Item ORDER BY {orderBy} LIMIT $limit",
            parameters,
            record =>
            {
                var edge = record.Relationship<Edge>("Item");
                if (edge != null)
                    items.Add(edge);
                return Task.CompletedTask;
            },
            cancellationToken);

        return ToPage(items, take, page.After != null, e => StoreKeyset.Encode(primary, e, e.VersionId.ToString(), e.SourcePortId, e.TargetPortId), total);
    }

    public Task<PageResult<NodeType>> ListNodeTypes(NodeTypeFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        clauses.Origin("n", filter?.Origin);
        clauses.Eq("n", "TypeId", filter?.TypeId);
        clauses.Contains("n", "Name", filter?.NameContains);
        clauses.Extensions("n", filter?.Extensions);
        var (primary, dir) = StoreSort.Resolve(StoreSort.NodeTypes, page.Sort, "typeId");
        var orderBy = StoreSort.OrderBy(primary, dir, "n.Origin", "n.TypeId");
        return Page<NodeType>(
            "MATCH (n:NodeType)",
            clauses,
            orderBy,
            n => StoreKeyset.Encode(primary, n, n.Origin, n.TypeId),
            page,
            cancellationToken,
            c => StoreKeyset.ApplyAfter(c, page.After, 3, primary, dir, "n.Origin", "n.TypeId"));
    }

    public Task<PageResult<Library>> ListLibraries(LibraryFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var clauses = new CypherClauses();
        clauses.Origin("n", filter?.Origin);
        clauses.Eq("n", "LibraryId", filter?.LibraryId);
        clauses.Contains("n", "Name", filter?.NameContains);
        clauses.Extensions("n", filter?.Extensions);
        var (primary, dir) = StoreSort.Resolve(StoreSort.Libraries, page.Sort, "libraryId");
        var orderBy = StoreSort.OrderBy(primary, dir, "n.Origin", "n.LibraryId");
        return Page<Library>(
            "MATCH (n:Library)",
            clauses,
            orderBy,
            n => StoreKeyset.Encode(primary, n, n.Origin, n.LibraryId),
            page,
            cancellationToken,
            c => StoreKeyset.ApplyAfter(c, page.After, 3, primary, dir, "n.Origin", "n.LibraryId"));
    }

    async Task<PageResult<T>> Page<T>(
        string match,
        CypherClauses clauses,
        string orderBy,
        Func<T, string> cursor,
        PageRequest page,
        CancellationToken cancellationToken,
        Action<CypherClauses>? applyCursor = null) where T : class, new()
    {
        var take = page.Take();
        var total = 0;
        if (page.IncludeTotalCount)
        {
            var countWhere = clauses.WherePrefix();
            var totalRows = await _db.RunQuery<CountRow>(
                $"{match} {countWhere} RETURN count(n) AS Total",
                new Dictionary<string, object?>(clauses.Parameters),
                cancellationToken);
            total = (int)(totalRows.FirstOrDefault()?.Total ?? 0);
        }
        applyCursor?.Invoke(clauses);
        var parameters = new Dictionary<string, object?>(clauses.Parameters) { ["limit"] = take + 1 };
        var where = clauses.WherePrefix();
        var items = await _db.RunQuery<DbOperations.CypherItemRecord<T>>(
            $"{match} {where} RETURN n AS Item ORDER BY {orderBy} LIMIT $limit",
            parameters,
            cancellationToken);
        var mapped = items.Where(r => r.Item != null).Select(r => r.Item!).ToList();
        return ToPage(mapped, take, page.After != null, cursor, total);
    }

    static PageResult<T> ToPage<T>(List<T> items, int take, bool hasPrevious, Func<T, string> cursor, int totalCount)
    {
        var hasNext = items.Count > take;
        if (hasNext)
            items.RemoveAt(items.Count - 1);
        var cursors = items.Select(cursor).ToList();
        return new PageResult<T>
        {
            Items = items,
            Cursors = cursors,
            TotalCount = totalCount,
            StartCursor = cursors.Count == 0 ? null : cursors[0],
            EndCursor = cursors.Count == 0 ? null : cursors[^1],
            HasNextPage = hasNext,
            HasPreviousPage = hasPrevious,
        };
    }

    static void ApplyDocumentFilter(string varName, DocumentFilter? filter, CypherClauses clauses)
    {
        if (filter == null)
            return;
        clauses.Origin(varName, filter.Origin);
        if (filter.IsNested is true)
            clauses.Eq(varName, "IsNested", true);
        else if (filter.IsNested is false)
            clauses.Add($"({varName}.IsNested IS NULL OR {varName}.IsNested = false)");
        if (filter.Committed is true)
            clauses.Eq(varName, "Committed", true);
        else if (filter.Committed is false)
            clauses.Add($"({varName}.Committed IS NULL OR {varName}.Committed = false)");
        clauses.Contains(varName, "FileName", filter.FileNameContains);
        clauses.Eq(varName, "DocumentId", filter.DocumentId);
        clauses.Eq(varName, "VersionId", filter.VersionId?.ToString());
        if (filter.CreatedAfterUtc is DateTime after)
        {
            var p = clauses.Bind("after", after);
            clauses.Add($"{varName}.FileCreationTimeUtc >= datetime(${p})");
        }
        if (filter.CreatedBeforeUtc is DateTime before)
        {
            var p = clauses.Bind("before", before);
            clauses.Add($"{varName}.FileCreationTimeUtc <= datetime(${p})");
        }
        clauses.Extensions(varName, filter.Extensions);
    }

    static void ApplyNodeFilter(string varName, NodeFilter? filter, CypherClauses clauses)
    {
        if (filter == null)
            return;
        clauses.Eq(varName, "VersionId", filter.VersionId?.ToString());
        clauses.Origin(varName, filter.Origin);
        clauses.Eq(varName, "Kind", filter.Kind);
        clauses.Eq(varName, "TypeId", filter.TypeId);
        clauses.Eq(varName, "Name", filter.Name);
        clauses.Eq(varName, "NickName", filter.NickName);
        clauses.Eq(varName, "Language", filter.Language);
        if (filter.Locked is bool locked)
            clauses.Eq(varName, "Locked", locked);
        if (filter.HasSource is true)
            clauses.Add($"{varName}.Source IS NOT NULL AND {varName}.Source <> ''");
        if (filter.HasSource is false)
            clauses.Add($"{varName}.Source IS NULL OR {varName}.Source = ''");
        if (filter.Bbox is BoundingBox box)
        {
            var minX = clauses.Bind("minX", box.MinX);
            var maxX = clauses.Bind("maxX", box.MaxX);
            var minY = clauses.Bind("minY", box.MinY);
            var maxY = clauses.Bind("maxY", box.MaxY);
            clauses.Add($"{varName}.X >= ${minX} AND {varName}.X <= ${maxX} AND {varName}.Y >= ${minY} AND {varName}.Y <= ${maxY}");
        }
        clauses.Extensions(varName, filter.Extensions);
    }

    static void ApplyPortFilter(string varName, PortFilter? filter, CypherClauses clauses)
    {
        if (filter == null)
            return;
        clauses.Eq(varName, "VersionId", filter.VersionId?.ToString());
        clauses.Eq(varName, "Name", filter.Name);
        clauses.Eq(varName, "Direction", filter.Direction);
        clauses.Eq(varName, "Access", filter.Access);
        clauses.Extensions(varName, filter.Extensions);
    }

    sealed class CountRow
    {
        public long Total { get; set; }
    }
}
