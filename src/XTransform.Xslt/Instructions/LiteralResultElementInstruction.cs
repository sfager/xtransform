// XSLT 2.0 §11.1 — Literal result elements.
// The LRE's element QName comes from its source element; attributes are compiled as AVTs
// (LiteralAttributeInstruction). Body is a nested instruction sequence.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>A compiled literal result element.</summary>
internal sealed class LiteralResultElementInstruction : XsltInstruction
{
    /// <summary>Initializes an LRE.</summary>
    public LiteralResultElementInstruction(
        QName name,
        IReadOnlyList<LiteralAttributeInstruction> attributes,
        XsltInstruction body,
        SourceSpan location)
        : base(location)
    {
        Name = name;
        Attributes = attributes;
        Body = body;
    }

    /// <summary>The expanded element name.</summary>
    public QName Name { get; }

    /// <summary>Attributes emitted before any content.</summary>
    public IReadOnlyList<LiteralAttributeInstruction> Attributes { get; }

    /// <summary>The nested body (children).</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        int nameCode = run.NamePool.Allocate(Name.NamespaceUri, Name.LocalName);
        receiver.StartElement(nameCode, Location);

        for (int i = 0; i < Attributes.Count; i++)
        {
            Attributes[i].Execute(context, receiver, run);
        }

        Body.Execute(context, receiver, run);
        receiver.EndElement();
    }
}
