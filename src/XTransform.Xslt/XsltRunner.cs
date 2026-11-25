// Top-level transformation driver. XSLT 2.0 §2.3 — the transformation starts by applying
// templates in the default mode to the initial context node. For P3-07 we only support the
// "transform an input document" entry point; "start from an initial named template" lands
// with the public API in P3-15.

using System;
using System.Collections.Generic;
using System.Threading;
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
    /// Transforms <paramref name="input"/> through <paramref name="compiled"/>. Without an
    /// <paramref name="initialTemplate"/> it starts by applying templates (in
    /// <paramref name="initialMode"/>, default mode when null) to the input document node;
    /// with one it invokes that named template, with <paramref name="input"/> as the context
    /// item when supplied. Returns the non-fatal diagnostics collected during the run.
    /// </summary>
#pragma warning disable CA1068 // Optional parameters after the token keep call sites tidy
    public static IReadOnlyList<Diagnostic> Transform(
        CompiledStylesheet compiled,
        XdmNode? input,
        IResultReceiver receiver,
        NamePool? namePool = null,
        IReadOnlyDictionary<QName, XdmSequence>? parameters = null,
        IResourceResolver? resolver = null,
        CancellationToken cancellationToken = default,
        int maxRecursionDepth = XsltRunState.DefaultMaxRecursionDepth,
        QName? initialMode = null,
        QName? initialTemplate = null)
#pragma warning restore CA1068
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (compiled is null)
        {
            throw new ArgumentNullException(nameof(compiled));
        }

        if (input is null && initialTemplate is null)
        {
            throw new ArgumentException(
                "An input document or an initial named template is required",
                nameof(input));
        }

        if (receiver is null)
        {
            throw new ArgumentNullException(nameof(receiver));
        }
#pragma warning restore CA1510

        RunState xpathRun = new RunState(cancellation: cancellationToken);
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

        DynamicContext rootCtx = input is null
            ? DynamicContext.Root(xpathRun)
            : DynamicContext.Root(xpathRun).WithContextItem(input, position: 1, size: 1);

        // Validate the entry point before running anything (XSLT 2.0 §2.3).
        TemplateDecl? entryTemplate = null;
        QName mode = initialMode ?? DeclarationCollector.DefaultMode;
        if (initialTemplate is QName templateName)
        {
            if (!compiled.Declarations.NamedTemplates.TryGetValue(templateName, out entryTemplate))
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0040,
                    $"The initial template '{templateName.ClarkName}' is not declared in the stylesheet",
                    SourceSpan.None);
            }
        }
        else if (!mode.Equals(DeclarationCollector.DefaultMode) &&
                 !System.Linq.Enumerable.Contains(compiled.Declarations.DeclaredModes, mode))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0045,
                $"The initial mode '{mode.ClarkName}' is not used by any template rule",
                SourceSpan.None);
        }

        GlobalVariableEvaluator.EvaluateAll(compiled, rootCtx, run);

        SequenceNormalizer normalizer = new SequenceNormalizer(receiver, effectivePool);
        normalizer.StartDocument();
        try
        {
            if (entryTemplate is not null)
            {
                TemplateInvoker.Invoke(
                    rootCtx,
                    normalizer,
                    run,
                    compiled.CompiledTemplates[entryTemplate],
                    entryTemplate,
                    System.Array.Empty<WithParamInstruction>(),
                    SourceSpan.None);
            }
            else
            {
                run.CurrentMode = mode;
                ModeDispatcher.Dispatch(
                    rootCtx,
                    normalizer,
                    run,
                    mode,
                    System.Array.Empty<WithParamInstruction>(),
                    SourceSpan.None);
            }
        }
        finally
        {
            normalizer.EndDocument();
        }

        return normalizer.Diagnostics;
    }
}
