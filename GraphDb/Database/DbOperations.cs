using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace GraphRoots.GraphDb;

public class DbOperations : IDbOperations
{
    public DbOperations(IDriver driver, string? database = null)
    {
        Driver = driver;
        Database = database;
    }

    IDriver Driver { get; }

    string? Database { get; }

    IAsyncSession OpenSession()
    {
        return string.IsNullOrEmpty(Database)
            ? Driver.AsyncSession()
            : Driver.AsyncSession(o => o.WithDatabase(Database));
    }

    static readonly AsyncLocal<IAsyncQueryRunner?> CurrentWriter = new();

    enum PropertyType
    {
        Equality = 1,
        Serialize = 2,
        All = 3,
    }

    Dictionary<(Type, Type), Func<object, object>> ValueSetters = new Dictionary<(Type, Type), Func<object, object>>()
    {
        { (typeof(string), typeof(Guid)), (obj) => Guid.Parse((string)obj) },
        { (typeof(double), typeof(double?)), (obj) => obj },
        { (typeof(double), typeof(float?)), (obj) => Convert.ToSingle(obj) },
        { (typeof(double), typeof(float)), (obj) => Convert.ToSingle(obj) },
        { (typeof(long), typeof(long?)), (obj) => obj },
        { (typeof(long), typeof(int?)), (obj) => Convert.ToInt32(obj) },
        { (typeof(long), typeof(int)), (obj) => Convert.ToInt32(obj) },
        { (typeof(bool), typeof(bool?)), (obj) => obj },
        { (typeof(ZonedDateTime), typeof(DateTime)), obj => DbValueConversion.FromZonedDateTime(obj) },
        { (typeof(ZonedDateTime), typeof(DateTime?)), obj => DbValueConversion.FromZonedDateTime(obj) },
        { (typeof(LocalDateTime), typeof(DateTime)), obj => DbValueConversion.FromLocalDateTime(obj) },
        { (typeof(LocalDateTime), typeof(DateTime?)), obj => DbValueConversion.FromLocalDateTime(obj) },
    };

    static string GetLabel(Type type)
    {
        var attr = type.GetCustomAttribute<DbLabelAttribute>(false);
        if (attr == null)
            throw new InvalidOperationException($"Type {type.FullName} is missing [DbLabel].");
        return attr.Name;
    }

    static int GetSchemaVersion(Type type)
    {
        var attr = type.GetCustomAttribute<DbSchemaVersionAttribute>(false);
        if (attr == null)
            throw new InvalidOperationException($"Type {type.FullName} is missing [DbSchemaVersion].");
        return attr.Version;
    }

    /// <summary>
    /// Get all names of properties for which 
    /// the <see cref="DbEqualityCheckAttribute"/> is defined, 
    /// or, if matchAll is true, the <see cref="DbSerializeAttribute"/>. 
    /// </summary>
    static string[] GetPropertyNames(Type nodeType, PropertyType propertyType)
    {
        var propertyInfos = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                   (((propertyType & PropertyType.Equality) == PropertyType.Equality) && property.IsDefined(typeof(DbEqualityCheckAttribute), false))
                || (((propertyType & PropertyType.Serialize) == PropertyType.Serialize) && property.IsDefined(typeof(DbSerializeAttribute), false))
            );
        return propertyInfos.Select(property => property.Name).ToArray();
    }

    string MakeNodeExpression(Type nodeType, string[] propertyNames, string prefix = "$", string varname = "")
    {
        var propsList = propertyNames.Select(name => $"{name}: {prefix}{name}");
        var label = GetLabel(nodeType);
        var props = string.Join(", ", propsList);
        return $"({varname}:{label} {{{props}}})";
    }

    string MakeRelationshipExpression(Type relType, string[] propertyNames, string? prefix = null, string varname = "")
    {
        var propsList = propertyNames.Select(name => $"{name}: {prefix}{name}");
        var label = GetLabel(relType);
        var props = string.Join(", ", propsList);
        var rel = string.IsNullOrEmpty(varname) ? $":{label}" : $"{varname}:{label}";
        return $"[{rel} {{{props}}}]";
    }

    IDictionary<string, object> MakeParameterDictionary(object node, string[] propertyNames, string prefix = "")
    {
        var nodeType = node.GetType();
        var parameters = new Dictionary<string, object>();
        foreach (var propertyName in propertyNames)
        {
            var propertyInfo = nodeType.GetProperty(propertyName);
            if (propertyInfo != null)
            {
                var value = propertyInfo.GetValue(node);
                if (value is Guid guid)
                    parameters.Add($"{prefix}{propertyName}", guid.ToString());
                else
                    parameters.Add($"{prefix}{propertyName}", value!);
            }
        }
        return parameters;
    }

    IDictionary<string, object> MakeSetDictionary(object node, Type nodeType, string[] propertyNamesSerialize)
    {
        var parametersSet = MakeParameterDictionary(node, propertyNamesSerialize);
        parametersSet["schemaVersion"] = GetSchemaVersion(nodeType);
        var extensions = GetExtensions(node);
        DbExtensionProperties.ValidateAndFlatten(extensions, CoreNames(nodeType), parametersSet);
        return parametersSet;
    }

    static IDictionary<string, object?>? GetExtensions(object node)
    {
        var property = node.GetType().GetProperty(DbExtensionProperties.PropertyName);
        return property?.GetValue(node) as IDictionary<string, object?>;
    }

    static readonly ConcurrentDictionary<Type, HashSet<string>> CoreNameCache = new();

    static HashSet<string> CoreNames(Type type)
    {
        return CoreNameCache.GetOrAdd(type, DbExtensionProperties.CorePropertyNames);
    }

    public async Task ExecuteWrite(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentWriter.Value != null)
        {
            await action(cancellationToken);
            return;
        }

        await using var session = OpenSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            CurrentWriter.Value = tx;
            try
            {
                await action(cancellationToken);
            }
            finally
            {
                CurrentWriter.Value = null;
            }
        });
    }

    async Task RunWrite(string query, object parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentWriter.Value != null)
        {
            await CurrentWriter.Value.RunAsync(query, parameters);
            return;
        }

        await using var session = OpenSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(query, parameters);
        });
    }

    public Task CreateNode<T>(T node, CancellationToken cancellationToken = default)
    {
        var nodeType = typeof(T);
        var propertyNames = GetPropertyNames(nodeType, PropertyType.All);
        var parameters = MakeSetDictionary(node!, nodeType, propertyNames);
        var label = GetLabel(nodeType);
        var query = $"CREATE (n:{label}) SET n += $set";
        return RunWrite(query, new { set = parameters }, cancellationToken);
    }

    public Task MergeNode<T>(T node, CancellationToken cancellationToken = default)
    {
        var nodeType = typeof(T);
        var propertyNamesEquality = GetPropertyNames(nodeType, PropertyType.Equality);
        var propertyNamesSerialize = GetPropertyNames(nodeType, PropertyType.Serialize);
        var query = $@"MERGE {MakeNodeExpression(nodeType, propertyNamesEquality, "$match.", "n")} 
ON CREATE SET n += $set, n.createdAt = datetime()
ON MATCH SET n += $set, n.updatedAt = datetime()
RETURN n";
        var parametersMatch = MakeParameterDictionary(node!, propertyNamesEquality);
        var parametersSet = MakeSetDictionary(node!, nodeType, propertyNamesSerialize);
        return RunWrite(query, new { match = parametersMatch, set = parametersSet }, cancellationToken);
    }

    public Task CreateNodes<T>(IEnumerable<T> nodes, CancellationToken cancellationToken = default)
    {
        var nodeType = typeof(T);
        var propertyNames = GetPropertyNames(nodeType, PropertyType.All);
        var label = GetLabel(nodeType);
        var query = $"UNWIND $nodes as node CREATE (n:{label}) SET n += node";
        var parameters = nodes.Select(node => MakeSetDictionary(node!, nodeType, propertyNames));
        return RunWrite(query, new { nodes = parameters }, cancellationToken);
    }

    public Task MergeNodes<T>(IEnumerable<T> nodes, CancellationToken cancellationToken = default)
    {
        var nodeType = typeof(T);
        var propertyNamesEquality = GetPropertyNames(nodeType, PropertyType.Equality);
        var propertyNamesSerialize = GetPropertyNames(nodeType, PropertyType.Serialize);
        var query = $@"UNWIND $nodes as node MERGE {MakeNodeExpression(nodeType, propertyNamesEquality, "node.match.", "n")}
ON CREATE SET n += node.set, n.createdAt = datetime()
ON MATCH SET n += node.set, n.updatedAt = datetime()
RETURN n";
        var parameters = nodes.Select(node =>
        {
            var parametersMatch = MakeParameterDictionary(node!, propertyNamesEquality);
            var parametersSet = MakeSetDictionary(node!, nodeType, propertyNamesSerialize);
            return new { match = parametersMatch, set = parametersSet };
        });
        return RunWrite(query, new { nodes = parameters }, cancellationToken);
    }

    public Task MergeRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default)
    {
        return MergeRelationships(new[] { Tuple.Create(nodeFrom, nodeTo, relationship) }, cancellationToken);
    }

    public Task MergeRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default)
    {
        var nodeFromType = typeof(TNodeFrom);
        var nodeFromPropertyNames = GetPropertyNames(nodeFromType, PropertyType.Equality);
        var nodeToType = typeof(TNodeTo);
        var nodeToPropertyNames = GetPropertyNames(nodeToType, PropertyType.Equality);
        var relationshipType = typeof(TRelationship);
        var relationshipEqualityNames = GetPropertyNames(relationshipType, PropertyType.Equality);
        var relationshipSerializeNames = GetPropertyNames(relationshipType, PropertyType.Serialize);

        var query = $@"
UNWIND $rels as rel
MATCH {MakeNodeExpression(nodeFromType, nodeFromPropertyNames, "rel.from.", "n")}
MATCH {MakeNodeExpression(nodeToType, nodeToPropertyNames, "rel.to.", "m")}
MERGE (n)-{MakeRelationshipExpression(relationshipType, relationshipEqualityNames, "rel.match.", "r")}->(m)
ON CREATE SET r += rel.set, r.createdAt = datetime()
ON MATCH SET r += rel.set, r.updatedAt = datetime()
        ";

        var parameters = relationships.Select(rel => new
        {
            from = MakeParameterDictionary(rel.Item1!, nodeFromPropertyNames),
            to = MakeParameterDictionary(rel.Item2!, nodeToPropertyNames),
            match = MakeParameterDictionary(rel.Item3!, relationshipEqualityNames),
            set = MakeSetDictionary(rel.Item3!, relationshipType, relationshipSerializeNames),
        });

        return RunWrite(query, new { rels = parameters }, cancellationToken);
    }

    public async Task<T?> GetNode<T>(T key, CancellationToken cancellationToken = default) where T : class, new()
    {
        var found = await GetNodes(new[] { key }, cancellationToken);
        return found.Count == 0 ? null : found[0];
    }

    public async Task<List<T>> GetNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default) where T : class, new()
    {
        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return [];

        var nodeType = typeof(T);
        var equality = GetPropertyNames(nodeType, PropertyType.Equality);
        var label = GetLabel(nodeType);
        var where = string.Join(" AND ", equality.Select(name => $"n.{name} = key.{name}"));
        var query = $"UNWIND $keys AS key MATCH (n:{label}) WHERE {where} RETURN n AS Item";
        var parameters = keyList.Select(k => MakeParameterDictionary(k!, equality)).ToList();
        var records = await RunQuery<CypherItemRecord<T>>(query, new { keys = parameters }, cancellationToken);
        return records.Where(r => r.Item != null).Select(r => r.Item!).ToList();
    }

    public Task DeleteNode<T>(T key, CancellationToken cancellationToken = default)
    {
        return DeleteNodes(new[] { key }, cancellationToken);
    }

    public Task DeleteNodes<T>(IEnumerable<T> keys, CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return Task.CompletedTask;

        var nodeType = typeof(T);
        var equality = GetPropertyNames(nodeType, PropertyType.Equality);
        var label = GetLabel(nodeType);
        var where = string.Join(" AND ", equality.Select(name => $"n.{name} = key.{name}"));
        var query = $"UNWIND $keys AS key MATCH (n:{label}) WHERE {where} DETACH DELETE n";
        var parameters = keyList.Select(k => MakeParameterDictionary(k!, equality)).ToList();
        return RunWrite(query, new { keys = parameters }, cancellationToken);
    }

    public Task DeleteRelationship<TNodeFrom, TNodeTo, TRelationship>(TNodeFrom nodeFrom, TNodeTo nodeTo, TRelationship relationship, CancellationToken cancellationToken = default)
    {
        return DeleteRelationships(new[] { Tuple.Create(nodeFrom, nodeTo, relationship) }, cancellationToken);
    }

    public Task DeleteRelationships<TNodeFrom, TNodeTo, TRelationship>(IEnumerable<Tuple<TNodeFrom, TNodeTo, TRelationship>> relationships, CancellationToken cancellationToken = default)
    {
        var relList = relationships.ToList();
        if (relList.Count == 0)
            return Task.CompletedTask;

        var nodeFromType = typeof(TNodeFrom);
        var nodeToType = typeof(TNodeTo);
        var relationshipType = typeof(TRelationship);
        var fromNames = GetPropertyNames(nodeFromType, PropertyType.Equality);
        var toNames = GetPropertyNames(nodeToType, PropertyType.Equality);
        var relNames = GetPropertyNames(relationshipType, PropertyType.Equality);
        var relLabel = GetLabel(relationshipType);
        var fromWhere = string.Join(" AND ", fromNames.Select(name => $"n.{name} = rel.from.{name}"));
        var toWhere = string.Join(" AND ", toNames.Select(name => $"m.{name} = rel.to.{name}"));
        var relWhere = relNames.Length == 0
            ? "true"
            : string.Join(" AND ", relNames.Select(name => $"r.{name} = rel.match.{name}"));
        var query = $@"
UNWIND $rels AS rel
MATCH (n:{GetLabel(nodeFromType)})-[r:{relLabel}]->(m:{GetLabel(nodeToType)})
WHERE {fromWhere} AND {toWhere} AND {relWhere}
DELETE r";
        var parameters = relList.Select(rel => new
        {
            from = MakeParameterDictionary(rel.Item1!, fromNames),
            to = MakeParameterDictionary(rel.Item2!, toNames),
            match = MakeParameterDictionary(rel.Item3!, relNames),
        });
        return RunWrite(query, new { rels = parameters }, cancellationToken);
    }

    public Task PurgeDatabase(CancellationToken cancellationToken = default)
    {
        return RunWrite("MATCH (n) DETACH DELETE n", new { }, cancellationToken);
    }

    public sealed class CypherItemRecord<T> where T : new()
    {
        public T? Item { get; set; }
    }

    public async Task CreateNodeIndices<Tnode>(IEnumerable<string> propertyNames, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodeType = typeof(Tnode);
        var label = GetLabel(nodeType);
        var indexName = $"node_range_index_{label}_{string.Join("_", propertyNames)}";
        var query = $"CREATE INDEX {indexName} IF NOT EXISTS FOR (n:{label}) ON ({string.Join(",", propertyNames.Select(n => $"n.{n}"))})";
        await RunWrite(query, new { }, cancellationToken);
    }

    public Task CreateNodeIndices<Tnode>(string propertyName, CancellationToken cancellationToken = default)
    {
        return CreateNodeIndices<Tnode>(new List<string> { propertyName }, cancellationToken);
    }

    public async Task CreateNodeIndices<Tnode>(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodeType = typeof(Tnode);
        var propertyNames = GetPropertyNames(nodeType, PropertyType.Equality);
        await CreateNodeIndices<Tnode>(propertyNames, cancellationToken);
        foreach (var prop in propertyNames)
            await CreateNodeIndices<Tnode>(prop, cancellationToken);
    }

    static readonly string[] LegacyUnscopedIndexNames =
    {
        "node_range_index_VersionId",
        "node_range_index_DocumentId",
        "node_range_index_NodeId",
        "node_range_index_PortId",
        "node_range_index_Origin",
        "node_range_index_TypeId",
        "node_range_index_LibraryId",
        "node_range_index_Version",
        "node_range_index_AssemblyVersion",
        "node_range_index_DocumentId_VersionId",
        "node_range_index_VersionId_NodeId",
        "node_range_index_VersionId_PortId",
        "node_range_index_Origin_TypeId",
        "node_range_index_Origin_LibraryId",
        "node_range_index_Origin_LibraryId_Version_AssemblyVersion",
    };

    public async Task VerifyConnectivity(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = OpenSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync("RETURN 1");
            await result.ConsumeAsync();
        });
    }

    public async Task EnsureSchema(CancellationToken cancellationToken = default)
    {
        foreach (var name in LegacyUnscopedIndexNames)
            await RunWrite($"DROP INDEX {name} IF EXISTS", new { }, cancellationToken);

        await CreateIdentityConstraint("Document", new[] { "DocumentId", "VersionId" }, cancellationToken);
        await CreateIdentityConstraint("Node", new[] { "VersionId", "NodeId" }, cancellationToken);
        await CreateIdentityConstraint("Port", new[] { "VersionId", "PortId" }, cancellationToken);
        await CreateIdentityConstraint("NodeType", new[] { "Origin", "TypeId" }, cancellationToken);
        await CreateIdentityConstraint("Library", new[] { "Origin", "LibraryId" }, cancellationToken);
        await CreateUniqueConstraint("LibraryVersion", new[] { "Origin", "LibraryId", "Version", "AssemblyVersion" }, cancellationToken);
        await CreateRelationshipUniqueConstraint("EDGE", new[] { "VersionId", "SourcePortId", "TargetPortId" }, cancellationToken);

        await CreateNodeIndices<Document>(cancellationToken);
        await CreateNodeIndices<Node>(cancellationToken);
        await CreateNodeIndices<Port>(cancellationToken);
        await CreateNodeIndices<NodeType>(cancellationToken);
        await CreateNodeIndices<Library>(cancellationToken);
        await CreateNodeIndices<LibraryVersion>(cancellationToken);
    }

    async Task CreateIdentityConstraint(string label, string[] propertyNames, CancellationToken cancellationToken)
    {
        try
        {
            await CreateNodeKeyConstraint(label, propertyNames, cancellationToken);
        }
        catch (Neo4jException ex) when (
            ex.Message.Contains("Enterprise", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            await CreateUniqueConstraint(label, propertyNames, cancellationToken);
        }
    }

    Task CreateNodeKeyConstraint(string label, string[] propertyNames, CancellationToken cancellationToken)
    {
        var props = string.Join(", ", propertyNames.Select(n => $"n.{n}"));
        var query = $"CREATE CONSTRAINT node_key_{label} IF NOT EXISTS FOR (n:{label}) REQUIRE ({props}) IS NODE KEY";
        return RunWrite(query, new { }, cancellationToken);
    }

    Task CreateUniqueConstraint(string label, string[] propertyNames, CancellationToken cancellationToken)
    {
        var props = string.Join(", ", propertyNames.Select(n => $"n.{n}"));
        var query = $"CREATE CONSTRAINT node_unique_{label} IF NOT EXISTS FOR (n:{label}) REQUIRE ({props}) IS UNIQUE";
        return RunWrite(query, new { }, cancellationToken);
    }

    Task CreateRelationshipUniqueConstraint(string type, string[] propertyNames, CancellationToken cancellationToken)
    {
        var props = string.Join(", ", propertyNames.Select(n => $"r.{n}"));
        var query = $"CREATE CONSTRAINT rel_unique_{type} IF NOT EXISTS FOR ()-[r:{type}]-() REQUIRE ({props}) IS UNIQUE";
        return RunWrite(query, new { }, cancellationToken);
    }

    object MapEntity(IEnumerable<KeyValuePair<string, object>> properties, object target, IDictionary<string, PropertyInfo> propertyDict)
    {
        foreach (var kvp in properties)
        {
            var key = kvp.Key;
            if (!propertyDict.ContainsKey(key))
                continue;
            var value = kvp.Value;
            if (value == null)
                continue;
            var propertyInfo = propertyDict[key];
            var valueType = value.GetType();
            if (propertyInfo.PropertyType.IsAssignableFrom(valueType))
                propertyDict[key].SetValue(target, value);
            else if (ValueSetters.TryGetValue((valueType, propertyInfo.PropertyType), out var setter))
                propertyDict[key].SetValue(target, setter(value));
            else if (value is INode nodeValue)
            {
                var mappedNode = MapNode(nodeValue, propertyInfo.PropertyType);
                propertyDict[key].SetValue(target, mappedNode);
            }
            else if (value is IRelationship relValue)
            {
                var mappedRel = MapRelationship(relValue, propertyInfo.PropertyType);
                propertyDict[key].SetValue(target, mappedRel);
            }
            else if (value is IPath pathValue)
            {
                var mappedPath = MapPath(pathValue, propertyInfo.PropertyType);
                propertyDict[key].SetValue(target, mappedPath);
            }
            else if (value is List<INode> nodeListValue && propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var elementType = propertyInfo.PropertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var listInstance = (IList)Activator.CreateInstance(listType)!;
                foreach (var node in nodeListValue)
                {
                    var mapped = MapNode(node, elementType);
                    listInstance.Add(mapped);
                }
                propertyDict[key].SetValue(target, listInstance);
            }
            else if (value is List<IRelationship> relListValue && propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var elementType = propertyInfo.PropertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var listInstance = (IList)Activator.CreateInstance(listType)!;
                foreach (var rel in relListValue)
                {
                    var mapped = MapRelationship(rel, elementType);
                    listInstance.Add(mapped);
                }
                propertyDict[key].SetValue(target, listInstance);
            }
            else
                throw new InvalidOperationException($"Cannot map value of type {valueType} to property {key} of type {propertyInfo.PropertyType} (consider extending the implemented ValueSetters)");
        }

        var extensionsProperty = target.GetType().GetProperty(DbExtensionProperties.PropertyName);
        if (extensionsProperty != null && extensionsProperty.CanWrite)
        {
            var rehydrated = DbExtensionProperties.Rehydrate(properties, CoreNames(target.GetType()));
            if (rehydrated.Count > 0)
                extensionsProperty.SetValue(target, rehydrated);
        }

        return target;
    }

    static readonly ConcurrentDictionary<Type, IDictionary<string, PropertyInfo>> TypePropertyCache = new();

    IDictionary<string, PropertyInfo> GetPropertyInfo(Type type)
    {
        return TypePropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f));
    }

    static readonly string NodeLabelsFieldName = nameof(INode.Labels);

    static readonly string ElementIdFieldName = nameof(IEntity.ElementId);

    object MapNode(INode node, Type type)
    {
        var nodeOut = Activator.CreateInstance(type)!;

        var furtherProps = new Dictionary<string, object>() {
            { NodeLabelsFieldName, node.Labels },
            { ElementIdFieldName, node.ElementId }
        };

        MapEntity(node.Properties.Concat(furtherProps), nodeOut, GetPropertyInfo(type));

        return nodeOut;
    }

    static readonly string RelationshipTypeName = nameof(IRelationship.Type);

    static readonly string RelationshipStartNodeElementId = nameof(IRelationship.StartNodeElementId);

    static readonly string RelationshipEndNodeElementId = nameof(IRelationship.EndNodeElementId);

    object MapRelationship(IRelationship rel, Type type)
    {
        var relOut = Activator.CreateInstance(type)!;

        var furtherProps = new Dictionary<string, object>() {
            { RelationshipTypeName, rel.Type },
            { RelationshipStartNodeElementId, rel.StartNodeElementId },
            { RelationshipEndNodeElementId, rel.EndNodeElementId },
            { ElementIdFieldName, rel.ElementId }
        };

        MapEntity(rel.Properties.Concat(furtherProps), relOut, GetPropertyInfo(type));

        return relOut;
    }

    static readonly string PathStartName = nameof(IPath.Start);

    static readonly string PathEndName = nameof(IPath.End);

    static readonly string PathNodesName = nameof(IPath.Nodes);

    static readonly string PathRelationshipsName = nameof(IPath.Relationships);

    object MapPath(IPath path, Type type)
    {
        var furtherProps = new Dictionary<string, object>() {
            { PathStartName, path.Start },
            { PathEndName, path.End },
            { PathNodesName, path.Nodes },
            { PathRelationshipsName, path.Relationships }
        };

        var pathOut = Activator.CreateInstance(type)!;

        MapEntity(furtherProps, pathOut, GetPropertyInfo(type));

        return pathOut;
    }

    Trecord MapRecord<Trecord>(IRecord record, IDictionary<string, PropertyInfo> propDict)
        where Trecord : new()
    {
        var recordOut = (object)(new Trecord());
        MapEntity(record.Values, recordOut, propDict);
        return (Trecord)recordOut;
    }

    public async Task RunQuery<Trecord>(string query, object? parameters, Func<Trecord, Task> func, CancellationToken cancellationToken = default)
        where Trecord : new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var type = typeof(Trecord);
        var propDict = GetPropertyInfo(type);

        if (CurrentWriter.Value != null)
        {
            var result = await CurrentWriter.Value.RunAsync(query, parameters);
            while (await result.FetchAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await func(MapRecord<Trecord>(result.Current, propDict));
            }
            return;
        }

        await using var session = OpenSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync(query, parameters);
            while (await result.FetchAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await func(MapRecord<Trecord>(result.Current, propDict));
            }
        });
    }

    public async Task<List<Trecord>> RunQuery<Trecord>(string query, object? parameters = null, CancellationToken cancellationToken = default)
            where Trecord : new()
    {
        var records = new List<Trecord>();
        await RunQuery<Trecord>(query, parameters, (record) =>
        {
            records.Add(record);
            return Task.CompletedTask;
        }, cancellationToken);

        return records;
    }

    public async Task QueryRecords(string query, object? parameters, Func<IQueryRecord, Task> func, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentWriter.Value != null)
        {
            var result = await CurrentWriter.Value.RunAsync(query, parameters);
            while (await result.FetchAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await func(new QueryRecord(result.Current, this));
            }
            return;
        }

        await using var session = OpenSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var result = await tx.RunAsync(query, parameters);
            while (await result.FetchAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await func(new QueryRecord(result.Current, this));
            }
        });
    }

    internal T MapTo<T>(INode node) where T : class, new()
    {
        return (T)MapNode(node, typeof(T));
    }

    internal T MapTo<T>(IRelationship rel) where T : class, new()
    {
        return (T)MapRelationship(rel, typeof(T));
    }

    sealed class QueryRecord : IQueryRecord
    {
        readonly IRecord _record;
        readonly DbOperations _db;

        public QueryRecord(IRecord record, DbOperations db)
        {
            _record = record;
            _db = db;
        }

        public bool Contains(string key) => _record.Values.ContainsKey(key);

        public object? this[string key] => _record.Values.TryGetValue(key, out var value) ? value : null;

        public T? Node<T>(string key) where T : class, new()
        {
            if (!_record.Values.TryGetValue(key, out var value) || value is null)
                return null;
            if (value is INode node)
                return _db.MapTo<T>(node);
            throw new InvalidOperationException($"Record field '{key}' is {value.GetType()} , not a node.");
        }

        public T? Relationship<T>(string key) where T : class, new()
        {
            if (!_record.Values.TryGetValue(key, out var value) || value is null)
                return null;
            if (value is IRelationship rel)
                return _db.MapTo<T>(rel);
            throw new InvalidOperationException($"Record field '{key}' is {value.GetType()} , not a relationship.");
        }

        public IReadOnlyList<T> Nodes<T>(string key) where T : class, new()
        {
            if (!_record.Values.TryGetValue(key, out var value) || value is null)
                return [];
            if (value is not IEnumerable enumerable)
                return [];
            var list = new List<T>();
            foreach (var item in enumerable)
            {
                if (item is INode node)
                    list.Add(_db.MapTo<T>(node));
            }
            return list;
        }

        public IReadOnlyList<T> Relationships<T>(string key) where T : class, new()
        {
            if (!_record.Values.TryGetValue(key, out var value) || value is null)
                return [];
            if (value is not IEnumerable enumerable)
                return [];
            var list = new List<T>();
            foreach (var item in enumerable)
            {
                if (item is IRelationship rel)
                    list.Add(_db.MapTo<T>(rel));
            }
            return list;
        }

        public long? Int64(string key)
        {
            if (!_record.Values.TryGetValue(key, out var value) || value is null)
                return null;
            return Convert.ToInt64(value);
        }
    }
}
