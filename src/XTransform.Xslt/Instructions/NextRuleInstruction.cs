// XSLT 2.0 §6.7 — xsl:next-match and xsl:apply-imports: both re-process the context item with the best template
// rule that the current template rule overrides, passing the xsl:with-param values on.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:next-match</c> or <c>xsl:apply-imports</c>.</summary>
internal sealed class NextRuleInstruction : XsltInstruction
{
    /// <summary>Initializes the instruction; <paramref name="importsOnly"/> is true for <c>xsl:apply-imports</c>.</summary>
    public NextRuleInstruction(bool importsOnly, IReadOnlyList<WithParamInstruction> withParams, SourceSpan location)
        : base(location)
    {
        ImportsOnly = importsOnly;
        WithParams = withParams;
    }

    /// <summary>True for <c>xsl:apply-imports</c> (only rules of imported modules), false for <c>xsl:next-match</c>.</summary>
    public bool ImportsOnly { get; }

    /// <summary>The <c>xsl:with-param</c> bindings.</summary>
    public IReadOnlyList<WithParamInstruction> WithParams { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // XTDE0560: there is no current template rule (outside a rule, or inside a stylesheet function).
        TemplateRule? current = run.CurrentRule;
        if (current is null)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0560,
                ImportsOnly
                    ? "xsl:apply-imports was evaluated when there is no current template rule"
                    : "xsl:next-match was evaluated when there is no current template rule",
                Location);
        }

        if (context.ContextItem is null)
        {
            throw new XsltDynamicException(
                XPathErrors.XPDY0002,
                "The context item is absent for xsl:next-match / xsl:apply-imports",
                Location);
        }

        context.CheckCancellation();
        ModeDispatcher.DispatchNext(context, receiver, run, current, ImportsOnly, WithParams, Location);
    }
}
