using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GraphRoots.GraphDb;

/// <summary>
/// Flatten and rehydrate namespaced extension properties on Neo4j nodes and relationships.
/// Keys must include a prefix (e.g. <c>gh.</c>) and must not collide with core property names.
/// Cypher never interpolates keys; use <see cref="CypherDynamicProperty"/> with a bound parameter.
/// </summary>
public static class DbExtensionProperties
{
    public const string PropertyName = "Extensions";

    static readonly Regex KeyPattern = new(@"^[a-z][a-z0-9]*\.[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);

    static readonly string[] ReservedNames =
    {
        "schemaVersion",
        "createdAt",
        "updatedAt",
        "ElementId",
        "Labels",
        "Type",
        "StartNodeElementId",
        "EndNodeElementId",
        PropertyName,
    };

    public static HashSet<string> CorePropertyNames(Type type)
    {
        var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !string.Equals(n, PropertyName, StringComparison.Ordinal));
        return new HashSet<string>(names.Concat(ReservedNames), StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsValidKey([NotNullWhen(true)] string? key) =>
        !string.IsNullOrWhiteSpace(key) && KeyPattern.IsMatch(key);

    public static void ValidateKey(string? key, ISet<string>? corePropertyNames = null)
    {
        if (!IsValidKey(key))
        {
            throw GraphStoreException.InvalidArgument(
                $"Extension key '{key}' must match 'prefix.localName' with a lowercase namespace (e.g. gh.componentGuid).");
        }

        var localName = key![(key.LastIndexOf('.') + 1)..];
        if (corePropertyNames != null &&
            (corePropertyNames.Contains(key) || corePropertyNames.Contains(localName)))
        {
            throw GraphStoreException.InvalidArgument($"Extension key '{key}' collides with a core property name.");
        }
    }

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
            ValidateKey(kvp.Key, corePropertyNames);
            if (kvp.Value == null)
                continue;
            target[kvp.Key] = NormalizeValue(kvp.Value);
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
            if (!IsValidKey(kvp.Key))
                continue;
            extensions[kvp.Key] = kvp.Value;
        }
        return extensions;
    }

    public static object NormalizeValue(object value)
    {
        if (value is Guid guid)
            return guid.ToString();
        if (value is string or bool)
            return value;
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (value is IDictionary or IDictionary<string, object?>)
            throw GraphStoreException.InvalidArgument("Extension values cannot be JSON objects; use a scalar or an array of scalars.");
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                if (item == null)
                    throw GraphStoreException.InvalidArgument("Extension arrays cannot contain null.");
                if (item is IDictionary || item is IEnumerable and not string)
                    throw GraphStoreException.InvalidArgument("Extension arrays cannot contain nested objects or arrays.");
                list.Add(NormalizeValue(item));
            }
            return list;
        }

        throw GraphStoreException.InvalidArgument($"Unsupported extension value type '{value.GetType().Name}'.");
    }
}
