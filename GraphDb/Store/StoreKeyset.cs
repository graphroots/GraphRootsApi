using System;
using System.Globalization;

namespace GraphRoots.GraphDb;

static class StoreKeyset
{
    internal enum ValueKind
    {
        String,
        Number,
        DateTime,
    }

    internal static ValueKind KindOf(string primary)
    {
        if (primary.EndsWith(".X", StringComparison.Ordinal) || primary.EndsWith(".Y", StringComparison.Ordinal))
            return ValueKind.Number;
        if (primary.EndsWith(".FileCreationTimeUtc", StringComparison.Ordinal))
            return ValueKind.DateTime;
        return ValueKind.String;
    }

    internal static string Encode(string primary, object entity, params string[] ties)
    {
        var parts = new string[ties.Length + 1];
        parts[0] = EncodeSort(PropertyValue(primary, entity));
        Array.Copy(ties, 0, parts, 1, ties.Length);
        return StoreCursor.Encode(parts);
    }

    internal static string EncodeSort(object? value)
    {
        if (value == null)
            return "N";
        return "V" + Serialize(value);
    }

    internal static void ApplyAfter(
        CypherClauses clauses,
        string? after,
        int expectedParts,
        string primary,
        string direction,
        params string[] ties)
    {
        if (after == null)
            return;
        var keys = StoreCursor.Decode(after);
        if (keys.Length != expectedParts)
            throw GraphStoreException.InvalidArgument("Invalid cursor.");

        var kind = KindOf(primary);
        var desc = string.Equals(direction, "DESC", StringComparison.OrdinalIgnoreCase);
        var token = keys[0];
        var cursorNull = token == "N";
        if (!cursorNull && (token.Length == 0 || token[0] != 'V'))
            throw GraphStoreException.InvalidArgument("Invalid cursor.");

        string? sortParam = null;
        if (!cursorNull)
            sortParam = clauses.Bind("cSort", Parse(token[1..], kind));

        var tieParams = new string[ties.Length];
        for (var i = 0; i < ties.Length; i++)
            tieParams[i] = clauses.Bind("cTie", keys[i + 1]);

        clauses.Add(AfterPredicate(primary, kind, desc, cursorNull, sortParam, ties, tieParams));
    }

    internal static string AfterPredicate(
        string primary,
        ValueKind kind,
        bool desc,
        bool cursorNull,
        string? sortParam,
        string[] ties,
        string[] tieParams)
    {
        var tiesGreater = TiesGreater(ties, tieParams);
        if (!desc)
        {
            if (cursorNull)
                return $"({primary} IS NULL AND ({tiesGreater}))";
            return $"(({primary} IS NOT NULL AND {Cmp(primary, kind, ">", sortParam!)}) OR ({Eq(primary, kind, sortParam!)} AND ({tiesGreater})) OR ({primary} IS NULL))";
        }

        if (cursorNull)
            return $"(({primary} IS NULL AND ({tiesGreater})) OR ({primary} IS NOT NULL))";
        return $"({primary} IS NOT NULL AND (({Cmp(primary, kind, "<", sortParam!)}) OR ({Eq(primary, kind, sortParam!)} AND ({tiesGreater}))))";
    }

    static string TiesGreater(string[] ties, string[] parms)
    {
        var acc = $"{ties[^1]} > ${parms[^1]}";
        for (var i = ties.Length - 2; i >= 0; i--)
            acc = $"{ties[i]} > ${parms[i]} OR ({ties[i]} = ${parms[i]} AND ({acc}))";
        return acc;
    }

    static string Cmp(string primary, ValueKind kind, string op, string param) =>
        kind == ValueKind.DateTime ? $"{primary} {op} datetime(${param})" : $"{primary} {op} ${param}";

    static string Eq(string primary, ValueKind kind, string param) =>
        kind == ValueKind.DateTime ? $"{primary} = datetime(${param})" : $"{primary} = ${param}";

    static object? PropertyValue(string primary, object entity)
    {
        var name = primary.Contains('.', StringComparison.Ordinal) ? primary[(primary.IndexOf('.') + 1)..] : primary;
        return entity.GetType().GetProperty(name)?.GetValue(entity);
    }

    static string Serialize(object value) => value switch
    {
        DateTime dt => ToUtc(dt).ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        float f => f.ToString("G9", CultureInfo.InvariantCulture),
        double d => d.ToString("G17", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? "",
    };

    static object Parse(string raw, ValueKind kind) => kind switch
    {
        ValueKind.Number => double.Parse(raw, CultureInfo.InvariantCulture),
        ValueKind.DateTime => ToUtc(DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
        _ => raw,
    };

    static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
