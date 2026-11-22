namespace XTransform.Xdm.Iterators;

/// <summary>
/// An iterator over a segment of an <see cref="XdmItem"/> array.
/// Does not copy the array; callers must ensure the array is not mutated after creation.
/// </summary>
internal sealed class ArrayIterator : IXdmIterator
{
    private readonly XdmItem[] _items;
    private readonly int _offset;
    private readonly int _count;
    private int _index;   // next index into _items (relative to _offset); starts at 0
    private int _position; // 1-based, 0 before first Next()

    internal ArrayIterator(XdmItem[] items) : this(items, 0, items.Length) { }

    internal ArrayIterator(XdmItem[] items, int offset, int count)
    {
        _items = items;
        _offset = offset;
        _count = count;
        _index = 0;
        _position = 0;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_index >= _count)
        {
            return null;
        }

        XdmItem item = _items[_offset + _index];
        _index++;
        _position++;
        return item;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new ArrayIterator(_items, _offset, _count);
}
