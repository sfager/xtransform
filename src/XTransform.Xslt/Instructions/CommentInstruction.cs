// XSLT 2.0 §11.8 — xsl:comment
//   Content comes from the body; empty-body is permitted. XTDE0450 when the resulting
//   content contains a "--" sequence or ends with "-" (illegal in XML comments).

using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:comment</c>.</summary>
internal sealed class CommentInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:comment instruction.</summary>
    public CommentInstruction(BoundExpression? select, XsltInstruction? body, SourceSpan location)
        : base(location)
    {
        Select = select;
        Body = body;
    }

    /// <summary>Compiled @select — null when the body produces the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>Body (when @select absent).</summary>
    public XsltInstruction? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string content;
        if (Select is not null)
        {
            StringBuilder sb = new StringBuilder();
            IXdmIterator iter = Select.Iterate(context, run);
            XdmItem? item;
            bool first = true;
            while ((item = iter.Next()) is not null)
            {
                if (!first) { sb.Append(' '); }
                sb.Append(item.GetStringValue());
                first = false;
            }

            content = sb.ToString();
        }
        else if (Body is not null)
        {
            CommentStringCollector collector = new CommentStringCollector();
            Body.Execute(context, collector, run);
            content = collector.Result;
        }
        else
        {
            content = string.Empty;
        }

        ValidateCommentContent(content);
        receiver.Comment(content);
    }

    private void ValidateCommentContent(string content)
    {
        // XML 1.0 §2.5: a comment may not contain "--" and may not end with "-".
        if (content.Contains("--") || (content.Length > 0 && content[content.Length - 1] == '-'))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0450,
                "xsl:comment content must not contain '--' or end with '-'",
                Location);
        }
    }

    private sealed class CommentStringCollector : IResultReceiver
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
