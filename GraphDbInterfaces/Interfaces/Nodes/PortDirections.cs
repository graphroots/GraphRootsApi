namespace GraphRoots.GraphDb;

/// <summary>
/// Well-known values for <see cref="Port.Direction"/>.
/// </summary>
public static class PortDirections
{
    public const string In = "In";

    public const string Out = "Out";

    /// <summary>
    /// Synthetic floating-param port used as both EDGE source and target.
    /// </summary>
    public const string Both = "Both";
}
