// Top-level transformation driver. XSLT 2.0 §2.3 — the transformation starts by applying
// templates in the default mode to the initial context node. For P3-07 we only support the
// "transform an input document" entry point; "start from an initial named template" lands
// with the public API in P3-15.

using System;
using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;
using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>
/// Runs a <see cref="CompiledStylesheet"/> against an input document, emitting the result to
/// an <see cref="IResultReceiver"/>.
/// </summary>
internal static class XsltRunner
{
    /// <summary>
    /// Transforms <paramref name="input"/> through <paramref name="compiled"/>. Starts by
    /// applying templates in the default mode to the input document node.
    /// </summary>
    public static void Transform(
        CompiledStylesheet compiled,
        XdmNode input,
        IResultReceiver receiver,
        int maxRecursionDepth = XsltRunState.DefaultMaxRecursionDepth)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (compiled is null)
        {
            throw new ArgumentNullException(nameof(compiled));
        }

        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (receiver is null)
        {
            throw new ArgumentNullException(nameof(receiver));
        }
#pragma warning restore CA1510

        RunState xpathRun = new RunState();
        XsltRunState run = new XsltRunState(compiled, xpathRun, maxRecursionDepth);

        receiver.StartDocument();
        try
        {
            DynamicContext rootCtx = DynamicContext.Root(xpathRun).WithContextItem(input, position: 1, size: 1);
            ModeDispatcher.Dispatch(
                rootCtx,
                receiver,
                run,
                DeclarationCollector.DefaultMode,
                System.Array.Empty<WithParamInstruction>(),
                SourceSpan.None);
        }
        finally
        {
            receiver.EndDocument();
        }
    }
}
