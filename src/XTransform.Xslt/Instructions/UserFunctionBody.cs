// A compiled xsl:function body plus its parameter names. Invoked by XsltFunctionLibrary's
// late-bound invoker. Values are captured via an ItemCollectorReceiver so the function can
// return a sequence of items; the common case is a body consisting of xsl:sequence or an
// XPath-valued xsl:value-of.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

internal sealed class UserFunctionBody
{
    public UserFunctionBody(
        IReadOnlyList<QName> paramNames,
        XsltInstruction body,
        IReadOnlyList<SequenceType?>? paramTypes = null,
        SequenceType? resultType = null,
        SourceSpan location = default)
    {
        ParamNames = paramNames;
        Body = body;
        ParamTypes = paramTypes;
        ResultType = resultType;
        Location = location;
    }

    /// <summary>Parameter names in declaration order.</summary>
    public IReadOnlyList<QName> ParamNames { get; }

    /// <summary>The as type of each parameter (null entries have none), or null when no parameter has one.</summary>
    public IReadOnlyList<SequenceType?>? ParamTypes { get; }

    /// <summary>The as type of the function result, or null.</summary>
    public SequenceType? ResultType { get; }

    /// <summary>Where the function is declared.</summary>
    public SourceSpan Location { get; }

    /// <summary>The compiled body instructions.</summary>
    public XsltInstruction Body { get; }

    /// <summary>
    /// Invokes the function with <paramref name="args"/> in slot order. Pushes each argument
    /// as a local variable binding, runs the body against an <see cref="SequenceResultReceiver"/>,
    /// and returns the collected sequence.
    /// </summary>
    public IXdmIterator Invoke(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState? run = context.Run.Tag as XsltRunState;
        if (run is null)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0640,
                "xsl:function invoked outside an XSLT run — missing XsltRunState on RunState.Tag",
                SourceSpan.None);
        }

        int marker = run.VariableScopeMarker;
        try
        {
            for (int i = 0; i < ParamNames.Count; i++)
            {
                XdmSequence argument = i < args.Length ? args[i] : XdmSequence.Empty;

                // Each argument is converted to the as type of its parameter (XTTE0790).
                SequenceType? paramType = ParamTypes is not null && i < ParamTypes.Count ? ParamTypes[i] : null;
                if (paramType is not null)
                {
                    argument = SequenceTypes.Convert(argument, paramType, XsltErrors.XTTE0790, Location, $"Argument {i + 1} of the function");
                }

                run.PushVariable(ParamNames[i], argument);
            }

            // XSLT 2.0 §10.3: the focus is undefined within a function body.
            SequenceResultReceiver collector = new SequenceResultReceiver(run.NamePool);
            Body.Execute(context.WithoutFocus(), collector, run);
            XdmSequence result = collector.ToSequence();

            // The result is converted to the as type of the function (XTTE0780).
            if (ResultType is not null)
            {
                result = SequenceTypes.Convert(result, ResultType, XsltErrors.XTTE0780, Location, "The result of the function");
            }

            return result.GetIterator();
        }
        finally
        {
            run.RestoreVariablesTo(marker);
        }
    }
}
