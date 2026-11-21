namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — following-sibling axis

/// <summary>
/// Iterator for the <c>following-sibling</c> axis.
/// Yields all siblings that follow the origin in document order.
/// Not applicable to attribute or namespace nodes (yields empty per XPath 2.0 §3.2.1.1).
/// </summary>
internal sealed class FollowingSiblingAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private int _current;
    private int _siblingDepth;
    private int _position;

    internal FollowingSiblingAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _position = 0;
        Reset();
    }

    private void Reset()
    {
        // Attribute / namespace nodes have no sibling axis.
        if (!_origin.IsRegularNode)
        {
            _current = -1;
            _siblingDepth = 0;
            return;
        }

        _siblingDepth = _tree.GetDepth(_origin.Index);
        _current = _tree.GetNextSibling(_origin.Index);
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_current >= 0)
        {
            int idx = _current;
            _current = _tree.GetNextSibling(idx);

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
    public IXdmIterator Clone() => new FollowingSiblingAxisIterator(_tree, _origin, _test);
}
