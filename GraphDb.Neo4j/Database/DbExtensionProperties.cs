using System.Collections.Generic;

namespace GraphRoots.GraphDb.Neo4j;

/// <summary>
/// Flatten and rehydrate namespaced extension properties on Neo4j nodes and relationships.
/// Key and value rules live in <see cref="ExtensionKeys"/>.
/// Cypher never interpolates keys; use <see cref="CypherDynamicProperty"/> with a bound parameter.
/// </summary>
public static class DbExtensionProperties
{
    public static string CypherDynamicProperty(string variable, string keyParameter) =>
        $"{variable}[${keyParameter}]";

    public static void ValidateAndFlatten(
        IDictionary<string, object?>? extensions,
        ISet<string> corePropertyNames,
        IDictionary<string, object> target)
    {
        if (extensions == null || extensions.Count == 0)
            return;

        foreach (var kvp in extensions)
        {
            ExtensionKeys.ValidateKey(kvp.Key, corePropertyNames);
            if (kvp.Value == null)
                continue;
            target[kvp.Key] = ExtensionKeys.NormalizeValue(kvp.Value);
        }
    }

    public static Dictionary<string, object?> Rehydrate(
        IEnumerable<KeyValuePair<string, object>> properties,
        ISet<string> corePropertyNames)
    {
        var extensions = new Dictionary<string, object?>();
        foreach (var kvp in properties)
        {
            if (kvp.Key.IndexOf('.') < 0)
                continue;
            if (corePropertyNames.Contains(kvp.Key))
                continue;
            if (!ExtensionKeys.IsValidKey(kvp.Key))
                continue;
            extensions[kvp.Key] = kvp.Value;
        }
        return extensions;
    }
}
