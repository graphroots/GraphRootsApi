using System;

namespace GraphRoots.GraphDb;

/// <summary>
/// Attribute for denoting the properties that should be used for equality checking.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class DbEqualityCheckAttribute : Attribute
{
}

/// <summary>
/// Attribute for denoting the properties that should be serialized to the database.
/// This does not need to be added if <see cref="DbEqualityCheckAttribute"/> is added.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class DbSerializeAttribute : Attribute
{
}

/// <summary>
/// Stable Neo4j label for a node or relationship type. Decoupled from the C# type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DbLabelAttribute : Attribute
{
    public DbLabelAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>
/// Schema version written to the node or relationship as <c>schemaVersion</c> on SET.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DbSchemaVersionAttribute : Attribute
{
    public DbSchemaVersionAttribute(int version)
    {
        Version = version;
    }

    public int Version { get; }
}
