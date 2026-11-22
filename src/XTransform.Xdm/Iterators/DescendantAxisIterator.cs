namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — descendant and descendant-or-self axes

/// <summary>
/// Iterator for the <c>descendant</c> and <c>descendant-or-self</c> axes.
/// Yields nodes in pre-order (document order).
/// Attribute and namespace nodes have no descendants; the axis is empty for those origins.
/// </summary>
internal sealed class DescendantAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private readonly bool _includeSelf;
    private int _current;
    private int _subtreeEnd;
    private int _position;

    internal DescendantAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test, bool includeSelf)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _includeSelf = includeSelf;
        _position = 0;
        Reset();
    }

    private void Reset()
    {
        if (!_origin.IsRegularNode)
        {
            _current = -1;
            _subtreeEnd = 0;
            return;
        }

        int idx = _origin.Index;
        _subtreeEnd = _tree.SubtreeEnd(idx);
        _current = _includeSelf ? idx : idx + 1;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_current < _subtreeEnd)
        {
            int idx = _current;
            _current++;

            NodeHandle handle = NodeHandle.ForNode(_tree, idx);
            XdmNode? node = _tree.WrapIfMatches(handle, _test);
            if (node != null)
            {
                _position++;
                return node;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new DescendantAxisIterator(_tree, _origin, _test, _includeSelf);
}
