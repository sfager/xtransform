// Wraps a compiled XPath expression with the ordered list of external-variable names it
// was compiled against. At runtime we create a fresh frame of the compiler-reported
// SlotCount and seed slots 0..Externals.Count-1 with the current values of those variables
// from XsltRunState.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Functions;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// A compiled XPath expression, carrying the ordered list of external-variable names it was
/// compiled with. The runtime creates a fresh local frame per evaluation and copies current
/// variable values from <see cref="XsltRunState"/> into the first N slots.
/// </summary>
internal sealed class BoundExpression
{
    /// <summary>Initializes a bound expression.</summary>
    public BoundExpression(ExpressionNode root, int slotCount, IReadOnlyList<QName> externals)
    {
        Root = root;
        SlotCount = slotCount;
        Externals = externals;
    }

    /// <summary>Compiled expression root.</summary>
    public ExpressionNode Root { get; }

    /// <summary>Total local-variable slots the expression needs at runtime.</summary>
    public int SlotCount { get; }

    /// <summary>External variables in slot-allocation order (slots 0..N-1).</summary>
    public IReadOnlyList<QName> Externals { get; }

    /// <summary>Builds a context whose frame has the externals seeded for evaluation.</summary>
    public DynamicContext Prepare(DynamicContext parent, XsltRunState run)
    {
        DynamicContext ctx = parent.WithFrame(SlotCount);
        for (int i = 0; i < Externals.Count; i++)
        {
            ctx.LocalFrame[i] = run.LookupVariable(Externals[i]);
        }

        return ctx;
    }

    /// <summary>Evaluates the expression and materialises the result sequence.</summary>
    public XdmSequence Evaluate(DynamicContext parent, XsltRunState run)
    {
        DynamicContext ctx = Prepare(parent, run);
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Root.Iterate(ctx);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return XdmSequence.From(items);
    }

    /// <summary>Evaluates and returns the result as an iterator (lazy).</summary>
    public IXdmIterator Iterate(DynamicContext parent, XsltRunState run)
    {
        return Root.Iterate(Prepare(parent, run));
    }

    /// <summary>Returns the effective boolean value of the expression.</summary>
    public bool EffectiveBooleanValue(DynamicContext parent, XsltRunState run)
    {
        // Delegate to the XPath expression's own early-stopping EBV implementation by
        // preparing the context (seeding externals) and calling EffectiveBooleanValue on
        // the compiled root.
        return Root.EffectiveBooleanValue(Prepare(parent, run));
    }
}
