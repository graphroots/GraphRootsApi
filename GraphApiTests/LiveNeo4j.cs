using System;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GraphRoots.GraphDb.Neo4j;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

static class LiveNeo4j
{
    static readonly string[] ReservedDatabases = ["GraphRoots", "neo4j", "system"];

    public static (string Uri, string User, string Password, string Database) Require()
    {
        var uri = Environment.GetEnvironmentVariable("NEO4J_TEST_URI");
        var database = Environment.GetEnvironmentVariable("NEO4J_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(database))
            Assert.Inconclusive("Set NEO4J_TEST_URI and NEO4J_TEST_DATABASE to run live Neo4j tests against an isolated database.");

        foreach (var reserved in ReservedDatabases)
        {
            if (string.Equals(database, reserved, StringComparison.OrdinalIgnoreCase))
                Assert.Inconclusive($"NEO4J_TEST_DATABASE '{database}' is reserved; use a disposable test database.");
        }

        return (
            uri,
            Environment.GetEnvironmentVariable("NEO4J_TEST_USER")
                ?? Environment.GetEnvironmentVariable("NEO4J_USER")
                ?? "neo4j",
            Environment.GetEnvironmentVariable("NEO4J_TEST_PASSWORD")
                ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD")
                ?? "",
            database);
    }

    /// <summary>
    /// Register the Neo4j backend exactly as the hosts do, against the isolated test database,
    /// and ensure its schema. Inconclusive when the live settings are missing.
    /// </summary>
    public static async Task<Neo4jTestBackend> Start()
    {
        var settings = Require();
        var services = new ServiceCollection();
        services.AddNeo4jGraphStore(new Neo4jStoreOptions
        {
            Uri = settings.Uri,
            User = settings.User,
            Password = settings.Password,
            Database = settings.Database,
        });
        var backend = new Neo4jTestBackend(services.BuildServiceProvider());
        await backend.Admin.Initialize();
        return backend;
    }
}

sealed class Neo4jTestBackend(ServiceProvider services) : IAsyncDisposable
{
    public IDbOperations Db => services.GetRequiredService<IDbOperations>();

    public IGraphStore Store => services.GetRequiredService<IGraphStore>();

    public IGraphStoreAdmin Admin => services.GetRequiredService<IGraphStoreAdmin>();

    public ValueTask DisposeAsync() => services.DisposeAsync();
}
