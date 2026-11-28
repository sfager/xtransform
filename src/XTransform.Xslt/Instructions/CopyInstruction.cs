// XSLT 2.0 §11.9 — xsl:copy
//   Shallow copy of the context node; body produces the children/attributes.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:copy</c>.</summary>
internal sealed class CopyInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:copy instruction.</summary>
    public CopyInstruction(XsltInstruction body, SourceSpan location)
        : base(location)
    {
        Body = body;
    }

    /// <summary>Body producing the copied node's content (attributes + children).</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        if (context.ContextItem is not XdmNode node)
        {
            // Atomic context item: xsl:copy emits the atomized value as text.
            if (context.ContextItem is XdmAtomicValue atomic)
            {
                receiver.Append(atomic);
            }

            return;
        }

        TinyTree tree = node.Handle.Tree;
        int index = node.Handle.Index;

        switch (node.NodeKind)
        {
            case XdmNodeKind.Element:
                receiver.StartElement(tree.GetNameCode(index), Location);
                if (receiver is IBaseUriReceiver baseReceiver && node.BaseUri is { } copiedBase)
                {
                    baseReceiver.BaseUri(copiedBase);
                }

                // Body produces the attributes + children. The caller is responsible for
                // ordering (xsl:attribute siblings before any content).
                Body.Execute(context, receiver, run);
                receiver.EndElement();
                return;

            case XdmNodeKind.Attribute:
                receiver.Attribute(tree.GetAttributeNameCode(index), tree.GetAttributeValue(index), Location);
                return;

            case XdmNodeKind.Text:
                receiver.Text(node.GetStringValue());
                return;

            case XdmNodeKind.Comment:
                receiver.Comment(node.GetStringValue());
                return;

            case XdmNodeKind.ProcessingInstruction:
                receiver.ProcessingInstruction(
                    node.NodeName!.Value.LocalName,
                    node.GetStringValue());
                return;

            case XdmNodeKind.Document:
                // A document node copies to a new document node with the content the body constructs (XSLT 2.0 §11.9).
                receiver.StartDocument();
                try
                {
                    Body.Execute(context, receiver, run);
                }
                finally
                {
                    receiver.EndDocument();
                }

                return;

            case XdmNodeKind.Namespace:
                receiver.Namespace(node.NodeName!.Value.LocalName, node.GetStringValue());
                return;
        }
    }
}
