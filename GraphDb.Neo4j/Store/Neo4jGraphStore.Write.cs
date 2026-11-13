using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace GraphRoots.GraphDb.Neo4j;

public sealed partial class Neo4jGraphStore
{
    public async Task<Document> ForkDocument(string documentId, Guid versionId, string? newDocumentId, CancellationToken cancellationToken = default)
    {
        var destId = string.IsNullOrWhiteSpace(newDocumentId) ? Guid.NewGuid().ToString() : newDocumentId;
        var destVersion = Guid.NewGuid();
        Document? source = null;

        await _db.ExecuteWrite(async ct =>
        {
            source = DocumentLifecycle.RequireDocument(await GetDocument(documentId, versionId, ct), documentId, versionId);
            var sourceHead = DocumentLifecycle.IsWorkingCopy(source)
                ? await GetBasedOn(source.DocumentId, source.VersionId, ct)
                : [];
            await CopyDocumentSubgraph(source.DocumentId, source.VersionId, destId, destVersion, committed: false, ct);
            await MergeBasedOn(destId, destVersion, DocumentLifecycle.ForkParents(source, sourceHead), ct);
        }, cancellationToken);

        return await GetDocument(destId, destVersion, cancellationToken)
            ?? new Document
            {
                DocumentId = destId,
                VersionId = destVersion,
                Origin = GraphOrigins.GraphRoots,
                FileName = source!.FileName,
                IsNested = source.IsNested,
                Committed = false,
            };
    }

    public async Task<CommitDocumentResult> CommitDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var commitVersion = Guid.NewGuid();
        await _db.ExecuteWrite(async ct =>
        {
            var working = DocumentLifecycle.RequireWorkingCopy(await LockDocument(documentId, versionId, ct), documentId, versionId);
            var parents = await GetBasedOn(working.DocumentId, working.VersionId, ct);
            await CopyDocumentSubgraph(working.DocumentId, working.VersionId, working.DocumentId, commitVersion, committed: true, ct);
            await MergeBasedOn(working.DocumentId, commitVersion, parents, ct);
            await RepointHead(working.DocumentId, working.VersionId, working.DocumentId, commitVersion, ct);
        }, cancellationToken);

        return await LoadCommitResult(documentId, commitVersion, documentId, versionId, cancellationToken);
    }

    public async Task<CommitDocumentResult> MergeDocument(
        string documentId,
        Guid versionId,
        IReadOnlyList<(string DocumentId, Guid VersionId)> otherParents,
        CancellationToken cancellationToken = default)
    {
        var commitVersion = Guid.NewGuid();
        await _db.ExecuteWrite(async ct =>
        {
            var working = DocumentLifecycle.RequireWorkingCopy(await LockDocument(documentId, versionId, ct), documentId, versionId);
            var requested = (otherParents ?? []).Distinct().ToList();
            var head = await GetBasedOn(working.DocumentId, working.VersionId, ct);
            var ancestors = await AncestorsAmong(working, requested, ct);
            var found = await GetDocuments(requested, ct);
            var resolved = new Dictionary<(string DocumentId, Guid VersionId), Document?>();
            for (var i = 0; i < requested.Count; i++)
                resolved[requested[i]] = found[i];

            var parents = DocumentLifecycle.MergeParents(working, head, otherParents, resolved, ancestors);
            await CopyDocumentSubgraph(working.DocumentId, working.VersionId, working.DocumentId, commitVersion, committed: true, ct);
            await MergeBasedOn(working.DocumentId, commitVersion, parents, ct);
            await RepointHead(working.DocumentId, working.VersionId, working.DocumentId, commitVersion, ct);
        }, cancellationToken);

        return await LoadCommitResult(documentId, commitVersion, documentId, versionId, cancellationToken);
    }

    /// <summary>
    /// Inside a write transaction: take the write lock on a document node (held until commit) and return its
    /// current state, so concurrent commits or merges on one working copy serialize and see each other's HEAD.
    /// </summary>
    async Task<Document?> LockDocument(string documentId, Guid versionId, CancellationToken cancellationToken)
    {
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Document>>(@"
MATCH (n:Document {DocumentId: $documentId, VersionId: $versionId})
SET n.updatedAt = datetime()
RETURN n AS Item",
            new { documentId, versionId = versionId.ToString() },
            cancellationToken);
        return rows.FirstOrDefault()?.Item;
    }

    /// <summary>
    /// The keys among <paramref name="candidates"/> that <paramref name="working"/> reaches over <c>BASED_ON</c>
    /// at any depth. Uses a breadth-first shortest path per candidate, so merge-heavy histories do not
    /// enumerate every path.
    /// </summary>
    async Task<IReadOnlySet<(string DocumentId, Guid VersionId)>> AncestorsAmong(
        Document working,
        IReadOnlyList<(string DocumentId, Guid VersionId)> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return new HashSet<(string DocumentId, Guid VersionId)>();
        var rows = await _db.RunQuery<DbOperations.CypherItemRecord<Document>>(@"
MATCH (w:Document {DocumentId: $documentId, VersionId: $versionId})
UNWIND $candidates AS c
MATCH (a:Document {DocumentId: c.documentId, VersionId: c.versionId})
WHERE a <> w
MATCH p = shortestPath((w)-[:BASED_ON*1..]->(a))
RETURN DISTINCT a AS Item",
            new
            {
                documentId = working.DocumentId,
                versionId = working.VersionId.ToString(),
                candidates = candidates
                    .Select(c => new Dictionary<string, object> { ["documentId"] = c.DocumentId, ["versionId"] = c.VersionId.ToString() })
                    .ToList(),
            },
            cancellationToken);
        return rows
            .Where(r => r.Item != null)
            .Select(r => (r.Item!.DocumentId, r.Item.VersionId))
            .ToHashSet();
    }

    public async Task<Document> CreateDocument(string? documentId, string? fileName, CancellationToken cancellationToken = default)
    {
        var document = new Document
        {
            DocumentId = string.IsNullOrWhiteSpace(documentId) ? Guid.NewGuid().ToString() : documentId,
            VersionId = Guid.NewGuid(),
            Origin = GraphOrigins.GraphRoots,
            FileName = fileName,
            IsNested = false,
            Committed = false,
        };
        try
        {
            await _db.CreateNode(document, cancellationToken);
        }
        catch (Exception ex) when (IsConstraintViolation(ex))
        {
            throw GraphStoreException.Conflict($"Document '{document.DocumentId}' already exists.");
        }
        return document;
    }

    public async Task<GraphPatchResult> ApplyPatch(GraphPatch patch, CancellationToken cancellationToken = default)
    {
        GraphPatchValidator.Validate(patch);
        var document = DocumentLifecycle.RequireWorkingCopy(
            await GetDocument(patch.DocumentId, patch.VersionId, cancellationToken),
            patch.DocumentId,
            patch.VersionId);

        var createdNodes = new List<string>();
        var deletedNodes = new List<string>();
        var createdPorts = new List<string>();
        var deletedPorts = new List<string>();

        try
        {
            await _db.ExecuteWrite(async ct =>
            {
                foreach (var op in patch.CreateNodes ?? [])
                {
                    await CreatePatchedNode(document, patch.VersionId, op, ct);
                    createdNodes.Add(op.NodeId);
                }

                foreach (var op in patch.UpdateNodes ?? [])
                    await UpdatePatchedNode(patch.VersionId, op, ct);

                foreach (var nodeId in patch.DeleteNodes ?? [])
                {
                    await CascadeDeleteNode(patch.VersionId, nodeId, ct);
                    deletedNodes.Add(nodeId);
                }

                foreach (var op in patch.CreatePorts ?? [])
                {
                    await CreatePatchedPort(patch.VersionId, op, ct);
                    createdPorts.Add(op.PortId);
                }

                foreach (var op in patch.UpdatePorts ?? [])
                    await UpdatePatchedPort(patch.VersionId, op, ct);

                foreach (var portId in patch.DeletePorts ?? [])
                {
                    await CascadeDeletePort(patch.VersionId, portId, ct);
                    deletedPorts.Add(portId);
                }

                foreach (var op in patch.CreateEdges ?? [])
                    await CreatePatchedEdge(patch.VersionId, op, ct);

                foreach (var op in patch.DeleteEdges ?? [])
                {
                    var edge = await GetEdge(patch.VersionId, op.SourcePortId, op.TargetPortId, ct)
                        ?? throw GraphStoreException.NotFound($"Edge {op.SourcePortId}->{op.TargetPortId} was not found.");
                    var source = new Port { VersionId = patch.VersionId, PortId = op.SourcePortId };
                    var target = new Port { VersionId = patch.VersionId, PortId = op.TargetPortId };
                    await _db.DeleteRelationship(source, target, edge, ct);
                }

                foreach (var op in patch.AddToGroups ?? [])
                {
                    await EnsureNode(patch.VersionId, op.MemberNodeId, ct);
                    var group = await EnsureNode(patch.VersionId, op.GroupNodeId, ct);
                    if (!string.Equals(group.Kind, NodeKinds.Group, StringComparison.Ordinal))
                        throw GraphStoreException.InvalidArgument($"addToGroups.groupNodeId '{op.GroupNodeId}' must be a Group node.");
                    await _db.MergeRelationship(
                        new Node { VersionId = patch.VersionId, NodeId = op.MemberNodeId },
                        new Node { VersionId = patch.VersionId, NodeId = op.GroupNodeId },
                        new MemberOf(),
                        ct);
                }

                foreach (var op in patch.RemoveFromGroups ?? [])
                {
                    await _db.DeleteRelationship(
                        new Node { VersionId = patch.VersionId, NodeId = op.MemberNodeId },
                        new Node { VersionId = patch.VersionId, NodeId = op.GroupNodeId },
                        new MemberOf(),
                        ct);
                }

                foreach (var op in patch.SetExtensions ?? [])
                    await ApplyExtensions(document, patch.VersionId, op, ct);
            }, cancellationToken);
        }
        catch (Exception ex) when (IsConstraintViolation(ex))
        {
            throw GraphStoreException.Conflict("Patch conflicted with an existing graph key.");
        }

        var updated = await GetDocument(patch.DocumentId, patch.VersionId, cancellationToken) ?? document;
        return new GraphPatchResult
        {
            Document = updated,
            CreatedNodeIds = createdNodes,
            DeletedNodeIds = deletedNodes,
            CreatedPortIds = createdPorts,
            DeletedPortIds = deletedPorts,
        };
    }

    async Task CreatePatchedNode(Document document, Guid versionId, CreateNodeOp op, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(op.NodeId))
            throw GraphStoreException.InvalidArgument("createNodes.nodeId is required.");
        var existing = await GetNode(versionId, op.NodeId, cancellationToken);
        if (existing != null)
            throw GraphStoreException.Conflict($"Node '{op.NodeId}' already exists.");

        var origin = string.IsNullOrWhiteSpace(op.Origin) ? GraphOrigins.GraphRoots : op.Origin;
        var node = new Node
        {
            VersionId = versionId,
            NodeId = op.NodeId,
            Origin = origin,
            TypeId = op.TypeId,
            Name = op.Name,
            NickName = op.NickName,
            Kind = op.Kind,
            Locked = op.Locked,
            X = op.X,
            Y = op.Y,
            Source = op.Source,
            Language = op.Language,
            Text = op.Text,
        };
        await _db.CreateNode(node, cancellationToken);
        await _db.MergeRelationship(document, node, new Contains(), cancellationToken);
        if (!string.IsNullOrEmpty(op.TypeId))
            await AttachNodeType(node, origin, op.TypeId, op.Name, cancellationToken);
    }

    async Task UpdatePatchedNode(Guid versionId, UpdateNodeOp op, CancellationToken cancellationToken)
    {
        var node = await GetNode(versionId, op.NodeId, cancellationToken)
            ?? throw GraphStoreException.NotFound($"Node '{op.NodeId}' was not found.");
        var previousTypeId = node.TypeId;
        if (op.TypeId.IsSpecified) node.TypeId = op.TypeId.Value;
        if (op.Name.IsSpecified) node.Name = op.Name.Value;
        if (op.NickName.IsSpecified) node.NickName = op.NickName.Value;
        if (op.Kind.IsSpecified) node.Kind = op.Kind.Value;
        if (op.Locked.IsSpecified) node.Locked = op.Locked.Value;
        if (op.X.IsSpecified) node.X = op.X.Value;
        if (op.Y.IsSpecified) node.Y = op.Y.Value;
        if (op.Source.IsSpecified) node.Source = op.Source.Value;
        if (op.Language.IsSpecified) node.Language = op.Language.Value;
        if (op.Text.IsSpecified) node.Text = op.Text.Value;
        await _db.MergeNode(node, cancellationToken);

        if (op.TypeId.IsSpecified && !string.Equals(previousTypeId, node.TypeId, StringComparison.Ordinal))
        {
            await DetachNodeTypes(versionId, op.NodeId, cancellationToken);
            if (!string.IsNullOrEmpty(node.TypeId))
                await AttachNodeType(node, node.Origin, node.TypeId, node.Name, cancellationToken);
        }
    }

    async Task AttachNodeType(Node node, string origin, string typeId, string? name, CancellationToken cancellationToken)
    {
        var schemaVersion = typeof(NodeType).GetCustomAttribute<DbSchemaVersionAttribute>()!.Version;
        await _db.QueryRecords(
            @"MERGE (t:NodeType {Origin: $origin, TypeId: $typeId})
ON CREATE SET t.Name = $name, t.schemaVersion = $schemaVersion, t.createdAt = datetime(), t.updatedAt = datetime()
ON MATCH SET t.updatedAt = datetime()
FOREACH (_ IN CASE WHEN (t.Name IS NULL OR t.Name = '') AND $name IS NOT NULL AND $name <> '' THEN [1] ELSE [] END | SET t.Name = $name)
RETURN t",
            new { origin, typeId, name, schemaVersion },
            _ => Task.CompletedTask,
            cancellationToken);
        var nodeType = new NodeType { Origin = origin, TypeId = typeId };
        await _db.MergeRelationship(nodeType, node, new HasInstance(), cancellationToken);
    }

    Task DetachNodeTypes(Guid versionId, string nodeId, CancellationToken cancellationToken)
    {
        return _db.QueryRecords(
            @"MATCH (t:NodeType)-[r:HAS_INSTANCE]->(n:Node {VersionId: $versionId, NodeId: $nodeId})
DELETE r
RETURN 1 AS Item",
            new { versionId = versionId.ToString(), nodeId },
            _ => Task.CompletedTask,
            cancellationToken);
    }

    async Task CreatePatchedPort(Guid versionId, CreatePortOp op, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(op.PortId) || string.IsNullOrWhiteSpace(op.NodeId))
            throw GraphStoreException.InvalidArgument("createPorts requires portId and nodeId.");
        var node = await EnsureNode(versionId, op.NodeId, cancellationToken);
        var existing = await GetPort(versionId, op.PortId, cancellationToken);
        if (existing != null)
            throw GraphStoreException.Conflict($"Port '{op.PortId}' already exists.");
        var port = new Port
        {
            VersionId = versionId,
            PortId = op.PortId,
            Name = op.Name,
            Direction = op.Direction,
            Access = op.Access,
        };
        await _db.CreateNode(port, cancellationToken);
        await _db.MergeRelationship(node, port, new HasPort(), cancellationToken);
    }

    async Task UpdatePatchedPort(Guid versionId, UpdatePortOp op, CancellationToken cancellationToken)
    {
        var port = await GetPort(versionId, op.PortId, cancellationToken)
            ?? throw GraphStoreException.NotFound($"Port '{op.PortId}' was not found.");
        if (op.Name.IsSpecified) port.Name = op.Name.Value;
        if (op.Direction.IsSpecified) port.Direction = op.Direction.Value;
        if (op.Access.IsSpecified) port.Access = op.Access.Value;
        await _db.MergeNode(port, cancellationToken);
    }

    async Task CreatePatchedEdge(Guid versionId, CreateEdgeOp op, CancellationToken cancellationToken)
    {
        var source = await GetPort(versionId, op.SourcePortId, cancellationToken)
            ?? throw GraphStoreException.NotFound($"Source port '{op.SourcePortId}' was not found.");
        var target = await GetPort(versionId, op.TargetPortId, cancellationToken)
            ?? throw GraphStoreException.NotFound($"Target port '{op.TargetPortId}' was not found.");
        var existing = await GetEdge(versionId, op.SourcePortId, op.TargetPortId, cancellationToken);
        if (existing != null)
            throw GraphStoreException.Conflict($"Edge {op.SourcePortId}->{op.TargetPortId} already exists.");
        await _db.MergeRelationship(source, target, new Edge
        {
            VersionId = versionId,
            SourcePortId = op.SourcePortId,
            TargetPortId = op.TargetPortId,
            SourceName = op.SourceName ?? source.Name,
            TargetName = op.TargetName ?? target.Name,
        }, cancellationToken);
    }

    async Task CascadeDeleteNode(Guid versionId, string nodeId, CancellationToken cancellationToken)
    {
        await EnsureNode(versionId, nodeId, cancellationToken);
        await _db.QueryRecords(
            @"MATCH (n:Node {VersionId: $versionId, NodeId: $nodeId})
OPTIONAL MATCH (n)-[:HAS_PORT]->(p:Port)
OPTIONAL MATCH (p)-[e:EDGE]-()
DETACH DELETE e, p, n
RETURN 1 AS Item",
            new { versionId = versionId.ToString(), nodeId },
            _ => Task.CompletedTask,
            cancellationToken);
    }

    async Task CascadeDeletePort(Guid versionId, string portId, CancellationToken cancellationToken)
    {
        if (await GetPort(versionId, portId, cancellationToken) == null)
            throw GraphStoreException.NotFound($"Port '{portId}' was not found.");
        await _db.QueryRecords(
            @"MATCH (p:Port {VersionId: $versionId, PortId: $portId})
OPTIONAL MATCH (p)-[e:EDGE]-()
DETACH DELETE e, p
RETURN 1 AS Item",
            new { versionId = versionId.ToString(), portId },
            _ => Task.CompletedTask,
            cancellationToken);
    }

    async Task<Node> EnsureNode(Guid versionId, string nodeId, CancellationToken cancellationToken)
    {
        return await GetNode(versionId, nodeId, cancellationToken)
            ?? throw GraphStoreException.NotFound($"Node '{nodeId}' was not found.");
    }

    async Task ApplyExtensions(Document document, Guid versionId, SetExtensionsOp op, CancellationToken cancellationToken)
    {
        var vid = versionId.ToString();
        string match;
        object parameters;
        HashSet<string> coreNames;
        switch (op.Entity)
        {
            case StoreEntityKind.Node:
                await EnsureNode(versionId, op.Id, cancellationToken);
                match = "MATCH (n:Node {VersionId: $versionId, NodeId: $id})";
                parameters = new { versionId = vid, id = op.Id };
                coreNames = ExtensionKeys.CorePropertyNames(typeof(Node));
                break;
            case StoreEntityKind.Port:
                if (await GetPort(versionId, op.Id, cancellationToken) == null)
                    throw GraphStoreException.NotFound($"Port '{op.Id}' was not found.");
                match = "MATCH (n:Port {VersionId: $versionId, PortId: $id})";
                parameters = new { versionId = vid, id = op.Id };
                coreNames = ExtensionKeys.CorePropertyNames(typeof(Port));
                break;
            case StoreEntityKind.Document:
                if (!string.Equals(op.Id, document.DocumentId, StringComparison.Ordinal))
                    throw GraphStoreException.InvalidArgument("setExtensions.id must match the patch documentId.");
                match = "MATCH (n:Document {DocumentId: $id, VersionId: $versionId})";
                parameters = new { versionId = vid, id = document.DocumentId };
                coreNames = ExtensionKeys.CorePropertyNames(typeof(Document));
                break;
            case StoreEntityKind.Edge:
                if (string.IsNullOrEmpty(op.SourcePortId) || string.IsNullOrEmpty(op.TargetPortId))
                    throw GraphStoreException.InvalidArgument("setExtensions for EDGE requires sourcePortId and targetPortId.");
                if (await GetEdge(versionId, op.SourcePortId, op.TargetPortId, cancellationToken) == null)
                    throw GraphStoreException.NotFound($"Edge {op.SourcePortId}->{op.TargetPortId} was not found.");
                match = "MATCH ()-[n:EDGE {VersionId: $versionId, SourcePortId: $sourcePortId, TargetPortId: $targetPortId}]->()";
                parameters = new { versionId = vid, sourcePortId = op.SourcePortId, targetPortId = op.TargetPortId };
                coreNames = ExtensionKeys.CorePropertyNames(typeof(Edge));
                break;
            default:
                throw GraphStoreException.InvalidArgument($"Unsupported entity kind '{op.Entity}'.");
        }

        foreach (var entry in op.Entries)
        {
            ExtensionKeys.ValidateKey(entry.Key, coreNames);
            var dict = ToDictionary(parameters);
            dict["extKey"] = entry.Key;
            if (entry.EqualsValue == null)
            {
                await _db.QueryRecords(
                    $"{match} REMOVE {DbExtensionProperties.CypherDynamicProperty("n", "extKey")} RETURN 1 AS Item",
                    dict,
                    _ => Task.CompletedTask,
                    cancellationToken);
            }
            else
            {
                dict["value"] = ExtensionKeys.NormalizeValue(entry.EqualsValue);
                await _db.QueryRecords(
                    $"{match} SET {DbExtensionProperties.CypherDynamicProperty("n", "extKey")} = $value RETURN 1 AS Item",
                    dict,
                    _ => Task.CompletedTask,
                    cancellationToken);
            }
        }
    }

    async Task CopyDocumentSubgraph(
        string sourceDocumentId,
        Guid sourceVersionId,
        string destId,
        Guid destVersion,
        bool committed,
        CancellationToken cancellationToken)
    {
        var p = new
        {
            documentId = sourceDocumentId,
            from = sourceVersionId.ToString(),
            destId,
            to = destVersion.ToString(),
            origin = GraphOrigins.GraphRoots,
            committed,
        };
        await Exec(@"
MATCH (src:Document {DocumentId: $documentId, VersionId: $from})
CREATE (dst:Document)
SET dst = src {.*, DocumentId: $destId, VersionId: $to, Origin: $origin, FilePath: null, Committed: $committed, createdAt: datetime(), updatedAt: datetime()}
RETURN dst AS Item", p, cancellationToken);
        await Exec(@"
MATCH (src:Document {DocumentId: $documentId, VersionId: $from})-[:CONTAINS]->(n:Node)
MATCH (dst:Document {DocumentId: $destId, VersionId: $to})
CREATE (n2:Node)
SET n2 = n {.*, VersionId: $to}
CREATE (dst)-[:CONTAINS]->(n2)
RETURN count(n2) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (n:Node {VersionId: $from})-[:HAS_PORT]->(port:Port)
MATCH (n2:Node {VersionId: $to, NodeId: n.NodeId})
CREATE (p2:Port)
SET p2 = port {.*, VersionId: $to}
CREATE (n2)-[:HAS_PORT]->(p2)
RETURN count(p2) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (a:Port {VersionId: $from})-[e:EDGE]->(b:Port)
MATCH (a2:Port {VersionId: $to, PortId: a.PortId})
MATCH (b2:Port {VersionId: $to, PortId: b.PortId})
CREATE (a2)-[e2:EDGE]->(b2)
SET e2 = e {.*, VersionId: $to}
RETURN count(e2) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (t:NodeType)-[:HAS_INSTANCE]->(n:Node {VersionId: $from})
MATCH (n2:Node {VersionId: $to, NodeId: n.NodeId})
MERGE (t)-[:HAS_INSTANCE]->(n2)
RETURN count(n2) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (m:Node {VersionId: $from})-[:MEMBER_OF]->(g:Node {VersionId: $from})
MATCH (m2:Node {VersionId: $to, NodeId: m.NodeId})
MATCH (g2:Node {VersionId: $to, NodeId: g.NodeId})
MERGE (m2)-[:MEMBER_OF]->(g2)
RETURN count(m2) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (lv:LibraryVersion)-[:USED_BY]->(src:Document {DocumentId: $documentId, VersionId: $from})
MATCH (dst:Document {DocumentId: $destId, VersionId: $to})
MERGE (lv)-[:USED_BY]->(dst)
RETURN count(lv) AS Item", p, cancellationToken);
        await Exec(@"
MATCH (src:Document {DocumentId: $documentId, VersionId: $from})-[:NESTS]->(nested:Document)
MATCH (dst:Document {DocumentId: $destId, VersionId: $to})
MERGE (dst)-[:NESTS]->(nested)
RETURN count(nested) AS Item", p, cancellationToken);
    }

    async Task MergeBasedOn(string childDocumentId, Guid childVersionId, IReadOnlyList<Document> parents, CancellationToken cancellationToken)
    {
        foreach (var parent in parents)
        {
            await Exec(@"
MATCH (child:Document {DocumentId: $childId, VersionId: $childVer})
MATCH (parent:Document {DocumentId: $parentId, VersionId: $parentVer})
MERGE (child)-[:BASED_ON]->(parent)
RETURN child",
                new
                {
                    childId = childDocumentId,
                    childVer = childVersionId.ToString(),
                    parentId = parent.DocumentId,
                    parentVer = parent.VersionId.ToString(),
                },
                cancellationToken);
        }
    }

    async Task RepointHead(string workingDocumentId, Guid workingVersionId, string commitDocumentId, Guid commitVersionId, CancellationToken cancellationToken)
    {
        await Exec(@"
MATCH (w:Document {DocumentId: $documentId, VersionId: $versionId})-[r:BASED_ON]->()
DELETE r
RETURN count(r) AS Item",
            new { documentId = workingDocumentId, versionId = workingVersionId.ToString() },
            cancellationToken);
        await Exec(@"
MATCH (w:Document {DocumentId: $workingId, VersionId: $workingVer})
MATCH (c:Document {DocumentId: $commitId, VersionId: $commitVer})
MERGE (w)-[:BASED_ON]->(c)
RETURN w",
            new
            {
                workingId = workingDocumentId,
                workingVer = workingVersionId.ToString(),
                commitId = commitDocumentId,
                commitVer = commitVersionId.ToString(),
            },
            cancellationToken);
    }

    async Task<CommitDocumentResult> LoadCommitResult(
        string commitDocumentId,
        Guid commitVersionId,
        string workingDocumentId,
        Guid workingVersionId,
        CancellationToken cancellationToken)
    {
        var commit = await GetDocument(commitDocumentId, commitVersionId, cancellationToken)
            ?? throw GraphStoreException.StoreError($"Committed document '{commitDocumentId}' version '{commitVersionId}' was not found after write.");
        var working = await GetDocument(workingDocumentId, workingVersionId, cancellationToken)
            ?? throw GraphStoreException.StoreError($"Working document '{workingDocumentId}' version '{workingVersionId}' was not found after write.");
        return new CommitDocumentResult { Document = commit, WorkingDocument = working };
    }

    Task Exec(string query, object parameters, CancellationToken cancellationToken) =>
        _db.QueryRecords(query, parameters, _ => Task.CompletedTask, cancellationToken);

    static Dictionary<string, object?> ToDictionary(object parameters)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var property in parameters.GetType().GetProperties())
            dict[property.Name] = property.GetValue(parameters);
        return dict;
    }

    static bool IsConstraintViolation(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is ClientException client &&
                (client.Code.Contains("Constraint", StringComparison.OrdinalIgnoreCase) ||
                 client.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                 client.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}
