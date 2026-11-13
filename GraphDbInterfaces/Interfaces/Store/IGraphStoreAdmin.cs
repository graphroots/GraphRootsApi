using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

/// <summary>
/// Backend lifecycle operations that sit outside the GraphQL contract.
/// Every storage backend registers one next to its <see cref="IGraphStore"/>.
/// </summary>
public interface IGraphStoreAdmin
{
    /// <summary>
    /// Human-readable description of where the data lives (for example URI and database),
    /// printed before destructive operations. Must not contain secrets.
    /// </summary>
    string Target { get; }

    /// <summary>
    /// Confirm the backend is reachable and create its schema (constraints, indexes, buckets, tables).
    /// Idempotent; called once at host startup and before live tests.
    /// </summary>
    Task Initialize(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all stored data. Destructive: used by tests and the CLI <c>--purge --yes</c>.
    /// </summary>
    Task Purge(CancellationToken cancellationToken = default);
}
