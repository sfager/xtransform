// XSLT 2.0 §13.2 — xsl:perform-sort: sorts a sequence (from @select or from its content) and returns it.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:perform-sort</c>.</summary>
internal sealed class PerformSortInstruction : XsltInstruction
{
    /// <summary>Initializes the instruction. At most one of <paramref name="select"/> and <paramref name="content"/> is non-null.</summary>
    public PerformSortInstruction(
        BoundExpression? select,
        VariableValueBody? content,
        IReadOnlyList<SortKey> sorts,
        SourceSpan location)
        : base(location)
    {
        Select = select;
        Content = content;
        Sorts = sorts;
    }

    /// <summary>The <c>select</c> expression, if any.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The content that produces the sequence when there is no <c>select</c>.</summary>
    public VariableValueBody? Content { get; }

    /// <summary>The sort keys.</summary>
    public IReadOnlyList<SortKey> Sorts { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator source = Select is not null
            ? Select.Iterate(context, run)
            : Content is not null
                ? Content.Evaluate(context, run).GetIterator()
                : XTransform.Xdm.Iterators.EmptyIterator.Instance;
        XdmItem? item;
        while ((item = source.Next()) is not null)
        {
            items.Add(item);
        }

        List<XdmItem> sorted = ItemSorter.Sort(items, Sorts, context, run);
        for (int i = 0; i < sorted.Count; i++)
        {
            receiver.Append(sorted[i]);
        }
    }
}
