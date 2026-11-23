// XSLT 2.0 §9 — xsl:variable. At runtime: evaluate the select expression, push the
// result onto the current variable scope. The compiler restores the scope marker when the
// enclosing element ends, so the binding has lexical extent from the declaration through
// the last sibling of its parent.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled local <c>xsl:variable</c>.</summary>
internal sealed class VariableInstruction : XsltInstruction
{
    /// <summary>Initializes a local xsl:variable.</summary>
    public VariableInstruction(QName name, BoundExpression? select, SourceSpan location)
        : base(location)
    {
        Name = name;
        Select = select;
    }

    /// <summary>The variable's expanded name.</summary>
    public QName Name { get; }

    /// <summary>The compiled select expression; <see langword="null"/> means empty sequence (body form not yet supported).</summary>
    public BoundExpression? Select { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        XdmSequence value = Select is null ? XdmSequence.Empty : Select.Evaluate(context, run);
        run.PushVariable(Name, value);
    }
}
