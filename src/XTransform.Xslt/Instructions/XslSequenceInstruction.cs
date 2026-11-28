// XSLT 2.0 §11.10 — xsl:sequence
//   Evaluate @select (or, in the body form, the sequence constructor) and emit each item into the result sequence
//   without copying. Atomic values stay atomic; nodes are forwarded by reference.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:sequence</c>.</summary>
internal sealed class XslSequenceInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:sequence instruction; <paramref name="select"/> or <paramref name="body"/> supplies the value.</summary>
    public XslSequenceInstruction(BoundExpression? select, SourceSpan location, VariableValueBody? body = null)
        : base(location)
    {
        Select = select;
        Body = body;
    }

    /// <summary>The compiled select expression, or null for the body form.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The content used when there is no select.</summary>
    public VariableValueBody? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        IXdmIterator iter = Select is not null
            ? Select.Iterate(context, run)
            : Body is not null ? Body.Evaluate(context, run).GetIterator() : XTransform.Xdm.Iterators.EmptyIterator.Instance;
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            receiver.Append(item);
        }
    }
}
