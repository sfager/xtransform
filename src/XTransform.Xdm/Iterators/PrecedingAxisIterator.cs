using System.Collections.Generic;

namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — preceding axis (reverse)

/// <summary>
/// Iterator for the <c>preceding</c> axis (reverse axis).
/// Yields all nodes before the origin in document order, excluding ancestors of the origin
/// and excluding attribute and namespace nodes.
/// Returned in reverse document order (XPath 2.0 §2.4).
/// </summary>
internal sealed class PrecedingAxisIterator : IXdmIterator
{
    private readonly TinyTree _tree;
    private readonly NodeHandle _origin;
    private readonly NodeTest? _test;
    private readonly NodeHandle[] _nodes;
    private int _index;
    private int _position;

    internal PrecedingAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
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
        // Build set of ancestor indices to skip.
        var ancestors = new HashSet<int>();
        int ownerIdx = _origin.OwnerElementIndex;
        int cur = ownerIdx;
        while (cur >= 0)
        {
            ancestors.Add(cur);
            cur = _tree.GetParent(cur);
        }

        // Collect all regular nodes at indices < ownerIdx that are not ancestors, in reverse order.
        var result = new List<NodeHandle>();
        for (int i = ownerIdx - 1; i >= 0; i--)
        {
            if (ancestors.Contains(i))
            {
                continue;
            }

            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            if (_test != null)
            {
                int nc = _tree.GetNameCode(i);
                XdmNodeKind kind = _tree.GetKind(i);
                if (!_test.Matches(kind, nc, _tree.NamePool))
                {
                    continue;
                }
            }

            result.Add(handle);
        }

        return result.ToArray();
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
    public IXdmIterator Clone() => new PrecedingAxisIterator(_tree, _origin, _test);
}
