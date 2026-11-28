// XSLT 2.0 §11.7 — xsl:namespace
//   @name is an AVT giving the prefix (zero-length for the default namespace); the value comes from @select or the body.

using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:namespace</c>: puts a namespace node on the element being constructed.</summary>
internal sealed class NamespaceInstruction : XsltInstruction
{
    private const string XmlNamespaceUri = "http://www.w3.org/XML/1998/namespace";
    private const string XmlnsNamespaceUri = "http://www.w3.org/2000/xmlns/";

    /// <summary>Initializes an xsl:namespace instruction.</summary>
    public NamespaceInstruction(CompiledAvt name, BoundExpression? select, XsltInstruction? body, SourceSpan location)
        : base(location)
    {
        Name = name;
        Select = select;
        Body = body;
    }

    /// <summary>The compiled @name AVT.</summary>
    public CompiledAvt Name { get; }

    /// <summary>The compiled @select, or null when the body gives the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The body, when there is no @select.</summary>
    public XsltInstruction? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string prefix = Name.Stringify(context, run).Trim();

        // XTDE0920: the name is a zero-length string or an NCName, and not xmlns.
        if ((prefix.Length > 0 && !LexicalNames.IsNCName(prefix)) || prefix == "xmlns")
        {
            throw new XsltDynamicException(XsltErrors.XTDE0920, $"'{prefix}' is not a valid name for a namespace node", Location);
        }

        string uri = Select is not null ? SelectToString(context, run) : BodyToString(context, run);

        // XTDE0930: the value must not be a zero-length string.
        if (uri.Length == 0)
        {
            throw new XsltDynamicException(XsltErrors.XTDE0930, "The value of a namespace node must not be a zero-length string", Location);
        }

        // XTDE0905: the value is an xs:anyURI other than the namespace of xmlns.
        if (!IsAnyUri(uri) || string.Equals(uri, XmlnsNamespaceUri, System.StringComparison.Ordinal))
        {
            throw new XsltDynamicException(XsltErrors.XTDE0905, $"'{uri}' is not a valid namespace URI", Location);
        }

        // XTDE0925: the prefix xml is bound to the XML namespace, and that namespace to no other prefix.
        if ((prefix == "xml") != string.Equals(uri, XmlNamespaceUri, System.StringComparison.Ordinal))
        {
            throw new XsltDynamicException(XsltErrors.XTDE0925, $"The prefix '{prefix}' cannot be bound to '{uri}'", Location);
        }

        receiver.Namespace(prefix, uri);
    }

    // An xs:anyURI has at most one fragment marker and no characters a URI reference cannot hold.
    private static bool IsAnyUri(string value)
    {
        int firstHash = value.IndexOf('#');
        if (firstHash >= 0 && firstHash != value.LastIndexOf('#'))
        {
            return false;
        }

        return System.Uri.TryCreate(value, System.UriKind.RelativeOrAbsolute, out _);
    }

    private string SelectToString(DynamicContext context, XsltRunState run)
    {
        IXdmIterator iterator = Select!.Iterate(context, run);
        StringBuilder builder = new StringBuilder();
        XdmItem? item;
        bool first = true;
        while ((item = iterator.Next()) is not null)
        {
            if (!first)
            {
                builder.Append(' ');
            }

            builder.Append(item.GetStringValue());
            first = false;
        }

        return builder.ToString();
    }

    private string BodyToString(DynamicContext context, XsltRunState run)
    {
        if (Body is null)
        {
            return string.Empty;
        }

        // The value is the string value of the content, so an attribute node in it contributes its value (XSLT 2.0 §5.7.2).
        StringCollectingReceiver collector = new StringCollectingReceiver(collectAttributeValues: true);
        Body.Execute(context, collector, run);
        return collector.Result;
    }
}
