namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — child axis

/// <summary>
/// Iterator for the child axis.  Yields the direct children of an element or document node
/// in document order (i.e., the next-sibling chain starting from the first child).
/// Attribute and namespace nodes have no children; yields nothing for those origins.
/// </summary>
internal sealed class ChildAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private int _current;   // current regular-node index; -1 when exhausted
    private int _childDepth;
    private int _position;

    internal ChildAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _position = 0;
        Reset();
    }

    private void Reset()
    {
        // Attribute / namespace nodes have no children.
        if (!_origin.IsRegularNode)
        {
            _current = -1;
            _childDepth = 0;
            return;
        }

        int idx = _origin.Index;
        _childDepth = _tree.GetDepth(idx) + 1;
        int next = idx + 1;
        if (next < _tree.NodeCount && _tree.GetDepth(next) == _childDepth)
        {
            _current = next;
        }
        else
        {
            _current = -1;
        }
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_current >= 0)
        {
            int idx = _current;
            // Advance to next sibling at the same depth.
            int ns = _tree.GetNextSibling(idx);
            // GetNextSibling returns -1 when last sibling. Verify depth to be safe.
            _current = (ns >= 0 && _tree.GetDepth(ns) == _childDepth) ? ns : -1;

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
    public IXdmIterator Clone() => new ChildAxisIterator(_tree, _origin, _test);
}
