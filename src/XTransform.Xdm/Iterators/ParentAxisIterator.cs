namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — parent axis

/// <summary>
/// Iterator for the <c>parent</c> axis.  Yields at most one node.
/// For attribute and namespace nodes the parent is the owner element.
/// For the document node (parent index -1) the axis is empty.
/// </summary>
internal sealed class ParentAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private bool _done;
    private int _position;

    internal ParentAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _done = false;
        _position = 0;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_done)
        {
            return null;
        }

        _done = true;

        NodeHandle? parentHandle = GetParentHandle();
        if (parentHandle == null)
        {
            return null;
        }

        XdmNode? node = _tree.WrapIfMatches(parentHandle.Value, _test);
        if (node != null)
        {
            _position++;
        }

        return node;
    }

    private NodeHandle? GetParentHandle()
    {
        if (_origin.IsAttribute)
        {
            int ownerIdx = _tree.GetAttributeParent(_origin.Index);
            return NodeHandle.ForNode(_tree, ownerIdx);
        }

        if (_origin.IsNamespace)
        {
            int ownerIdx = _tree.GetNamespaceParent(_origin.Index);
            return NodeHandle.ForNode(_tree, ownerIdx);
        }

        int parentIdx = _tree.GetParent(_origin.Index);
        if (parentIdx < 0)
        {
            return null;
        }

        return NodeHandle.ForNode(_tree, parentIdx);
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new ParentAxisIterator(_tree, _origin, _test);
}
