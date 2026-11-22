using System.Collections.Generic;

namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — preceding-sibling axis (reverse)

/// <summary>
/// Iterator for the <c>preceding-sibling</c> axis (reverse axis).
/// Yields siblings that precede the origin in document order, returned in reverse document order
/// (closest sibling first).  XPath 2.0 §2.4: Position counts items as returned.
/// Not applicable to attribute or namespace nodes (yields empty per XPath 2.0 §3.2.1.1).
/// </summary>
internal sealed class PrecedingSiblingAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private readonly NodeHandle[] _nodes;
    private int _index;
    private int _position;

    internal PrecedingSiblingAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _tree = tree;
        _origin = origin;
        _test = test;
        _nodes = BuildNodes();
        _index = 0;
        _position = 0;
    }

    private NodeHandle[] BuildNodes()
    {
        if (!_origin.IsRegularNode)
        {
            return System.Array.Empty<NodeHandle>();
        }

        int originIdx = _origin.Index;
        int parentIdx = _tree.GetParent(originIdx);

        // Find first child of parent.
        int firstChild;
        if (parentIdx < 0)
        {
            // The document node (index 0) has no parent; it has no siblings either.
            return System.Array.Empty<NodeHandle>();
        }
        else
        {
            firstChild = _tree.GetFirstChild(parentIdx);
        }

        if (firstChild < 0)
        {
            return System.Array.Empty<NodeHandle>();
        }

        // Collect all siblings before origin in document order, then reverse.
        var siblings = new List<NodeHandle>();
        int cur = firstChild;
        while (cur >= 0 && cur != originIdx)
        {
            NodeHandle h = NodeHandle.ForNode(_tree, cur);
            if (_test == null)
            {
                siblings.Add(h);
            }
            else
            {
                int nc = _tree.GetNameCode(cur);
                XdmNodeKind kind = _tree.GetKind(cur);
                if (_test.Matches(kind, nc, _tree.NamePool))
                {
                    siblings.Add(h);
                }
            }

            cur = _tree.GetNextSibling(cur);
        }

        // Reverse to get closest-first (reverse document order).
        siblings.Reverse();
        return siblings.ToArray();
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_index < _nodes.Length)
        {
            NodeHandle handle = _nodes[_index];
            _index++;
            _position++;
            return new XdmNode(handle);
        }

        return null;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new PrecedingSiblingAxisIterator(_tree, _origin, _test);
}
