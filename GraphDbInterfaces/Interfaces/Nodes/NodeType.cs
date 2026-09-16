using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// A catalog type that <see cref="Node"/> instances realize
/// (GH component GUID, Dynamo creationName).
/// </summary>
[DbLabel("NodeType")]
[DbSchemaVersion(2)]
public class NodeType
{
    [DbEqualityCheck]
    public string Origin { get; set; } = "";

    [DbEqualityCheck]
    public string TypeId { get; set; } = "";

    [DbSerialize]
    public string? Name { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
