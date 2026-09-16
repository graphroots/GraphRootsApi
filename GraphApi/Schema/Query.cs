using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public class Query
{
    public async Task<object?> Entity(string id, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var (type, parts) = RelayIds.Decode(id);
        return type switch
        {
            "Document" when parts.Length == 2 => await store.GetDocument(parts[0], FilterMapping.ParseGuid(parts[1], "versionId"), cancellationToken),
            "Node" when parts.Length == 2 => await store.GetNode(FilterMapping.ParseGuid(parts[0], "versionId"), parts[1], cancellationToken),
            "Port" when parts.Length == 2 => await store.GetPort(FilterMapping.ParseGuid(parts[0], "versionId"), parts[1], cancellationToken),
            "Edge" when parts.Length == 3 => await store.GetEdge(FilterMapping.ParseGuid(parts[0], "versionId"), parts[1], parts[2], cancellationToken),
            "NodeType" when parts.Length == 2 => await store.GetNodeType(parts[0], parts[1], cancellationToken),
            "Library" when parts.Length == 2 => await store.GetLibrary(parts[0], parts[1], cancellationToken),
            "LibraryVersion" when parts.Length == 4 => await store.GetLibraryVersion(parts[0], parts[1], parts[2], string.IsNullOrEmpty(parts[3]) ? "unknown" : parts[3], cancellationToken),
            _ => throw GraphStoreException.InvalidArgument($"Unknown entity id type '{type}'."),
        };
    }

    public Task<Document?> Document(string documentId, string versionId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetDocument(documentId, FilterMapping.ParseGuid(versionId, "versionId"), cancellationToken);

    public async Task<GqlConnection<Document>> Documents(GqlDocumentFilter? filter, GqlDocumentSort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<Document>.From(await store.ListDocuments(FilterMapping.Document(filter), FilterMapping.Page(first, after, sort: FilterMapping.DocumentSort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<Node?> Node(string versionId, string nodeId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetNode(FilterMapping.ParseGuid(versionId, "versionId"), nodeId, cancellationToken);

    public async Task<GqlConnection<Node>> Nodes(GqlNodeFilter? filter, GqlNodeSort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<Node>.From(await store.ListNodes(FilterMapping.Node(filter), FilterMapping.Page(first, after, sort: FilterMapping.NodeSort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<Port?> Port(string versionId, string portId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetPort(FilterMapping.ParseGuid(versionId, "versionId"), portId, cancellationToken);

    public async Task<GqlConnection<Port>> Ports(GqlPortFilter? filter, GqlPortSort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<Port>.From(await store.ListPorts(FilterMapping.Port(filter), FilterMapping.Page(first, after, sort: FilterMapping.PortSort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<Edge?> Edge(string versionId, string sourcePortId, string targetPortId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetEdge(FilterMapping.ParseGuid(versionId, "versionId"), sourcePortId, targetPortId, cancellationToken);

    public async Task<GqlConnection<Edge>> Edges(GqlEdgeFilter? filter, GqlEdgeSort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<Edge>.From(await store.ListEdges(FilterMapping.Edge(filter), FilterMapping.Page(first, after, sort: FilterMapping.EdgeSort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<NodeType?> NodeType(GqlOrigin origin, string typeId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetNodeType(EnumMapping.ToStoreFilter(origin), typeId, cancellationToken);

    public async Task<GqlConnection<NodeType>> NodeTypes(GqlNodeTypeFilter? filter, GqlNodeTypeSort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<NodeType>.From(await store.ListNodeTypes(FilterMapping.NodeType(filter), FilterMapping.Page(first, after, sort: FilterMapping.NodeTypeSort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<Library?> Library(GqlOrigin origin, string libraryId, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetLibrary(EnumMapping.ToStoreFilter(origin), libraryId, cancellationToken);

    public async Task<GqlConnection<Library>> Libraries(GqlLibraryFilter? filter, GqlLibrarySort? sort, int? first, string? after, IResolverContext context, [Service] IGraphStore store, CancellationToken cancellationToken)
        => GqlConnection<Library>.From(await store.ListLibraries(FilterMapping.Library(filter), FilterMapping.Page(first, after, sort: FilterMapping.LibrarySort(sort), includeTotalCount: SelectedTotal(context)), cancellationToken));

    public Task<LibraryVersion?> LibraryVersion(GqlOrigin origin, string libraryId, string version, string assemblyVersion, [Service] IGraphStore store, CancellationToken cancellationToken)
        => store.GetLibraryVersion(EnumMapping.ToStoreFilter(origin), libraryId, version, assemblyVersion, cancellationToken);

    public async Task<GqlDataflowPath?> Path(string documentId, string versionId, string fromNodeId, string toNodeId, [DefaultValue(8)] int maxHops, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var path = await store.FindPath(documentId, FilterMapping.ParseGuid(versionId, "versionId"), fromNodeId, toNodeId, maxHops, cancellationToken);
        return path == null ? null : new GqlDataflowPath { Nodes = path.Nodes, Ports = path.Ports, Edges = path.Edges };
    }

    public async Task<GqlConnection<GqlSubgraphMatch>> MatchSubgraph(
        GqlSubgraphPattern pattern,
        GqlMatchScope? scope,
        GqlMatchOptions? options,
        int? first,
        string? after,
        IResolverContext context,
        [Service] IGraphStore store,
        CancellationToken cancellationToken)
    {
        var page = await store.MatchSubgraph(
            FilterMapping.Pattern(pattern),
            FilterMapping.Scope(scope),
            FilterMapping.Options(options),
            FilterMapping.Page(first, after, SubgraphMatcher.MaxMatchResults, includeTotalCount: SelectedTotal(context)),
            cancellationToken);
        var mapped = new PageResult<GqlSubgraphMatch>
        {
            Items = page.Items.Select(FilterMapping.Match).ToList(),
            Cursors = page.Cursors,
            TotalCount = page.TotalCount,
            StartCursor = page.StartCursor,
            EndCursor = page.EndCursor,
            HasNextPage = page.HasNextPage,
            HasPreviousPage = page.HasPreviousPage,
        };
        return GqlConnection<GqlSubgraphMatch>.From(mapped);
    }

    public static bool SelectedTotal(IResolverContext context) => context.IsSelected("totalCount");
}
