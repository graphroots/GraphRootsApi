using System;
using System.Collections.Generic;
using System.Linq;
using GraphRoots.GraphApi;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphApiTests;

static class ImportSnapshotGql
{
    public static GqlImportSnapshotInput From(ImportSnapshot snapshot) => new()
    {
        Documents = snapshot.Documents.Select(d => new GqlImportDocumentInput
        {
            DocumentId = d.DocumentId,
            VersionId = d.VersionId.ToString(),
            Origin = EnumMapping.ToGqlOrigin(d.Origin),
            FileName = d.FileName,
            FilePath = d.FilePath,
            IsNested = d.IsNested,
            FileCreationTimeUtc = d.FileCreationTimeUtc,
            FileLastWriteTimeUtc = d.FileLastWriteTimeUtc,
            Extensions = Ext(d.Extensions),
        }).ToList(),
        Nodes = snapshot.Nodes.Select(n => new GqlImportNodeInput
        {
            DocumentId = n.DocumentId,
            VersionId = n.VersionId.ToString(),
            NodeId = n.NodeId,
            Origin = EnumMapping.ToGqlOrigin(n.Origin),
            TypeId = n.TypeId,
            Name = n.Name,
            NickName = n.NickName,
            Kind = EnumMapping.ToGqlKind(n.Kind),
            Locked = n.Locked,
            X = n.X,
            Y = n.Y,
            Source = n.Source,
            Language = n.Language,
            Text = n.Text,
            Extensions = Ext(n.Extensions),
        }).ToList(),
        Ports = snapshot.Ports.Select(p => new GqlImportPortInput
        {
            DocumentId = p.DocumentId,
            VersionId = p.VersionId.ToString(),
            NodeId = p.NodeId,
            PortId = p.PortId,
            Name = p.Name,
            Direction = EnumMapping.ToGqlDirection(p.Direction),
            Access = p.Access,
            Extensions = Ext(p.Extensions),
        }).ToList(),
        Edges = snapshot.Edges.Select(e => new GqlImportEdgeInput
        {
            DocumentId = e.DocumentId,
            VersionId = e.VersionId.ToString(),
            SourcePortId = e.SourcePortId,
            TargetPortId = e.TargetPortId,
            SourceName = e.SourceName,
            TargetName = e.TargetName,
            Extensions = Ext(e.Extensions),
        }).ToList(),
        GroupMemberships = snapshot.GroupMemberships.Select(m => new GqlImportGroupMembershipInput
        {
            VersionId = m.VersionId.ToString(),
            MemberNodeId = m.MemberNodeId,
            GroupNodeId = m.GroupNodeId,
        }).ToList(),
        Nests = snapshot.Nests.Select(n => new GqlImportNestInput
        {
            ParentDocumentId = n.ParentDocumentId,
            ParentVersionId = n.ParentVersionId.ToString(),
            ChildDocumentId = n.ChildDocumentId,
            ChildVersionId = n.ChildVersionId.ToString(),
        }).ToList(),
        Libraries = snapshot.Libraries.Select(l => new GqlImportLibraryInput
        {
            Origin = EnumMapping.ToGqlOrigin(l.Origin),
            LibraryId = l.LibraryId,
            Name = l.Name,
            Author = l.Author,
            Extensions = Ext(l.Extensions),
        }).ToList(),
        LibraryVersions = snapshot.LibraryVersions.Select(v => new GqlImportLibraryVersionInput
        {
            Origin = EnumMapping.ToGqlOrigin(v.Origin),
            LibraryId = v.LibraryId,
            Version = v.Version,
            AssemblyVersion = v.AssemblyVersion,
            Name = v.Name,
            Author = v.Author,
            Extensions = Ext(v.Extensions),
        }).ToList(),
        NodeTypes = snapshot.NodeTypes.Select(t => new GqlImportNodeTypeInput
        {
            Origin = EnumMapping.ToGqlOrigin(t.Origin),
            TypeId = t.TypeId,
            Name = t.Name,
            LibraryId = t.LibraryId,
            Version = t.Version,
            AssemblyVersion = t.AssemblyVersion,
            Extensions = Ext(t.Extensions),
        }).ToList(),
        UsedBy = snapshot.UsedBy.Select(u => new GqlImportUsedByInput
        {
            Origin = EnumMapping.ToGqlOrigin(u.Origin),
            LibraryId = u.LibraryId,
            Version = u.Version,
            AssemblyVersion = u.AssemblyVersion,
            DocumentId = u.DocumentId,
            VersionId = u.VersionId.ToString(),
        }).ToList(),
    };

    static IReadOnlyList<GqlExtensionPredicate>? Ext(IDictionary<string, object?>? extensions)
    {
        if (extensions == null || extensions.Count == 0)
            return null;
        return extensions
            .Where(kvp => kvp.Value != null)
            .Select(kvp => new GqlExtensionPredicate { Key = kvp.Key, EqualsValue = kvp.Value })
            .ToList();
    }
}
