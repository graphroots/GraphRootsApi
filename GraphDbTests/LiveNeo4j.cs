using System;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GraphRoots.GraphDb.Neo4j;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;

namespace GraphRoots.GraphDbTests;

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
    /// Open the isolated test database and ensure its schema. Inconclusive when the live settings are missing.
    /// </summary>
    public static async Task<Neo4jTestBackend> Start()
    {
        var settings = Require();
        var driver = GraphDatabase.Driver(settings.Uri, AuthTokens.Basic(settings.User, settings.Password));
        var db = new DbOperations(driver, settings.Database);
        var backend = new Neo4jTestBackend(
            driver,
            db,
            new Neo4jGraphStore(db),
            new Neo4jGraphStoreAdmin(db, $"{settings.Uri} database {settings.Database}"));
        await backend.Admin.Initialize();
        return backend;
    }
}

sealed class Neo4jTestBackend(IDriver driver, IDbOperations db, IGraphStore store, IGraphStoreAdmin admin) : IAsyncDisposable
{
    public IDbOperations Db { get; } = db;

    public IGraphStore Store { get; } = store;

    public IGraphStoreAdmin Admin { get; } = admin;

    public ValueTask DisposeAsync() => driver.DisposeAsync();
}
