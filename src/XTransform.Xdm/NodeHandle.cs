using System;

namespace XTransform.Xdm;

/// <summary>
/// Lightweight handle to a single node within a <see cref="TinyTree"/>.
/// A value of this struct is only meaningful in conjunction with the tree it was obtained from.
/// </summary>
internal readonly struct NodeHandle : IEquatable<NodeHandle>
{
    /// <summary>Initializes a new handle to the node at <paramref name="index"/> in <paramref name="tree"/>.</summary>
    internal NodeHandle(TinyTree tree, int index)
    {
        Tree = tree;
        Index = index;
    }

    /// <summary>The tree that owns this node.</summary>
    internal TinyTree Tree { get; }

    /// <summary>Zero-based document-order index of the node within its tree.</summary>
    internal int Index { get; }

    /// <summary>Returns true when both handles refer to the same node in the same tree instance.</summary>
    public bool Equals(NodeHandle other) =>
        ReferenceEquals(Tree, other.Tree) && Index == other.Index;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NodeHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Tree is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Tree));
            hash = hash * 31 + Index;
            return hash;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(NodeHandle l, NodeHandle r) => l.Equals(r);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(NodeHandle l, NodeHandle r) => !l.Equals(r);
}
