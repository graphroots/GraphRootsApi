using HotChocolate.Types;

namespace GraphRoots.GraphApi;

public sealed class DocumentFilterType : InputObjectType<GqlDocumentFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlDocumentFilter> descriptor)
    {
        descriptor.Name("DocumentFilter");
        descriptor.Field(t => t.DocumentId).Type<IdType>();
        descriptor.Field(t => t.VersionId).Type<IdType>();
        descriptor.Field(t => t.LibraryId).Type<IdType>();
    }
}

public sealed class NodeFilterType : InputObjectType<GqlNodeFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlNodeFilter> descriptor)
    {
        descriptor.Name("NodeFilter");
        descriptor.Field(t => t.VersionId).Type<IdType>();
        descriptor.Field(t => t.DocumentId).Type<IdType>();
    }
}

public sealed class PortFilterType : InputObjectType<GqlPortFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPortFilter> descriptor)
    {
        descriptor.Name("PortFilter");
        descriptor.Field(t => t.VersionId).Type<IdType>();
        descriptor.Field(t => t.NodeId).Type<IdType>();
    }
}

public sealed class EdgeFilterType : InputObjectType<GqlEdgeFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlEdgeFilter> descriptor)
    {
        descriptor.Name("EdgeFilter");
        descriptor.Field(t => t.VersionId).Type<IdType>();
        descriptor.Field(t => t.SourcePortId).Type<IdType>();
        descriptor.Field(t => t.TargetPortId).Type<IdType>();
    }
}

public sealed class NodeTypeFilterType : InputObjectType<GqlNodeTypeFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlNodeTypeFilter> descriptor)
    {
        descriptor.Name("NodeTypeFilter");
        descriptor.Field(t => t.TypeId).Type<IdType>();
    }
}

public sealed class LibraryFilterType : InputObjectType<GqlLibraryFilter>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlLibraryFilter> descriptor)
    {
        descriptor.Name("LibraryFilter");
        descriptor.Field(t => t.LibraryId).Type<IdType>();
    }
}

public sealed class DocumentSortType : InputObjectType<GqlDocumentSort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlDocumentSort> descriptor)
    {
        descriptor.Name("DocumentSort");
        descriptor.Field(t => t.Field).Type<DocumentSortFieldType>().DefaultValue(GqlDocumentSortField.DOCUMENT_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class NodeSortType : InputObjectType<GqlNodeSort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlNodeSort> descriptor)
    {
        descriptor.Name("NodeSort");
        descriptor.Field(t => t.Field).Type<NodeSortFieldType>().DefaultValue(GqlNodeSortField.NODE_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class PortSortType : InputObjectType<GqlPortSort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPortSort> descriptor)
    {
        descriptor.Name("PortSort");
        descriptor.Field(t => t.Field).Type<PortSortFieldType>().DefaultValue(GqlPortSortField.PORT_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class EdgeSortType : InputObjectType<GqlEdgeSort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlEdgeSort> descriptor)
    {
        descriptor.Name("EdgeSort");
        descriptor.Field(t => t.Field).Type<EdgeSortFieldType>().DefaultValue(GqlEdgeSortField.VERSION_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class NodeTypeSortType : InputObjectType<GqlNodeTypeSort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlNodeTypeSort> descriptor)
    {
        descriptor.Name("NodeTypeSort");
        descriptor.Field(t => t.Field).Type<NodeTypeSortFieldType>().DefaultValue(GqlNodeTypeSortField.TYPE_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class LibrarySortType : InputObjectType<GqlLibrarySort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlLibrarySort> descriptor)
    {
        descriptor.Name("LibrarySort");
        descriptor.Field(t => t.Field).Type<LibrarySortFieldType>().DefaultValue(GqlLibrarySortField.LIBRARY_ID);
        descriptor.Field(t => t.Direction).Type<SortDirectionType>().DefaultValue(GqlSortDirection.ASC);
    }
}

public sealed class BoundingBoxInputType : InputObjectType<GqlBoundingBox>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlBoundingBox> descriptor) => descriptor.Name("BoundingBoxInput");
}

public sealed class ExtensionPredicateInputType : InputObjectType<GqlExtensionPredicate>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlExtensionPredicate> descriptor)
    {
        descriptor.Name("ExtensionPredicateInput");
        descriptor.Field(t => t.EqualsValue).Type<JsonType>().Name("equals");
    }
}

public sealed class SubgraphPatternInputType : InputObjectType<GqlSubgraphPattern>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlSubgraphPattern> descriptor) => descriptor.Name("SubgraphPatternInput");
}

public sealed class PatternNodeInputType : InputObjectType<GqlPatternNode>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPatternNode> descriptor)
    {
        descriptor.Name("PatternNodeInput");
        descriptor.Field(t => t.Key).Type<NonNullType<IdType>>();
    }
}

public sealed class PatternPortInputType : InputObjectType<GqlPatternPort>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPatternPort> descriptor)
    {
        descriptor.Name("PatternPortInput");
        descriptor.Field(t => t.Key).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Node).Type<NonNullType<IdType>>();
    }
}

public sealed class PatternEdgeInputType : InputObjectType<GqlPatternEdge>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPatternEdge> descriptor)
    {
        descriptor.Name("PatternEdgeInput");
        descriptor.Field(t => t.Key).Type<IdType>();
        descriptor.Field(t => t.SourcePort).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPort).Type<NonNullType<IdType>>();
    }
}

public sealed class PatternConnectionInputType : InputObjectType<GqlPatternConnection>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlPatternConnection> descriptor)
    {
        descriptor.Name("PatternConnectionInput");
        descriptor.Field(t => t.Key).Type<IdType>();
        descriptor.Field(t => t.SourceNode).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetNode).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.MinHops).Type<IntType>().DefaultValue(1);
        descriptor.Field(t => t.MaxHops).Type<IntType>().DefaultValue(1);
    }
}

public sealed class MatchScopeInputType : InputObjectType<GqlMatchScope>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlMatchScope> descriptor)
    {
        descriptor.Name("MatchScopeInput");
        descriptor.Field(t => t.VersionId).Type<IdType>();
        descriptor.Field(t => t.DocumentId).Type<IdType>();
        descriptor.Field(t => t.IncludeNested).Type<BooleanType>().DefaultValue(false);
    }
}

public sealed class MatchOptionsInputType : InputObjectType<GqlMatchOptions>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlMatchOptions> descriptor)
    {
        descriptor.Name("MatchOptionsInput");
        descriptor.Field(t => t.Injective).Type<BooleanType>().DefaultValue(true);
        descriptor.Field(t => t.Induced).Type<BooleanType>().DefaultValue(false);
    }
}

public sealed class ForkDocumentInputType : InputObjectType<GqlForkDocumentInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlForkDocumentInput> descriptor)
    {
        descriptor.Name("ForkDocumentInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.NewDocumentId).Type<IdType>();
    }
}

public sealed class CreateDocumentInputType : InputObjectType<GqlCreateDocumentInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlCreateDocumentInput> descriptor)
    {
        descriptor.Name("CreateDocumentInput");
        descriptor.Field(t => t.DocumentId).Type<IdType>();
    }
}

public sealed class CommitDocumentInputType : InputObjectType<GqlCommitDocumentInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlCommitDocumentInput> descriptor)
    {
        descriptor.Name("CommitDocumentInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
    }
}

public sealed class DocumentKeyInputType : InputObjectType<GqlDocumentKeyInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlDocumentKeyInput> descriptor)
    {
        descriptor.Name("DocumentKeyInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
    }
}

public sealed class MergeDocumentInputType : InputObjectType<GqlMergeDocumentInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlMergeDocumentInput> descriptor)
    {
        descriptor.Name("MergeDocumentInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.OtherParents).Type<NonNullType<ListType<NonNullType<DocumentKeyInputType>>>>();
    }
}

public sealed class GraphPatchInputType : InputObjectType<GqlGraphPatchInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlGraphPatchInput> descriptor)
    {
        descriptor.Name("GraphPatchInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.DeleteNodes).Type<ListType<NonNullType<IdType>>>();
        descriptor.Field(t => t.DeletePorts).Type<ListType<NonNullType<IdType>>>();
    }
}

public sealed class CreateNodeInputType : InputObjectType<GqlCreateNodeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlCreateNodeInput> descriptor)
    {
        descriptor.Name("CreateNodeInput");
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Origin).Type<OriginType>().DefaultValue(GqlOrigin.GRAPHROOTS);
    }
}

public sealed class UpdateNodeInputType : InputObjectType<GqlUpdateNodeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlUpdateNodeInput> descriptor)
    {
        descriptor.Name("UpdateNodeInput");
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
    }
}

public sealed class CreatePortInputType : InputObjectType<GqlCreatePortInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlCreatePortInput> descriptor)
    {
        descriptor.Name("CreatePortInput");
        descriptor.Field(t => t.PortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
    }
}

public sealed class UpdatePortInputType : InputObjectType<GqlUpdatePortInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlUpdatePortInput> descriptor)
    {
        descriptor.Name("UpdatePortInput");
        descriptor.Field(t => t.PortId).Type<NonNullType<IdType>>();
    }
}

public sealed class CreateEdgeInputType : InputObjectType<GqlCreateEdgeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlCreateEdgeInput> descriptor)
    {
        descriptor.Name("CreateEdgeInput");
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
    }
}

public sealed class DeleteEdgeInputType : InputObjectType<GqlDeleteEdgeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlDeleteEdgeInput> descriptor)
    {
        descriptor.Name("DeleteEdgeInput");
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
    }
}

public sealed class GroupMembershipInputType : InputObjectType<GqlGroupMembershipInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlGroupMembershipInput> descriptor)
    {
        descriptor.Name("GroupMembershipInput");
        descriptor.Field(t => t.MemberNodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.GroupNodeId).Type<NonNullType<IdType>>();
    }
}

public sealed class SetExtensionsInputType : InputObjectType<GqlSetExtensionsInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlSetExtensionsInput> descriptor)
    {
        descriptor.Name("SetExtensionsInput");
        descriptor.Field(t => t.Id).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.SourcePortId).Type<IdType>();
        descriptor.Field(t => t.TargetPortId).Type<IdType>();
    }
}

public sealed class WirePortsInputType : InputObjectType<GqlWirePortsInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlWirePortsInput> descriptor)
    {
        descriptor.Name("WirePortsInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
    }
}

public sealed class UnwirePortsInputType : InputObjectType<GqlUnwirePortsInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlUnwirePortsInput> descriptor)
    {
        descriptor.Name("UnwirePortsInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
    }
}

public sealed class MoveNodeInputType : InputObjectType<GqlMoveNodeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlMoveNodeInput> descriptor)
    {
        descriptor.Name("MoveNodeInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
    }
}

public sealed class SetExtensionsMutationInputType : InputObjectType<GqlSetExtensionsMutationInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlSetExtensionsMutationInput> descriptor)
    {
        descriptor.Name("SetExtensionsMutationInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Id).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.SourcePortId).Type<IdType>();
        descriptor.Field(t => t.TargetPortId).Type<IdType>();
    }
}

public sealed class ImportSnapshotInputType : InputObjectType<GqlImportSnapshotInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportSnapshotInput> descriptor)
    {
        descriptor.Name("ImportSnapshotInput");
        descriptor.Field(t => t.Documents).Type<NonNullType<ListType<NonNullType<ImportDocumentInputType>>>>();
        descriptor.Field(t => t.Nodes).Type<ListType<NonNullType<ImportNodeInputType>>>().Cost(0);
        descriptor.Field(t => t.Ports).Type<ListType<NonNullType<ImportPortInputType>>>().Cost(0);
        descriptor.Field(t => t.Edges).Type<ListType<NonNullType<ImportEdgeInputType>>>().Cost(0);
        descriptor.Field(t => t.GroupMemberships).Type<ListType<NonNullType<ImportGroupMembershipInputType>>>().Cost(0);
        descriptor.Field(t => t.Nests).Type<ListType<NonNullType<ImportNestInputType>>>().Cost(0);
        descriptor.Field(t => t.Libraries).Type<ListType<NonNullType<ImportLibraryInputType>>>().Cost(0);
        descriptor.Field(t => t.LibraryVersions).Type<ListType<NonNullType<ImportLibraryVersionInputType>>>().Cost(0);
        descriptor.Field(t => t.NodeTypes).Type<ListType<NonNullType<ImportNodeTypeInputType>>>().Cost(0);
        descriptor.Field(t => t.UsedBy).Type<ListType<NonNullType<ImportUsedByInputType>>>().Cost(0);
    }
}

public sealed class ImportDocumentInputType : InputObjectType<GqlImportDocumentInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportDocumentInput> descriptor)
    {
        descriptor.Name("ImportDocumentInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.FileCreationTimeUtc).Type<IsoDateTimeType>();
        descriptor.Field(t => t.FileLastWriteTimeUtc).Type<IsoDateTimeType>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportNodeInputType : InputObjectType<GqlImportNodeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportNodeInput> descriptor)
    {
        descriptor.Name("ImportNodeInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportPortInputType : InputObjectType<GqlImportPortInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportPortInput> descriptor)
    {
        descriptor.Name("ImportPortInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.NodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.PortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportEdgeInputType : InputObjectType<GqlImportEdgeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportEdgeInput> descriptor)
    {
        descriptor.Name("ImportEdgeInput");
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.SourcePortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.TargetPortId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportGroupMembershipInputType : InputObjectType<GqlImportGroupMembershipInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportGroupMembershipInput> descriptor)
    {
        descriptor.Name("ImportGroupMembershipInput");
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.MemberNodeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.GroupNodeId).Type<NonNullType<IdType>>();
    }
}

public sealed class ImportNestInputType : InputObjectType<GqlImportNestInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportNestInput> descriptor)
    {
        descriptor.Name("ImportNestInput");
        descriptor.Field(t => t.ParentDocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.ParentVersionId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.ChildDocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.ChildVersionId).Type<NonNullType<IdType>>();
    }
}

public sealed class ImportLibraryInputType : InputObjectType<GqlImportLibraryInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportLibraryInput> descriptor)
    {
        descriptor.Name("ImportLibraryInput");
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.LibraryId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportLibraryVersionInputType : InputObjectType<GqlImportLibraryVersionInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportLibraryVersionInput> descriptor)
    {
        descriptor.Name("ImportLibraryVersionInput");
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.LibraryId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportNodeTypeInputType : InputObjectType<GqlImportNodeTypeInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportNodeTypeInput> descriptor)
    {
        descriptor.Name("ImportNodeTypeInput");
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.TypeId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.LibraryId).Type<IdType>();
        descriptor.Field(t => t.Extensions).Type<ListType<NonNullType<ExtensionPredicateInputType>>>();
    }
}

public sealed class ImportUsedByInputType : InputObjectType<GqlImportUsedByInput>
{
    protected override void Configure(IInputObjectTypeDescriptor<GqlImportUsedByInput> descriptor)
    {
        descriptor.Name("ImportUsedByInput");
        descriptor.Field(t => t.Origin).Type<NonNullType<OriginType>>();
        descriptor.Field(t => t.LibraryId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.DocumentId).Type<NonNullType<IdType>>();
        descriptor.Field(t => t.VersionId).Type<NonNullType<IdType>>();
    }
}

public sealed class OriginType : EnumType<GqlOrigin>
{
    protected override void Configure(IEnumTypeDescriptor<GqlOrigin> descriptor)
    {
        descriptor.Name("Origin");
        descriptor.Description("Source tool of a stored entity.");
    }
}

public sealed class NodeKindType : EnumType<GqlNodeKind>
{
    protected override void Configure(IEnumTypeDescriptor<GqlNodeKind> descriptor)
    {
        descriptor.Name("NodeKind");
        descriptor.Description("Semantic class of a Node.");
    }
}

public sealed class PortDirectionType : EnumType<GqlPortDirection>
{
    protected override void Configure(IEnumTypeDescriptor<GqlPortDirection> descriptor)
    {
        descriptor.Name("PortDirection");
        descriptor.Description("Port direction. Synthetic floating-param ports may be BOTH.");
    }
}

public sealed class EntityKindType : EnumType<GqlEntityKind>
{
    protected override void Configure(IEnumTypeDescriptor<GqlEntityKind> descriptor) => descriptor.Name("EntityKind");
}

public sealed class SortDirectionType : EnumType<GqlSortDirection>
{
    protected override void Configure(IEnumTypeDescriptor<GqlSortDirection> descriptor) => descriptor.Name("SortDirection");
}

public sealed class DocumentSortFieldType : EnumType<GqlDocumentSortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlDocumentSortField> descriptor) => descriptor.Name("DocumentSortField");
}

public sealed class NodeSortFieldType : EnumType<GqlNodeSortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlNodeSortField> descriptor) => descriptor.Name("NodeSortField");
}

public sealed class PortSortFieldType : EnumType<GqlPortSortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlPortSortField> descriptor) => descriptor.Name("PortSortField");
}

public sealed class EdgeSortFieldType : EnumType<GqlEdgeSortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlEdgeSortField> descriptor) => descriptor.Name("EdgeSortField");
}

public sealed class NodeTypeSortFieldType : EnumType<GqlNodeTypeSortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlNodeTypeSortField> descriptor) => descriptor.Name("NodeTypeSortField");
}

public sealed class LibrarySortFieldType : EnumType<GqlLibrarySortField>
{
    protected override void Configure(IEnumTypeDescriptor<GqlLibrarySortField> descriptor) => descriptor.Name("LibrarySortField");
}
