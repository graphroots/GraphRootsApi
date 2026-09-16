using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// An input or output of a <see cref="Node"/>. Floating GH parameters use a
/// synthetic port whose <see cref="PortId"/> equals the node instance id.
/// </summary>
[DbLabel("Port")]
[DbSchemaVersion(2)]
public class Port
{
    [DbEqualityCheck]
    public Guid VersionId { get; set; }

    [DbEqualityCheck]
    public string PortId { get; set; } = "";

    [DbSerialize]
    public string? Name { get; set; }

    /// <summary>
    /// One of <see cref="PortDirections"/> (<c>In</c>, <c>Out</c>, or <c>Both</c>).
    /// Declared GH params are <c>In</c> or <c>Out</c>. Synthetic floating-param
    /// ports are set from EDGE incidence and may be <c>Both</c>.
    /// </summary>
    [DbSerialize]
    public string? Direction { get; set; }

    /// <summary>
    /// Access or lacing: item, list, tree, or a tool-specific string.
    /// </summary>
    [DbSerialize]
    public string? Access { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
