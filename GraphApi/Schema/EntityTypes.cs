using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public sealed class DocumentType : ObjectType<Document>
{
    protected override void Configure(IObjectTypeDescriptor<Document> descriptor)
    {
        descriptor.Implements<EntityType>();
        descriptor.Field("id")
            .Description("Opaque refetch key. Store identity is documentId + versionId.")
            .Type<NonNullType<IdType>>()
            .Resolve(ctx => RelayIds.Document(ctx.Parent<Document>()));
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>().Resolve(ctx => ctx.Parent<Document>().VersionId.ToString());
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>().Resolve(ctx => EnumMapping.ToGqlOrigin(ctx.Parent<Document>().Origin));
        descriptor.Field(t => t.Committed).Type<NonNullType<BooleanType>>();
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<Document>().Extensions));
        descriptor.Field("extension")
            .Argument("key", a => a.Type<NonNullType<StringType>>())
            .Type<JsonType>()
            .Resolve(ctx => FilterMapping.Extension(ctx.Parent<Document>().Extensions, ctx.ArgumentValue<string>("key")));
        descriptor.Field("nodes")
            .Argument("filter", a => a.Type<NodeFilterType>())
            .Argument("sort", a => a.Type<NodeSortType>())
            .Argument("first", a => a.Type<IntType>())
            .Argument("after", a => a.Type<StringType>())
            .Type<NonNullType<NodeConnectionType>>()
            .ResolveWith<DocumentResolvers>(r => r.Nodes(default!, default, default, default, default, default!, default!, default));
        descriptor.Field("nestedDocuments").Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .ResolveWith<DocumentResolvers>(r => r.Nested(default!, default!, default));
        descriptor.Field("parentDocuments").Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .ResolveWith<DocumentResolvers>(r => r.Parents(default!, default!, default));
        descriptor.Field("basedOn").Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .ResolveWith<DocumentResolvers>(r => r.BasedOn(default!, default!, default));
        descriptor.Field("derivedDocuments").Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .ResolveWith<DocumentResolvers>(r => r.Derived(default!, default!, default));
        descriptor.Field("historyAncestors")
            .Argument("minHops", a => a.Type<IntType>().DefaultValue(1))
            .Argument("maxHops", a => a.Type<IntType>().DefaultValue(1))
            .Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .Cost(20)
            .ResolveWith<DocumentResolvers>(r => r.HistoryAncestors(default!, 1, 1, default!, default));
        descriptor.Field("historyDescendants")
            .Argument("minHops", a => a.Type<IntType>().DefaultValue(1))
            .Argument("maxHops", a => a.Type<IntType>().DefaultValue(1))
            .Type<NonNullType<ListType<NonNullType<DocumentType>>>>()
            .Cost(20)
            .ResolveWith<DocumentResolvers>(r => r.HistoryDescendants(default!, 1, 1, default!, default));
        descriptor.Field("libraries").Type<NonNullType<ListType<NonNullType<LibraryVersionType>>>>()
            .ResolveWith<DocumentResolvers>(r => r.Libraries(default!, default!, default));
        descriptor.Field("isolatedNodes").Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .ResolveWith<DocumentResolvers>(r => r.Isolated(default!, default!, default));
        descriptor.Field("graph").Type<NonNullType<DocumentGraphType>>().Cost(50)
            .ResolveWith<DocumentResolvers>(r => r.Graph(default!, default!, default));
        descriptor.Field("stats").Type<NonNullType<DocumentStatsType>>().Cost(10)
            .ResolveWith<DocumentResolvers>(r => r.Stats(default!, default!, default));
    }
}

public sealed class DocumentResolvers
{
    public async Task<GqlConnection<Node>> Nodes(
        [Parent] Document document,
        GqlNodeFilter? filter,
        GqlNodeSort? sort,
        int? first,
        string? after,
        IResolverContext context,
        [Service] IGraphStore store,
        CancellationToken ct)
    {
        var merged = FilterMapping.Node(filter) ?? new NodeFilter();
        if (merged.VersionId is Guid versionId && versionId != document.VersionId)
            throw GraphStoreException.InvalidArgument("nodes.filter.versionId must match the parent document versionId.");
        if (!string.IsNullOrEmpty(merged.DocumentId) && merged.DocumentId != document.DocumentId)
            throw GraphStoreException.InvalidArgument("nodes.filter.documentId must match the parent documentId.");
        var scoped = new NodeFilter
        {
            VersionId = document.VersionId,
            DocumentId = document.DocumentId,
            Origin = merged.Origin,
            Kind = merged.Kind,
            TypeId = merged.TypeId,
            Name = merged.Name,
            NickName = merged.NickName,
            Locked = merged.Locked,
            Language = merged.Language,
            HasSource = merged.HasSource,
            Bbox = merged.Bbox,
            Extensions = merged.Extensions,
        };
        return GqlConnection<Node>.From(await store.ListNodes(
            scoped,
            FilterMapping.Page(first, after, sort: FilterMapping.NodeSort(sort), includeTotalCount: Query.SelectedTotal(context)),
            ct));
    }

    public async Task<IReadOnlyList<Document>> Nested([Parent] Document document, NestedDocumentsDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((document.DocumentId, document.VersionId), ct) ?? [];

    public async Task<IReadOnlyList<Document>> Parents([Parent] Document document, ParentDocumentsDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((document.DocumentId, document.VersionId), ct) ?? [];

    public async Task<IReadOnlyList<Document>> BasedOn([Parent] Document document, BasedOnDocumentsDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((document.DocumentId, document.VersionId), ct) ?? [];

    public async Task<IReadOnlyList<Document>> Derived([Parent] Document document, DerivedDocumentsDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((document.DocumentId, document.VersionId), ct) ?? [];

    public Task<IReadOnlyList<Document>> HistoryAncestors([Parent] Document document, int minHops, int maxHops, [Service] IGraphStore store, CancellationToken ct)
        => store.TraverseHistory(document.DocumentId, document.VersionId, HistoryDirection.Ancestors, minHops, maxHops, ct);

    public Task<IReadOnlyList<Document>> HistoryDescendants([Parent] Document document, int minHops, int maxHops, [Service] IGraphStore store, CancellationToken ct)
        => store.TraverseHistory(document.DocumentId, document.VersionId, HistoryDirection.Descendants, minHops, maxHops, ct);

    public async Task<IReadOnlyList<LibraryVersion>> Libraries([Parent] Document document, LibrariesForDocumentDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((document.DocumentId, document.VersionId), ct) ?? [];

    public Task<IReadOnlyList<Node>> Isolated([Parent] Document document, [Service] IGraphStore store, CancellationToken ct)
        => store.GetIsolatedNodes(document.DocumentId, document.VersionId, ct);

    public async Task<GqlDocumentGraph> Graph([Parent] Document document, [Service] IGraphStore store, CancellationToken ct)
    {
        var graph = await store.GetDocumentGraph(document.DocumentId, document.VersionId, ct);
        return new GqlDocumentGraph { Nodes = graph.Nodes, Ports = graph.Ports, Edges = graph.Edges };
    }

    public async Task<GqlDocumentStats> Stats([Parent] Document document, [Service] IGraphStore store, CancellationToken ct)
    {
        var stats = await store.GetDocumentStats(document.DocumentId, document.VersionId, ct);
        return new GqlDocumentStats
        {
            NodeCount = stats.NodeCount,
            PortCount = stats.PortCount,
            EdgeCount = stats.EdgeCount,
            CountsByKind = stats.CountsByKind.Select(b => new GqlCountBucket
            {
                Key = EnumMapping.ToGqlKind(b.Key)?.ToString() ?? b.Key,
                Count = b.Count,
            }).ToList(),
            CountsByTypeId = stats.CountsByTypeId.Select(b => new GqlCountBucket { Key = b.Key, Count = b.Count }).ToList(),
        };
    }
}

public sealed class NodeTypeObject : ObjectType<Node>
{
    protected override void Configure(IObjectTypeDescriptor<Node> descriptor)
    {
        descriptor.Name("Node");
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.Node(ctx.Parent<Node>()));
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>().Resolve(ctx => ctx.Parent<Node>().VersionId.ToString());
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Origin).Type<OriginType>().Resolve(ctx => EnumMapping.ToGqlOrigin(ctx.Parent<Node>().Origin));
        descriptor.Field(t => t.Kind).Type<NodeKindType>().Resolve(ctx => EnumMapping.ToGqlKind(ctx.Parent<Node>().Kind));
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<Node>().Extensions));
        descriptor.Field("extension")
            .Argument("key", a => a.Type<NonNullType<StringType>>())
            .Type<JsonType>()
            .Resolve(ctx => FilterMapping.Extension(ctx.Parent<Node>().Extensions, ctx.ArgumentValue<string>("key")));
        descriptor.Field("document").ResolveWith<NodeResolvers>(r => r.Document(default!, default!, default));
        descriptor.Field("ports")
            .Argument("direction", a => a.Type<PortDirectionType>())
            .Type<NonNullType<ListType<NonNullType<PortType>>>>()
            .ResolveWith<NodeResolvers>(r => r.Ports(default!, default, default!, default));
        descriptor.Field("nodeType").ResolveWith<NodeResolvers>(r => r.NodeType(default!, default!, default));
        descriptor.Field("groups").Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .ResolveWith<NodeResolvers>(r => r.Groups(default!, default!, default));
        descriptor.Field("members").Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .ResolveWith<NodeResolvers>(r => r.Members(default!, default!, default));
        descriptor.Field("clusterDocument").ResolveWith<NodeResolvers>(r => r.Cluster(default!, default!, default));
        descriptor.Field("outgoing").Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .ResolveWith<NodeResolvers>(r => r.Outgoing(default!, default!, default));
        descriptor.Field("incoming").Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .ResolveWith<NodeResolvers>(r => r.Incoming(default!, default!, default));
        descriptor.Field("downstream")
            .Argument("minHops", a => a.Type<IntType>().DefaultValue(1))
            .Argument("maxHops", a => a.Type<IntType>().DefaultValue(1))
            .Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .Cost(20)
            .ResolveWith<NodeResolvers>(r => r.Downstream(default!, 1, 1, default!, default));
        descriptor.Field("upstream")
            .Argument("minHops", a => a.Type<IntType>().DefaultValue(1))
            .Argument("maxHops", a => a.Type<IntType>().DefaultValue(1))
            .Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>()
            .Cost(20)
            .ResolveWith<NodeResolvers>(r => r.Upstream(default!, 1, 1, default!, default));
    }
}

public sealed class NodeResolvers
{
    public Task<Document?> Document([Parent] Node node, DocumentsForNodesDataLoader loader, CancellationToken ct)
        => loader.LoadAsync((node.VersionId, node.NodeId), ct);

    public async Task<IReadOnlyList<Port>> Ports([Parent] Node node, GqlPortDirection? direction, PortsByNodeDataLoader loader, CancellationToken ct)
    {
        var ports = await loader.LoadAsync((node.VersionId, node.NodeId), ct) ?? [];
        if (direction == null)
            return ports;
        var wanted = EnumMapping.ToStore(direction.Value);
        return ports.Where(p => p.Direction == wanted || p.Direction == PortDirections.Both).ToList();
    }

    public Task<NodeType?> NodeType([Parent] Node node, NodeTypesForNodesDataLoader loader, CancellationToken ct)
        => loader.LoadAsync((node.VersionId, node.NodeId), ct);

    public async Task<IReadOnlyList<Node>> Groups([Parent] Node node, GroupsByNodeDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((node.VersionId, node.NodeId), ct) ?? [];

    public async Task<IReadOnlyList<Node>> Members([Parent] Node node, MembersByNodeDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((node.VersionId, node.NodeId), ct) ?? [];

    public Task<Document?> Cluster([Parent] Node node, [Service] IGraphStore store, CancellationToken ct)
        => store.GetClusterDocument(node.VersionId, node.NodeId, ct);

    public Task<IReadOnlyList<Node>> Outgoing([Parent] Node node, [Service] IGraphStore store, CancellationToken ct)
        => store.Traverse(node.VersionId, node.NodeId, DataflowDirection.Outgoing, 1, 1, ct);

    public Task<IReadOnlyList<Node>> Incoming([Parent] Node node, [Service] IGraphStore store, CancellationToken ct)
        => store.Traverse(node.VersionId, node.NodeId, DataflowDirection.Incoming, 1, 1, ct);

    public Task<IReadOnlyList<Node>> Downstream([Parent] Node node, int minHops, int maxHops, [Service] IGraphStore store, CancellationToken ct)
        => store.Traverse(node.VersionId, node.NodeId, DataflowDirection.Outgoing, minHops, maxHops, ct);

    public Task<IReadOnlyList<Node>> Upstream([Parent] Node node, int minHops, int maxHops, [Service] IGraphStore store, CancellationToken ct)
        => store.Traverse(node.VersionId, node.NodeId, DataflowDirection.Incoming, minHops, maxHops, ct);
}

public sealed class PortType : ObjectType<Port>
{
    protected override void Configure(IObjectTypeDescriptor<Port> descriptor)
    {
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.Port(ctx.Parent<Port>()));
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>().Resolve(ctx => ctx.Parent<Port>().VersionId.ToString());
        descriptor.Field(t => t.PortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Direction).Type<PortDirectionType>().Resolve(ctx => EnumMapping.ToGqlDirection(ctx.Parent<Port>().Direction));
        descriptor.Field("nodeId").Type<IdType>().ResolveWith<PortResolvers>(r => r.NodeId(default!, default!, default));
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<Port>().Extensions));
        descriptor.Field("extension")
            .Argument("key", a => a.Type<NonNullType<StringType>>())
            .Type<JsonType>()
            .Resolve(ctx => FilterMapping.Extension(ctx.Parent<Port>().Extensions, ctx.ArgumentValue<string>("key")));
        descriptor.Field("node").ResolveWith<PortResolvers>(r => r.Node(default!, default!, default!, default));
        descriptor.Field("incomingEdges").Type<NonNullType<ListType<NonNullType<EdgeType>>>>()
            .ResolveWith<PortResolvers>(r => r.Incoming(default!, default!, default));
        descriptor.Field("outgoingEdges").Type<NonNullType<ListType<NonNullType<EdgeType>>>>()
            .ResolveWith<PortResolvers>(r => r.Outgoing(default!, default!, default));
    }
}

public sealed class PortResolvers
{
    public Task<string?> NodeId([Parent] Port port, NodeIdByPortDataLoader loader, CancellationToken ct)
        => loader.LoadAsync((port.VersionId, port.PortId), ct);

    public async Task<Node?> Node([Parent] Port port, NodeIdByPortDataLoader nodeIds, NodeByKeyDataLoader nodes, CancellationToken ct)
    {
        var nodeId = await nodeIds.LoadAsync((port.VersionId, port.PortId), ct);
        if (nodeId == null)
            return null;
        return await nodes.LoadAsync((port.VersionId, nodeId), ct);
    }

    public async Task<IReadOnlyList<Edge>> Incoming([Parent] Port port, IncomingEdgesByPortDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((port.VersionId, port.PortId), ct) ?? [];

    public async Task<IReadOnlyList<Edge>> Outgoing([Parent] Port port, OutgoingEdgesByPortDataLoader loader, CancellationToken ct)
        => await loader.LoadAsync((port.VersionId, port.PortId), ct) ?? [];
}

public sealed class EdgeType : ObjectType<Edge>
{
    protected override void Configure(IObjectTypeDescriptor<Edge> descriptor)
    {
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.Edge(ctx.Parent<Edge>()));
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>().Resolve(ctx => ctx.Parent<Edge>().VersionId.ToString());
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<Edge>().Extensions));
        descriptor.Field("extension")
            .Argument("key", a => a.Type<NonNullType<StringType>>())
            .Type<JsonType>()
            .Resolve(ctx => FilterMapping.Extension(ctx.Parent<Edge>().Extensions, ctx.ArgumentValue<string>("key")));
        descriptor.Field("source").ResolveWith<EdgeResolvers>(r => r.Source(default!, default!, default));
        descriptor.Field("target").ResolveWith<EdgeResolvers>(r => r.Target(default!, default!, default));
        descriptor.Field("sourceNode").ResolveWith<EdgeResolvers>(r => r.SourceNode(default!, default!, default!, default));
        descriptor.Field("targetNode").ResolveWith<EdgeResolvers>(r => r.TargetNode(default!, default!, default!, default));
    }
}

public sealed class EdgeResolvers
{
    public Task<Port?> Source([Parent] Edge edge, PortByKeyDataLoader loader, CancellationToken ct)
        => loader.LoadAsync((edge.VersionId, edge.SourcePortId), ct);

    public Task<Port?> Target([Parent] Edge edge, PortByKeyDataLoader loader, CancellationToken ct)
        => loader.LoadAsync((edge.VersionId, edge.TargetPortId), ct);

    public async Task<Node?> SourceNode([Parent] Edge edge, NodeIdByPortDataLoader nodeIds, NodeByKeyDataLoader nodes, CancellationToken ct)
    {
        var nodeId = await nodeIds.LoadAsync((edge.VersionId, edge.SourcePortId), ct);
        return nodeId == null ? null : await nodes.LoadAsync((edge.VersionId, nodeId), ct);
    }

    public async Task<Node?> TargetNode([Parent] Edge edge, NodeIdByPortDataLoader nodeIds, NodeByKeyDataLoader nodes, CancellationToken ct)
    {
        var nodeId = await nodeIds.LoadAsync((edge.VersionId, edge.TargetPortId), ct);
        return nodeId == null ? null : await nodes.LoadAsync((edge.VersionId, nodeId), ct);
    }
}

public sealed class NodeTypeType : ObjectType<NodeType>
{
    protected override void Configure(IObjectTypeDescriptor<NodeType> descriptor)
    {
        descriptor.Name("NodeType");
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.NodeType(ctx.Parent<NodeType>()));
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>().Resolve(ctx => EnumMapping.ToGqlOrigin(ctx.Parent<NodeType>().Origin));
        descriptor.Field(t => t.TypeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<NodeType>().Extensions));
        descriptor.Field("instances")
            .Argument("filter", a => a.Type<NodeFilterType>())
            .Argument("sort", a => a.Type<NodeSortType>())
            .Argument("first", a => a.Type<IntType>())
            .Argument("after", a => a.Type<StringType>())
            .Type<NonNullType<NodeConnectionType>>()
            .ResolveWith<CatalogResolvers>(r => r.Instances(default!, default, default, default, default, default!, default!, default));
        descriptor.Field("definedBy").Type<NonNullType<ListType<NonNullType<LibraryVersionType>>>>()
            .ResolveWith<CatalogResolvers>(r => r.DefinedBy(default!, default!, default));
    }
}

public sealed class LibraryType : ObjectType<Library>
{
    protected override void Configure(IObjectTypeDescriptor<Library> descriptor)
    {
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.Library(ctx.Parent<Library>()));
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>().Resolve(ctx => EnumMapping.ToGqlOrigin(ctx.Parent<Library>().Origin));
        descriptor.Field(t => t.LibraryId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<Library>().Extensions));
        descriptor.Field("versions").Type<NonNullType<ListType<NonNullType<LibraryVersionType>>>>()
            .ResolveWith<CatalogResolvers>(r => r.Versions(default!, default!, default));
    }
}

public sealed class LibraryVersionType : ObjectType<LibraryVersion>
{
    protected override void Configure(IObjectTypeDescriptor<LibraryVersion> descriptor)
    {
        descriptor.Implements<EntityType>();
        descriptor.Field("id").Type<NonNullType<IdType>>().Resolve(ctx => RelayIds.LibraryVersion(ctx.Parent<LibraryVersion>()));
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>().Resolve(ctx => EnumMapping.ToGqlOrigin(ctx.Parent<LibraryVersion>().Origin));
        descriptor.Field(t => t.LibraryId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.AssemblyVersion).Type<NonNullType<StringType>>()
            .Resolve(ctx => string.IsNullOrEmpty(ctx.Parent<LibraryVersion>().AssemblyVersion)
                ? "unknown"
                : ctx.Parent<LibraryVersion>().AssemblyVersion!);
        descriptor.Field(t => t.Extensions).Type<NonNullType<ListType<NonNullType<ExtensionPropertyType>>>>()
            .Resolve(ctx => FilterMapping.Extensions(ctx.Parent<LibraryVersion>().Extensions));
        descriptor.Field("library").ResolveWith<CatalogResolvers>(r => r.Library(default!, default!, default));
        descriptor.Field("documents")
            .Argument("sort", a => a.Type<DocumentSortType>())
            .Argument("first", a => a.Type<IntType>())
            .Argument("after", a => a.Type<StringType>())
            .Type<NonNullType<DocumentConnectionType>>()
            .ResolveWith<CatalogResolvers>(r => r.Documents(default!, default, default, default, default!, default!, default));
        descriptor.Field("nodeTypes").Type<NonNullType<ListType<NonNullType<NodeTypeType>>>>()
            .ResolveWith<CatalogResolvers>(r => r.NodeTypes(default!, default!, default));
    }
}

public sealed class CatalogResolvers
{
    public async Task<GqlConnection<Node>> Instances(
        [Parent] NodeType nodeType,
        GqlNodeFilter? filter,
        GqlNodeSort? sort,
        int? first,
        string? after,
        IResolverContext context,
        [Service] IGraphStore store,
        CancellationToken ct)
    {
        var mapped = FilterMapping.Node(filter) ?? new NodeFilter();
        var scoped = new NodeFilter
        {
            VersionId = mapped.VersionId,
            DocumentId = mapped.DocumentId,
            Origin = nodeType.Origin,
            Kind = mapped.Kind,
            TypeId = nodeType.TypeId,
            Name = mapped.Name,
            NickName = mapped.NickName,
            Locked = mapped.Locked,
            Language = mapped.Language,
            HasSource = mapped.HasSource,
            Bbox = mapped.Bbox,
            Extensions = mapped.Extensions,
        };
        return GqlConnection<Node>.From(await store.ListNodes(
            scoped,
            FilterMapping.Page(first, after, sort: FilterMapping.NodeSort(sort), includeTotalCount: Query.SelectedTotal(context)),
            ct));
    }

    public Task<IReadOnlyList<LibraryVersion>> DefinedBy([Parent] NodeType nodeType, [Service] IGraphStore store, CancellationToken ct)
        => store.GetDefinedBy(nodeType.Origin, nodeType.TypeId, ct);

    public Task<IReadOnlyList<LibraryVersion>> Versions([Parent] Library library, [Service] IGraphStore store, CancellationToken ct)
        => store.GetLibraryVersions(library.Origin, library.LibraryId, ct);

    public Task<Library?> Library([Parent] LibraryVersion version, [Service] IGraphStore store, CancellationToken ct)
        => store.GetLibrary(version.Origin, version.LibraryId, ct);

    public async Task<GqlConnection<Document>> Documents(
        [Parent] LibraryVersion version,
        GqlDocumentSort? sort,
        int? first,
        string? after,
        IResolverContext context,
        [Service] IGraphStore store,
        CancellationToken ct)
        => GqlConnection<Document>.From(await store.ListDocuments(
            new DocumentFilter
            {
                LibraryId = version.LibraryId,
                Origin = version.Origin,
                LibraryVersion = version.Version,
                LibraryAssemblyVersion = version.AssemblyVersion,
            },
            FilterMapping.Page(first, after, sort: FilterMapping.DocumentSort(sort), includeTotalCount: Query.SelectedTotal(context)),
            ct));

    public Task<IReadOnlyList<NodeType>> NodeTypes([Parent] LibraryVersion version, [Service] IGraphStore store, CancellationToken ct)
        => store.GetNodeTypesForLibraryVersion(version.Origin, version.LibraryId, version.Version, version.AssemblyVersion, ct);
}
