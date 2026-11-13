using System.Threading.Tasks;
using GraphRoots.GraphDb;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

/// <summary>
/// Runs the GraphQL store tests against the live Neo4j test database.
/// </summary>
[TestClass]
public sealed class Neo4jGraphQLStoreTest : GraphQLStoreTestBase
{
    static Neo4jTestBackend? Backend;
    static ServiceProvider? Services;
    static IRequestExecutor? SharedExecutor;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        Backend = await LiveNeo4j.Start();
        (Services, SharedExecutor) = await CreateExecutor(Backend.Store);
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (Services != null)
            await Services.DisposeAsync();
        if (Backend != null)
            await Backend.DisposeAsync();
    }

    protected override IGraphStore Store => Backend!.Store;

    protected override IGraphStoreAdmin Admin => Backend!.Admin;

    protected override IRequestExecutor Executor => SharedExecutor!;
}
