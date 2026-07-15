namespace CustomResultError.Common;

public readonly struct ItemsList<T>
{
    public ItemsList() { }

    public List<T> Items { get; init; } = [];

    public int Count => Items.Count;
}
