// Top-level transformation driver. XSLT 2.0 §2.3 — the transformation starts by applying
// templates in the default mode to the initial context node. For P3-07 we only support the
// "transform an input document" entry point; "start from an initial named template" lands
// with the public API in P3-15.

using System;
using System.Collections.Generic;
using XTransform.Serialization;
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
        NamePool? namePool = null,
        IReadOnlyDictionary<QName, XdmSequence>? parameters = null,
        IResourceResolver? resolver = null,
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
        NamePool effectivePool = namePool ?? new NamePool();
        XsltRunState run = new XsltRunState(compiled, xpathRun, effectivePool, maxRecursionDepth)
        {
            Resolver = resolver,
        };
        // Expose XsltRunState to XPath built-ins (fn:document, fn:key, user functions).
        xpathRun.Tag = run;

        if (parameters is not null)
        {
            foreach (KeyValuePair<QName, XdmSequence> kv in parameters)
            {
                if (!compiled.Declarations.GlobalVariables.TryGetValue(kv.Key, out GlobalVariableDecl? decl)
                    || !decl.IsParam)
                {
                    throw new System.ArgumentException(
                        $"No global xsl:param named '{kv.Key.ClarkName}' is declared in this stylesheet",
                        nameof(parameters));
                }

                run.ExternalParameters[kv.Key] = kv.Value;
            }
        }

        DynamicContext rootCtx = DynamicContext.Root(xpathRun).WithContextItem(input, position: 1, size: 1);
        GlobalVariableEvaluator.EvaluateAll(compiled, rootCtx, run);

        SequenceNormalizer normalizer = new SequenceNormalizer(receiver);
        normalizer.StartDocument();
        try
        {
            ModeDispatcher.Dispatch(
                rootCtx,
                normalizer,
                run,
                DeclarationCollector.DefaultMode,
                System.Array.Empty<WithParamInstruction>(),
                SourceSpan.None);
        }
        finally
        {
            normalizer.EndDocument();
        }

        // Promote any XTDE0410 warnings collected during the run.
        LastRunDiagnostics = normalizer.Diagnostics;
    }

    /// <summary>Diagnostics from the most recent <see cref="Transform"/> run (used by tests).</summary>
    public static IReadOnlyList<Diagnostic> LastRunDiagnostics { get; private set; } = System.Array.Empty<Diagnostic>();
}
