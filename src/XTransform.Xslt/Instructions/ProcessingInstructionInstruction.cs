// XSLT 2.0 §11.7 — xsl:processing-instruction
//   @name is an AVT producing the target NCName; content is from @select or body.

using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:processing-instruction</c>.</summary>
internal sealed class ProcessingInstructionInstruction : XsltInstruction
{
    /// <summary>Initializes a processing-instruction constructor.</summary>
    public ProcessingInstructionInstruction(
        CompiledAvt name,
        BoundExpression? select,
        XsltInstruction? body,
        SourceSpan location)
        : base(location)
    {
        Name = name;
        Select = select;
        Body = body;
    }

    /// <summary>The compiled @name AVT (target).</summary>
    public CompiledAvt Name { get; }

    /// <summary>Compiled @select; null when the body produces the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The optional body.</summary>
    public XsltInstruction? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string target = Name.Stringify(context, run);

        string data;
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

            data = sb.ToString();
        }
        else if (Body is not null)
        {
            PiCollector collector = new PiCollector();
            Body.Execute(context, collector, run);
            data = collector.Result;
        }
        else
        {
            data = string.Empty;
        }

        receiver.ProcessingInstruction(target, data);
    }

    private sealed class PiCollector : IResultReceiver
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
