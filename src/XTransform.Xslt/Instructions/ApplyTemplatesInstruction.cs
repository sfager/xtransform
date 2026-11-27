using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// How the <c>mode</c> attribute was spelled on <c>xsl:apply-templates</c>.
/// </summary>
internal enum ApplyTemplatesMode
{
    /// <summary>No <c>mode</c> attribute or <c>mode="#default"</c>: the default mode.</summary>
    Default,
    /// <summary><c>mode="#current"</c>: inherit the caller's mode at runtime.</summary>
    Current,
    /// <summary>A specific mode QName in <see cref="ApplyTemplatesInstruction.Mode"/>.</summary>
    Named,
}

/// <summary>
/// Compiled <c>xsl:apply-templates</c>. For each item from <see cref="Select"/>,
/// chooses the best-matching template rule in the resolved mode table and runs it;
/// falls back to the built-in rules when no match (XSLT 2.0 §6.6).
/// </summary>
internal sealed class ApplyTemplatesInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:apply-templates instruction.</summary>
    public ApplyTemplatesInstruction(
        BoundExpression select,
        ApplyTemplatesMode modeSpec,
        QName mode,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan location,
        IReadOnlyList<SortKey>? sorts = null)
        : base(location)
    {
        Select = select;
        ModeSpec = modeSpec;
        Mode = mode;
        WithParams = withParams;
        Sorts = sorts ?? System.Array.Empty<SortKey>();
    }

    /// <summary>Compiled <c>@select</c> (default: <c>child::node()</c>).</summary>
    public BoundExpression Select { get; }

    /// <summary>How the mode was declared.</summary>
    public ApplyTemplatesMode ModeSpec { get; }

    /// <summary>The resolved mode when <see cref="ModeSpec"/> is <see cref="ApplyTemplatesMode.Named"/>.</summary>
    public QName Mode { get; }

    /// <summary>The <c>xsl:sort</c> keys (XSLT 2.0 §13); empty when the items are processed in selection order.</summary>
    public IReadOnlyList<SortKey> Sorts { get; }

    /// <summary>The <c>xsl:with-param</c> bindings.</summary>
    public IReadOnlyList<WithParamInstruction> WithParams { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // Materialise the selection to drive focus/position/size through the loop.
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        if (Sorts.Count > 0)
        {
            items = ItemSorter.Sort(items, Sorts, context, run);
        }

        QName effectiveMode = ModeSpec switch
        {
            ApplyTemplatesMode.Current => run.CurrentMode,
            ApplyTemplatesMode.Named => Mode,
            _ => DeclarationCollector.DefaultMode,
        };

        int size = items.Count;

        // The xsl:with-param values are computed once, in the focus of this instruction (XSLT 2.0 §6.5).
        IReadOnlyList<WithParamInstruction> bound = WithParams;
        if (WithParams.Count > 0)
        {
            List<WithParamInstruction> values = new List<WithParamInstruction>(WithParams.Count);
            foreach (WithParamInstruction p in WithParams)
            {
                values.Add(p.Bind(context, run));
            }

            bound = values;
        }

        QName savedMode = run.CurrentMode;
        try
        {
            run.CurrentMode = effectiveMode;
            for (int i = 0; i < size; i++)
            {
                context.CheckCancellation();
                DynamicContext childCtx = context.WithCurrentItem(items[i], position: i + 1, size: size);
                ModeDispatcher.Dispatch(childCtx, receiver, run, effectiveMode, bound, Location);
            }
        }
        finally
        {
            run.CurrentMode = savedMode;
        }
    }
}
