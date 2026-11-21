using System;
using XTransform.Xdm.Iterators;

namespace XTransform.Xdm;

// XPath 2.0 §3.2.1 — Axes

internal sealed partial class TinyTree
{
    // ── Public Iterate entry-point ─────────────────────────────────────────────

    /// <summary>
    /// Returns an iterator that yields all nodes on <paramref name="axis"/> relative to
    /// <paramref name="origin"/> that satisfy <paramref name="test"/> (or every node when
    /// <paramref name="test"/> is <see langword="null"/>).
    /// </summary>
    // XPath 2.0 §3.2.1
    internal IXdmIterator Iterate(NodeHandle origin, Axis axis, NodeTest? test)
    {
        // XPath 2.0 §3.2.1.1 — axis semantics
        switch (axis)
        {
            case Axis.Child:
                return new ChildAxisIterator(this, origin, test);

            case Axis.Descendant:
                return new DescendantAxisIterator(this, origin, test, includeSelf: false);

            case Axis.DescendantOrSelf:
                return new DescendantAxisIterator(this, origin, test, includeSelf: true);

            case Axis.Self:
                return new SelfAxisIterator(this, origin, test);

            case Axis.Attribute:
                return new AttributeAxisIterator(this, origin, test);

            case Axis.Namespace:
                return new NamespaceAxisIterator(this, origin, test);

            case Axis.Parent:
                return new ParentAxisIterator(this, origin, test);

            case Axis.Ancestor:
                return new AncestorAxisIterator(this, origin, test, includeSelf: false);

            case Axis.AncestorOrSelf:
                return new AncestorAxisIterator(this, origin, test, includeSelf: true);

            case Axis.FollowingSibling:
                return new FollowingSiblingAxisIterator(this, origin, test);

            case Axis.PrecedingSibling:
                return new PrecedingSiblingAxisIterator(this, origin, test);

            case Axis.Following:
                return new FollowingAxisIterator(this, origin, test);

            case Axis.Preceding:
                return new PrecedingAxisIterator(this, origin, test);

            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown axis.");
        }
    }

    // ── Attribute / namespace string-value helpers ─────────────────────────────

    /// <summary>
    /// Returns the XPath string value of the node referred to by <paramref name="handle"/>.
    /// Dispatches on the handle's kind tag so callers need not care about index spaces.
    /// </summary>
    // XPath 2.0 §5.11
    internal string GetStringValue(NodeHandle handle)
    {
        if (handle.IsAttribute)
        {
            return GetAttributeValue(handle.Index);
        }

        if (handle.IsNamespace)
        {
            return GetNamespaceUri(handle.Index);
        }

        // Regular node.
        return GetStringValue(handle.Index);
    }

    // ── SubtreeEnd helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the exclusive end index of the subtree rooted at <paramref name="nodeIndex"/>.
    /// All indices in [nodeIndex, SubtreeEnd(nodeIndex)) belong to the subtree.
    /// </summary>
    internal int SubtreeEnd(int nodeIndex)
    {
        int depth = GetDepth(nodeIndex);
        int i = nodeIndex + 1;
        while (i < NodeCount && GetDepth(i) > depth)
        {
            i++;
        }

        return i;
    }

    // ── Internal node-wrapping helper ──────────────────────────────────────────

    /// <summary>
    /// Wraps a <see cref="NodeHandle"/> in an <see cref="XdmNode"/> only if
    /// <paramref name="test"/> is null or matches the node.
    /// Returns null when the test fails.
    /// </summary>
    internal XdmNode? WrapIfMatches(NodeHandle handle, NodeTest? test)
    {
        if (test != null)
        {
            int nc = handle.IsAttribute
                ? GetAttributeNameCode(handle.Index)
                : handle.IsNamespace
                    ? NamePool.NoName  // namespace nodes are anonymous in the name-code sense
                    : GetNameCode(handle.Index);

            if (!test.Matches(handle.NodeKind, nc, NamePool))
            {
                return null;
            }
        }

        return new XdmNode(handle);
    }

    /// <summary>
    /// Returns the first child index of a regular node, or -1 if none.
    /// Children are the first node at depth+1 that immediately follows the element.
    /// </summary>
    internal int GetFirstChild(int nodeIndex)
    {
        XdmNodeKind kind = GetKind(nodeIndex);
        if (kind != XdmNodeKind.Document && kind != XdmNodeKind.Element)
        {
            return -1;
        }

        int childDepth = GetDepth(nodeIndex) + 1;
        int next = nodeIndex + 1;
        if (next < NodeCount && GetDepth(next) == childDepth)
        {
            return next;
        }

        return -1;
    }
}
