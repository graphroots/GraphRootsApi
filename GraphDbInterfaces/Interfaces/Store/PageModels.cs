using System.Collections.Generic;

namespace GraphRoots.GraphDb;

public enum StoreSortDirection
{
    Asc,
    Desc,
}

public sealed class SortSpec
{
    public string Field { get; init; } = "";

    public StoreSortDirection Direction { get; init; } = StoreSortDirection.Asc;
}

public sealed class PageRequest
{
    public int? First { get; init; }

    public string? After { get; init; }

    public int DefaultSize { get; init; } = 50;

    public int MaxSize { get; init; } = 500;

    public SortSpec? Sort { get; init; }

    public bool IncludeTotalCount { get; init; } = true;

    public int Take()
    {
        var size = First ?? DefaultSize;
        if (size < 1)
            throw GraphStoreException.InvalidArgument("first must be at least 1.");
        if (First is int requested && requested > MaxSize)
            throw GraphStoreException.InvalidArgument($"first must be at most {MaxSize}.");
        return size > MaxSize ? MaxSize : size;
    }
}

public sealed class PageResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public IReadOnlyList<string> Cursors { get; init; } = [];

    public int TotalCount { get; init; }

    public string? StartCursor { get; init; }

    public string? EndCursor { get; init; }

    public bool HasNextPage { get; init; }

    public bool HasPreviousPage { get; init; }
}
