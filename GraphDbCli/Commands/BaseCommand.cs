using System.Collections.Generic;
using CommandLine;
using GraphRoots.GraphDb.Neo4j;
using Microsoft.Extensions.Configuration;

namespace GraphRoots.GraphDbCli
{
    /// <summary>
    /// Base functionality for commands (storage backend settings, etc)
    /// </summary>
    class BaseCommand
    {
        [Option("neo4j-user", HelpText = "Neo4j username (user secrets or NEO4J_USER if not specified)")]
        public string? Neo4jUser { get; set; }

        [Option("neo4j-host", HelpText = "Neo4j URI (user secrets or NEO4J_URI if not specified, default: neo4j://127.0.0.1)")]
        public string? Neo4jHost { get; set; }

        [Option("neo4j-database", HelpText = "Neo4j database name (user secrets or NEO4J_DATABASE if not specified, default: GraphRoots)")]
        public string? Neo4jDatabase { get; set; }

        /// <summary>
        /// Storage backend settings: user secrets and environment variables, overridden by command-line options.
        /// The backend is selected by <c>GRAPHROOTS_STORE</c> (default <c>neo4j</c>). Passwords come only from
        /// user secrets or the environment, never from a flag.
        /// </summary>
        protected IConfiguration StoreConfiguration()
        {
            var overrides = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(Neo4jHost))
                overrides[Neo4jStoreOptions.UriKey] = Neo4jHost;
            if (!string.IsNullOrWhiteSpace(Neo4jUser))
                overrides[Neo4jStoreOptions.UserKey] = Neo4jUser;
            if (!string.IsNullOrWhiteSpace(Neo4jDatabase))
                overrides[Neo4jStoreOptions.DatabaseKey] = Neo4jDatabase;

            return new ConfigurationBuilder()
                .AddConfiguration(CliServices.Configuration)
                .AddInMemoryCollection(overrides)
                .Build();
        }
    }
}
