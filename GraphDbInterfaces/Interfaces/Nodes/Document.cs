using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// A versioned computational graph: a Grasshopper document or cluster,
/// a Dynamo workspace, or a future solver graph.
/// </summary>
[DbLabel("Document")]
[DbSchemaVersion(2)]
public class Document
{
    /// <summary>
    /// Stable document identity from the source tool (GH DocumentID, Dynamo workspace id).
    /// </summary>
    [DbEqualityCheck]
    public string DocumentId { get; set; } = "";

    /// <summary>
    /// Content-addressed version (UUID5 of file or nested-graph bytes).
    /// </summary>
    [DbEqualityCheck]
    public Guid VersionId { get; set; }

    [DbSerialize]
    public string Origin { get; set; } = "";

    [DbSerialize]
    public string? FileName { get; set; }

    [DbSerialize]
    public string? FilePath { get; set; }

    /// <summary>
    /// True when this document is nested (GH cluster, Dynamo custom node).
    /// </summary>
    [DbSerialize]
    public bool? IsNested { get; set; }

    [DbSerialize]
    public DateTime? FileCreationTimeUtc { get; set; }

    [DbSerialize]
    public DateTime? FileLastWriteTimeUtc { get; set; }

    /// <summary>
    /// True when this node is a frozen GraphRoots snapshot (a commit or merge).
    /// Imported documents are snapshots via <see cref="Origin"/> even when this is false.
    /// </summary>
    [DbSerialize]
    public bool Committed { get; set; }

    public IDictionary<string, object?>? Extensions { get; set; }
}
