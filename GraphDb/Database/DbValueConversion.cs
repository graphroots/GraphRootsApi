using System;
using Neo4j.Driver;

namespace GraphRoots.GraphDb;

/// <summary>
/// Converts Neo4j temporal values to <see cref="DateTime"/> for record mapping.
/// </summary>
public static class DbValueConversion
{
    public static DateTime FromZonedDateTime(object value)
    {
        return ((ZonedDateTime)value).ToDateTimeOffset().UtcDateTime;
    }

    public static DateTime? FromZonedDateTimeNullable(object value)
    {
        return FromZonedDateTime(value);
    }

    public static DateTime FromLocalDateTime(object value)
    {
        return ((LocalDateTime)value).ToDateTime();
    }

    public static DateTime? FromLocalDateTimeNullable(object value)
    {
        return FromLocalDateTime(value);
    }
}
