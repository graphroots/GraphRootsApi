using System.Collections.Generic;

namespace GraphRoots.GraphDb;

/// <summary>
/// Well-known values for <c>Origin</c> on core graph types.
/// </summary>
public static class GraphOrigins
{
    public const string Grasshopper = "grasshopper";

    public const string Dynamo = "dynamo";

    public const string GraphRoots = "graphroots";

    /// <summary>
    /// Read-filter sentinel for GraphQL <c>UNKNOWN</c>. Not a persisted origin.
    /// </summary>
    public const string UnknownFilter = "__unknown__";

    public static readonly string[] Known = [Grasshopper, Dynamo, GraphRoots];

    public static bool IsKnown(string? origin) =>
        origin is Grasshopper or Dynamo or GraphRoots;

    public static void AddPredicate(List<string> predicates, Dictionary<string, object?> parameters, string varName, string? origin, string paramName)
    {
        if (string.IsNullOrEmpty(origin))
            return;
        if (origin == UnknownFilter)
        {
            parameters[paramName] = Known;
            predicates.Add($"NOT coalesce({varName}.Origin, '') IN ${paramName}");
            return;
        }
        parameters[paramName] = origin;
        predicates.Add($"{varName}.Origin = ${paramName}");
    }
}
