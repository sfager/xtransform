namespace XTransform.Xdm;

/// <summary>
/// Identifies the kind of an XDM node, as defined in XPath 2.0 Data Model §2.
/// </summary>
public enum XdmNodeKind : byte
{
    /// <summary>A document node (root of a tree). XDM §6.1.</summary>
    Document,

    /// <summary>An element node. XDM §6.2.</summary>
    Element,

    /// <summary>An attribute node. XDM §6.3.</summary>
    Attribute,

    /// <summary>A text node. XDM §6.7.</summary>
    Text,

    /// <summary>A comment node. XDM §6.6.</summary>
    Comment,

    /// <summary>A processing-instruction node. XDM §6.5.</summary>
    ProcessingInstruction,

    /// <summary>A namespace node. XDM §6.4.</summary>
    Namespace,
}
