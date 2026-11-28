// XSLT 2.0 §10.2 — use-attribute-sets: the attributes of the named sets are added to the element being constructed.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Adds the attributes of the named attribute sets, in the order named, to the element that has just started.</summary>
internal sealed class UseAttributeSetsInstruction : XsltInstruction
{
    /// <summary>Initializes the instruction.</summary>
    public UseAttributeSetsInstruction(AttributeSetSlot[] sets, SourceSpan location)
        : base(location)
    {
        Sets = sets;
    }

    /// <summary>The sets to use.</summary>
    public AttributeSetSlot[] Sets { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // The attributes see the global variables only, not the local ones of the instruction that uses the set (XSLT 2.0 §10.2).
        int previous = run.HideLocalVariables();
        try
        {
            for (int i = 0; i < Sets.Length; i++)
            {
                Sets[i].Body?.Execute(context, receiver, run);
            }
        }
        finally
        {
            run.RestoreLocalVariables(previous);
        }
    }
}
