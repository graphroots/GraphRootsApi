using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using Microsoft.Extensions.Hosting;

namespace GraphRoots.GraphApi;

/// <summary>
/// Checks the configured storage backend and prepares its schema before the host serves requests.
/// </summary>
public sealed class GraphStoreStartup : IHostedService
{
    readonly IGraphStoreAdmin _admin;

    public GraphStoreStartup(IGraphStoreAdmin admin)
    {
        _admin = admin;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _admin.Initialize(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
