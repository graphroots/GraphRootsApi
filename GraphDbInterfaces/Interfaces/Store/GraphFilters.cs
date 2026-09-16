using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

public sealed class ExtensionPredicate
{
    public string Key { get; init; } = "";

    public object? EqualsValue { get; init; }
}

public sealed class BoundingBox
{
    public float MinX { get; init; }

    public float MinY { get; init; }

    public float MaxX { get; init; }

    public float MaxY { get; init; }
}

public sealed class DocumentFilter
{
    public string? Origin { get; init; }

    public bool? IsNested { get; init; }

    public bool? Committed { get; init; }

    public string? FileNameContains { get; init; }

    public string? DocumentId { get; init; }

    public Guid? VersionId { get; init; }

    public string? LibraryId { get; init; }

    public string? LibraryVersion { get; init; }

    public string? LibraryAssemblyVersion { get; init; }

    public DateTime? CreatedAfterUtc { get; init; }

    public DateTime? CreatedBeforeUtc { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class NodeFilter
{
    public Guid? VersionId { get; init; }

    public string? DocumentId { get; init; }

    public string? Origin { get; init; }

    public string? Kind { get; init; }

    public string? TypeId { get; init; }

    public string? Name { get; init; }

    public string? NickName { get; init; }

    public bool? Locked { get; init; }

    public string? Language { get; init; }

    public bool? HasSource { get; init; }

    public BoundingBox? Bbox { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class PortFilter
{
    public Guid? VersionId { get; init; }

    public string? NodeId { get; init; }

    public string? Name { get; init; }

    public string? Direction { get; init; }

    public string? Access { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class EdgeFilter
{
    public Guid? VersionId { get; init; }

    public string? SourcePortId { get; init; }

    public string? TargetPortId { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class NodeTypeFilter
{
    public string? Origin { get; init; }

    public string? TypeId { get; init; }

    public string? NameContains { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}

public sealed class LibraryFilter
{
    public string? Origin { get; init; }

    public string? LibraryId { get; init; }

    public string? NameContains { get; init; }

    public IReadOnlyList<ExtensionPredicate>? Extensions { get; init; }
}
