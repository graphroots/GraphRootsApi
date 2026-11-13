using System;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace GraphRoots.GraphDb.Neo4j;

public static class Neo4jServiceCollectionExtensions
{
    /// <summary>
    /// Register the Neo4j backend as singletons: <see cref="IDriver"/>, <see cref="IDbOperations"/>,
    /// <see cref="IGraphStore"/> (<see cref="Neo4jGraphStore"/>), and <see cref="IGraphStoreAdmin"/>.
    /// The driver is disposed with the service provider.
    /// </summary>
    public static IServiceCollection AddNeo4jGraphStore(this IServiceCollection services, Neo4jStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                $"{Neo4jStoreOptions.PasswordKey} is required. Set it via user secrets or the {Neo4jStoreOptions.PasswordKey} environment variable.");
        }

        services.AddSingleton(options);
        services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(options.Uri, AuthTokens.Basic(options.User, options.Password)));
        services.AddSingleton<IDbOperations>(sp => new DbOperations(sp.GetRequiredService<IDriver>(), options.Database));
        services.AddSingleton<IGraphStore>(sp => new Neo4jGraphStore(sp.GetRequiredService<IDbOperations>()));
        services.AddSingleton<IGraphStoreAdmin>(sp => new Neo4jGraphStoreAdmin(sp.GetRequiredService<IDbOperations>(), options.Target));
        return services;
    }
}
