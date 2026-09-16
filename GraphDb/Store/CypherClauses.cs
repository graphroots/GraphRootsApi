using System.Collections.Generic;
using System.Text;

namespace GraphRoots.GraphDb;

sealed class CypherClauses
{
    readonly StringBuilder _where = new();
    readonly Dictionary<string, object?> _parameters = new();
    int _i;

    public Dictionary<string, object?> Parameters => _parameters;

    public string Bind(string hint, object? value)
    {
        var name = $"{hint}_{_i++}";
        _parameters[name] = value;
        return name;
    }

    public void Add(string clause)
    {
        if (_where.Length > 0)
            _where.Append(" AND ");
        _where.Append(clause);
    }

    public void Eq(string varName, string property, object? value)
    {
        if (value == null)
            return;
        if (value is string s && s.Length == 0)
            return;
        var p = Bind(property, value);
        Add($"{varName}.{property} = ${p}");
    }

    public void Origin(string varName, string? origin)
    {
        if (string.IsNullOrEmpty(origin))
            return;
        if (origin == GraphOrigins.UnknownFilter)
        {
            var p = Bind("knownOrigins", GraphOrigins.Known);
            Add($"NOT coalesce({varName}.Origin, '') IN ${p}");
            return;
        }
        Eq(varName, "Origin", origin);
    }

    public void Contains(string varName, string property, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        var p = Bind(property, value);
        Add($"toLower(toString({varName}.{property})) CONTAINS toLower(${p})");
    }

    public void Extension(string varName, ExtensionPredicate predicate)
    {
        DbExtensionProperties.ValidateKey(predicate.Key);
        var key = Bind("extKey", predicate.Key);
        var value = Bind("ext", predicate.EqualsValue == null ? null : DbExtensionProperties.NormalizeValue(predicate.EqualsValue));
        Add($"{DbExtensionProperties.CypherDynamicProperty(varName, key)} = ${value}");
    }

    public void Extensions(string varName, IReadOnlyList<ExtensionPredicate>? predicates)
    {
        if (predicates == null)
            return;
        foreach (var predicate in predicates)
            Extension(varName, predicate);
    }

    public string WherePrefix()
    {
        return _where.Length == 0 ? "" : "WHERE " + _where + " ";
    }

    public string AndPrefix()
    {
        return _where.Length == 0 ? "" : "AND " + _where + " ";
    }

    public string WhereAnd()
    {
        return _where.Length == 0 ? "" : " AND " + _where;
    }
}
