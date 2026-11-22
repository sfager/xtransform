namespace XTransform.Xdm;

/// <summary>
/// Identifies an XPath axis used in location-path steps, as defined in XPath 2.0 §3.2.1.
/// </summary>
public enum Axis : byte
{
    /// <summary>The child axis contains the children of the context node. XPath 2.0 §3.2.1.1.</summary>
    Child,

    /// <summary>The descendant axis contains the descendants of the context node. XPath 2.0 §3.2.1.1.</summary>
    Descendant,

    /// <summary>The descendant-or-self axis contains the context node and its descendants. XPath 2.0 §3.2.1.1.</summary>
    DescendantOrSelf,

    /// <summary>The self axis contains just the context node itself. XPath 2.0 §3.2.1.1.</summary>
    Self,

    /// <summary>The attribute axis contains the attributes of the context node. XPath 2.0 §3.2.1.1.</summary>
    Attribute,

    /// <summary>The parent axis contains the parent of the context node, if there is one. XPath 2.0 §3.2.1.1.</summary>
    Parent,

    /// <summary>The ancestor axis contains the ancestors of the context node. XPath 2.0 §3.2.1.1.</summary>
    Ancestor,

    /// <summary>The ancestor-or-self axis contains the context node and its ancestors. XPath 2.0 §3.2.1.1.</summary>
    AncestorOrSelf,

    /// <summary>The following-sibling axis contains all the following siblings of the context node. XPath 2.0 §3.2.1.1.</summary>
    FollowingSibling,

    /// <summary>The preceding-sibling axis contains all the preceding siblings of the context node. XPath 2.0 §3.2.1.1.</summary>
    PrecedingSibling,

    /// <summary>The following axis contains all nodes in the same document that are after the context node. XPath 2.0 §3.2.1.1.</summary>
    Following,

    /// <summary>The preceding axis contains all nodes in the same document that are before the context node. XPath 2.0 §3.2.1.1.</summary>
    Preceding,

    /// <summary>The namespace axis contains the namespace nodes of the context element. XPath 2.0 §3.2.1.1.</summary>
    Namespace,
}
