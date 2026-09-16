namespace GraphRoots.GraphDb;

/// <summary>
/// Version lineage: a newer <see cref="Document"/> is based on an older snapshot.
/// Direction is git-like (child → parent).
/// </summary>
[DbLabel("BASED_ON")]
[DbSchemaVersion(2)]
public class BasedOn
{
}
