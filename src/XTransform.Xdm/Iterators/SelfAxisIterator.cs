namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — self axis

/// <summary>
/// Iterator for the <c>self</c> axis.  Yields at most the origin node itself.
/// </summary>
internal sealed class SelfAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private bool _done;
    private int _position;

    internal SelfAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
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
        XdmNode? node = _tree.WrapIfMatches(_origin, _test);
        if (node != null)
        {
            _position++;
        }

        return node;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new SelfAxisIterator(_tree, _origin, _test);
}
