using System.Collections.Generic;
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
    public WithParamInstruction(QName name, ExpressionNode? select, SourceSpan location)
    {
        Name = name;
        Select = select;
        Location = location;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>The compiled <c>@select</c> expression. <see langword="null"/> when a body produces the value (P3-08 scope).</summary>
    public ExpressionNode? Select { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }

    /// <summary>Evaluates the with-param binding in the given context.</summary>
    public XdmSequence Evaluate(DynamicContext context)
    {
        if (Select is null)
        {
            // Body-provided parameter values land in P3-08 (temporary trees). For now,
            // treat missing @select as the empty sequence.
            return XdmSequence.Empty;
        }

        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Select.Iterate(context);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return XdmSequence.From(items);
    }
}
