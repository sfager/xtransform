using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:if</c>. Executes <see cref="Body"/> when <see cref="Test"/> is EBV-true.</summary>
internal sealed class IfInstruction : XsltInstruction
{
    /// <summary>Initializes an <c>xsl:if</c> instruction.</summary>
    public IfInstruction(BoundExpression test, XsltInstruction body, SourceSpan location)
        : base(location)
    {
        Test = test;
        Body = body;
    }

    /// <summary>The compiled <c>@test</c> expression.</summary>
    public BoundExpression Test { get; }

    /// <summary>Body to run when <see cref="Test"/> evaluates to true.</summary>
    public XsltInstruction Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        if (Test.EffectiveBooleanValue(context, run))
        {
            Body.Execute(context, receiver, run);
        }
    }
}
