using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Base class for compiled XSLT instructions. One instance per <c>xsl:*</c> element (or
/// literal result element) in the source. The <c>Execute</c> method runs the instruction
/// against the supplied dynamic context, routing any produced content to the receiver.
/// </summary>
internal abstract class XsltInstruction
{
    /// <summary>Initializes an instruction with its source location.</summary>
    protected XsltInstruction(SourceSpan location)
    {
        Location = location;
    }

    /// <summary>Source location this instruction was compiled from.</summary>
    public SourceSpan Location { get; }

    /// <summary>Runs the instruction.</summary>
    public abstract void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run);
}
