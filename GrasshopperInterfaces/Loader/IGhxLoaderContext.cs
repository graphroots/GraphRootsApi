using GraphRoots.GraphDb;
using GraphRoots.Grasshopper.Parser;

namespace GraphRoots.Grasshopper
{
    /// <summary>
    /// Grasshopper-specific import context: source artifact plus the parsed archive.
    /// </summary>
    public interface IGhxLoaderContext : ISourceArtifact
    {
        IGhxArchive GhxArchive { get; }
    }
}
