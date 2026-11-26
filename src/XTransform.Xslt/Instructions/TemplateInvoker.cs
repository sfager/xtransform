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
            if (compiled.ResultType is null)
            {
                compiled.Body.Execute(context, receiver, run);
            }
            else
            {
                // The result of the body is converted to the as type of the template (XTTE0505).
                SequenceResultReceiver capture = new SequenceResultReceiver(run.NamePool);
                compiled.Body.Execute(context, capture, run);
                XdmSequence result = SequenceTypes.Convert(capture.ToSequence(), compiled.ResultType, XsltErrors.XTTE0505, source.Location, "The result of the template");
                foreach (XdmItem item in result)
                {
                    receiver.Append(item);
                }
            }
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
                // A supplied value is converted to the as type of the parameter (XTTE0590).
                XdmSequence given = supplied.Evaluate(context, run);
                if (slot.AsType is not null)
                {
                    given = SequenceTypes.Convert(given, slot.AsType, XsltErrors.XTTE0590, supplied.Location, $"The value supplied for parameter ${slot.Name.LocalName}");
                }

                run.PushVariable(slot.Name, given);
                continue;
            }

            if (slot.IsRequired)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0700,
                    $"Required parameter '{slot.Name.ClarkName}' not supplied",
                    callSite);
            }

            XdmSequence value;
            if (slot.DefaultSelect is not null)
            {
                value = slot.DefaultSelect.Evaluate(context, run);
            }
            else if (slot.DefaultBody is not null)
            {
                value = slot.DefaultBody.Evaluate(context, run);
            }
            else if (slot.AsType is null)
            {
                // No default and no as: the zero-length string (XSLT 2.0 §9.3).
                run.PushVariable(slot.Name, XdmSequence.Of(XdmAtomicValue.String(string.Empty)));
                continue;
            }
            else
            {
                // No default with an as type: the empty sequence, which that type must allow; otherwise the parameter is
                // effectively required (XTDE0610).
                value = XdmSequence.Empty;
                if (!slot.AsType.Matches(value))
                {
                    throw new XsltDynamicException(
                        XsltErrors.XTDE0610,
                        $"Parameter '{slot.Name.ClarkName}' has no default and its type does not allow the empty sequence, so a value must be supplied",
                        callSite);
                }

                run.PushVariable(slot.Name, value);
                continue;
            }

            if (slot.AsType is not null)
            {
                value = SequenceTypes.Convert(value, slot.AsType, XsltErrors.XTTE0600, slot.Location, $"The default value of parameter ${slot.Name.LocalName}");
            }

            run.PushVariable(slot.Name, value);
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
