// Helper shared by CallTemplateInstruction and ApplyTemplatesInstruction: binds
// xsl:with-param values to the invoked template's xsl:param slots, honours "required",
// and runs the body in a fresh local frame.

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
        run.EnterTemplate(source, callSite);
        try
        {
            DynamicContext frameCtx = context.WithFrame(compiled.SlotCount);
            BindParameters(frameCtx, run, compiled, withParams, callSite);
            compiled.Body.Execute(frameCtx, receiver, run);
        }
        finally
        {
            run.LeaveTemplate();
        }
    }

    private static void BindParameters(
        DynamicContext frameCtx,
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
                frameCtx.LocalFrame[slot.SlotIndex] = supplied.Evaluate(frameCtx);
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
                frameCtx.LocalFrame[slot.SlotIndex] = MaterialiseIterator(slot.DefaultSelect, frameCtx);
            }
            // else: implicit empty sequence — frame is pre-initialised to Empty by WithFrame.
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

    private static XdmSequence MaterialiseIterator(ExpressionNode expression, DynamicContext context)
    {
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = expression.Iterate(context);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return XdmSequence.From(items);
    }
}
