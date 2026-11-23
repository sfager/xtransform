using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Container instruction that executes its children in order. Used as the compiled body
/// of <c>xsl:template</c> / <c>xsl:if</c> / <c>xsl:when</c> / <c>xsl:otherwise</c> / etc.
/// </summary>
internal sealed class SequenceInstruction : XsltInstruction
{
    /// <summary>Initializes a sequence with the given children.</summary>
    public SequenceInstruction(IReadOnlyList<XsltInstruction> children, SourceSpan location)
        : base(location)
    {
        Children = children;
    }

    /// <summary>The child instructions, in document order.</summary>
    public IReadOnlyList<XsltInstruction> Children { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Execute(context, receiver, run);
        }
    }

    /// <summary>Shared empty sequence (used for empty template bodies).</summary>
    public static readonly SequenceInstruction Empty =
        new SequenceInstruction(System.Array.Empty<XsltInstruction>(), SourceSpan.None);
}
