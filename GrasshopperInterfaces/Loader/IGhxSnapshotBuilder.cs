using System.Threading;
using GraphRoots.GraphDb;

namespace GraphRoots.Grasshopper
{
    public interface IGhxSnapshotBuilder
    {
        ImportSnapshot Build(IGhxLoaderContext context, CancellationToken cancellationToken = default);
    }
}
