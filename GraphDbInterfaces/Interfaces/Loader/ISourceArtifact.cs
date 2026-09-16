using System;

namespace GraphRoots.GraphDb
{
    /// <summary>
    /// Tool-neutral metadata about a file (or other artifact) being imported.
    /// </summary>
    public interface ISourceArtifact
    {
        /// <summary>
        /// Content-addressed version identifier.
        /// </summary>
        Guid VersionId { get; }

        string FileName { get; }

        string FilePath { get; }

        DateTime FileCreationTimeUtc { get; }

        DateTime FileLastWriteTimeUtc { get; }
    }
}
