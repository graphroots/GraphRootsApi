using System;
using System.Collections.Generic;
using System.Linq;
using GraphRoots.GraphDb;
using HotChocolate;

namespace GraphRoots.GraphApi;

public static class FilterMapping
{
    public static PageRequest Page(int? first, string? after, int maxSize = 500, SortSpec? sort = null, bool includeTotalCount = true) =>
        new() { First = first, After = after, MaxSize = maxSize, Sort = sort, IncludeTotalCount = includeTotalCount };

    public static Guid ParseGuid(string value, string name)
    {
        if (!Guid.TryParse(value, out var guid))
            throw GraphStoreException.InvalidArgument($"{name} must be a UUID.");
        return guid;
    }

    public static Guid? ParseOptionalGuid(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseGuid(value, name);

    public static IReadOnlyList<GqlExtensionProperty> Extensions(IDictionary<string, object?>? extensions)
    {
        if (extensions == null || extensions.Count == 0)
            return [];
        return extensions.Select(kvp => new GqlExtensionProperty { Key = kvp.Key, Value = kvp.Value }).ToList();
    }

    public static object? Extension(IDictionary<string, object?>? extensions, string key)
    {
        if (extensions == null || !extensions.TryGetValue(key, out var value))
            return null;
        return value;
    }

    public static DocumentFilter? Document(GqlDocumentFilter? filter)
    {
        if (filter == null)
            return null;
        return new DocumentFilter
        {
            Origin = OriginFilter(filter.Origin),
            IsNested = filter.IsNested,
            Committed = filter.Committed,
            FileNameContains = filter.FileNameContains,
            DocumentId = filter.DocumentId,
            VersionId = ParseOptionalGuid(filter.VersionId, "versionId"),
            LibraryId = filter.LibraryId,
            CreatedAfterUtc = filter.CreatedAfterUtc,
            CreatedBeforeUtc = filter.CreatedBeforeUtc,
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? DocumentSort(GqlDocumentSort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static NodeFilter? Node(GqlNodeFilter? filter)
    {
        if (filter == null)
            return null;
        return new NodeFilter
        {
            VersionId = ParseOptionalGuid(filter.VersionId, "versionId"),
            DocumentId = filter.DocumentId,
            Origin = OriginFilter(filter.Origin),
            Kind = filter.Kind is GqlNodeKind k ? EnumMapping.ToStore(k) : null,
            TypeId = filter.TypeId,
            Name = filter.Name,
            NickName = filter.NickName,
            Locked = filter.Locked,
            Language = filter.Language,
            HasSource = filter.HasSource,
            Bbox = filter.Bbox == null ? null : new BoundingBox
            {
                MinX = filter.Bbox.MinX,
                MinY = filter.Bbox.MinY,
                MaxX = filter.Bbox.MaxX,
                MaxY = filter.Bbox.MaxY,
            },
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? NodeSort(GqlNodeSort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static PortFilter? Port(GqlPortFilter? filter)
    {
        if (filter == null)
            return null;
        return new PortFilter
        {
            VersionId = ParseOptionalGuid(filter.VersionId, "versionId"),
            NodeId = filter.NodeId,
            Name = filter.Name,
            Direction = filter.Direction is GqlPortDirection d ? EnumMapping.ToStore(d) : null,
            Access = filter.Access,
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? PortSort(GqlPortSort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static EdgeFilter? Edge(GqlEdgeFilter? filter)
    {
        if (filter == null)
            return null;
        return new EdgeFilter
        {
            VersionId = ParseOptionalGuid(filter.VersionId, "versionId"),
            SourcePortId = filter.SourcePortId,
            TargetPortId = filter.TargetPortId,
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? EdgeSort(GqlEdgeSort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static NodeTypeFilter? NodeType(GqlNodeTypeFilter? filter)
    {
        if (filter == null)
            return null;
        return new NodeTypeFilter
        {
            Origin = OriginFilter(filter.Origin),
            TypeId = filter.TypeId,
            NameContains = filter.NameContains,
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? NodeTypeSort(GqlNodeTypeSort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static LibraryFilter? Library(GqlLibraryFilter? filter)
    {
        if (filter == null)
            return null;
        return new LibraryFilter
        {
            Origin = OriginFilter(filter.Origin),
            LibraryId = filter.LibraryId,
            NameContains = filter.NameContains,
            Extensions = Ext(filter.Extensions),
        };
    }

    public static SortSpec? LibrarySort(GqlLibrarySort? sort) =>
        sort == null ? null : new SortSpec { Field = EnumMapping.ToStore(sort.Field), Direction = EnumMapping.ToStore(sort.Direction) };

    public static SubgraphPattern Pattern(GqlSubgraphPattern pattern) => new()
    {
        Nodes = pattern.Nodes.Select(n => new PatternNode
        {
            Key = n.Key,
            TypeId = n.TypeId,
            Kind = n.Kind is GqlNodeKind k ? EnumMapping.ToStore(k) : null,
            Name = n.Name,
            NickName = n.NickName,
            Locked = n.Locked,
            Language = n.Language,
            Origin = OriginFilter(n.Origin),
            Extensions = Ext(n.Extensions),
        }).ToList(),
        Ports = pattern.Ports?.Select(p => new PatternPort
        {
            Key = p.Key,
            Node = p.Node,
            Name = p.Name,
            Direction = p.Direction is GqlPortDirection d ? EnumMapping.ToStore(d) : null,
            Access = p.Access,
        }).ToList(),
        Edges = pattern.Edges?.Select(e => new PatternEdge
        {
            Key = e.Key,
            SourcePort = e.SourcePort,
            TargetPort = e.TargetPort,
        }).ToList(),
        Connections = pattern.Connections?.Select(c => new PatternConnection
        {
            Key = c.Key,
            SourceNode = c.SourceNode,
            TargetNode = c.TargetNode,
            SourcePortName = c.SourcePortName,
            TargetPortName = c.TargetPortName,
            SourceDirection = c.SourceDirection is GqlPortDirection sd ? EnumMapping.ToStore(sd) : null,
            TargetDirection = c.TargetDirection is GqlPortDirection td ? EnumMapping.ToStore(td) : null,
            MinHops = c.MinHops,
            MaxHops = c.MaxHops,
        }).ToList(),
    };

    public static MatchScope? Scope(GqlMatchScope? scope)
    {
        if (scope == null)
            return null;
        return new MatchScope
        {
            VersionId = ParseOptionalGuid(scope.VersionId, "versionId"),
            DocumentId = scope.DocumentId,
            Origin = OriginFilter(scope.Origin),
            IncludeNested = scope.IncludeNested,
        };
    }

    public static MatchOptions? Options(GqlMatchOptions? options)
    {
        if (options == null)
            return null;
        return new MatchOptions { Injective = options.Injective, Induced = options.Induced };
    }

    public static GraphPatch Patch(GqlGraphPatchInput input) => new()
    {
        DocumentId = input.DocumentId,
        VersionId = ParseGuid(input.VersionId, "versionId"),
        CreateNodes = input.CreateNodes?.Select(n => new CreateNodeOp
        {
            NodeId = n.NodeId,
            Origin = EnumMapping.ToStore(n.Origin),
            TypeId = n.TypeId,
            Name = n.Name,
            NickName = n.NickName,
            Kind = n.Kind is GqlNodeKind k ? EnumMapping.ToStore(k) : null,
            Locked = n.Locked,
            X = n.X,
            Y = n.Y,
            Source = n.Source,
            Language = n.Language,
            Text = n.Text,
        }).ToList(),
        UpdateNodes = input.UpdateNodes?.Select(n => new UpdateNodeOp
        {
            NodeId = n.NodeId,
            TypeId = Opt(n.TypeId),
            Name = Opt(n.Name),
            NickName = Opt(n.NickName),
            Kind = n.Kind.HasValue ? new OptionalSet<string?>(n.Kind.Value is GqlNodeKind k ? EnumMapping.ToStore(k) : null) : default,
            Locked = Opt(n.Locked),
            X = Opt(n.X),
            Y = Opt(n.Y),
            Source = Opt(n.Source),
            Language = Opt(n.Language),
            Text = Opt(n.Text),
        }).ToList(),
        DeleteNodes = input.DeleteNodes,
        CreatePorts = input.CreatePorts?.Select(p => new CreatePortOp
        {
            PortId = p.PortId,
            NodeId = p.NodeId,
            Name = p.Name,
            Direction = p.Direction is GqlPortDirection d ? EnumMapping.ToStore(d) : null,
            Access = p.Access,
        }).ToList(),
        UpdatePorts = input.UpdatePorts?.Select(p => new UpdatePortOp
        {
            PortId = p.PortId,
            Name = Opt(p.Name),
            Direction = p.Direction.HasValue
                ? new OptionalSet<string?>(p.Direction.Value is GqlPortDirection d ? EnumMapping.ToStore(d) : null)
                : default,
            Access = Opt(p.Access),
        }).ToList(),
        DeletePorts = input.DeletePorts,
        CreateEdges = input.CreateEdges?.Select(e => new CreateEdgeOp
        {
            SourcePortId = e.SourcePortId,
            TargetPortId = e.TargetPortId,
            SourceName = e.SourceName,
            TargetName = e.TargetName,
        }).ToList(),
        DeleteEdges = input.DeleteEdges?.Select(e => new DeleteEdgeOp
        {
            SourcePortId = e.SourcePortId,
            TargetPortId = e.TargetPortId,
        }).ToList(),
        AddToGroups = input.AddToGroups?.Select(g => new GroupMembershipOp
        {
            MemberNodeId = g.MemberNodeId,
            GroupNodeId = g.GroupNodeId,
        }).ToList(),
        RemoveFromGroups = input.RemoveFromGroups?.Select(g => new GroupMembershipOp
        {
            MemberNodeId = g.MemberNodeId,
            GroupNodeId = g.GroupNodeId,
        }).ToList(),
        SetExtensions = input.SetExtensions?.Select(s => new SetExtensionsOp
        {
            Entity = EnumMapping.ToStore(s.Entity),
            Id = s.Id,
            SourcePortId = s.SourcePortId,
            TargetPortId = s.TargetPortId,
            Entries = Ext(s.Entries) ?? [],
        }).ToList(),
    };

    public static ImportSnapshot Snapshot(GqlImportSnapshotInput input) => new()
    {
        Documents = (input.Documents ?? []).Select(d => new ImportDocument
        {
            DocumentId = d.DocumentId,
            VersionId = ParseGuid(d.VersionId, "documents.versionId"),
            Origin = EnumMapping.ToStore(d.Origin),
            FileName = d.FileName,
            FilePath = d.FilePath,
            IsNested = d.IsNested,
            FileCreationTimeUtc = d.FileCreationTimeUtc,
            FileLastWriteTimeUtc = d.FileLastWriteTimeUtc,
            Extensions = ExtDict(d.Extensions),
        }).ToList(),
        Nodes = input.Nodes?.Select(n => new ImportNode
        {
            DocumentId = n.DocumentId,
            VersionId = ParseGuid(n.VersionId, "nodes.versionId"),
            NodeId = n.NodeId,
            Origin = EnumMapping.ToStore(n.Origin),
            TypeId = n.TypeId,
            Name = n.Name,
            NickName = n.NickName,
            Kind = n.Kind is GqlNodeKind k ? EnumMapping.ToStore(k) : null,
            Locked = n.Locked,
            X = n.X,
            Y = n.Y,
            Source = n.Source,
            Language = n.Language,
            Text = n.Text,
            Extensions = ExtDict(n.Extensions),
        }).ToList() ?? [],
        Ports = input.Ports?.Select(p => new ImportPort
        {
            DocumentId = p.DocumentId,
            VersionId = ParseGuid(p.VersionId, "ports.versionId"),
            NodeId = p.NodeId,
            PortId = p.PortId,
            Name = p.Name,
            Direction = p.Direction is GqlPortDirection d ? EnumMapping.ToStore(d) : null,
            Access = p.Access,
            Extensions = ExtDict(p.Extensions),
        }).ToList() ?? [],
        Edges = input.Edges?.Select(e => new ImportEdge
        {
            DocumentId = e.DocumentId,
            VersionId = ParseGuid(e.VersionId, "edges.versionId"),
            SourcePortId = e.SourcePortId,
            TargetPortId = e.TargetPortId,
            SourceName = e.SourceName,
            TargetName = e.TargetName,
            Extensions = ExtDict(e.Extensions),
        }).ToList() ?? [],
        GroupMemberships = input.GroupMemberships?.Select(m => new ImportGroupMembership
        {
            VersionId = ParseGuid(m.VersionId, "groupMemberships.versionId"),
            MemberNodeId = m.MemberNodeId,
            GroupNodeId = m.GroupNodeId,
        }).ToList() ?? [],
        Nests = input.Nests?.Select(n => new ImportNest
        {
            ParentDocumentId = n.ParentDocumentId,
            ParentVersionId = ParseGuid(n.ParentVersionId, "nests.parentVersionId"),
            ChildDocumentId = n.ChildDocumentId,
            ChildVersionId = ParseGuid(n.ChildVersionId, "nests.childVersionId"),
        }).ToList() ?? [],
        Libraries = input.Libraries?.Select(l => new ImportLibrary
        {
            Origin = EnumMapping.ToStore(l.Origin),
            LibraryId = l.LibraryId,
            Name = l.Name,
            Author = l.Author,
            Extensions = ExtDict(l.Extensions),
        }).ToList() ?? [],
        LibraryVersions = input.LibraryVersions?.Select(v => new ImportLibraryVersion
        {
            Origin = EnumMapping.ToStore(v.Origin),
            LibraryId = v.LibraryId,
            Version = v.Version,
            AssemblyVersion = v.AssemblyVersion,
            Name = v.Name,
            Author = v.Author,
            Extensions = ExtDict(v.Extensions),
        }).ToList() ?? [],
        NodeTypes = input.NodeTypes?.Select(t => new ImportNodeType
        {
            Origin = EnumMapping.ToStore(t.Origin),
            TypeId = t.TypeId,
            Name = t.Name,
            LibraryId = t.LibraryId,
            Version = t.Version,
            AssemblyVersion = t.AssemblyVersion,
            Extensions = ExtDict(t.Extensions),
        }).ToList() ?? [],
        UsedBy = input.UsedBy?.Select(u => new ImportUsedBy
        {
            Origin = EnumMapping.ToStore(u.Origin),
            LibraryId = u.LibraryId,
            Version = u.Version,
            AssemblyVersion = u.AssemblyVersion,
            DocumentId = u.DocumentId,
            VersionId = ParseGuid(u.VersionId, "usedBy.versionId"),
        }).ToList() ?? [],
    };

    public static GqlSubgraphMatch Match(SubgraphMatch match) => new()
    {
        Document = match.Document,
        Nodes = match.Nodes.Select(n => new GqlNodeBinding { Key = n.Key, Node = n.Node }).ToList(),
        Ports = match.Ports.Select(p => new GqlPortBinding { Key = p.Key, Port = p.Port }).ToList(),
        Edges = match.Edges.Select(e => new GqlEdgeBinding { Key = e.Key, Edge = e.Edge }).ToList(),
        Connections = match.Connections.Select(c => new GqlConnectionRealization
        {
            Key = c.Key,
            Ports = c.Ports,
            Edges = c.Edges,
        }).ToList(),
    };

    static string? OriginFilter(GqlOrigin? origin) =>
        origin is GqlOrigin o ? EnumMapping.ToStoreFilter(o) : null;

    static OptionalSet<T> Opt<T>(Optional<T> value) =>
        value.HasValue ? new OptionalSet<T>(value.Value) : default;

    static IReadOnlyList<ExtensionPredicate>? Ext(IReadOnlyList<GqlExtensionPredicate>? items) =>
        items?.Select(e => new ExtensionPredicate { Key = e.Key, EqualsValue = e.EqualsValue }).ToList();

    static IDictionary<string, object?>? ExtDict(IReadOnlyList<GqlExtensionPredicate>? items)
    {
        if (items == null || items.Count == 0)
            return null;
        var dict = new Dictionary<string, object?>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.EqualsValue == null)
                continue;
            dict[item.Key] = item.EqualsValue;
        }
        return dict.Count == 0 ? null : dict;
    }
}
