namespace XTransform.Xdm;

/// <summary>
/// Base class for all XDM items (atomic values and nodes).
/// </summary>
/// <remarks>
/// XPath 2.0 §2.1 defines the XDM data model; an item is either a node or an atomic value.
/// </remarks>
public abstract class XdmItem
{
    /// <summary>Prevents external derivation; only <see cref="XdmAtomicValue"/> and XdmNode (P1-09) may inherit.</summary>
    private protected XdmItem() { }

    /// <summary>
    /// <see langword="true"/> when this item is a node; <see langword="false"/> for atomic values.
    /// </summary>
    public abstract bool IsNode { get; }

    /// <summary>
    /// <see langword="true"/> when this item is an atomic value; <see langword="false"/> for nodes.
    /// </summary>
    public abstract bool IsAtomic { get; }

    /// <summary>
    /// Returns the XPath string value of this item.
    /// For atomic values this is the canonical XSD lexical representation.
    /// </summary>
    /// <returns>The string value; never <see langword="null"/>.</returns>
    public abstract string GetStringValue();
}
