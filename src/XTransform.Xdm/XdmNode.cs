using System;
using System.Collections.Generic;

namespace XTransform.Xdm;

// XPath 2.0 §2.1 — nodes are items; XDM §6 — node properties

/// <summary>
/// An XDM node item wrapping a <see cref="NodeHandle"/> within a <see cref="TinyTree"/>.
/// Instances are immutable and may be compared for identity and document order.
/// </summary>
public sealed class XdmNode : XdmItem, IEquatable<XdmNode>
{
    internal XdmNode(NodeHandle handle)
    {
        Handle = handle;
    }

    // ── Internal access ────────────────────────────────────────────────────────

    /// <summary>The underlying handle (tree + index + kind tag).</summary>
    internal NodeHandle Handle { get; }

    /// <summary>Convenience accessor for the owning tree.</summary>
    private TinyTree Tree => Handle.Tree;

    // ── XdmItem overrides ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override bool IsNode => true;

    /// <inheritdoc/>
    public override bool IsAtomic => false;

    /// <summary>
    /// Returns the XPath string value of this node.
    /// Delegates to <see cref="TinyTree.GetStringValue(NodeHandle)"/>.
    /// </summary>
    // XPath 2.0 §5.11
    public override string GetStringValue() => Tree.GetStringValue(Handle);

    // ── Node properties ────────────────────────────────────────────────────────

    /// <summary>Gets the kind of this node.</summary>
    public XdmNodeKind NodeKind => Handle.NodeKind;

    /// <summary>
    /// Gets the expanded name of this node, or <see langword="null"/> for nodes that have no name
    /// (document, text, comment).
    /// For processing-instruction nodes the local name is the target.
    /// For namespace nodes the local name is the prefix (empty string for the default namespace).
    /// </summary>
    // XPath 2.0 §5.11 — node name
    public QName? NodeName
    {
        get
        {
            switch (Handle.NodeKind)
            {
                case XdmNodeKind.Document:
                case XdmNodeKind.Text:
                case XdmNodeKind.Comment:
                    return null;

                case XdmNodeKind.Element:
                {
                    int nc = Tree.GetNameCode(Handle.Index);
                    return nc < 0 ? null : Tree.NamePool.GetQName(nc);
                }

                case XdmNodeKind.Attribute:
                {
                    int nc = Tree.GetAttributeNameCode(Handle.Index);
                    return nc < 0 ? null : Tree.NamePool.GetQName(nc);
                }

                case XdmNodeKind.ProcessingInstruction:
                {
                    // PI target stored as the local name of the name code (no namespace).
                    int nc = Tree.GetNameCode(Handle.Index);
                    if (nc < 0)
                    {
                        return null;
                    }

                    string local = Tree.NamePool.GetLocalName(nc);
                    return new QName(string.Empty, string.Empty, local);
                }

                case XdmNodeKind.Namespace:
                {
                    // XPath 2.0 §5.11: namespace node's name is a QName with local = prefix, no namespace URI.
                    string prefix = Tree.GetNamespacePrefix(Handle.Index);
                    return new QName(string.Empty, string.Empty, prefix);
                }

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Gets the parent of this node, or <see langword="null"/> for the document node.
    /// For attribute and namespace nodes, returns the owner element.
    /// </summary>
    // XPath 2.0 §5.11 — parent property
    public XdmNode? Parent
    {
        get
        {
            if (Handle.IsAttribute)
            {
                int ownerIdx = Tree.GetAttributeParent(Handle.Index);
                return new XdmNode(NodeHandle.ForNode(Tree, ownerIdx));
            }

            if (Handle.IsNamespace)
            {
                int ownerIdx = Tree.GetNamespaceParent(Handle.Index);
                return new XdmNode(NodeHandle.ForNode(Tree, ownerIdx));
            }

            int parentIdx = Tree.GetParent(Handle.Index);
            if (parentIdx < 0)
            {
                return null;
            }

            return new XdmNode(NodeHandle.ForNode(Tree, parentIdx));
        }
    }

    /// <summary>Gets the base URI of the document containing this node.</summary>
    public string? BaseUri => Handle.IsNamespace ? null : Tree.GetBaseUri(Handle.IsAttribute ? Tree.GetAttributeParent(Handle.Index) : Handle.Index);

    /// <summary>Gets the document URI: where a loaded document came from; null for a node of a constructed tree, and for any node but a document node.</summary>
    public string? DocumentUri => NodeKind == XdmNodeKind.Document ? Tree.DocumentUri : null;

    // ── Typed value ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the typed value of this node (fn:data).
    /// For untyped trees: element, attribute, document, text, and namespace nodes yield
    /// <c>xs:untypedAtomic</c>; comment and processing-instruction nodes yield <c>xs:string</c>.
    /// </summary>
    // XPath 2.0 §2.6.2 — fn:data; XDM §6
    public XdmSequence GetTypedValue()
    {
        string sv = GetStringValue();
        switch (NodeKind)
        {
            case XdmNodeKind.Comment:
            case XdmNodeKind.ProcessingInstruction:
                return XdmSequence.Of(XdmAtomicValue.String(sv));

            default:
                return XdmSequence.Of(XdmAtomicValue.UntypedAtomic(sv));
        }
    }

    // ── Axis navigation ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all nodes on <paramref name="axis"/> relative to this node,
    /// with no node test applied (all kinds accepted).
    /// </summary>
    // XPath 2.0 §3.2.1
    public IEnumerable<XdmNode> Axis(Axis axis)
    {
        IXdmIterator it = Tree.Iterate(Handle, axis, null);
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            yield return (XdmNode)item;
        }
    }

    // ── Document-order comparison ──────────────────────────────────────────────

    /// <summary>
    /// Compares this node with <paramref name="other"/> in document order.
    /// Returns a negative number, zero, or a positive number per the standard compare contract.
    /// Cross-tree comparison is stable but arbitrary (ordered by <see cref="TinyTree.TreeId"/>).
    /// Attributes come after their element and before the element's first child.
    /// Namespace nodes come after their element and before attributes.
    /// </summary>
    // XPath 2.0 §2.4 — document order
    public int CompareDocumentOrder(XdmNode other)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }
#pragma warning restore CA1510

        // Cross-tree: stable ordering by monotonic tree id.
        if (!ReferenceEquals(Tree, other.Tree))
        {
            return Tree.TreeId.CompareTo(other.Tree.TreeId);
        }

        int aOwner = Handle.OwnerElementIndex;
        int bOwner = other.Handle.OwnerElementIndex;
        if (aOwner != bOwner)
        {
            return aOwner.CompareTo(bOwner);
        }

        // Same owner element (or both are the same regular node).
        // Secondary order: regular node (0) < namespace (1) < attribute (2).
        // This places namespaces and attributes after their element but before its first child.
        int aSec = Handle.IsNamespace ? 1 : Handle.IsAttribute ? 2 : 0;
        int bSec = other.Handle.IsNamespace ? 1 : other.Handle.IsAttribute ? 2 : 0;
        if (aSec != bSec)
        {
            return aSec.CompareTo(bSec);
        }

        // Same owner and same kind-order: compare by index within the index space.
        return Handle.Index.CompareTo(other.Handle.Index);
    }

    // ── Equality ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(XdmNode? other)
    {
        if (other is null)
        {
            return false;
        }

        return Handle == other.Handle;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is XdmNode n && Equals(n);

    /// <inheritdoc/>
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(XdmNode? left, XdmNode? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(XdmNode? left, XdmNode? right) => !(left == right);
}
