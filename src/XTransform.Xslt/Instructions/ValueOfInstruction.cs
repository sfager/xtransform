// XSLT 2.0 §12.4 — xsl:value-of
//   select → sequence of items; each is atomized to a string; joined by @separator
//   (default: a single space in XSLT 2.0) and emitted as one text node.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:value-of</c>.</summary>
internal sealed class ValueOfInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:value-of instruction.</summary>
    public ValueOfInstruction(ExpressionNode select, string separator, SourceSpan location)
        : base(location)
    {
        Select = select;
        Separator = separator;
    }

    /// <summary>The compiled select expression.</summary>
    public ExpressionNode Select { get; }

    /// <summary>Separator between adjacent item string values (XSLT 2.0 default: " ").</summary>
    public string Separator { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Select.Iterate(context);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        if (items.Count == 0)
        {
            // Empty selection → no text node emitted.
            return;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(Separator);
            }

            sb.Append(items[i].GetStringValue());
        }

        receiver.Text(sb.ToString());
    }
}
