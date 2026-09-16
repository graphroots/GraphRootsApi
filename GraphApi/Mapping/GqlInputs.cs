using System;
using System.Collections.Generic;
using GraphRoots.GraphDb;
using HotChocolate;
using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public sealed class GqlExtensionProperty
{
    public string Key { get; init; } = "";

    public object? Value { get; init; }
}

public sealed class GqlCountBucket
{
    public string Key { get; init; } = "";

    public int Count { get; init; }
}

public sealed class GqlPageInfo
{
    public bool HasNextPage { get; init; }

    public bool HasPreviousPage { get; init; }

    public string? StartCursor { get; init; }

    public string? EndCursor { get; init; }
}

public sealed class GqlEdge<T>
{
    public string Cursor { get; init; } = "";

    public T Node { get; init; } = default!;
}

public sealed class GqlConnection<T>
{
    public IReadOnlyList<GqlEdge<T>> Edges { get; init; } = [];

    public IReadOnlyList<T> Nodes { get; init; } = [];

    public GqlPageInfo PageInfo { get; init; } = new();

    public int TotalCount { get; init; }

    public static GqlConnection<T> From(PageResult<T> page)
    {
        var edges = new List<GqlEdge<T>>();
        for (var i = 0; i < page.Items.Count; i++)
        {
            var cursor = i < page.Cursors.Count ? page.Cursors[i] : page.EndCursor;
            edges.Add(new GqlEdge<T> { Cursor = cursor ?? "", Node = page.Items[i] });
        }
        return new GqlConnection<T>
        {
            Nodes = page.Items,
            Edges = edges,
            TotalCount = page.TotalCount,
            PageInfo = new GqlPageInfo
            {
                HasNextPage = page.HasNextPage,
                HasPreviousPage = page.HasPreviousPage,
                StartCursor = page.StartCursor,
                EndCursor = page.EndCursor,
            },
        };
    }
}

public sealed class GqlExtensionPredicate
{
    public string Key { get; init; } = "";

    public object? EqualsValue { get; init; }
}

public sealed class GqlBoundingBox
{
    public float MinX { get; init; }
    public float MinY { get; init; }
    public float MaxX { get; init; }
    public float MaxY { get; init; }
}

public sealed class GqlDocumentFilter
{
    public GqlOrigin? Origin { get; init; }
    public bool? IsNested { get; init; }
    public bool? Committed { get; init; }
    public string? FileNameContains { get; init; }
    public string? DocumentId { get; init; }
    public string? VersionId { get; init; }
    public string? LibraryId { get; init; }
    public DateTime? CreatedAfterUtc { get; init; }
    public DateTime? CreatedBeforeUtc { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlDocumentSort
{
    [DefaultValue(GqlDocumentSortField.DOCUMENT_ID)]
    public GqlDocumentSortField Field { get; init; } = GqlDocumentSortField.DOCUMENT_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlNodeFilter
{
    public string? VersionId { get; init; }
    public string? DocumentId { get; init; }
    public GqlOrigin? Origin { get; init; }
    public GqlNodeKind? Kind { get; init; }
    public string? TypeId { get; init; }
    public string? Name { get; init; }
    public string? NickName { get; init; }
    public bool? Locked { get; init; }
    public string? Language { get; init; }
    public bool? HasSource { get; init; }
    public GqlBoundingBox? Bbox { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlNodeSort
{
    [DefaultValue(GqlNodeSortField.NODE_ID)]
    public GqlNodeSortField Field { get; init; } = GqlNodeSortField.NODE_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlPortFilter
{
    public string? VersionId { get; init; }
    public string? NodeId { get; init; }
    public string? Name { get; init; }
    public GqlPortDirection? Direction { get; init; }
    public string? Access { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlPortSort
{
    [DefaultValue(GqlPortSortField.PORT_ID)]
    public GqlPortSortField Field { get; init; } = GqlPortSortField.PORT_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlEdgeFilter
{
    public string? VersionId { get; init; }
    public string? SourcePortId { get; init; }
    public string? TargetPortId { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlEdgeSort
{
    [DefaultValue(GqlEdgeSortField.VERSION_ID)]
    public GqlEdgeSortField Field { get; init; } = GqlEdgeSortField.VERSION_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlNodeTypeFilter
{
    public GqlOrigin? Origin { get; init; }
    public string? TypeId { get; init; }
    public string? NameContains { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlNodeTypeSort
{
    [DefaultValue(GqlNodeTypeSortField.TYPE_ID)]
    public GqlNodeTypeSortField Field { get; init; } = GqlNodeTypeSortField.TYPE_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlLibraryFilter
{
    public GqlOrigin? Origin { get; init; }
    public string? LibraryId { get; init; }
    public string? NameContains { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlLibrarySort
{
    [DefaultValue(GqlLibrarySortField.LIBRARY_ID)]
    public GqlLibrarySortField Field { get; init; } = GqlLibrarySortField.LIBRARY_ID;
    [DefaultValue(GqlSortDirection.ASC)]
    public GqlSortDirection Direction { get; init; } = GqlSortDirection.ASC;
}

public sealed class GqlPatternNode
{
    public string Key { get; init; } = "";
    public string? TypeId { get; init; }
    public GqlNodeKind? Kind { get; init; }
    public string? Name { get; init; }
    public string? NickName { get; init; }
    public bool? Locked { get; init; }
    public string? Language { get; init; }
    public GqlOrigin? Origin { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlPatternPort
{
    public string Key { get; init; } = "";
    public string Node { get; init; } = "";
    public string? Name { get; init; }
    public GqlPortDirection? Direction { get; init; }
    public string? Access { get; init; }
}

public sealed class GqlPatternEdge
{
    public string? Key { get; init; }
    public string SourcePort { get; init; } = "";
    public string TargetPort { get; init; } = "";
}

public sealed class GqlPatternConnection
{
    public string? Key { get; init; }
    public string SourceNode { get; init; } = "";
    public string TargetNode { get; init; } = "";
    public string? SourcePortName { get; init; }
    public string? TargetPortName { get; init; }
    public GqlPortDirection? SourceDirection { get; init; }
    public GqlPortDirection? TargetDirection { get; init; }
    [DefaultValue(1)]
    public int MinHops { get; init; } = 1;
    [DefaultValue(1)]
    public int MaxHops { get; init; } = 1;
}

public sealed class GqlSubgraphPattern
{
    public IReadOnlyList<GqlPatternNode> Nodes { get; init; } = [];
    public IReadOnlyList<GqlPatternPort>? Ports { get; init; }
    public IReadOnlyList<GqlPatternEdge>? Edges { get; init; }
    public IReadOnlyList<GqlPatternConnection>? Connections { get; init; }
}

public sealed class GqlMatchScope
{
    public string? VersionId { get; init; }
    public string? DocumentId { get; init; }
    public GqlOrigin? Origin { get; init; }
    [DefaultValue(false)]
    public bool IncludeNested { get; init; }
}

public sealed class GqlMatchOptions
{
    [DefaultValue(true)]
    public bool Injective { get; init; } = true;
    [DefaultValue(false)]
    public bool Induced { get; init; }
}

public sealed class GqlForkDocumentInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string? NewDocumentId { get; init; }
}

public sealed class GqlCreateDocumentInput
{
    public string? DocumentId { get; init; }
    public string? FileName { get; init; }
}

public sealed class GqlCommitDocumentInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
}

public sealed class GqlDocumentKeyInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
}

public sealed class GqlMergeDocumentInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public IReadOnlyList<GqlDocumentKeyInput>? OtherParents { get; init; }
}

public sealed class GqlCreateNodeInput
{
    public string NodeId { get; init; } = "";
    [DefaultValue(GqlOrigin.GRAPHROOTS)]
    public GqlOrigin Origin { get; init; } = GqlOrigin.GRAPHROOTS;
    public string? TypeId { get; init; }
    public string? Name { get; init; }
    public string? NickName { get; init; }
    public GqlNodeKind? Kind { get; init; }
    public bool? Locked { get; init; }
    public float? X { get; init; }
    public float? Y { get; init; }
    public string? Source { get; init; }
    public string? Language { get; init; }
    public string? Text { get; init; }
}

public sealed class GqlUpdateNodeInput
{
    public string NodeId { get; init; } = "";
    public Optional<string?> TypeId { get; set; }
    public Optional<string?> Name { get; set; }
    public Optional<string?> NickName { get; set; }
    public Optional<GqlNodeKind?> Kind { get; set; }
    public Optional<bool?> Locked { get; set; }
    public Optional<float?> X { get; set; }
    public Optional<float?> Y { get; set; }
    public Optional<string?> Source { get; set; }
    public Optional<string?> Language { get; set; }
    public Optional<string?> Text { get; set; }
}

public sealed class GqlCreatePortInput
{
    public string PortId { get; init; } = "";
    public string NodeId { get; init; } = "";
    public string? Name { get; init; }
    public GqlPortDirection? Direction { get; init; }
    public string? Access { get; init; }
}

public sealed class GqlUpdatePortInput
{
    public string PortId { get; init; } = "";
    public Optional<string?> Name { get; set; }
    public Optional<GqlPortDirection?> Direction { get; set; }
    public Optional<string?> Access { get; set; }
}

public sealed class GqlCreateEdgeInput
{
    public string SourcePortId { get; init; } = "";
    public string TargetPortId { get; init; } = "";
    public string? SourceName { get; init; }
    public string? TargetName { get; init; }
}

public sealed class GqlDeleteEdgeInput
{
    public string SourcePortId { get; init; } = "";
    public string TargetPortId { get; init; } = "";
}

public sealed class GqlGroupMembershipInput
{
    public string MemberNodeId { get; init; } = "";
    public string GroupNodeId { get; init; } = "";
}

public sealed class GqlSetExtensionsInput
{
    public GqlEntityKind Entity { get; init; }
    public string Id { get; init; } = "";
    public string? SourcePortId { get; init; }
    public string? TargetPortId { get; init; }
    public IReadOnlyList<GqlExtensionPredicate> Entries { get; init; } = [];
}

public sealed class GqlGraphPatchInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public IReadOnlyList<GqlCreateNodeInput>? CreateNodes { get; init; }
    public IReadOnlyList<GqlUpdateNodeInput>? UpdateNodes { get; init; }
    public IReadOnlyList<string>? DeleteNodes { get; init; }
    public IReadOnlyList<GqlCreatePortInput>? CreatePorts { get; init; }
    public IReadOnlyList<GqlUpdatePortInput>? UpdatePorts { get; init; }
    public IReadOnlyList<string>? DeletePorts { get; init; }
    public IReadOnlyList<GqlCreateEdgeInput>? CreateEdges { get; init; }
    public IReadOnlyList<GqlDeleteEdgeInput>? DeleteEdges { get; init; }
    public IReadOnlyList<GqlGroupMembershipInput>? AddToGroups { get; init; }
    public IReadOnlyList<GqlGroupMembershipInput>? RemoveFromGroups { get; init; }
    public IReadOnlyList<GqlSetExtensionsInput>? SetExtensions { get; init; }
}

public sealed class GqlWirePortsInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string SourcePortId { get; init; } = "";
    public string TargetPortId { get; init; } = "";
    public string? SourceName { get; init; }
    public string? TargetName { get; init; }
}

public sealed class GqlUnwirePortsInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string SourcePortId { get; init; } = "";
    public string TargetPortId { get; init; } = "";
}

public sealed class GqlMoveNodeInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string NodeId { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
}

public sealed class GqlSetExtensionsMutationInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public GqlEntityKind Entity { get; init; }
    public string Id { get; init; } = "";
    public string? SourcePortId { get; init; }
    public string? TargetPortId { get; init; }
    public IReadOnlyList<GqlExtensionPredicate> Entries { get; init; } = [];
}

public sealed class GqlForkDocumentPayload
{
    public Document Document { get; init; } = new();
}

public sealed class GqlDocumentPayload
{
    public Document Document { get; init; } = new();
}

public sealed class GqlCommitDocumentPayload
{
    public Document Document { get; init; } = new();
    public Document WorkingDocument { get; init; } = new();
}

public sealed class GqlGraphPatchPayload
{
    public Document Document { get; init; } = new();
    public IReadOnlyList<string> CreatedNodeIds { get; init; } = [];
    public IReadOnlyList<string> DeletedNodeIds { get; init; } = [];
    public IReadOnlyList<string> CreatedPortIds { get; init; } = [];
    public IReadOnlyList<string> DeletedPortIds { get; init; } = [];
}

public sealed class GqlDocumentGraph
{
    public IReadOnlyList<Node> Nodes { get; init; } = [];
    public IReadOnlyList<Port> Ports { get; init; } = [];
    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

public sealed class GqlDocumentStats
{
    public int NodeCount { get; init; }
    public int PortCount { get; init; }
    public int EdgeCount { get; init; }
    public IReadOnlyList<GqlCountBucket> CountsByKind { get; init; } = [];
    public IReadOnlyList<GqlCountBucket> CountsByTypeId { get; init; } = [];
}

public sealed class GqlNodeBinding
{
    public string Key { get; init; } = "";
    public Node Node { get; init; } = new();
}

public sealed class GqlPortBinding
{
    public string Key { get; init; } = "";
    public Port Port { get; init; } = new();
}

public sealed class GqlEdgeBinding
{
    public string Key { get; init; } = "";
    public Edge Edge { get; init; } = new();
}

public sealed class GqlConnectionRealization
{
    public string? Key { get; init; }
    public IReadOnlyList<Port> Ports { get; init; } = [];
    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

public sealed class GqlSubgraphMatch
{
    public Document? Document { get; init; }
    public IReadOnlyList<GqlNodeBinding> Nodes { get; init; } = [];
    public IReadOnlyList<GqlPortBinding> Ports { get; init; } = [];
    public IReadOnlyList<GqlEdgeBinding> Edges { get; init; } = [];
    public IReadOnlyList<GqlConnectionRealization> Connections { get; init; } = [];
}

public sealed class GqlDataflowPath
{
    public IReadOnlyList<Node> Nodes { get; init; } = [];
    public IReadOnlyList<Port> Ports { get; init; } = [];
    public IReadOnlyList<Edge> Edges { get; init; } = [];
}

public sealed class GqlImportSnapshotInput
{
    public IReadOnlyList<GqlImportDocumentInput> Documents { get; init; } = [];
    public IReadOnlyList<GqlImportNodeInput>? Nodes { get; init; }
    public IReadOnlyList<GqlImportPortInput>? Ports { get; init; }
    public IReadOnlyList<GqlImportEdgeInput>? Edges { get; init; }
    public IReadOnlyList<GqlImportGroupMembershipInput>? GroupMemberships { get; init; }
    public IReadOnlyList<GqlImportNestInput>? Nests { get; init; }
    public IReadOnlyList<GqlImportLibraryInput>? Libraries { get; init; }
    public IReadOnlyList<GqlImportLibraryVersionInput>? LibraryVersions { get; init; }
    public IReadOnlyList<GqlImportNodeTypeInput>? NodeTypes { get; init; }
    public IReadOnlyList<GqlImportUsedByInput>? UsedBy { get; init; }
}

public sealed class GqlImportDocumentInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public GqlOrigin Origin { get; init; }
    public string? FileName { get; init; }
    public string? FilePath { get; init; }
    public bool? IsNested { get; init; }
    public DateTime? FileCreationTimeUtc { get; init; }
    public DateTime? FileLastWriteTimeUtc { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportNodeInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string NodeId { get; init; } = "";
    public GqlOrigin Origin { get; init; }
    public string? TypeId { get; init; }
    public string? Name { get; init; }
    public string? NickName { get; init; }
    public GqlNodeKind? Kind { get; init; }
    public bool? Locked { get; init; }
    public float? X { get; init; }
    public float? Y { get; init; }
    public string? Source { get; init; }
    public string? Language { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportPortInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string NodeId { get; init; } = "";
    public string PortId { get; init; } = "";
    public string? Name { get; init; }
    public GqlPortDirection? Direction { get; init; }
    public string? Access { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportEdgeInput
{
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
    public string SourcePortId { get; init; } = "";
    public string TargetPortId { get; init; } = "";
    public string? SourceName { get; init; }
    public string? TargetName { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportGroupMembershipInput
{
    public string VersionId { get; init; } = "";
    public string MemberNodeId { get; init; } = "";
    public string GroupNodeId { get; init; } = "";
}

public sealed class GqlImportNestInput
{
    public string ParentDocumentId { get; init; } = "";
    public string ParentVersionId { get; init; } = "";
    public string ChildDocumentId { get; init; } = "";
    public string ChildVersionId { get; init; } = "";
}

public sealed class GqlImportLibraryInput
{
    public GqlOrigin Origin { get; init; }
    public string LibraryId { get; init; } = "";
    public string? Name { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportLibraryVersionInput
{
    public GqlOrigin Origin { get; init; }
    public string LibraryId { get; init; } = "";
    public string Version { get; init; } = "";
    public string? AssemblyVersion { get; init; }
    public string? Name { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportNodeTypeInput
{
    public GqlOrigin Origin { get; init; }
    public string TypeId { get; init; } = "";
    public string? Name { get; init; }
    public string? LibraryId { get; init; }
    public string? Version { get; init; }
    public string? AssemblyVersion { get; init; }
    public IReadOnlyList<GqlExtensionPredicate>? Extensions { get; init; }
}

public sealed class GqlImportUsedByInput
{
    public GqlOrigin Origin { get; init; }
    public string LibraryId { get; init; } = "";
    public string Version { get; init; } = "";
    public string? AssemblyVersion { get; init; }
    public string DocumentId { get; init; } = "";
    public string VersionId { get; init; } = "";
}

public sealed class GqlImportSnapshotPayload
{
    public Document Document { get; init; } = new();
    public int NestedDocumentCount { get; init; }
    public int NodeCount { get; init; }
    public int PortCount { get; init; }
    public int EdgeCount { get; init; }
}
