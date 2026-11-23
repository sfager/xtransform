using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Base class for every node in the stylesheet AST produced by
/// <see cref="StylesheetReader"/>. Only element and text content is represented; comments
/// and processing instructions in the stylesheet source are discarded (XSLT 2.0 §3.6).
/// </summary>
internal abstract class StylesheetNode
{
    /// <summary>Source location of this node.</summary>
    public SourceSpan Location { get; }

    /// <summary>Initializes a node with its source location.</summary>
    protected StylesheetNode(SourceSpan location)
    {
        Location = location;
    }
}

/// <summary>
/// An element in the stylesheet AST. Attributes are exposed as raw strings; attribute-value
/// template parsing is the job of P3-02, not the reader.
/// </summary>
internal sealed class StylesheetElement : StylesheetNode
{
    private readonly List<StylesheetAttribute> _attributes;
    private readonly List<StylesheetNode> _children;

    /// <summary>Initializes an element with resolved name, ns bindings, and empty child lists.</summary>
    public StylesheetElement(
        QName name,
        NamespaceBindings inScopeNamespaces,
        SourceSpan location)
        : base(location)
    {
        Name = name;
        InScopeNamespaces = inScopeNamespaces;
        _attributes = new List<StylesheetAttribute>();
        _children = new List<StylesheetNode>();
    }

    /// <summary>The expanded element name (namespace URI + local).</summary>
    public QName Name { get; }

    /// <summary>In-scope namespace bindings at this element.</summary>
    public NamespaceBindings InScopeNamespaces { get; }

    /// <summary>True when the element is in the XSLT namespace.</summary>
    public bool IsXsltElement =>
        string.Equals(Name.NamespaceUri, XsltNamespace.Uri, System.StringComparison.Ordinal);

    /// <summary>True when the element is unknown — i.e. in the XSLT namespace but its local name is not an XSLT 2.0 element.</summary>
    public bool IsUnknownXsltElement => IsXsltElement && !XsltElementNames.IsKnown(Name.LocalName);

    /// <summary>
    /// Non-null when this element declared <c>xsl:version</c> or sits on <c>xsl:stylesheet</c>/<c>xsl:transform</c>
    /// with a <c>version</c> attribute. Empty string when not declared here.
    /// </summary>
    public string DeclaredVersion { get; internal set; } = string.Empty;

    /// <summary>Attributes declared on this element, excluding namespace declarations.</summary>
    public IReadOnlyList<StylesheetAttribute> Attributes => _attributes;

    /// <summary>Child nodes (elements + text) in document order.</summary>
    public IReadOnlyList<StylesheetNode> Children => _children;

    internal void AppendAttribute(StylesheetAttribute attribute) => _attributes.Add(attribute);

    internal void AppendChild(StylesheetNode child) => _children.Add(child);

    /// <summary>Replace the children list wholesale (used by passes that filter or rewrite children).</summary>
    internal void ReplaceChildren(IEnumerable<StylesheetNode> newChildren)
    {
        _children.Clear();
        _children.AddRange(newChildren);
    }
}

/// <summary>A text node (whitespace + content) child of an element.</summary>
internal sealed class StylesheetText : StylesheetNode
{
    /// <summary>Initializes a text node with raw text and location.</summary>
    public StylesheetText(string text, SourceSpan location)
        : base(location)
    {
        Text = text;
    }

    /// <summary>The raw text content.</summary>
    public string Text { get; }

    /// <summary>True when the text consists entirely of whitespace per XML §2.10.</summary>
    public bool IsWhitespaceOnly
    {
        get
        {
            for (int i = 0; i < Text.Length; i++)
            {
                char c = Text[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return false;
                }
            }

            return true;
        }
    }
}

/// <summary>An attribute on a stylesheet element. The raw value is kept intact (AVT splitting is P3-02).</summary>
internal sealed class StylesheetAttribute
{
    /// <summary>Initializes an attribute with resolved name, raw value, and location.</summary>
    public StylesheetAttribute(QName name, string value, SourceSpan location)
    {
        Name = name;
        Value = value;
        Location = location;
    }

    /// <summary>The expanded attribute name.</summary>
    public QName Name { get; }

    /// <summary>The attribute value, verbatim from the source.</summary>
    public string Value { get; }

    /// <summary>Source location (points at the element line/col; column precision is XmlReader-dependent).</summary>
    public SourceSpan Location { get; }

    /// <summary>
    /// The parsed attribute value template, when <see cref="StylesheetProcessor"/> has treated this
    /// attribute as an AVT; <see langword="null"/> when the attribute's value should be taken verbatim
    /// (e.g. <c>select</c>, <c>test</c>, <c>match</c>).
    /// </summary>
    public AttributeValueTemplate? ParsedTemplate { get; internal set; }
}
