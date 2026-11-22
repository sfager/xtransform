namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — following axis

/// <summary>
/// Iterator for the <c>following</c> axis.
/// Yields all nodes after the origin in document order, excluding descendants of the origin
/// and excluding attribute and namespace nodes (per XPath 2.0 §3.2.1.1).
/// </summary>
internal sealed class FollowingAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private int _current;
    private int _position;

    internal FollowingAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _position = 0;
        Reset();
    }

    private void Reset()
    {
        // Start after the subtree of the owner element (for attr/ns) or origin node.
        int ownerIdx = _origin.OwnerElementIndex;
        _current = _tree.SubtreeEnd(ownerIdx);
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_current < _tree.NodeCount)
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
    public IXdmIterator Clone() => new FollowingAxisIterator(_tree, _origin, _test);
}
