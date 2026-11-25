// XSLT 2.0 §6.6 — built-in template rules. Applied when xsl:apply-templates finds no
// matching rule in the chosen mode.
//   document / element → <xsl:apply-templates/> (recurse into children, preserving mode)
//   text / attribute   → copy the string value
//   comment / PI / namespace → no-op (consumed; no output)

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;
using XTransform.XPath;

namespace XTransform.Xslt;

internal static class BuiltInRules
{
    /// <summary>Applies the built-in rule to the current focus node.</summary>
    public static void Apply(
        DynamicContext context,
        IResultReceiver receiver,
        XsltRunState run,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan callSite)
    {
        if (context.ContextItem is not XdmNode node)
        {
            // Atomics: emit their string value as a text node (XSLT 2.0 §6.6 built-in for text).
            if (context.ContextItem is XdmAtomicValue atomic)
            {
                receiver.Text(atomic.GetStringValue());
            }

            return;
        }

        switch (node.NodeKind)
        {
            case XdmNodeKind.Document:
            case XdmNodeKind.Element:
                // Recurse into children.
                QName mode = run.CurrentMode;
                int i = 0;
                List<XdmNode> children = new List<XdmNode>();
                foreach (XdmNode child in node.Axis(Axis.Child))
                {
                    children.Add(child);
                }

                int size = children.Count;
                for (i = 0; i < size; i++)
                {
                    DynamicContext childCtx = context.WithCurrentItem(children[i], position: i + 1, size: size);
                    ModeDispatcher.Dispatch(childCtx, receiver, run, mode, withParams, callSite);
                }

                break;

            case XdmNodeKind.Text:
            case XdmNodeKind.Attribute:
                receiver.Text(node.GetStringValue());
                break;

            case XdmNodeKind.Comment:
            case XdmNodeKind.ProcessingInstruction:
            case XdmNodeKind.Namespace:
                // No output.
                break;
        }
    }
}
