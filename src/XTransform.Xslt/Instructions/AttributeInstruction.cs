// XSLT 2.0 §11.3 — xsl:attribute
//   @name is an AVT producing a lexical QName; @namespace is an optional AVT. If @select is
//   present it provides the value; otherwise the body's text content is used.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:attribute</c>.</summary>
internal sealed class AttributeInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:attribute instruction.</summary>
    public AttributeInstruction(
        CompiledAvt name,
        CompiledAvt? namespaceAvt,
        NamespaceBindings staticScope,
        BoundExpression? select,
        XsltInstruction? body,
        SourceSpan location,
        CompiledAvt? separator = null)
        : base(location)
    {
        Separator = separator;
        Name = name;
        Namespace = namespaceAvt;
        StaticScope = staticScope;
        Select = select;
        Body = body;
    }

    /// <summary>Compiled @separator AVT; the default is a single space with select, none with content.</summary>
    public CompiledAvt? Separator { get; }

    /// <summary>Compiled @name AVT.</summary>
    public CompiledAvt Name { get; }

    /// <summary>Optional compiled @namespace AVT.</summary>
    public CompiledAvt? Namespace { get; }

    /// <summary>Static in-scope namespaces at the xsl:attribute declaration.</summary>
    public NamespaceBindings StaticScope { get; }

    /// <summary>Compiled @select — null when the body provides the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>Body (when @select is absent); produces text events that form the attribute value.</summary>
    public XsltInstruction? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string lexical = Name.Stringify(context, run).Trim();
        string? explicitNs = Namespace?.Stringify(context, run);

        // XSLT 2.0 §11.3.1 (XTDE0850): the name must be a lexical QName.
        if (!LexicalNames.IsQName(lexical))
        {
            throw new XsltDynamicException(XsltErrors.XTDE0850, $"'{lexical}' is not a lexical QName, so it cannot name an attribute", Location);
        }

        LexicalNames.Split(lexical, out string prefix, out string local);

        string ns;
        if (explicitNs is not null)
        {
            ns = explicitNs;

            // XSLT 2.0 §11.3.1: the attribute is in the namespace given, even when its prefix is bound to another one in
            // the stylesheet, and an attribute in a namespace needs a prefix. The prefix is implementation-defined here:
            // the name's own when the stylesheet binds it to that namespace or not at all, otherwise a generated one.
            if (ns.Length > 0)
            {
                string? bound = prefix.Length > 0 ? StaticScope.Resolve(prefix) : null;
                if (prefix.Length == 0 || (bound is not null && !string.Equals(bound, ns, System.StringComparison.Ordinal)))
                {
                    prefix = GeneratedPrefix();
                }
            }
        }
        else if (prefix.Length == 0)
        {
            // XTDE0855: an attribute named xmlns would declare a namespace.
            if (local == "xmlns")
            {
                throw new XsltDynamicException(XsltErrors.XTDE0855, "An attribute cannot be named xmlns", Location);
            }

            // Attributes with no prefix are in no namespace (XSLT 2.0 §11.3, unlike elements).
            ns = string.Empty;
        }
        else
        {
            // XTDE0860: with no namespace attribute, the prefix must be bound where the instruction is.
            ns = StaticScope.Resolve(prefix)
                ?? throw new XsltDynamicException(XsltErrors.XTDE0860, $"The prefix '{prefix}' of the attribute name '{lexical}' has no namespace declaration in scope", Location);
        }

        // Evaluate the value: @select wins; otherwise run the body into a scratch collector.
        // XSLT 2.0 §11.3.1: the default separator is a space with select and a zero-length string with content.
        string separator = Separator?.Stringify(context, run) ?? (Select is not null ? " " : string.Empty);
        string value = Select is not null
            ? SelectToString(context, run, separator)
            : BodyToString(context, run, separator);

        int nameCode = run.NamePool.Allocate(prefix, ns, local);
        receiver.Attribute(nameCode, value, Location);
    }

    // ns0, ns1, ...: the first one the stylesheet does not bind where the instruction is.
    private string GeneratedPrefix()
    {
        for (int i = 0; ; i++)
        {
            string candidate = "ns" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (StaticScope.Resolve(candidate) is null)
            {
                return candidate;
            }
        }
    }

    private string SelectToString(DynamicContext context, XsltRunState run, string separator)
    {
        IXdmIterator iter = Select!.Iterate(context, run);
        StringBuilder sb = new StringBuilder();
        XdmItem? item;
        bool first = true;
        while ((item = iter.Next()) is not null)
        {
            if (!first)
            {
                sb.Append(separator);
            }

            sb.Append(item.GetStringValue());
            first = false;
        }

        return sb.ToString();
    }

    private string BodyToString(DynamicContext context, XsltRunState run, string separator)
    {
        if (Body is null)
        {
            return string.Empty;
        }

        StringCollectingReceiver collector = new StringCollectingReceiver { AtomicSeparator = separator };
        Body.Execute(context, collector, run);
        return collector.Result;
    }
}
