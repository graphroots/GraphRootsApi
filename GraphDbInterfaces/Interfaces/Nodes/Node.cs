using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// An operator, parameter, group, annotation, or cluster reference in a <see cref="Document"/>.
/// </summary>
[DbLabel("Node")]
[DbSchemaVersion(2)]
public class Node
{
    [DbEqualityCheck]
    public Guid VersionId { get; set; }

    /// <summary>
    /// Instance identity in the source tool (GH instance GUID, Dynamo node id).
    /// </summary>
    [DbEqualityCheck]
    public string NodeId { get; set; } = "";

    [DbSerialize]
    public string Origin { get; set; } = "";

    /// <summary>
    /// Catalog type key, same as <see cref="NodeType.TypeId"/>
    /// (GH component GUID, Dynamo creationName).
    /// </summary>
    [DbSerialize]
    public string? TypeId { get; set; }

    [DbSerialize]
    public string? Name { get; set; }

    [DbSerialize]
    public string? NickName { get; set; }

    /// <summary>
    /// One of <see cref="NodeKinds"/>.
    /// </summary>
    [DbSerialize]
    public string? Kind { get; set; }

    [DbSerialize]
    public bool? Locked { get; set; }

    [DbSerialize]
    public float? X { get; set; }

    [DbSerialize]
    public float? Y { get; set; }

    /// <summary>
    /// Executable or display text (script body, code block). Scribble text uses <see cref="Text"/>.
    /// </summary>
    [DbSerialize]
    public string? Source { get; set; }

    [DbSerialize]
    public string? Language { get; set; }

    [DbSerialize]
    public string? Text { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
