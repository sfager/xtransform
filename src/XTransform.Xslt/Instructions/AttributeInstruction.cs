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
        string lexical = Name.Stringify(context, run);
        string? explicitNs = Namespace?.Stringify(context, run);

        int colon = lexical.IndexOf(':');
        string prefix = colon < 0 ? string.Empty : lexical.Substring(0, colon);
        string local = colon < 0 ? lexical : lexical.Substring(colon + 1);

        string ns;
        if (explicitNs is not null)
        {
            ns = explicitNs;
        }
        else if (prefix.Length == 0)
        {
            // Attributes with no prefix are in no namespace (XSLT 2.0 §11.3, unlike elements).
            ns = string.Empty;
        }
        else
        {
            ns = StaticScope.Resolve(prefix) ?? string.Empty;
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
