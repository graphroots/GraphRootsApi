using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public interface IGraphStore
{
    Task<Document?> GetDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<Node?> GetNode(Guid versionId, string nodeId, CancellationToken cancellationToken = default);

    Task<Port?> GetPort(Guid versionId, string portId, CancellationToken cancellationToken = default);

    Task<Edge?> GetEdge(Guid versionId, string sourcePortId, string targetPortId, CancellationToken cancellationToken = default);

    Task<NodeType?> GetNodeType(string origin, string typeId, CancellationToken cancellationToken = default);

    Task<Library?> GetLibrary(string origin, string libraryId, CancellationToken cancellationToken = default);

    Task<LibraryVersion?> GetLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document?>> GetDocuments(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node?>> GetNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Port?>> GetPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Port>>> GetPortsByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string PortId), string?>> GetNodeIdsByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetOutgoingEdgesByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> GetIncomingEdgesByPorts(IReadOnlyList<(Guid VersionId, string PortId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), Document?>> GetDocumentsForNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), NodeType?>> GetNodeTypesForNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetGroupsByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> GetMembersByNodes(IReadOnlyList<(Guid VersionId, string NodeId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetNestedDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetParentDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetBasedOnByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> GetDerivedDocumentsByKeys(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>> GetLibrariesForDocuments(IReadOnlyList<(string DocumentId, Guid VersionId)> keys, CancellationToken cancellationToken = default);

    Task<PageResult<Document>> ListDocuments(DocumentFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<PageResult<Node>> ListNodes(NodeFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<PageResult<Port>> ListPorts(PortFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<PageResult<Edge>> ListEdges(EdgeFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<PageResult<NodeType>> ListNodeTypes(NodeTypeFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<PageResult<Library>> ListLibraries(LibraryFilter? filter, PageRequest page, CancellationToken cancellationToken = default);

    Task<DocumentGraphResult> GetDocumentGraph(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<DocumentStatsResult> GetDocumentStats(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetNestedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetParentDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetBasedOn(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetDerivedDocuments(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> TraverseHistory(string documentId, Guid versionId, HistoryDirection direction, int minHops, int maxHops, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryVersion>> GetLibrariesForDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryVersion>> GetLibraryVersions(string origin, string libraryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> GetGroups(Guid versionId, string nodeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> GetMembers(Guid versionId, string nodeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> GetIsolatedNodes(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryVersion>> GetDefinedBy(string origin, string typeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeType>> GetNodeTypesForLibraryVersion(string origin, string libraryId, string version, string? assemblyVersion, CancellationToken cancellationToken = default);

    Task<Document?> GetClusterDocument(Guid versionId, string nodeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Node>> Traverse(Guid versionId, string nodeId, DataflowDirection direction, int minHops, int maxHops, CancellationToken cancellationToken = default);

    Task<DataflowPathResult?> FindPath(string documentId, Guid versionId, string fromNodeId, string toNodeId, int maxHops, CancellationToken cancellationToken = default);

    Task<PageResult<SubgraphMatch>> MatchSubgraph(SubgraphPattern pattern, MatchScope? scope, MatchOptions? options, PageRequest page, CancellationToken cancellationToken = default);

    Task<Document> ForkDocument(string documentId, Guid versionId, string? newDocumentId, CancellationToken cancellationToken = default);

    Task<Document> CreateDocument(string? documentId, string? fileName, CancellationToken cancellationToken = default);

    Task<CommitDocumentResult> CommitDocument(string documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<CommitDocumentResult> MergeDocument(string documentId, Guid versionId, IReadOnlyList<(string DocumentId, Guid VersionId)> otherParents, CancellationToken cancellationToken = default);

    Task<GraphPatchResult> ApplyPatch(GraphPatch patch, CancellationToken cancellationToken = default);

    Task<ImportSnapshotResult> ImportSnapshot(ImportSnapshot snapshot, CancellationToken cancellationToken = default);
}
