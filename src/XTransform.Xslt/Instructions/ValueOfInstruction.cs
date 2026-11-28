// XSLT 2.0 §11.4 — xsl:value-of
//   select, or the sequence constructor if there is no select → a sequence of items; each is atomized to a string and the
//   strings are joined by @separator (the default is a single space with select, a zero-length string with content)
//   and emitted as one text node.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:value-of</c>.</summary>
internal sealed class ValueOfInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:value-of instruction; exactly one of <paramref name="select"/> and <paramref name="body"/> is used.</summary>
    public ValueOfInstruction(
        BoundExpression? select,
        CompiledAvt? separator,
        SourceSpan location,
        VariableValueBody? body = null)
        : base(location)
    {
        Select = select;
        Separator = separator;
        Body = body;
    }

    /// <summary>The compiled select expression, or null when the value comes from <see cref="Body"/>.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The content (a sequence constructor) used when there is no select.</summary>
    public VariableValueBody? Body { get; }

    /// <summary>The <c>separator</c> AVT; null means the default (" " with select, "" with content).</summary>
    public CompiledAvt? Separator { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = Select is not null
            ? Select.Iterate(context, run)
            : Body is not null ? Body.Evaluate(context, run).GetIterator() : XTransform.Xdm.Iterators.EmptyIterator.Instance;
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            // Simple content (XSLT 2.0 §5.7.2): zero-length text nodes in the sequence are discarded (a zero-length
            // string is not a node and is kept).
            if (item is XdmNode { NodeKind: XdmNodeKind.Text } textNode && textNode.GetStringValue().Length == 0)
            {
                continue;
            }

            items.Add(item);
        }

        string separator = Separator is not null ? Separator.Stringify(context, run) : Select is not null ? " " : string.Empty;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(items[i].GetStringValue());
        }

        receiver.EmitTextNode(sb.ToString());
    }
}
