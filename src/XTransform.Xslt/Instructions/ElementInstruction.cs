// XSLT 2.0 §11.2 — xsl:element
//   @name is an AVT producing a lexical QName; @namespace is an optional AVT. When
//   @namespace is omitted, the prefix (if any) is resolved against the stylesheet's
//   in-scope namespaces at the xsl:element declaration; an unprefixed name adopts the
//   stylesheet element's default namespace.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:element</c>.</summary>
internal sealed class ElementInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:element instruction.</summary>
    public ElementInstruction(
        CompiledAvt name,
        CompiledAvt? namespaceAvt,
        NamespaceBindings staticScope,
        string defaultElementNamespace,
        XsltInstruction body,
        SourceSpan location)
        : base(location)
    {
        Name = name;
        Namespace = namespaceAvt;
        StaticScope = staticScope;
        DefaultElementNamespace = defaultElementNamespace;
        Body = body;
    }

    /// <summary>The compiled @name AVT.</summary>
    public CompiledAvt Name { get; }

    /// <summary>The optional compiled @namespace AVT.</summary>
    public CompiledAvt? Namespace { get; }

    /// <summary>In-scope namespaces of the originating xsl:element declaration.</summary>
    public NamespaceBindings StaticScope { get; }

    /// <summary>Default element namespace at the xsl:element declaration.</summary>
    public string DefaultElementNamespace { get; }

    /// <summary>Body producing the element's content (attributes + children).</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string lexical = Name.Stringify(context, run).Trim();
        string? explicitNs = Namespace?.Stringify(context, run);

        // XSLT 2.0 §11.2 (XTDE0820): the name must be a lexical QName.
        if (!LexicalNames.IsQName(lexical))
        {
            throw new XsltDynamicException(XsltErrors.XTDE0820, $"'{lexical}' is not a lexical QName, so it cannot name an element", Location);
        }

        LexicalNames.Split(lexical, out string prefix, out string local);

        string ns;
        if (explicitNs is not null)
        {
            ns = explicitNs;
        }
        else if (prefix.Length == 0)
        {
            ns = DefaultElementNamespace;
        }
        else
        {
            // XTDE0830: with no namespace attribute, the prefix must be bound where the instruction is.
            ns = StaticScope.Resolve(prefix)
                ?? throw new XsltDynamicException(XsltErrors.XTDE0830, $"The prefix '{prefix}' of the element name '{lexical}' has no namespace declaration in scope", Location);
        }

        int nameCode = run.NamePool.Allocate(ns, local);
        receiver.StartElement(nameCode, Location);
        Body.Execute(context, receiver, run);
        receiver.EndElement();
    }
}
