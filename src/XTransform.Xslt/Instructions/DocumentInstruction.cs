// XSLT 2.0 §11.1.3 — xsl:document
//   Wraps its body in a document node. For our serialisation-centric receiver chain this
//   maps to a StartDocument / EndDocument pair.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:document</c>.</summary>
internal sealed class DocumentInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:document instruction.</summary>
    public DocumentInstruction(XsltInstruction body, SourceSpan location)
        : base(location)
    {
        Body = body;
    }

    /// <summary>The body producing the document's content.</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        receiver.StartDocument();
        try
        {
            Body.Execute(context, receiver, run);
        }
        finally
        {
            receiver.EndDocument();
        }
    }
}
