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
        SourceSpan location)
        : base(location)
    {
        Name = name;
        Namespace = namespaceAvt;
        StaticScope = staticScope;
        Select = select;
        Body = body;
    }

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
        string value = Select is not null
            ? SelectToString(context, run)
            : BodyToString(context, run);

        int nameCode = run.NamePool.Allocate(ns, local);
        receiver.Attribute(nameCode, value, Location);
    }

    private string SelectToString(DynamicContext context, XsltRunState run)
    {
        IXdmIterator iter = Select!.Iterate(context, run);
        StringBuilder sb = new StringBuilder();
        XdmItem? item;
        bool first = true;
        while ((item = iter.Next()) is not null)
        {
            if (!first)
            {
                sb.Append(' ');
            }

            sb.Append(item.GetStringValue());
            first = false;
        }

        return sb.ToString();
    }

    private string BodyToString(DynamicContext context, XsltRunState run)
    {
        if (Body is null)
        {
            return string.Empty;
        }

        StringCollectingReceiver collector = new StringCollectingReceiver();
        Body.Execute(context, collector, run);
        return collector.Result;
    }

    private sealed class StringCollectingReceiver : IResultReceiver
    {
        private readonly StringBuilder _sb = new StringBuilder();
        public string Result => _sb.ToString();

        public void StartDocument() { }
        public void EndDocument() { }
        public void StartElement(int nameCode, SourceSpan location) { }
        public void Namespace(string prefix, string uri) { }
        public void Attribute(int nameCode, string value, SourceSpan location) { }
        public void EndElement() { }
        public void Text(string text) => _sb.Append(text);
        public void Comment(string text) { }
        public void ProcessingInstruction(string target, string data) { }
        public void Append(XdmItem item)
        {
            if (item is not null)
            {
                _sb.Append(item.GetStringValue());
            }
        }
    }
}
