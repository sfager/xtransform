using System;
using System.Runtime.CompilerServices;

namespace XTransform.Xdm;

/// <summary>
/// Lightweight handle to a single node within a <see cref="TinyTree"/>.
/// A value of this struct is only meaningful in conjunction with the tree it was obtained from.
/// The <c>_kindTag</c> discriminates between regular nodes (0), attribute nodes (1) and
/// namespace nodes (2) which live in separate index spaces inside the tree.
/// </summary>
internal readonly struct NodeHandle : IEquatable<NodeHandle>
{
    // 0 = regular node (kind from Tree.GetKind(Index))
    // 1 = attribute
    // 2 = namespace
    private readonly byte _kindTag;

    private NodeHandle(TinyTree tree, int index, byte kindTag)
    {
        Tree = tree;
        Index = index;
        _kindTag = kindTag;
    }

    // ── Factories ──────────────────────────────────────────────────────────────

    /// <summary>Creates a handle for a regular (node-array) node at <paramref name="index"/>.</summary>
    internal static NodeHandle ForNode(TinyTree tree, int index) => new NodeHandle(tree, index, 0);

    /// <summary>Creates a handle for the attribute at <paramref name="attrIndex"/> in the attribute array.</summary>
    internal static NodeHandle ForAttribute(TinyTree tree, int attrIndex) => new NodeHandle(tree, attrIndex, 1);

    /// <summary>Creates a handle for the namespace at <paramref name="nsIndex"/> in the namespace array.</summary>
    internal static NodeHandle ForNamespace(TinyTree tree, int nsIndex) => new NodeHandle(tree, nsIndex, 2);

    /// <summary>
    /// Backward-compatible constructor that treats <paramref name="index"/> as a regular node index.
    /// Existing call sites that use <c>new NodeHandle(tree, index)</c> continue to compile.
    /// </summary>
    internal NodeHandle(TinyTree tree, int index) : this(tree, index, 0) { }

    // ── Core properties ────────────────────────────────────────────────────────

    /// <summary>The tree that owns this node.</summary>
    internal TinyTree Tree { get; }

    /// <summary>Zero-based index within the kind's own index space (node, attribute, or namespace).</summary>
    internal int Index { get; }

    /// <summary>True when this handle references an attribute node.</summary>
    internal bool IsAttribute => _kindTag == 1;

    /// <summary>True when this handle references a namespace node.</summary>
    internal bool IsNamespace => _kindTag == 2;

    /// <summary>True when this handle references a regular (node-array) node.</summary>
    internal bool IsRegularNode => _kindTag == 0;

    // ── Derived properties ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the XDM node kind for this handle.
    /// For regular nodes the kind is read from the tree; for attribute/namespace it is constant.
    /// </summary>
    internal XdmNodeKind NodeKind =>
        _kindTag switch
        {
            1 => XdmNodeKind.Attribute,
            2 => XdmNodeKind.Namespace,
            _ => Tree.GetKind(Index),
        };

    /// <summary>
    /// Returns the element node index that owns an attribute or namespace handle.
    /// For regular nodes returns the node's own index.
    /// Used by <see cref="XdmNode.CompareDocumentOrder"/>.
    /// </summary>
    internal int OwnerElementIndex =>
        _kindTag switch
        {
            1 => Tree.GetAttributeParent(Index),
            2 => Tree.GetNamespaceParent(Index),
            _ => Index,
        };

    // ── Equality ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(NodeHandle other) =>
        ReferenceEquals(Tree, other.Tree) && Index == other.Index && _kindTag == other._kindTag;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NodeHandle h && Equals(h);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Tree is null ? 0 : RuntimeHelpers.GetHashCode(Tree));
            hash = hash * 31 + Index;
            hash = hash * 31 + _kindTag;
            return hash;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(NodeHandle l, NodeHandle r) => l.Equals(r);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(NodeHandle l, NodeHandle r) => !l.Equals(r);
}
