using System;
using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// Version-control rules shared by every backend (docs/graphql/mutations.md): which documents are writable,
/// what a fork records as its parent, and which merge parents are allowed.
/// Pure functions over state the backend has loaded, ideally inside the write transaction that acts on the result.
/// They throw <see cref="GraphStoreException"/> and never touch storage.
/// </summary>
public static class DocumentLifecycle
{
    /// <summary>A mutable GraphRoots document: <c>origin == graphroots</c> and not committed.</summary>
    public static bool IsWorkingCopy(Document document) =>
        !document.Committed &&
        string.Equals(document.Origin, GraphOrigins.GraphRoots, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Throw <c>IMMUTABLE_DOCUMENT</c> unless <paramref name="document"/> is a working copy.
    /// Imported documents and commits are snapshots; they are forked, never patched.
    /// </summary>
    public static void EnsureWritable(Document document)
    {
        if (document.Committed)
        {
            throw GraphStoreException.ImmutableDocument(
                $"Document '{document.DocumentId}' version '{document.VersionId}' is committed; fork it first.");
        }
        if (!string.Equals(document.Origin, GraphOrigins.GraphRoots, StringComparison.OrdinalIgnoreCase))
        {
            throw GraphStoreException.ImmutableDocument(
                $"Document '{document.DocumentId}' origin '{document.Origin}' is immutable; fork it first.");
        }
    }

    /// <summary>Throw <c>NOT_FOUND</c> when missing and <c>IMMUTABLE_DOCUMENT</c> when not a working copy.</summary>
    public static Document RequireWorkingCopy(Document? document, string documentId, Guid versionId)
    {
        if (document == null)
            throw NotFound(documentId, versionId);
        EnsureWritable(document);
        return document;
    }

    /// <summary>Throw <c>NOT_FOUND</c> when the document is missing.</summary>
    public static Document RequireDocument(Document? document, string documentId, Guid versionId) =>
        document ?? throw NotFound(documentId, versionId);

    /// <summary>
    /// <c>BASED_ON</c> parents of a fork: the source itself when it is a snapshot, otherwise the source
    /// working copy's HEAD (a fork never points at a mutable document).
    /// </summary>
    public static IReadOnlyList<Document> ForkParents(Document source, IReadOnlyList<Document> sourceHead) =>
        IsWorkingCopy(source) ? sourceHead : [source];

    /// <summary>
    /// Resolve the <c>BASED_ON</c> parents of a merge commit: <c>distinct(HEAD ∪ otherParents)</c>, at least two.
    /// Requests are checked in order; the first violation wins.
    /// </summary>
    /// <param name="working">The working copy being merged (already checked with <see cref="RequireWorkingCopy"/>).</param>
    /// <param name="head">The working copy's current <c>BASED_ON</c> targets.</param>
    /// <param name="otherParents">Parents requested by the client.</param>
    /// <param name="resolved">The stored document for each requested key, or <c>null</c> when missing.</param>
    /// <param name="ancestors">
    /// The requested keys that are ancestors of <paramref name="working"/> at <b>any</b> depth
    /// (not limited by <see cref="StoreLimits.MaxHops"/>).
    /// </param>
    public static IReadOnlyList<Document> MergeParents(
        Document working,
        IReadOnlyList<Document> head,
        IReadOnlyList<(string DocumentId, Guid VersionId)>? otherParents,
        IReadOnlyDictionary<(string DocumentId, Guid VersionId), Document?> resolved,
        IReadOnlySet<(string DocumentId, Guid VersionId)> ancestors)
    {
        if (otherParents == null || otherParents.Count == 0)
            throw GraphStoreException.InvalidArgument("mergeDocument.otherParents must contain at least one parent.");

        var parents = new List<Document>();
        var seen = new HashSet<(string DocumentId, Guid VersionId)>();
        foreach (var parent in head)
        {
            if (seen.Add((parent.DocumentId, parent.VersionId)))
                parents.Add(parent);
        }

        foreach (var key in otherParents)
        {
            if (string.IsNullOrWhiteSpace(key.DocumentId))
                throw GraphStoreException.InvalidArgument("mergeDocument.otherParents.documentId is required.");
            if (key.DocumentId == working.DocumentId && key.VersionId == working.VersionId)
                throw GraphStoreException.InvalidArgument("mergeDocument.otherParents must not include the working copy.");
            if (ancestors.Contains(key))
                throw GraphStoreException.InvalidArgument($"Document '{key.DocumentId}' version '{key.VersionId}' is already an ancestor of the working copy.");
            if (seen.Contains(key))
                continue;
            var parent = (resolved.TryGetValue(key, out var found) ? found : null)
                ?? throw NotFound(key.DocumentId, key.VersionId);
            if (IsWorkingCopy(parent))
                throw GraphStoreException.InvalidArgument($"Document '{parent.DocumentId}' version '{parent.VersionId}' is a working copy; merge parents must be snapshots.");
            seen.Add(key);
            parents.Add(parent);
        }

        if (parents.Count < 2)
            throw GraphStoreException.InvalidArgument("mergeDocument requires at least two distinct parents (HEAD plus other parents).");
        return parents;
    }

    static GraphStoreException NotFound(string documentId, Guid versionId) =>
        GraphStoreException.NotFound($"Document '{documentId}' version '{versionId}' was not found.");
}
