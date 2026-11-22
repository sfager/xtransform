namespace XTransform.Xdm.Iterators;

/// <summary>
/// An iterator that yields exactly one <see cref="XdmItem"/> and then signals end-of-sequence.
/// </summary>
internal sealed class SingleItemIterator : IXdmIterator
{
    private readonly XdmItem _item;
    private int _position;

    internal SingleItemIterator(XdmItem item)
    {
        _item = item;
        _position = 0;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_position == 0)
        {
            _position = 1;
            return _item;
        }

        return null;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new SingleItemIterator(_item);
}
