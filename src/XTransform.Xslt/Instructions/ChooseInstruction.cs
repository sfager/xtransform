using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Compiled <c>xsl:choose</c>. Runs the body of the first <c>xsl:when</c> whose test evaluates
/// to true; otherwise runs <see cref="OtherwiseBody"/> when present. Other branches are not
/// evaluated.
/// </summary>
internal sealed class ChooseInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:choose instruction.</summary>
    public ChooseInstruction(
        IReadOnlyList<ChooseBranch> whens,
        XsltInstruction? otherwiseBody,
        SourceSpan location)
        : base(location)
    {
        Whens = whens;
        OtherwiseBody = otherwiseBody;
    }

    /// <summary>The <c>xsl:when</c> branches in source order.</summary>
    public IReadOnlyList<ChooseBranch> Whens { get; }

    /// <summary>The optional <c>xsl:otherwise</c> body.</summary>
    public XsltInstruction? OtherwiseBody { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        for (int i = 0; i < Whens.Count; i++)
        {
            ChooseBranch branch = Whens[i];
            if (branch.Test.EffectiveBooleanValue(context, run))
            {
                branch.Body.Execute(context, receiver, run);
                return;
            }
        }

        OtherwiseBody?.Execute(context, receiver, run);
    }
}

/// <summary>One branch of an <see cref="ChooseInstruction"/>.</summary>
internal sealed class ChooseBranch
{
    /// <summary>Initializes an xsl:when branch.</summary>
    public ChooseBranch(BoundExpression test, XsltInstruction body)
    {
        Test = test;
        Body = body;
    }

    /// <summary>The compiled <c>@test</c> expression.</summary>
    public BoundExpression Test { get; }

    /// <summary>The body to execute when the test passes.</summary>
    public XsltInstruction Body { get; }
}
