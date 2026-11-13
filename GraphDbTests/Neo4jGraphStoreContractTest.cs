using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GraphRoots.GraphStoreContractTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

/// <summary>
/// Runs the backend-agnostic store contract against the live Neo4j test database.
/// </summary>
[TestClass]
public sealed class Neo4jGraphStoreContractTest : GraphStoreContractTestBase
{
    static Neo4jTestBackend? Backend;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _) => Backend = await LiveNeo4j.Start();

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (Backend != null)
            await Backend.DisposeAsync();
    }

    protected override IGraphStore Store => Backend!.Store;

    protected override IGraphStoreAdmin Admin => Backend!.Admin;
}
