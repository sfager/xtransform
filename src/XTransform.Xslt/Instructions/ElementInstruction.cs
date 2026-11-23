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
        string lexical = Name.Stringify(context, run);
        string? explicitNs = Namespace?.Stringify(context, run);

        int colon = lexical.IndexOf(':');
        string prefix;
        string local;
        if (colon < 0)
        {
            prefix = string.Empty;
            local = lexical;
        }
        else
        {
            prefix = lexical.Substring(0, colon);
            local = lexical.Substring(colon + 1);
        }

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
            ns = StaticScope.Resolve(prefix) ?? string.Empty;
        }

        int nameCode = run.NamePool.Allocate(ns, local);
        receiver.StartElement(nameCode, Location);
        Body.Execute(context, receiver, run);
        receiver.EndElement();
    }
}
