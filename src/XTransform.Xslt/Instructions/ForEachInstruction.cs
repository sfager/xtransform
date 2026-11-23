using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Compiled <c>xsl:for-each</c>. Iterates the result of <see cref="Select"/>, setting the
/// focus (context item / position / size) to each item in turn and running <see cref="Body"/>.
/// </summary>
internal sealed class ForEachInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:for-each instruction.</summary>
    public ForEachInstruction(BoundExpression select, XsltInstruction body, SourceSpan location)
        : base(location)
    {
        Select = select;
        Body = body;
    }

    /// <summary>The compiled <c>@select</c> expression.</summary>
    public BoundExpression Select { get; }

    /// <summary>The body to run with each item as focus.</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // Materialise so fn:last() is cheap to answer during the iteration.
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        int size = items.Count;
        for (int i = 0; i < size; i++)
        {
            DynamicContext child = context.WithContextItem(items[i], position: i + 1, size: size);
            int marker = run.VariableScopeMarker;
            try
            {
                Body.Execute(child, receiver, run);
            }
            finally
            {
                run.RestoreVariablesTo(marker);
            }
        }
    }
}
