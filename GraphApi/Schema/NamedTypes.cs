using GraphRoots.GraphDb;
using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public sealed class DocumentConnectionType : ObjectType<GqlConnection<Document>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<Document>> descriptor)
    {
        descriptor.Name("DocumentConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<DocumentEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<DocumentType>>>>();
    }
}

public sealed class DocumentEdgeType : ObjectType<GqlEdge<Document>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<Document>> descriptor) => descriptor.Name("DocumentEdge");
}

public sealed class NodeConnectionType : ObjectType<GqlConnection<Node>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<Node>> descriptor)
    {
        descriptor.Name("NodeConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<NodeEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<NodeTypeObject>>>>();
    }
}

public sealed class NodeEdgeType : ObjectType<GqlEdge<Node>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<Node>> descriptor) => descriptor.Name("NodeEdge");
}

public sealed class PortConnectionType : ObjectType<GqlConnection<Port>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<Port>> descriptor)
    {
        descriptor.Name("PortConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<PortEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<PortType>>>>();
    }
}

public sealed class PortEdgeType : ObjectType<GqlEdge<Port>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<Port>> descriptor) => descriptor.Name("PortEdge");
}

public sealed class EdgeConnectionType : ObjectType<GqlConnection<Edge>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<Edge>> descriptor)
    {
        descriptor.Name("EdgeConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<EdgeEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<EdgeType>>>>();
    }
}

public sealed class EdgeEdgeType : ObjectType<GqlEdge<Edge>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<Edge>> descriptor) => descriptor.Name("EdgeEdge");
}

public sealed class NodeTypeConnectionType : ObjectType<GqlConnection<NodeType>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<NodeType>> descriptor)
    {
        descriptor.Name("NodeTypeConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<NodeTypeEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<NodeTypeType>>>>();
    }
}

public sealed class NodeTypeEdgeType : ObjectType<GqlEdge<NodeType>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<NodeType>> descriptor) => descriptor.Name("NodeTypeEdge");
}

public sealed class LibraryConnectionType : ObjectType<GqlConnection<Library>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<Library>> descriptor)
    {
        descriptor.Name("LibraryConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<LibraryEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<LibraryType>>>>();
    }
}

public sealed class LibraryEdgeType : ObjectType<GqlEdge<Library>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<Library>> descriptor) => descriptor.Name("LibraryEdge");
}

public sealed class SubgraphMatchConnectionType : ObjectType<GqlConnection<GqlSubgraphMatch>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnection<GqlSubgraphMatch>> descriptor)
    {
        descriptor.Name("SubgraphMatchConnection");
        descriptor.Field(t => t.Edges).Type<NonNullType<ListType<NonNullType<SubgraphMatchEdgeType>>>>();
        descriptor.Field(t => t.Nodes).Type<NonNullType<ListType<NonNullType<SubgraphMatchType>>>>();
    }
}

public sealed class SubgraphMatchEdgeType : ObjectType<GqlEdge<GqlSubgraphMatch>>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdge<GqlSubgraphMatch>> descriptor) => descriptor.Name("SubgraphMatchEdge");
}

public sealed class PageInfoType : ObjectType<GqlPageInfo>
{
    protected override void Configure(IObjectTypeDescriptor<GqlPageInfo> descriptor) => descriptor.Name("PageInfo");
}

public sealed class ExtensionPropertyType : ObjectType<GqlExtensionProperty>
{
    protected override void Configure(IObjectTypeDescriptor<GqlExtensionProperty> descriptor)
    {
        descriptor.Name("ExtensionProperty");
        descriptor.Field(t => t.Value).Type<JsonType>();
    }
}

public sealed class ForkDocumentPayloadType : ObjectType<GqlForkDocumentPayload>
{
    protected override void Configure(IObjectTypeDescriptor<GqlForkDocumentPayload> descriptor) => descriptor.Name("ForkDocumentPayload");
}

public sealed class DocumentPayloadType : ObjectType<GqlDocumentPayload>
{
    protected override void Configure(IObjectTypeDescriptor<GqlDocumentPayload> descriptor) => descriptor.Name("DocumentPayload");
}

public sealed class CommitDocumentPayloadType : ObjectType<GqlCommitDocumentPayload>
{
    protected override void Configure(IObjectTypeDescriptor<GqlCommitDocumentPayload> descriptor) => descriptor.Name("CommitDocumentPayload");
}

public sealed class GraphPatchPayloadType : ObjectType<GqlGraphPatchPayload>
{
    protected override void Configure(IObjectTypeDescriptor<GqlGraphPatchPayload> descriptor)
    {
        descriptor.Name("GraphPatchPayload");
        descriptor.Field(t => t.CreatedNodeIds).Type<NonNullType<ListType<NonNullType<IdType>>>>();
        descriptor.Field(t => t.DeletedNodeIds).Type<NonNullType<ListType<NonNullType<IdType>>>>();
        descriptor.Field(t => t.CreatedPortIds).Type<NonNullType<ListType<NonNullType<IdType>>>>();
        descriptor.Field(t => t.DeletedPortIds).Type<NonNullType<ListType<NonNullType<IdType>>>>();
    }
}

public sealed class ImportSnapshotPayloadType : ObjectType<GqlImportSnapshotPayload>
{
    protected override void Configure(IObjectTypeDescriptor<GqlImportSnapshotPayload> descriptor) =>
        descriptor.Name("ImportSnapshotPayload");
}

public sealed class DocumentGraphType : ObjectType<GqlDocumentGraph>
{
    protected override void Configure(IObjectTypeDescriptor<GqlDocumentGraph> descriptor) => descriptor.Name("DocumentGraph");
}

public sealed class DocumentStatsType : ObjectType<GqlDocumentStats>
{
    protected override void Configure(IObjectTypeDescriptor<GqlDocumentStats> descriptor) => descriptor.Name("DocumentStats");
}

public sealed class CountBucketType : ObjectType<GqlCountBucket>
{
    protected override void Configure(IObjectTypeDescriptor<GqlCountBucket> descriptor) => descriptor.Name("CountBucket");
}

public sealed class NodeBindingType : ObjectType<GqlNodeBinding>
{
    protected override void Configure(IObjectTypeDescriptor<GqlNodeBinding> descriptor)
    {
        descriptor.Name("NodeBinding");
        descriptor.Field(t => t.Key).Type<NonNullType<IdType>>();
    }
}

public sealed class PortBindingType : ObjectType<GqlPortBinding>
{
    protected override void Configure(IObjectTypeDescriptor<GqlPortBinding> descriptor)
    {
        descriptor.Name("PortBinding");
        descriptor.Field(t => t.Key).Type<NonNullType<IdType>>();
    }
}

public sealed class EdgeBindingType : ObjectType<GqlEdgeBinding>
{
    protected override void Configure(IObjectTypeDescriptor<GqlEdgeBinding> descriptor)
    {
        descriptor.Name("EdgeBinding");
        descriptor.Field(t => t.Key).Type<NonNullType<IdType>>();
    }
}

public sealed class ConnectionRealizationType : ObjectType<GqlConnectionRealization>
{
    protected override void Configure(IObjectTypeDescriptor<GqlConnectionRealization> descriptor)
    {
        descriptor.Name("ConnectionRealization");
        descriptor.Field(t => t.Key).Type<IdType>();
    }
}

public sealed class SubgraphMatchType : ObjectType<GqlSubgraphMatch>
{
    protected override void Configure(IObjectTypeDescriptor<GqlSubgraphMatch> descriptor) => descriptor.Name("SubgraphMatch");
}

public sealed class DataflowPathType : ObjectType<GqlDataflowPath>
{
    protected override void Configure(IObjectTypeDescriptor<GqlDataflowPath> descriptor) => descriptor.Name("DataflowPath");
}

public sealed class JsonType : AnyType
{
    public JsonType() : base("JSON")
    {
        Description = "Opaque JSON value (extension property payloads).";
    }
}

public sealed class IsoDateTimeType : DateTimeType
{
    public IsoDateTimeType() : base("DateTime")
    {
        Description = "ISO-8601 timestamp.";
    }
}
