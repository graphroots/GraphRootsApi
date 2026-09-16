using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// A specific version of a <see cref="Library"/>.
/// </summary>
[DbLabel("LibraryVersion")]
[DbSchemaVersion(2)]
public class LibraryVersion
{
    [DbEqualityCheck]
    public string Origin { get; set; } = "";

    [DbEqualityCheck]
    public string LibraryId { get; set; } = "";

    [DbEqualityCheck]
    public string Version { get; set; } = "";

    [DbEqualityCheck]
    public string? AssemblyVersion { get; set; }

    [DbSerialize]
    public string? Name { get; set; }

    [DbSerialize]
    public string? Author { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
