using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb.Neo4j;

/// <summary>
/// Neo4j lifecycle: connectivity check plus <see cref="IDbOperations.EnsureSchema"/>, and <c>DETACH DELETE</c> purge.
/// </summary>
public sealed class Neo4jGraphStoreAdmin : IGraphStoreAdmin
{
    readonly IDbOperations _db;

    public Neo4jGraphStoreAdmin(IDbOperations db, string target)
    {
        _db = db;
        Target = target;
    }

    public string Target { get; }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        await _db.VerifyConnectivity(cancellationToken);
        await _db.EnsureSchema(cancellationToken);
    }

    public Task Purge(CancellationToken cancellationToken = default) => _db.PurgeDatabase(cancellationToken);
}
