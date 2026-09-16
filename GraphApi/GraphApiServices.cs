using System;
using GraphRoots.GraphDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace GraphRoots.GraphApi;

public static class GraphApiServices
{
    public static IServiceCollection AddGraphRootsGraphQL(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .ModifyRequestOptions(o =>
            {
                o.ExecutionTimeout = TimeSpan.FromSeconds(120);
                o.IncludeExceptionDetails = string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "Development",
                    StringComparison.OrdinalIgnoreCase);
            })
            .ModifyCostOptions(o =>
            {
                o.MaxFieldCost = 1000;
                o.MaxTypeCost = 100_000;
            })
            .AddMaxExecutionDepthRule(16)
            .AddErrorFilter<GraphStoreErrorFilter>()
            .BindRuntimeType<DateTime, IsoDateTimeType>()
            .AddQueryType<QueryType>()
            .AddMutationType<MutationType>()
            .AddType<EntityType>()
            .AddType<JsonType>()
            .AddType<IsoDateTimeType>()
            .AddType<DocumentType>()
            .AddType<NodeTypeObject>()
            .AddType<PortType>()
            .AddType<EdgeType>()
            .AddType<NodeTypeType>()
            .AddType<LibraryType>()
            .AddType<LibraryVersionType>()
            .AddType<DocumentConnectionType>()
            .AddType<DocumentEdgeType>()
            .AddType<NodeConnectionType>()
            .AddType<NodeEdgeType>()
            .AddType<PortConnectionType>()
            .AddType<PortEdgeType>()
            .AddType<EdgeConnectionType>()
            .AddType<EdgeEdgeType>()
            .AddType<NodeTypeConnectionType>()
            .AddType<NodeTypeEdgeType>()
            .AddType<LibraryConnectionType>()
            .AddType<LibraryEdgeType>()
            .AddType<SubgraphMatchConnectionType>()
            .AddType<SubgraphMatchEdgeType>()
            .AddType<PageInfoType>()
            .AddType<ExtensionPropertyType>()
            .AddType<ForkDocumentPayloadType>()
            .AddType<DocumentPayloadType>()
            .AddType<CommitDocumentPayloadType>()
            .AddType<GraphPatchPayloadType>()
            .AddType<DocumentGraphType>()
            .AddType<DocumentStatsType>()
            .AddType<CountBucketType>()
            .AddType<NodeBindingType>()
            .AddType<PortBindingType>()
            .AddType<EdgeBindingType>()
            .AddType<ConnectionRealizationType>()
            .AddType<SubgraphMatchType>()
            .AddType<DataflowPathType>()
            .AddType<DocumentFilterType>()
            .AddType<NodeFilterType>()
            .AddType<PortFilterType>()
            .AddType<EdgeFilterType>()
            .AddType<NodeTypeFilterType>()
            .AddType<LibraryFilterType>()
            .AddType<DocumentSortType>()
            .AddType<NodeSortType>()
            .AddType<PortSortType>()
            .AddType<EdgeSortType>()
            .AddType<NodeTypeSortType>()
            .AddType<LibrarySortType>()
            .AddType<BoundingBoxInputType>()
            .AddType<ExtensionPredicateInputType>()
            .AddType<SubgraphPatternInputType>()
            .AddType<PatternNodeInputType>()
            .AddType<PatternPortInputType>()
            .AddType<PatternEdgeInputType>()
            .AddType<PatternConnectionInputType>()
            .AddType<MatchScopeInputType>()
            .AddType<MatchOptionsInputType>()
            .AddType<ForkDocumentInputType>()
            .AddType<CreateDocumentInputType>()
            .AddType<CommitDocumentInputType>()
            .AddType<DocumentKeyInputType>()
            .AddType<MergeDocumentInputType>()
            .AddType<GraphPatchInputType>()
            .AddType<CreateNodeInputType>()
            .AddType<UpdateNodeInputType>()
            .AddType<CreatePortInputType>()
            .AddType<UpdatePortInputType>()
            .AddType<CreateEdgeInputType>()
            .AddType<DeleteEdgeInputType>()
            .AddType<GroupMembershipInputType>()
            .AddType<SetExtensionsInputType>()
            .AddType<WirePortsInputType>()
            .AddType<UnwirePortsInputType>()
            .AddType<MoveNodeInputType>()
            .AddType<SetExtensionsMutationInputType>()
            .AddType<ImportSnapshotInputType>()
            .AddType<ImportDocumentInputType>()
            .AddType<ImportNodeInputType>()
            .AddType<ImportPortInputType>()
            .AddType<ImportEdgeInputType>()
            .AddType<ImportGroupMembershipInputType>()
            .AddType<ImportNestInputType>()
            .AddType<ImportLibraryInputType>()
            .AddType<ImportLibraryVersionInputType>()
            .AddType<ImportNodeTypeInputType>()
            .AddType<ImportUsedByInputType>()
            .AddType<ImportSnapshotPayloadType>()
            .AddType<OriginType>()
            .AddType<NodeKindType>()
            .AddType<PortDirectionType>()
            .AddType<EntityKindType>()
            .AddType<SortDirectionType>()
            .AddType<DocumentSortFieldType>()
            .AddType<NodeSortFieldType>()
            .AddType<PortSortFieldType>()
            .AddType<EdgeSortFieldType>()
            .AddType<NodeTypeSortFieldType>()
            .AddType<LibrarySortFieldType>()
            .AddDataLoader<DocumentByKeyDataLoader>()
            .AddDataLoader<NodeByKeyDataLoader>()
            .AddDataLoader<PortByKeyDataLoader>()
            .AddDataLoader<PortsByNodeDataLoader>()
            .AddDataLoader<NodeIdByPortDataLoader>()
            .AddDataLoader<OutgoingEdgesByPortDataLoader>()
            .AddDataLoader<IncomingEdgesByPortDataLoader>()
            .AddDataLoader<DocumentsForNodesDataLoader>()
            .AddDataLoader<NodeTypesForNodesDataLoader>()
            .AddDataLoader<GroupsByNodeDataLoader>()
            .AddDataLoader<MembersByNodeDataLoader>()
            .AddDataLoader<NestedDocumentsDataLoader>()
            .AddDataLoader<ParentDocumentsDataLoader>()
            .AddDataLoader<BasedOnDocumentsDataLoader>()
            .AddDataLoader<DerivedDocumentsDataLoader>()
            .AddDataLoader<LibrariesForDocumentDataLoader>();
        return services;
    }

    public static IServiceCollection AddGraphRootsStore(this IServiceCollection services, IConfiguration configuration)
    {
        var uri = configuration["NEO4J_URI"] ?? Environment.GetEnvironmentVariable("NEO4J_URI") ?? "neo4j://127.0.0.1";
        var user = configuration["NEO4J_USER"] ?? Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
        var password = configuration["NEO4J_PASSWORD"] ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD");
        var database = configuration["NEO4J_DATABASE"] ?? Environment.GetEnvironmentVariable("NEO4J_DATABASE") ?? "GraphRoots";
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("NEO4J_PASSWORD is required.");

        services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(uri, AuthTokens.Basic(user, password)));
        services.AddSingleton<IDbOperations>(sp => new DbOperations(sp.GetRequiredService<IDriver>(), database));
        services.AddSingleton<IGraphStore>(sp => new GraphStore(sp.GetRequiredService<IDbOperations>()));
        services.AddHostedService<GraphStoreStartup>();
        return services;
    }
}
