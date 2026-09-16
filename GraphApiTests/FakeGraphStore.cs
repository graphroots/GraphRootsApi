using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphApiTests;

sealed class FakeGraphStore : IGraphStore
{
    public Document Sample { get; } = new()
    {
        DocumentId = "doc-1",
        VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Origin = GraphOrigins.GraphRoots,
        FileName = "sample",
        Extensions = new Dictionary<string, object?> { ["gh.note"] = "hello" },
    };

    public LibraryVersion SampleLibrary { get; } = new()
    {
        Origin = GraphOrigins.Grasshopper,
        LibraryId = "lib-1",
        Version = "1.0.0",
        AssemblyVersion = "1.0.0.0",
        Name = "SampleLib",
    };

    public Exception? MatchFailure { get; set; }

    public LibraryVersion SampleUnknownLibrary { get; } = new()
    {
        Origin = GraphOrigins.Grasshopper,
        LibraryId = "lib-1",
        Version = "unknown",
        AssemblyVersion = "unknown",
        Name = "UnknownAsm",
    };

    public GraphPatch? LastPatch { get; private set; }

    public ImportSnapshot? LastSnapshot { get; private set; }

    public Task<Document?> GetDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(documentId == Sample.DocumentId && versionId == Sample.VersionId ? Sample : null);

    public Task<IReadOnlyList<Document?>> GetDocuments(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document?>>(keys.Select(k => k.DocumentId == Sample.DocumentId && k.VersionId == Sample.VersionId ? Sample : null).ToList());

    public Task<PageResult<Document>> ListDocuments(DocumentFilter? filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Sample.DocumentId));
        return Task.FromResult(new PageResult<Document>
        {
            Items = [Sample],
            Cursors = [cursor],
            TotalCount = page.IncludeTotalCount ? 1 : 0,
            StartCursor = cursor,
            EndCursor = cursor,
            HasNextPage = false,
            HasPreviousPage = page.After != null,
        });
    }

    public Task<PageResult<SubgraphMatch>> MatchSubgraph(SubgraphPattern pattern, MatchScope? scope, MatchOptions? options, PageRequest page, CancellationToken cancellationToken = default)
    {
        if (MatchFailure != null)
            throw MatchFailure;
        var unscoped = scope == null ||
            (scope.VersionId == null && string.IsNullOrEmpty(scope.DocumentId) && string.IsNullOrEmpty(scope.Origin));
        if (unscoped && page.First == null)
            throw GraphStoreException.LimitRequired("Corpus-wide matchSubgraph requires first.");
        SubgraphMatcher.Validate(pattern);
        return Task.FromResult(new PageResult<SubgraphMatch>
        {
            Items = [new SubgraphMatch { Document = Sample, Nodes = [], Ports = [], Edges = [], Connections = [] }],
            Cursors = ["cursor-1"],
            TotalCount = page.IncludeTotalCount ? 1 : 0,
            StartCursor = "cursor-1",
            EndCursor = "cursor-1",
        });
    }

    public Task<Document> CreateDocument(string? documentId, string? fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document
        {
            DocumentId = documentId ?? "new",
            VersionId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            Origin = GraphOrigins.GraphRoots,
            FileName = fileName,
        });

    public Task<GraphPatchResult> ApplyPatch(GraphPatch patch, CancellationToken cancellationToken = default)
    {
        GraphPatchValidator.Validate(patch);
        LastPatch = patch;
        return Task.FromResult(new GraphPatchResult { Document = Sample, CreatedNodeIds = patch.CreateNodes?.Select(n => n.NodeId).ToList() ?? [] });
    }

    public Task<ImportSnapshotResult> ImportSnapshot(ImportSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ImportSnapshotValidator.Validate(snapshot);
        LastSnapshot = snapshot;
        var root = snapshot.Documents.Single(d => d.IsNested != true);
        return Task.FromResult(new ImportSnapshotResult
        {
            Document = new Document
            {
                DocumentId = root.DocumentId,
                VersionId = root.VersionId,
                Origin = root.Origin,
                FileName = root.FileName,
                Committed = false,
            },
            NestedDocumentCount = snapshot.Documents.Count(d => d.IsNested == true),
            NodeCount = snapshot.Nodes.Count,
            PortCount = snapshot.Ports.Count,
            EdgeCount = snapshot.Edges.Count,
        });
    }

    public Task<Document> ForkDocument(string documentId, Guid versionId, string? newDocumentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Document
        {
            DocumentId = newDocumentId ?? "fork",
            VersionId = Guid.NewGuid(),
            Origin = GraphOrigins.GraphRoots,
            Committed = false,
        });

    public Task<CommitDocumentResult> CommitDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommitDocumentResult
        {
            Document = new Document
            {
                DocumentId = documentId,
                VersionId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
                Origin = GraphOrigins.GraphRoots,
                Committed = true,
            },
            WorkingDocument = Sample,
        });

    public Task<CommitDocumentResult> MergeDocument(
        string documentId,
        Guid versionId,
        IReadOnlyList<(string DocumentId, Guid VersionId)> otherParents,
        CancellationToken cancellationToken = default) =>
        CommitDocument(documentId, versionId, cancellationToken);

    public Task<Node?> GetNode(Guid versionId, string nodeId, CancellationToken cancellationToken = default) => None<Node>();
    public Task<Port?> GetPort(Guid versionId, string portId, CancellationToken cancellationToken = default) => None<Port>();
    public Task<Edge?> GetEdge(Guid versionId, string sourcePortId, string targetPortId, CancellationToken cancellationToken = default) => None<Edge>();
    public Task<NodeType?> GetNodeType(string origin, string typeId, CancellationToken cancellationToken = default) => None<NodeType>();
    public Task<Library?> GetLibrary(string origin, string libraryId, CancellationToken cancellationToken = default) => None<Library>();
    public Task<LibraryVersion?> GetLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default)
    {
        if (origin == SampleLibrary.Origin && libraryId == SampleLibrary.LibraryId && version == SampleLibrary.Version && assemblyVersion == SampleLibrary.AssemblyVersion)
            return Task.FromResult<LibraryVersion?>(SampleLibrary);
        if (origin == SampleUnknownLibrary.Origin && libraryId == SampleUnknownLibrary.LibraryId && version == SampleUnknownLibrary.Version && assemblyVersion == SampleUnknownLibrary.AssemblyVersion)
            return Task.FromResult<LibraryVersion?>(SampleUnknownLibrary);
        return Task.FromResult<LibraryVersion?>(null);
    }
    public Task<IReadOnlyList<Node?>> GetNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) => Empty<Node?>();
    public Task<IReadOnlyList<Port?>> GetPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default) => Empty<Port?>();
    public Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Port>>> GetPortsByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) => Dict<Port>();
    public Task<IReadOnlyDictionary<(Guid VersionId, string PortId), string?>> GetNodeIdsByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(Guid VersionId, string PortId), string?>>(keys.ToDictionary(k => k, _ => (string?)null));
    public Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetOutgoingEdgesByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default) => Dict<Edge>(keys);
    public Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetIncomingEdgesByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default) => Dict<Edge>(keys);
    public Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), Document?>> GetDocumentsForNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(Guid VersionId, string NodeId), Document?>>(keys.ToDictionary(k => k, _ => (Document?)null));
    public Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), NodeType?>> GetNodeTypesForNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(Guid VersionId, string NodeId), NodeType?>>(keys.ToDictionary(k => k, _ => (NodeType?)null));
    public Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetGroupsByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) => Dict<Node>();
    public Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetMembersByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default) => Dict<Node>();
    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetNestedDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>>(keys.ToDictionary(k => k, _ => (IReadOnlyList<Document>)[]));
    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetParentDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>>(keys.ToDictionary(k => k, _ => (IReadOnlyList<Document>)[]));
    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetBasedOnByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>>(keys.ToDictionary(k => k, _ => (IReadOnlyList<Document>)[]));
    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetDerivedDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>>(keys.ToDictionary(k => k, _ => (IReadOnlyList<Document>)[]));
    public Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>> GetLibrariesForDocuments(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>>(
            keys.ToDictionary(k => k, k => (IReadOnlyList<LibraryVersion>)(k.DocumentId == Sample.DocumentId && k.VersionId == Sample.VersionId ? [SampleLibrary] : [])));
    public Task<PageResult<Node>> ListNodes(NodeFilter? filter, PageRequest page, CancellationToken cancellationToken = default) => EmptyPage<Node>();
    public Task<PageResult<Port>> ListPorts(PortFilter? filter, PageRequest page, CancellationToken cancellationToken = default) => EmptyPage<Port>();
    public Task<PageResult<Edge>> ListEdges(EdgeFilter? filter, PageRequest page, CancellationToken cancellationToken = default) => EmptyPage<Edge>();
    public Task<PageResult<NodeType>> ListNodeTypes(NodeTypeFilter? filter, PageRequest page, CancellationToken cancellationToken = default) => EmptyPage<NodeType>();
    public Task<PageResult<Library>> ListLibraries(LibraryFilter? filter, PageRequest page, CancellationToken cancellationToken = default) => EmptyPage<Library>();
    public Task<DocumentGraphResult> GetDocumentGraph(string documentId, Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentGraphResult());
    public Task<DocumentStatsResult> GetDocumentStats(string documentId, Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentStatsResult
        {
            NodeCount = 2,
            CountsByKind = [("Operator", 1), ("Parameter", 1)],
        });
    public Task<IReadOnlyList<Document>> GetNestedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default) => Empty<Document>();
    public Task<IReadOnlyList<Document>> GetParentDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default) => Empty<Document>();
    public Task<IReadOnlyList<Document>> GetBasedOn(string documentId, Guid versionId, CancellationToken cancellationToken = default) => Empty<Document>();
    public Task<IReadOnlyList<Document>> GetDerivedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default) => Empty<Document>();
    public Task<IReadOnlyList<Document>> TraverseHistory(string documentId, Guid versionId, HistoryDirection direction, int minHops, int maxHops, CancellationToken cancellationToken = default) => Empty<Document>();
    public Task<IReadOnlyList<LibraryVersion>> GetLibrariesForDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LibraryVersion>>(documentId == Sample.DocumentId && versionId == Sample.VersionId ? [SampleLibrary] : []);
    public Task<IReadOnlyList<LibraryVersion>> GetLibraryVersions(string origin, string libraryId, CancellationToken cancellationToken = default) => Empty<LibraryVersion>();
    public Task<IReadOnlyList<Node>> GetGroups(Guid versionId, string nodeId, CancellationToken cancellationToken = default) => Empty<Node>();
    public Task<IReadOnlyList<Node>> GetMembers(Guid versionId, string nodeId, CancellationToken cancellationToken = default) => Empty<Node>();
    public Task<IReadOnlyList<Node>> GetIsolatedNodes(string documentId, Guid versionId, CancellationToken cancellationToken = default) => Empty<Node>();
    public Task<IReadOnlyList<LibraryVersion>> GetDefinedBy(string origin, string typeId, CancellationToken cancellationToken = default) => Empty<LibraryVersion>();
    public Task<IReadOnlyList<NodeType>> GetNodeTypesForLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default) => Empty<NodeType>();
    public Task<Document?> GetClusterDocument(Guid versionId, string nodeId, CancellationToken cancellationToken = default) => None<Document>();
    public Task<IReadOnlyList<Node>> Traverse(Guid versionId, string nodeId, DataflowDirection direction, int minHops, int maxHops, CancellationToken cancellationToken = default) => Empty<Node>();
    public Task<DataflowPathResult?> FindPath(string documentId, Guid versionId, string fromNodeId, string toNodeId, int maxHops, CancellationToken cancellationToken = default) =>
        Task.FromResult<DataflowPathResult?>(null);

    static Task<T?> None<T>() where T : class => Task.FromResult<T?>(null);
    static Task<IReadOnlyList<T>> Empty<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    static Task<PageResult<T>> EmptyPage<T>() => Task.FromResult(new PageResult<T>());
    static Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<T>>> Dict<T>() =>
        Task.FromResult<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<T>>>(new Dictionary<(Guid, string), IReadOnlyList<T>>());
    static Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<T>>> Dict<T>(IReadOnlyList<(Guid VersionId, string PortId)> keys) =>
        Task.FromResult<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<T>>>(keys.ToDictionary(k => k, _ => (IReadOnlyList<T>)[]));
}
