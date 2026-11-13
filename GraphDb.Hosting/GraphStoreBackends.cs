using System;
using System.Collections.Generic;
using GraphRoots.GraphDb.Neo4j;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRoots.GraphDb.Hosting;

/// <summary>
/// Chooses the storage backend from the <c>GRAPHROOTS_STORE</c> setting (default <c>neo4j</c>).
/// This is the only code that knows every backend; hosts call it and then talk to
/// <see cref="IGraphStore"/> and <see cref="IGraphStoreAdmin"/> only.
/// </summary>
public static class GraphStoreBackends
{
    public const string ConfigurationKey = "GRAPHROOTS_STORE";

    public const string Neo4jBackend = "neo4j";

    public static IReadOnlyList<string> Supported { get; } = [Neo4jBackend];

    /// <summary>The configured backend name (lowercase), falling back to the environment, then <c>neo4j</c>.</summary>
    public static string Selected(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            value = Environment.GetEnvironmentVariable(ConfigurationKey);
        return string.IsNullOrWhiteSpace(value) ? Neo4jBackend : value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Register <see cref="IGraphStore"/> and <see cref="IGraphStoreAdmin"/> for the selected backend.
    /// Throws when the backend is unknown or its required settings (secrets) are missing.
    /// </summary>
    public static IServiceCollection AddConfiguredGraphStore(this IServiceCollection services, IConfiguration configuration)
    {
        var backend = Selected(configuration);
        return backend switch
        {
            Neo4jBackend => services.AddNeo4jGraphStore(Neo4jStoreOptions.FromConfiguration(configuration)),
            _ => throw Unknown(backend),
        };
    }

    /// <summary>
    /// Where the selected backend keeps its data, without connecting or requiring secrets
    /// (for messages printed before destructive operations).
    /// </summary>
    public static string DescribeTarget(IConfiguration configuration)
    {
        var backend = Selected(configuration);
        return backend switch
        {
            Neo4jBackend => Neo4jStoreOptions.FromConfiguration(configuration).Target,
            _ => throw Unknown(backend),
        };
    }

    static InvalidOperationException Unknown(string backend) =>
        new($"Unknown {ConfigurationKey} '{backend}'. Supported: {string.Join(", ", Supported)}.");
}
