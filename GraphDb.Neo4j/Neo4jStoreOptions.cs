using System;
using Microsoft.Extensions.Configuration;

namespace GraphRoots.GraphDb.Neo4j;

/// <summary>
/// Connection settings for the Neo4j backend. Not a record on purpose: the generated
/// <c>ToString</c> would print <see cref="Password"/>.
/// </summary>
public sealed class Neo4jStoreOptions
{
    public const string UriKey = "NEO4J_URI";
    public const string UserKey = "NEO4J_USER";
    public const string PasswordKey = "NEO4J_PASSWORD";
    public const string DatabaseKey = "NEO4J_DATABASE";

    public string Uri { get; init; } = "neo4j://127.0.0.1";

    public string User { get; init; } = "neo4j";

    /// <summary>Only from user secrets or <c>NEO4J_PASSWORD</c>, never a command-line flag.</summary>
    public string? Password { get; init; }

    public string Database { get; init; } = "GraphRoots";

    /// <summary>Where the data lives, without secrets.</summary>
    public string Target => $"{Uri} database {Database}";

    /// <summary>
    /// Read <c>NEO4J_URI</c>, <c>NEO4J_USER</c>, <c>NEO4J_PASSWORD</c>, and <c>NEO4J_DATABASE</c> from configuration,
    /// falling back to environment variables, then to the defaults. Does not require the password.
    /// </summary>
    public static Neo4jStoreOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new Neo4jStoreOptions();
        return new Neo4jStoreOptions
        {
            Uri = Read(configuration, UriKey) ?? defaults.Uri,
            User = Read(configuration, UserKey) ?? defaults.User,
            Password = Read(configuration, PasswordKey),
            Database = Read(configuration, DatabaseKey) ?? defaults.Database,
        };
    }

    static string? Read(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
