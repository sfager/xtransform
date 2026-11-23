// XSLT 2.0 §3.12 — [xsl:]use-when expression evaluation during stylesheet processing.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Functions;

namespace XTransform.Xslt;

/// <summary>
/// Compiles and evaluates a <c>use-when</c> expression. The static context exposes only the
/// three XSLT functions permitted here (via <see cref="UseWhenFunctionLibrary"/>), no user
/// variables, and no context item — any reference to a user variable raises
/// <see cref="XPathStaticException"/> with <c>XPST0008</c>.
/// </summary>
internal static class UseWhenEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> and returns its effective boolean value.
    /// </summary>
    /// <exception cref="XTransformException">
    /// Static errors (<c>XPST0003</c>, <c>XPST0008</c>, <c>XPST0017</c>) and dynamic errors
    /// surface unchanged for the caller to wrap or collect.
    /// </exception>
    public static bool Evaluate(
        string expression,
        NamespaceBindings namespaces,
        SourceSpan location)
    {
        IStaticNamespaceResolver resolver = new NamespaceBindingsResolver(
            namespaces,
            defaultElementNamespace: string.Empty,
            defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

        CompiledExpression compiled = XPathCompiler.ParseAndCompile(
            expression,
            location,
            resolver,
            UseWhenFunctionLibrary.Instance,
            externalVariables: null);

        RunState run = new RunState();
        DynamicContext context = DynamicContext.Root(run).WithFrame(compiled.SlotCount);

        // Materialise the sequence and compute EBV using the shared F&O §15.1.1 helper.
        IXdmIterator iter = compiled.Root.Iterate(context);
        List<XdmItem> items = new List<XdmItem>();
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return BooleanFunctions.EffectiveBooleanValue(XdmSequence.From(items));
    }
}
