using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Compiled <c>xsl:with-param</c>. Evaluates to a (name → sequence) binding consumed by a
/// matching <c>xsl:param</c> in the invoked template or function.
/// </summary>
internal sealed class WithParamInstruction
{
    /// <summary>Initializes a with-param binding.</summary>
    public WithParamInstruction(QName name, BoundExpression? select, SourceSpan location)
    {
        Name = name;
        Select = select;
        Location = location;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>The compiled <c>@select</c> expression. <see langword="null"/> when a body produces the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }

    /// <summary>Evaluates the with-param binding in the given context.</summary>
    public XdmSequence Evaluate(DynamicContext context, XsltRunState run)
    {
        if (Select is null)
        {
            // Body-provided parameter values land with xsl:variable body support; for now
            // treat a missing @select as the empty sequence.
            return XdmSequence.Empty;
        }

        return Select.Evaluate(context, run);
    }
}
