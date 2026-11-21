namespace XTransform.Xdm.Iterators;

/// <summary>
/// An iterator that concatenates two or more source iterators in order.
/// <see cref="Position"/> is a cumulative 1-based count across all sources.
/// </summary>
internal sealed class ConcatIterator : IXdmIterator
{
    private readonly IXdmIterator[] _sources;
    private int _sourceIndex;
    private int _position;

    internal ConcatIterator(IXdmIterator[] sources)
    {
        _sources = sources;
        _sourceIndex = 0;
        _position = 0;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_sourceIndex < _sources.Length)
        {
            XdmItem? item = _sources[_sourceIndex].Next();
            if (item != null)
            {
                _position++;
                return item;
            }

            _sourceIndex++;
        }

        return null;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone()
    {
        IXdmIterator[] cloned = new IXdmIterator[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            cloned[i] = _sources[i].Clone();
        }

        return new ConcatIterator(cloned);
    }
}
