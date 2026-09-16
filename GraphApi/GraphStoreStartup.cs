using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using Microsoft.Extensions.Hosting;

namespace GraphRoots.GraphApi;

public sealed class GraphStoreStartup : IHostedService
{
    readonly IDbOperations _db;

    public GraphStoreStartup(IDbOperations db)
    {
        _db = db;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _db.VerifyConnectivity(cancellationToken);
        await _db.EnsureSchema(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
