namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — attribute axis

/// <summary>
/// Iterator for the <c>attribute</c> axis.
/// For element origins: yields all attributes via the alpha chain.
/// For all other origins: empty.
/// </summary>
internal sealed class AttributeAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private int _currentAttr;   // attribute-array index; -1 when exhausted
    private int _ownerElement;  // element owning the first attribute
    private int _position;

    internal AttributeAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _position = 0;
        Reset();
    }

    private void Reset()
    {
        // Attribute axis is only non-empty for element nodes.
        if (_origin.IsRegularNode && _tree.GetKind(_origin.Index) == XdmNodeKind.Element)
        {
            _ownerElement = _origin.Index;
            _currentAttr = _tree.GetFirstAttribute(_ownerElement);
        }
        else
        {
            _ownerElement = -1;
            _currentAttr = -1;
        }
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_currentAttr >= 0)
        {
            int ai = _currentAttr;

            // Advance: next attribute belongs to same owner when its parent matches.
            int nextAi = ai + 1;
            if (nextAi < _tree.AttributeCount && _tree.GetAttributeParent(nextAi) == _ownerElement)
            {
                _currentAttr = nextAi;
            }
            else
            {
                _currentAttr = -1;
            }

            NodeHandle handle = NodeHandle.ForAttribute(_tree, ai);
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
    public IXdmIterator Clone() => new AttributeAxisIterator(_tree, _origin, _test);
}
