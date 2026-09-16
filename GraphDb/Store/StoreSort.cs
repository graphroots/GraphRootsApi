using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

static class StoreSort
{
    public static readonly IReadOnlyDictionary<string, string> Documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["documentId"] = "n.DocumentId",
        ["versionId"] = "n.VersionId",
        ["fileName"] = "n.FileName",
        ["createdAt"] = "n.FileCreationTimeUtc",
    };

    public static readonly IReadOnlyDictionary<string, string> Nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["nodeId"] = "n.NodeId",
        ["versionId"] = "n.VersionId",
        ["name"] = "n.Name",
        ["typeId"] = "n.TypeId",
        ["kind"] = "n.Kind",
        ["x"] = "n.X",
        ["y"] = "n.Y",
    };

    public static readonly IReadOnlyDictionary<string, string> Ports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["portId"] = "n.PortId",
        ["versionId"] = "n.VersionId",
        ["name"] = "n.Name",
        ["direction"] = "n.Direction",
    };

    public static readonly IReadOnlyDictionary<string, string> Edges = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["versionId"] = "e.VersionId",
        ["sourcePortId"] = "e.SourcePortId",
        ["targetPortId"] = "e.TargetPortId",
    };

    public static readonly IReadOnlyDictionary<string, string> NodeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["origin"] = "n.Origin",
        ["typeId"] = "n.TypeId",
        ["name"] = "n.Name",
    };

    public static readonly IReadOnlyDictionary<string, string> Libraries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["origin"] = "n.Origin",
        ["libraryId"] = "n.LibraryId",
        ["name"] = "n.Name",
    };

    public static (string Cypher, string Direction) Resolve(IReadOnlyDictionary<string, string> map, SortSpec? sort, string fallbackField)
    {
        var field = sort?.Field;
        if (string.IsNullOrWhiteSpace(field))
            field = fallbackField;
        if (!map.TryGetValue(field, out var cypher))
            throw GraphStoreException.InvalidArgument($"Unsupported sort field '{field}'.");
        var direction = sort?.Direction == StoreSortDirection.Desc ? "DESC" : "ASC";
        return (cypher, direction);
    }

    public static string OrderBy(string primary, string primaryDir, params string[] tieBreakers)
    {
        var parts = new List<string> { $"{primary} {primaryDir}" };
        foreach (var tie in tieBreakers)
        {
            if (!string.Equals(tie, primary, StringComparison.Ordinal))
                parts.Add($"{tie} ASC");
        }
        return string.Join(", ", parts);
    }
}
