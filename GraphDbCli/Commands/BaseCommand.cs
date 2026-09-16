using System;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Neo4j.Driver;

namespace GraphRoots.GraphDbCli
{
    /// <summary>
    /// Base functionality for commands (authentication, etc)
    /// </summary>
    class BaseCommand
    {
        [Option("neo4j-user", HelpText = "Neo4j username (user secrets or NEO4J_USER if not specified)")]
        public string? Neo4jUser { get; set; }

        [Option("neo4j-host", HelpText = "Neo4j URI (user secrets or NEO4J_URI if not specified, default: neo4j://127.0.0.1)")]
        public string? Neo4jHost { get; set; }

        [Option("neo4j-database", HelpText = "Neo4j database name (user secrets or NEO4J_DATABASE if not specified, default: GraphRoots)")]
        public string? Neo4jDatabase { get; set; }

        static IConfiguration Config => CliServices.Configuration;

        protected string ResolveNeo4jUri()
        {
            return FirstNonEmpty(Neo4jHost, Config["NEO4J_URI"]) ?? "neo4j://127.0.0.1";
        }

        protected string ResolveNeo4jDatabase()
        {
            return FirstNonEmpty(Neo4jDatabase, Config["NEO4J_DATABASE"]) ?? "GraphRoots";
        }

        protected string ResolveNeo4jTarget()
        {
            return $"{ResolveNeo4jUri()} database {ResolveNeo4jDatabase()}";
        }

        /// <summary>
        /// Get a Neo4j driver instance. URI, user, and database can come from command-line
        /// options, user secrets, or environment variables. Password is read only from
        /// user secrets or NEO4J_PASSWORD.
        /// </summary>
        protected IDriver GetNeo4jDriver()
        {
            var uri = ResolveNeo4jUri();
            var user = FirstNonEmpty(Neo4jUser, Config["NEO4J_USER"]) ?? "neo4j";
            var password = Config["NEO4J_PASSWORD"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("NEO4J_PASSWORD is required. Set it via user secrets or the NEO4J_PASSWORD environment variable.");

            return GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }

        static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }
    }
}
