using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GreenDonut;

namespace GraphRoots.GraphApi;

public sealed class DocumentByKeyDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), Document?>
{
    readonly IGraphStore _store;

    public DocumentByKeyDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override async Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), Document?>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        var items = await _store.GetDocuments(keys, cancellationToken);
        return keys.Zip(items).ToDictionary(p => p.First, p => p.Second);
    }
}

public sealed class NodeByKeyDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), Node?>
{
    readonly IGraphStore _store;

    public NodeByKeyDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override async Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), Node?>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        var items = await _store.GetNodes(keys, cancellationToken);
        return keys.Zip(items).ToDictionary(p => p.First, p => p.Second);
    }
}

public sealed class PortByKeyDataLoader : BatchDataLoader<(Guid VersionId, string PortId), Port?>
{
    readonly IGraphStore _store;

    public PortByKeyDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override async Task<IReadOnlyDictionary<(Guid VersionId, string PortId), Port?>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken)
    {
        var items = await _store.GetPorts(keys, cancellationToken);
        return keys.Zip(items).ToDictionary(p => p.First, p => p.Second);
    }
}

public sealed class PortsByNodeDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), IReadOnlyList<Port>>
{
    readonly IGraphStore _store;

    public PortsByNodeDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Port>>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetPortsByNodes(keys, cancellationToken);
    }
}

public sealed class NodeIdByPortDataLoader : BatchDataLoader<(Guid VersionId, string PortId), string?>
{
    readonly IGraphStore _store;

    public NodeIdByPortDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string PortId), string?>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetNodeIdsByPorts(keys, cancellationToken);
    }
}

public sealed class OutgoingEdgesByPortDataLoader : BatchDataLoader<(Guid VersionId, string PortId), IReadOnlyList<Edge>>
{
    readonly IGraphStore _store;

    public OutgoingEdgesByPortDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetOutgoingEdgesByPorts(keys, cancellationToken);
    }
}

public sealed class IncomingEdgesByPortDataLoader : BatchDataLoader<(Guid VersionId, string PortId), IReadOnlyList<Edge>>
{
    readonly IGraphStore _store;

    public IncomingEdgesByPortDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string PortId), IReadOnlyList<Edge>>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string PortId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetIncomingEdgesByPorts(keys, cancellationToken);
    }
}

public sealed class DocumentsForNodesDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), Document?>
{
    readonly IGraphStore _store;

    public DocumentsForNodesDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), Document?>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetDocumentsForNodes(keys, cancellationToken);
    }
}

public sealed class NodeTypesForNodesDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), NodeType?>
{
    readonly IGraphStore _store;

    public NodeTypesForNodesDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), NodeType?>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetNodeTypesForNodes(keys, cancellationToken);
    }
}

public sealed class GroupsByNodeDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), IReadOnlyList<Node>>
{
    readonly IGraphStore _store;

    public GroupsByNodeDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetGroupsByNodes(keys, cancellationToken);
    }
}

public sealed class MembersByNodeDataLoader : BatchDataLoader<(Guid VersionId, string NodeId), IReadOnlyList<Node>>
{
    readonly IGraphStore _store;

    public MembersByNodeDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(Guid VersionId, string NodeId), IReadOnlyList<Node>>> LoadBatchAsync(
        IReadOnlyList<(Guid VersionId, string NodeId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetMembersByNodes(keys, cancellationToken);
    }
}

public sealed class NestedDocumentsDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>
{
    readonly IGraphStore _store;

    public NestedDocumentsDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetNestedDocumentsByKeys(keys, cancellationToken);
    }
}

public sealed class ParentDocumentsDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>
{
    readonly IGraphStore _store;

    public ParentDocumentsDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetParentDocumentsByKeys(keys, cancellationToken);
    }
}

public sealed class BasedOnDocumentsDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>
{
    readonly IGraphStore _store;

    public BasedOnDocumentsDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetBasedOnByKeys(keys, cancellationToken);
    }
}

public sealed class DerivedDocumentsDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>
{
    readonly IGraphStore _store;

    public DerivedDocumentsDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<Document>>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetDerivedDocumentsByKeys(keys, cancellationToken);
    }
}

public sealed class LibrariesForDocumentDataLoader : BatchDataLoader<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>
{
    readonly IGraphStore _store;

    public LibrariesForDocumentDataLoader(IBatchScheduler scheduler, DataLoaderOptions options, IGraphStore store)
        : base(scheduler, options)
    {
        _store = store;
    }

    protected override Task<IReadOnlyDictionary<(string DocumentId, Guid VersionId), IReadOnlyList<LibraryVersion>>> LoadBatchAsync(
        IReadOnlyList<(string DocumentId, Guid VersionId)> keys,
        CancellationToken cancellationToken)
    {
        return _store.GetLibrariesForDocuments(keys, cancellationToken);
    }
}
