using GraphRoots.GraphDb;
using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public sealed class QueryType : ObjectType<Query>
{
    protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
    {
        descriptor.Field(t => t.Entity(default!, default!, default))
            .Argument("id", a => a.Type<NonNullType<IdType>>())
            .Type<EntityType>();
        descriptor.Field(t => t.Document(default!, default!, default!, default))
            .Argument("documentId", a => a.Type<NonNullType<IdType>>())
            .Argument("versionId", a => a.Type<NonNullType<IdType>>())
            .Type<DocumentType>();
        descriptor.Field(t => t.Documents(default, default, default, default, default!, default!, default))
            .Type<NonNullType<DocumentConnectionType>>();
        descriptor.Field(t => t.Node(default!, default!, default!, default))
            .Argument("versionId", a => a.Type<NonNullType<IdType>>())
            .Argument("nodeId", a => a.Type<NonNullType<IdType>>())
            .Type<NodeTypeObject>();
        descriptor.Field(t => t.Nodes(default, default, default, default, default!, default!, default))
            .Type<NonNullType<NodeConnectionType>>();
        descriptor.Field(t => t.Port(default!, default!, default!, default))
            .Argument("versionId", a => a.Type<NonNullType<IdType>>())
            .Argument("portId", a => a.Type<NonNullType<IdType>>())
            .Type<PortType>();
        descriptor.Field(t => t.Ports(default, default, default, default, default!, default!, default))
            .Type<NonNullType<PortConnectionType>>();
        descriptor.Field(t => t.Edge(default!, default!, default!, default!, default))
            .Argument("versionId", a => a.Type<NonNullType<IdType>>())
            .Argument("sourcePortId", a => a.Type<NonNullType<IdType>>())
            .Argument("targetPortId", a => a.Type<NonNullType<IdType>>())
            .Type<EdgeType>();
        descriptor.Field(t => t.Edges(default, default, default, default, default!, default!, default))
            .Type<NonNullType<EdgeConnectionType>>();
        descriptor.Field(t => t.NodeType(default, default!, default!, default))
            .Argument("typeId", a => a.Type<NonNullType<IdType>>())
            .Type<NodeTypeType>();
        descriptor.Field(t => t.NodeTypes(default, default, default, default, default!, default!, default))
            .Type<NonNullType<NodeTypeConnectionType>>();
        descriptor.Field(t => t.Library(default, default!, default!, default))
            .Argument("libraryId", a => a.Type<NonNullType<IdType>>())
            .Type<LibraryType>();
        descriptor.Field(t => t.Libraries(default, default, default, default, default!, default!, default))
            .Type<NonNullType<LibraryConnectionType>>();
        descriptor.Field(t => t.LibraryVersion(default, default!, default!, default!, default!, default))
            .Argument("libraryId", a => a.Type<NonNullType<IdType>>())
            .Argument("assemblyVersion", a => a.Type<NonNullType<StringType>>())
            .Type<LibraryVersionType>();
        descriptor.Field(t => t.Path(default!, default!, default!, default!, default, default!, default))
            .Argument("documentId", a => a.Type<NonNullType<IdType>>())
            .Argument("versionId", a => a.Type<NonNullType<IdType>>())
            .Argument("fromNodeId", a => a.Type<NonNullType<IdType>>())
            .Argument("toNodeId", a => a.Type<NonNullType<IdType>>())
            .Argument("maxHops", a => a.Type<IntType>().DefaultValue(8))
            .Type<DataflowPathType>()
            .Cost(20);
        descriptor.Field(t => t.MatchSubgraph(default!, default, default, default, default, default!, default!, default))
            .Type<NonNullType<SubgraphMatchConnectionType>>()
            .Cost(100);
    }
}

public sealed class MutationType : ObjectType<Mutation>
{
    protected override void Configure(IObjectTypeDescriptor<Mutation> descriptor)
    {
        descriptor.Field(t => t.ImportSnapshot(default!, default!, default)).Cost(0);
    }
}

public sealed class EntityType : InterfaceType
{
    protected override void Configure(IInterfaceTypeDescriptor descriptor)
    {
        descriptor.Name("Entity");
        descriptor.Description("Opaque refetch key shared by stored entities.");
        descriptor.Field("id")
            .Description("Opaque refetch key. Store identity is the composite equality key.")
            .Type<NonNullType<IdType>>();
    }
}
