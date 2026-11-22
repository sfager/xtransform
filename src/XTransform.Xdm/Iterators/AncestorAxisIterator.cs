namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — ancestor and ancestor-or-self axes (reverse)

/// <summary>
/// Iterator for the <c>ancestor</c> and <c>ancestor-or-self</c> axes.
/// Yields nodes in reverse document order (closest ancestor first).
/// XPath 2.0 §2.4: proximity position counts items as returned, so Position increments normally.
/// </summary>
internal sealed class AncestorAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private readonly bool _includeSelf;
    private int _current;        // current regular-node index; -1 when exhausted
    private bool _selfEmitted;
    private int _position;

    internal AncestorAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test, bool includeSelf)
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
        _selfEmitted = false;

        // For attribute/namespace start from the owner element.
        if (_origin.IsAttribute)
        {
            _current = _tree.GetAttributeParent(_origin.Index);
        }
        else if (_origin.IsNamespace)
        {
            _current = _tree.GetNamespaceParent(_origin.Index);
        }
        else
        {
            _current = _includeSelf ? _origin.Index : _tree.GetParent(_origin.Index);
        }
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        // For AncestorOrSelf and origin is an attribute/namespace, emit origin first.
        if (_includeSelf && !_selfEmitted && (_origin.IsAttribute || _origin.IsNamespace))
        {
            _selfEmitted = true;
            XdmNode? selfNode = _tree.WrapIfMatches(_origin, _test);
            if (selfNode != null)
            {
                _position++;
                return selfNode;
            }
        }

        while (_current >= 0)
        {
            int idx = _current;
            _current = _tree.GetParent(idx);

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
    public IXdmIterator Clone() => new AncestorAxisIterator(_tree, _origin, _test, _includeSelf);
}
