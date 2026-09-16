using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

public sealed class ImportSnapshot
{
    public IReadOnlyList<ImportDocument> Documents { get; init; } = [];

    public IReadOnlyList<ImportNode> Nodes { get; init; } = [];

    public IReadOnlyList<ImportPort> Ports { get; init; } = [];

    public IReadOnlyList<ImportEdge> Edges { get; init; } = [];

    public IReadOnlyList<ImportGroupMembership> GroupMemberships { get; init; } = [];

    public IReadOnlyList<ImportNest> Nests { get; init; } = [];

    public IReadOnlyList<ImportLibrary> Libraries { get; init; } = [];

    public IReadOnlyList<ImportLibraryVersion> LibraryVersions { get; init; } = [];

    public IReadOnlyList<ImportNodeType> NodeTypes { get; init; } = [];

    public IReadOnlyList<ImportUsedBy> UsedBy { get; init; } = [];
}

public sealed class ImportDocument
{
    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }

    public string Origin { get; init; } = "";

    public string? FileName { get; init; }

    public string? FilePath { get; init; }

    public bool? IsNested { get; init; }

    public DateTime? FileCreationTimeUtc { get; init; }

    public DateTime? FileLastWriteTimeUtc { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportNode
{
    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }

    public string NodeId { get; init; } = "";

    public string Origin { get; init; } = "";

    public string? TypeId { get; init; }

    public string? Name { get; init; }

    public string? NickName { get; init; }

    public string? Kind { get; init; }

    public bool? Locked { get; init; }

    public float? X { get; init; }

    public float? Y { get; init; }

    public string? Source { get; init; }

    public string? Language { get; init; }

    public string? Text { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportPort
{
    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }

    public string NodeId { get; init; } = "";

    public string PortId { get; init; } = "";

    public string? Name { get; init; }

    public string? Direction { get; set; }

    public string? Access { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportEdge
{
    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }

    public string SourcePortId { get; init; } = "";

    public string TargetPortId { get; init; } = "";

    public string? SourceName { get; init; }

    public string? TargetName { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportGroupMembership
{
    public Guid VersionId { get; init; }

    public string MemberNodeId { get; init; } = "";

    public string GroupNodeId { get; init; } = "";
}

public sealed class ImportNest
{
    public string ParentDocumentId { get; init; } = "";

    public Guid ParentVersionId { get; init; }

    public string ChildDocumentId { get; init; } = "";

    public Guid ChildVersionId { get; init; }
}

public sealed class ImportLibrary
{
    public string Origin { get; init; } = "";

    public string LibraryId { get; init; } = "";

    public string? Name { get; init; }

    public string? Author { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportLibraryVersion
{
    public string Origin { get; init; } = "";

    public string LibraryId { get; init; } = "";

    public string Version { get; init; } = "";

    public string? AssemblyVersion { get; init; }

    public string? Name { get; init; }

    public string? Author { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportNodeType
{
    public string Origin { get; init; } = "";

    public string TypeId { get; init; } = "";

    public string? Name { get; init; }

    public string? LibraryId { get; init; }

    public string? Version { get; init; }

    public string? AssemblyVersion { get; init; }

    public IDictionary<string, object?>? Extensions { get; init; }
}

public sealed class ImportUsedBy
{
    public string Origin { get; init; } = "";

    public string LibraryId { get; init; } = "";

    public string Version { get; init; } = "";

    public string? AssemblyVersion { get; init; }

    public string DocumentId { get; init; } = "";

    public Guid VersionId { get; init; }
}

public sealed class ImportSnapshotResult
{
    public Document Document { get; init; } = new();

    public int NestedDocumentCount { get; init; }

    public int NodeCount { get; init; }

    public int PortCount { get; init; }

    public int EdgeCount { get; init; }
}
