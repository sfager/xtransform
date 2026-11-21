using System;

namespace XTransform.Xdm.Iterators;

/// <summary>
/// An iterator that maps each item from an inner iterator through a
/// <see cref="Func{XdmItem, IXdmIterator}"/> expander, then concatenates the resulting
/// sub-iterators. <see cref="Position"/> is the global 1-based count of items yielded.
/// </summary>
internal sealed class MappingIterator : IXdmIterator
{
    private readonly IXdmIterator _source;
    private readonly Func<XdmItem, IXdmIterator> _mapper;
    private IXdmIterator? _current;   // current sub-iterator (null = not started or exhausted)
    private int _position;
    private bool _done;

    internal MappingIterator(IXdmIterator source, Func<XdmItem, IXdmIterator> mapper)
    {
        _source = source;
        _mapper = mapper;
        _current = null;
        _position = 0;
        _done = false;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_done)
        {
            return null;
        }

        while (true)
        {
            // Try to pull from current sub-iterator.
            if (_current != null)
            {
                XdmItem? sub = _current.Next();
                if (sub != null)
                {
                    _position++;
                    return sub;
                }

                _current = null;
            }

            // Advance to the next source item.
            XdmItem? sourceItem = _source.Next();
            if (sourceItem == null)
            {
                _done = true;
                return null;
            }

            _current = _mapper(sourceItem);
        }
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new MappingIterator(_source.Clone(), _mapper);
}
