using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// Directed connection from a source <see cref="Port"/> to a target <see cref="Port"/>.
/// </summary>
[DbLabel("EDGE")]
[DbSchemaVersion(2)]
public class Edge
{
    [DbEqualityCheck]
    public Guid VersionId { get; set; }

    [DbEqualityCheck]
    public string SourcePortId { get; set; } = "";

    [DbEqualityCheck]
    public string TargetPortId { get; set; } = "";

    [DbSerialize]
    public string? SourceName { get; set; }

    [DbSerialize]
    public string? TargetName { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
