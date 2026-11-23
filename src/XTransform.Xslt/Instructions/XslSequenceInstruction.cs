// XSLT 2.0 §12.5 — xsl:sequence
//   Evaluate @select and emit each item into the result sequence without copying.
//   Atomics become text; nodes are forwarded by reference (receiver chain decides).

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:sequence</c>.</summary>
internal sealed class XslSequenceInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:sequence instruction.</summary>
    public XslSequenceInstruction(BoundExpression select, SourceSpan location)
        : base(location)
    {
        Select = select;
    }

    /// <summary>The compiled select expression.</summary>
    public BoundExpression Select { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            receiver.Append(item);
        }
    }
}
