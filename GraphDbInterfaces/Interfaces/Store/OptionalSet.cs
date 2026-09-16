namespace GraphRoots.GraphDb;

public readonly struct OptionalSet<T>
{
    public OptionalSet(T? value)
    {
        IsSpecified = true;
        Value = value;
    }

    public bool IsSpecified { get; }

    public T? Value { get; }

    public static OptionalSet<T> Unspecified => default;
}
