using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public sealed partial class GraphStore
{
    public async Task<ImportSnapshotResult> ImportSnapshot(ImportSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ImportSnapshotValidator.Validate(snapshot);

        var documents = snapshot.Documents ?? [];
        var nodes = snapshot.Nodes ?? [];
        var ports = snapshot.Ports ?? [];
        var edges = snapshot.Edges ?? [];
        var memberships = snapshot.GroupMemberships ?? [];
        var nests = snapshot.Nests ?? [];
        var libraries = snapshot.Libraries ?? [];
        var libraryVersions = snapshot.LibraryVersions ?? [];
        var nodeTypes = snapshot.NodeTypes ?? [];
        var usedBy = snapshot.UsedBy ?? [];
        var root = documents.Single(d => d.IsNested != true);

        await _db.ExecuteWrite(async ct =>
        {
            await MergeIfAny(documents.Select(ToDocument).ToList(), ct);
            await MergeIfAny(nodes.Select(ToNode).ToList(), ct);
            await MergeIfAny(ports.Select(ToPort).ToList(), ct);

            await MergeRelsIfAny(
                ports.Select(p => Tuple.Create(
                    new Node { VersionId = p.VersionId, NodeId = p.NodeId },
                    new Port { VersionId = p.VersionId, PortId = p.PortId },
                    new HasPort())).ToList(),
                ct);

            await MergeRelsIfAny(
                edges.Select(e => Tuple.Create(
                    new Port { VersionId = e.VersionId, PortId = e.SourcePortId },
                    new Port { VersionId = e.VersionId, PortId = e.TargetPortId },
                    ToEdge(e))).ToList(),
                ct);

            await MergeRelsIfAny(
                memberships.Select(m => Tuple.Create(
                    new Node { VersionId = m.VersionId, NodeId = m.MemberNodeId },
                    new Node { VersionId = m.VersionId, NodeId = m.GroupNodeId },
                    new MemberOf())).ToList(),
                ct);

            await MergeIfAny(libraries.Select(ToLibrary).ToList(), ct);
            await MergeIfAny(libraryVersions.Select(ToLibraryVersion).ToList(), ct);

            await MergeRelsIfAny(
                libraryVersions.Select(v => Tuple.Create(
                    new Library { Origin = v.Origin, LibraryId = v.LibraryId },
                    ToLibraryVersion(v),
                    new HasVersion())).ToList(),
                ct);

            await MergeRelsIfAny(
                usedBy.Select(u => Tuple.Create(
                    ToLibraryVersionKey(u),
                    new Document { DocumentId = u.DocumentId, VersionId = u.VersionId },
                    new UsedBy())).ToList(),
                ct);

            await MergeIfAny(nodeTypes.Select(ToNodeType).ToList(), ct);

            await MergeRelsIfAny(
                nodeTypes
                    .Where(t => !string.IsNullOrWhiteSpace(t.LibraryId))
                    .Select(t => Tuple.Create(
                        new LibraryVersion
                        {
                            Origin = t.Origin,
                            LibraryId = t.LibraryId!,
                            Version = t.Version!,
                            AssemblyVersion = t.AssemblyVersion,
                        },
                        ToNodeType(t),
                        new Defines())).ToList(),
                ct);

            await MergeRelsIfAny(
                nodes.Select(n => Tuple.Create(
                    new NodeType { Origin = n.Origin, TypeId = n.TypeId! },
                    ToNode(n),
                    new HasInstance())).ToList(),
                ct);

            await MergeRelsIfAny(
                nodes.Select(n => Tuple.Create(
                    new Document { DocumentId = n.DocumentId, VersionId = n.VersionId },
                    ToNode(n),
                    new Contains())).ToList(),
                ct);

            await MergeRelsIfAny(
                nests.Select(n => Tuple.Create(
                    new Document { DocumentId = n.ParentDocumentId, VersionId = n.ParentVersionId },
                    new Document { DocumentId = n.ChildDocumentId, VersionId = n.ChildVersionId },
                    new Nests())).ToList(),
                ct);
        }, cancellationToken);

        var imported = await GetDocument(root.DocumentId, root.VersionId, cancellationToken)
            ?? throw GraphStoreException.StoreError(
                $"Imported document '{root.DocumentId}' version '{root.VersionId}' was not found after write.");

        return new ImportSnapshotResult
        {
            Document = imported,
            NestedDocumentCount = documents.Count(d => d.IsNested == true),
            NodeCount = nodes.Count,
            PortCount = ports.Count,
            EdgeCount = edges.Count,
        };
    }

    Task MergeIfAny<T>(IReadOnlyList<T> nodes, CancellationToken cancellationToken) =>
        nodes.Count == 0 ? Task.CompletedTask : _db.MergeNodes(nodes, cancellationToken);

    Task MergeRelsIfAny<TFrom, TTo, TRel>(IReadOnlyList<Tuple<TFrom, TTo, TRel>> relationships, CancellationToken cancellationToken) =>
        relationships.Count == 0 ? Task.CompletedTask : _db.MergeRelationships(relationships, cancellationToken);

    static Document ToDocument(ImportDocument document) => new()
    {
        DocumentId = document.DocumentId,
        VersionId = document.VersionId,
        Origin = document.Origin,
        FileName = document.FileName,
        FilePath = document.FilePath,
        IsNested = document.IsNested ?? false,
        FileCreationTimeUtc = document.FileCreationTimeUtc,
        FileLastWriteTimeUtc = document.FileLastWriteTimeUtc,
        Committed = false,
        Extensions = document.Extensions,
    };

    static Node ToNode(ImportNode node) => new()
    {
        VersionId = node.VersionId,
        NodeId = node.NodeId,
        Origin = node.Origin,
        TypeId = node.TypeId,
        Name = node.Name,
        NickName = node.NickName,
        Kind = node.Kind,
        Locked = node.Locked,
        X = node.X,
        Y = node.Y,
        Source = node.Source,
        Language = node.Language,
        Text = node.Text,
        Extensions = node.Extensions,
    };

    static Port ToPort(ImportPort port) => new()
    {
        VersionId = port.VersionId,
        PortId = port.PortId,
        Name = port.Name,
        Direction = port.Direction,
        Access = port.Access,
        Extensions = port.Extensions,
    };

    static Edge ToEdge(ImportEdge edge) => new()
    {
        VersionId = edge.VersionId,
        SourcePortId = edge.SourcePortId,
        TargetPortId = edge.TargetPortId,
        SourceName = edge.SourceName,
        TargetName = edge.TargetName,
        Extensions = edge.Extensions,
    };

    static Library ToLibrary(ImportLibrary library) => new()
    {
        Origin = library.Origin,
        LibraryId = library.LibraryId,
        Name = library.Name,
        Author = library.Author,
        Extensions = library.Extensions,
    };

    static LibraryVersion ToLibraryVersion(ImportLibraryVersion version) => new()
    {
        Origin = version.Origin,
        LibraryId = version.LibraryId,
        Version = version.Version,
        AssemblyVersion = version.AssemblyVersion,
        Name = version.Name,
        Author = version.Author,
        Extensions = version.Extensions,
    };

    static LibraryVersion ToLibraryVersionKey(ImportUsedBy usedBy) => new()
    {
        Origin = usedBy.Origin,
        LibraryId = usedBy.LibraryId,
        Version = usedBy.Version,
        AssemblyVersion = usedBy.AssemblyVersion,
    };

    static NodeType ToNodeType(ImportNodeType nodeType) => new()
    {
        Origin = nodeType.Origin,
        TypeId = nodeType.TypeId,
        Name = nodeType.Name,
        Extensions = nodeType.Extensions,
    };
}
