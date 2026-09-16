using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// A plugin or package identity (GH GHA, Dynamo package).
/// </summary>
[DbLabel("Library")]
[DbSchemaVersion(2)]
public class Library
{
    [DbEqualityCheck]
    public string Origin { get; set; } = "";

    [DbEqualityCheck]
    public string LibraryId { get; set; } = "";

    [DbSerialize]
    public string? Name { get; set; }

    [DbSerialize]
    public string? Author { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
