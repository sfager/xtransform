// XSLT 2.0 §11.9.2 — xsl:copy-of
//   Deep copy of each selected item. Atomics become text; nodes are walked via
//   TinyTreeEmitter.EmitSubtree so attributes, namespaces, and descendants come along.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:copy-of</c>.</summary>
internal sealed class CopyOfInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:copy-of instruction.</summary>
    public CopyOfInstruction(BoundExpression select, SourceSpan location)
        : base(location)
    {
        Select = select;
    }

    /// <summary>The compiled select expression.</summary>
    public BoundExpression Select { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            switch (item)
            {
                case XdmAtomicValue atomic:
                    receiver.Text(atomic.GetStringValue());
                    break;

                case XdmNode node:
                    // For attributes and namespaces, EmitSubtree doesn't apply — they live
                    // outside the main node array. Emit them directly.
                    if (node.NodeKind == XdmNodeKind.Attribute)
                    {
                        TinyTree tree = node.Handle.Tree;
                        int index = node.Handle.Index;
                        receiver.Attribute(
                            tree.GetAttributeNameCode(index),
                            tree.GetAttributeValue(index),
                            Location);
                    }
                    else if (node.NodeKind == XdmNodeKind.Namespace)
                    {
                        receiver.Namespace(node.NodeName!.Value.LocalName, node.GetStringValue());
                    }
                    else
                    {
                        TinyTreeEmitter.EmitSubtree(node.Handle.Tree, node.Handle.Index, receiver);
                    }

                    break;
            }
        }
    }
}
