using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraphRoots.GraphDb;

public interface IDbOperations
{
    /// <summary>
    /// Runs <paramref name="action"/> inside a single write transaction.
    /// Nested calls reuse the current transaction.
    /// </summary>
    Task ExecuteWrite(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a node in the database.
    /// </summary>
    Task CreateNode<T>(T node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge (update) a node in the database.
    /// Nodes are identified by the properties marked with <see cref="DbEqualityCheckAttribute"/>.
    /// </summary>
    Task MergeNode<T>(T node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create nodes in the database.
    /// </summary>
    Task CreateNodes<T>(IEnumerable<T> node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge (update) nodes in the database.
    /// Nodes are identified by the properties marked with <see cref="DbEqualityCheckAttribute"/>.
    /// </summary>
    Task MergeNodes<T>(IEnumerable<T> node, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge (create or update) a relationship between two nodes in the database.
    /// Relationships are identified by the properties marked with <see cref="DbEqualityCheckAttribute"/>.
    /// </summary>
    Task MergeRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge (create or update) relationships between two nodes in the database.
    /// Relationships are identified by the properties marked with <see cref="DbEqualityCheckAttribute"/>.
    /// </summary>
    Task MergeRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a node by its equality-check properties, or <c>null</c> if missing.
    /// </summary>
    Task<T?> GetNode<T>(T key, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Get nodes by their equality-check properties. Missing keys are omitted.
    /// </summary>
    Task<List<T>> GetNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// Delete a node and its relationships (<c>DETACH DELETE</c>).
    /// </summary>
    Task DeleteNode<T>(T key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete nodes and their relationships (<c>DETACH DELETE</c>).
    /// </summary>
    Task DeleteNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a relationship identified by endpoint equality keys and relationship equality keys.
    /// </summary>
    Task DeleteRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete relationships identified by endpoint equality keys and relationship equality keys.
    /// </summary>
    Task DeleteRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge the entire database.
    /// </summary>
    Task PurgeDatabase(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create indices for node properties according to attributes.
    /// </summary>
    Task CreateNodeIndices<Tnode>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm a query can run against the configured database.
    /// Do not use the driver's default connectivity check, which always
    /// targets the server default database (<c>neo4j</c>).
    /// </summary>
    Task VerifyConnectivity(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drop leftover unscoped index names, create uniqueness / node-key constraints,
    /// and ensure label-scoped range indexes. Safe to call repeatedly.
    /// </summary>
    Task EnsureSchema(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the given query, fetches records asynchronously, tries to convert them, and 
    /// calls the given action for each of them. Use this if you expect a large number of results.
    /// Throws an <see cref="InvalidOperationException"/> in case the mapping fails.
    /// </summary>
    Task RunQuery<Trecord>(string query, object? parameters, Func<Trecord, Task> func, CancellationToken cancellationToken = default)
        where Trecord : new();

    /// <summary>
    /// Runs the given query, fetches all results, and tries to map them to the given type. 
    /// Throws an <see cref="InvalidOperationException"/> in case the mapping fails.
    /// </summary>
    Task<List<Trecord>> RunQuery<Trecord>(string query, object? parameters = null, CancellationToken cancellationToken = default)
        where Trecord : new();

    /// <summary>
    /// Runs a read query and yields each record without a fixed CLR shape.
    /// </summary>
    Task QueryRecords(string query, object? parameters, Func<IQueryRecord, Task> func, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single Cypher record with typed node and relationship mapping.
/// </summary>
public interface IQueryRecord
{
    bool Contains(string key);

    T? Node<T>(string key) where T : class, new();

    T? Relationship<T>(string key) where T : class, new();

    IReadOnlyList<T> Nodes<T>(string key) where T : class, new();

    IReadOnlyList<T> Relationships<T>(string key) where T : class, new();

    long? Int64(string key);

    object? this[string key] { get; }
}

/// <summary>
/// Minimal interface representing a node in the graph database.
/// </summary>
public interface IGraphDbNode
{
    /// <summary>
    /// The unique element ID of the node.
    /// </summary>
    string? ElementId { get; }

    /// <summary>
    /// The labels assigned to the node.
    /// </summary>
    IReadOnlyList<string>? Labels { get; }
}

/// <summary>
/// Minimal interface representing a relationship in the graph database.
/// </summary>
public interface IGraphDbRelationship
{
    /// <summary>
    /// The unique element ID of the relationship.
    /// </summary>
    string? ElementId { get; }
    /// <summary>
    /// The type of the relationship.
    /// </summary>
    string? Type { get; }
    /// <summary>
    /// The unique element ID of the start node.
    /// </summary>
    string? StartNodeElementId { get; }
    /// <summary>
    /// The unique element ID of the end node.
    /// </summary>
    string? EndNodeElementId { get; }
}

/// <summary>
/// Interface representing a path in the graph database.
/// </summary>
public interface IGraphDbPath<Tnode, Trelationship>
{
    /// <summary>
    /// The start node of the path.
    /// </summary>
    Tnode Start { get; }

    /// <summary>
    /// The end node of the path.
    /// </summary>
    Tnode End { get; }

    /// <summary>
    /// The nodes in the path.
    /// </summary>
    IReadOnlyList<Tnode> Nodes { get; }

    /// <summary>
    /// The relationships in the path.
    /// </summary>
    IReadOnlyList<Trelationship> Relationships { get; }
}
