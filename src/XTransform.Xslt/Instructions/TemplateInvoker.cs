// Helper shared by CallTemplateInstruction and ApplyTemplatesInstruction: binds
// xsl:with-param values to the invoked template's xsl:param slots, honours "required",
// pushes them onto the variable scope stack, and runs the body.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

internal static class TemplateInvoker
{
    /// <summary>Invokes <paramref name="compiled"/> with the given with-param bindings.</summary>
    public static void Invoke(
        DynamicContext context,
        IResultReceiver receiver,
        XsltRunState run,
        CompiledTemplate compiled,
        TemplateDecl source,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan callSite)
    {
        // Template entry — cheap cancellation check (P3-12).
        context.CheckCancellation();

        run.EnterTemplate(source, callSite);
        int varMarker = run.VariableScopeMarker;
        try
        {
            BindParameters(context, run, compiled, withParams, callSite);
            compiled.Body.Execute(context, receiver, run);
        }
        finally
        {
            run.RestoreVariablesTo(varMarker);
            run.LeaveTemplate();
        }
    }

    private static void BindParameters(
        DynamicContext context,
        XsltRunState run,
        CompiledTemplate compiled,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan callSite)
    {
        foreach (ParamSlot slot in compiled.Parameters)
        {
            WithParamInstruction? supplied = FindWithParam(withParams, slot.Name);
            if (supplied is not null)
            {
                run.PushVariable(slot.Name, supplied.Evaluate(context, run));
                continue;
            }

            if (slot.IsRequired)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0700,
                    $"Required parameter '{slot.Name.ClarkName}' not supplied",
                    callSite);
            }

            if (slot.DefaultSelect is not null)
            {
                run.PushVariable(slot.Name, slot.DefaultSelect.Evaluate(context, run));
            }
            else
            {
                run.PushVariable(slot.Name, XdmSequence.Empty);
            }
        }
    }

    private static WithParamInstruction? FindWithParam(
        IReadOnlyList<WithParamInstruction> withParams,
        QName name)
    {
        for (int i = 0; i < withParams.Count; i++)
        {
            if (withParams[i].Name.Equals(name))
            {
                return withParams[i];
            }
        }

        return null;
    }
}
