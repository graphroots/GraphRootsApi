using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using HotChocolate;

namespace GraphRoots.GraphApi;

public class Mutation
{
    public async Task<GqlForkDocumentPayload> ForkDocument(GqlForkDocumentInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var document = await store.ForkDocument(input.DocumentId, FilterMapping.ParseGuid(input.VersionId, "versionId"), input.NewDocumentId, cancellationToken);
        return new GqlForkDocumentPayload { Document = document };
    }

    public async Task<GqlDocumentPayload> CreateDocument(GqlCreateDocumentInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var document = await store.CreateDocument(input.DocumentId, input.FileName, cancellationToken);
        return new GqlDocumentPayload { Document = document };
    }

    public async Task<GqlCommitDocumentPayload> CommitDocument(GqlCommitDocumentInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var result = await store.CommitDocument(input.DocumentId, FilterMapping.ParseGuid(input.VersionId, "versionId"), cancellationToken);
        return ToCommitPayload(result);
    }

    public async Task<GqlCommitDocumentPayload> MergeDocument(GqlMergeDocumentInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var others = (input.OtherParents ?? []).Select(p =>
            (p.DocumentId, FilterMapping.ParseGuid(p.VersionId, "versionId"))).ToList();
        var result = await store.MergeDocument(
            input.DocumentId,
            FilterMapping.ParseGuid(input.VersionId, "versionId"),
            others,
            cancellationToken);
        return ToCommitPayload(result);
    }

    public async Task<GqlGraphPatchPayload> ApplyGraphPatch(GqlGraphPatchInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var result = await store.ApplyPatch(FilterMapping.Patch(input), cancellationToken);
        return ToPayload(result);
    }

    public Task<GqlGraphPatchPayload> WirePorts(GqlWirePortsInput input, [Service] IGraphStore store, CancellationToken cancellationToken) =>
        ApplyGraphPatch(new GqlGraphPatchInput
        {
            DocumentId = input.DocumentId,
            VersionId = input.VersionId,
            CreateEdges =
            [
                new GqlCreateEdgeInput
                {
                    SourcePortId = input.SourcePortId,
                    TargetPortId = input.TargetPortId,
                    SourceName = input.SourceName,
                    TargetName = input.TargetName,
                },
            ],
        }, store, cancellationToken);

    public Task<GqlGraphPatchPayload> UnwirePorts(GqlUnwirePortsInput input, [Service] IGraphStore store, CancellationToken cancellationToken) =>
        ApplyGraphPatch(new GqlGraphPatchInput
        {
            DocumentId = input.DocumentId,
            VersionId = input.VersionId,
            DeleteEdges =
            [
                new GqlDeleteEdgeInput { SourcePortId = input.SourcePortId, TargetPortId = input.TargetPortId },
            ],
        }, store, cancellationToken);

    public Task<GqlGraphPatchPayload> MoveNode(GqlMoveNodeInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var update = new GqlUpdateNodeInput { NodeId = input.NodeId };
        update.X = input.X;
        update.Y = input.Y;
        return ApplyGraphPatch(new GqlGraphPatchInput
        {
            DocumentId = input.DocumentId,
            VersionId = input.VersionId,
            UpdateNodes = [update],
        }, store, cancellationToken);
    }

    public async Task<GqlImportSnapshotPayload> ImportSnapshot(GqlImportSnapshotInput input, [Service] IGraphStore store, CancellationToken cancellationToken)
    {
        var result = await store.ImportSnapshot(FilterMapping.Snapshot(input), cancellationToken);
        return new GqlImportSnapshotPayload
        {
            Document = result.Document,
            NestedDocumentCount = result.NestedDocumentCount,
            NodeCount = result.NodeCount,
            PortCount = result.PortCount,
            EdgeCount = result.EdgeCount,
        };
    }

    public Task<GqlGraphPatchPayload> SetExtensions(GqlSetExtensionsMutationInput input, [Service] IGraphStore store, CancellationToken cancellationToken) =>
        ApplyGraphPatch(new GqlGraphPatchInput
        {
            DocumentId = input.DocumentId,
            VersionId = input.VersionId,
            SetExtensions =
            [
                new GqlSetExtensionsInput
                {
                    Entity = input.Entity,
                    Id = input.Id,
                    SourcePortId = input.SourcePortId,
                    TargetPortId = input.TargetPortId,
                    Entries = input.Entries,
                },
            ],
        }, store, cancellationToken);

    static GqlGraphPatchPayload ToPayload(GraphPatchResult result) => new()
    {
        Document = result.Document,
        CreatedNodeIds = result.CreatedNodeIds,
        DeletedNodeIds = result.DeletedNodeIds,
        CreatedPortIds = result.CreatedPortIds,
        DeletedPortIds = result.DeletedPortIds,
    };

    static GqlCommitDocumentPayload ToCommitPayload(CommitDocumentResult result) => new()
    {
        Document = result.Document,
        WorkingDocument = result.WorkingDocument,
    };
}
