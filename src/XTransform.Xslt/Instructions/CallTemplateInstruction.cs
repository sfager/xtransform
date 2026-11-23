using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Compiled <c>xsl:call-template</c>. Looks up the named template, binds any
/// <c>xsl:with-param</c> values, and runs the body in a fresh local frame.
/// </summary>
internal sealed class CallTemplateInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:call-template instruction.</summary>
    public CallTemplateInstruction(
        QName templateName,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan location)
        : base(location)
    {
        TemplateName = templateName;
        WithParams = withParams;
    }

    /// <summary>Expanded name of the named template to invoke.</summary>
    public QName TemplateName { get; }

    /// <summary>The <c>xsl:with-param</c> bindings in source order.</summary>
    public IReadOnlyList<WithParamInstruction> WithParams { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        if (!run.Stylesheet.Declarations.NamedTemplates.TryGetValue(TemplateName, out TemplateDecl? decl))
        {
            // Compile-time validation in P3-04 already ensured the reference resolves; the
            // runtime path should never reach here but we throw defensively.
            throw new XsltDynamicException(
                XsltErrors.XTDE0700,
                $"Named template '{TemplateName.ClarkName}' not found at runtime",
                Location);
        }

        if (!run.Stylesheet.CompiledTemplates.TryGetValue(decl, out CompiledTemplate? compiled))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0700,
                $"Named template '{TemplateName.ClarkName}' has no compiled body",
                Location);
        }

        TemplateInvoker.Invoke(context, receiver, run, compiled, decl, WithParams, Location);
    }
}
