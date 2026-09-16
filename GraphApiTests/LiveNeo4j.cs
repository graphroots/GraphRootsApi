using System;
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
}
