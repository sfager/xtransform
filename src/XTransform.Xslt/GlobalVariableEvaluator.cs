// XSLT 2.0 §9.3 — global variables / params. Evaluated lazily on first reference, cached
// in XsltRunState, with XTDE0640 raised on direct or indirect self-reference. Required
// stylesheet parameters without an external override raise XTDE0050.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

internal static class GlobalVariableEvaluator
{
    /// <summary>
    /// Evaluates every global variable / parameter declared in <paramref name="compiled"/> and
    /// seeds the cache on <paramref name="run"/>. Must be called once before template dispatch.
    /// </summary>
    public static void EvaluateAll(CompiledStylesheet compiled, DynamicContext initialContext, XsltRunState run)
    {
        DeclarationTable decls = compiled.Declarations;

        foreach (KeyValuePair<QName, GlobalVariableDecl> kv in decls.GlobalVariables)
        {
            Evaluate(kv.Key, decls, initialContext, run);
        }
    }

    private static XdmSequence Evaluate(
        QName name,
        DeclarationTable decls,
        DynamicContext initialContext,
        XsltRunState run)
    {
        if (run.Globals.TryGetValue(name, out XdmSequence? cached))
        {
            return cached;
        }

        if (!run.GlobalsInProgress.Add(name))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0640,
                $"Circular reference in global variable '{name.ClarkName}'",
                SourceSpan.None);
        }

        try
        {
            GlobalVariableDecl decl = decls.GlobalVariables[name];

            // External parameter overrides win when present.
            if (decl.IsParam && run.ExternalParameters.TryGetValue(name, out XdmSequence? external))
            {
                // A supplied parameter value is converted to the as type (XTTE0590).
                SequenceType? suppliedType = SequenceTypes.ParseOrNull(decl.AsType, decl.Element);
                if (suppliedType is not null)
                {
                    external = SequenceTypes.Convert(external, suppliedType, XsltErrors.XTTE0590, decl.Location, $"The value supplied for parameter ${name.LocalName}");
                }

                run.Globals[name] = external;
                return external;
            }

            if (decl.IsParam && decl.IsRequired)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0050,
                    $"No value supplied for required stylesheet parameter '{name.ClarkName}'",
                    decl.Location);
            }

            XdmSequence value;
            if (string.IsNullOrEmpty(decl.SelectExpression))
            {
                // XSLT 2.0 §9.3: the content (a document node when there is no as attribute), else "" or ().
                if (run.Stylesheet.GlobalBodies.TryGetValue(decl, out VariableValueBody? body))
                {
                    ForceReferencedGlobals(decl, CollectText(decl.Element), decls, initialContext, run);
                    value = body.Evaluate(initialContext, run);
                }
                else
                {
                    value = decl.AsType is null ? XdmSequence.Of(XdmAtomicValue.String(string.Empty)) : XdmSequence.Empty;
                }
            }
            else
            {
                NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                    decl.Element.InScopeNamespaces,
                    defaultElementNamespace: string.Empty,
                    defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

                Dictionary<QName, SequenceType> externals = new Dictionary<QName, SequenceType>();
                foreach (QName globalName in decls.GlobalVariables.Keys)
                {
                    externals[globalName] = SequenceType.AnySequence;
                }

                CompiledExpression ce = XPathCompiler.ParseAndCompile(
                    decl.SelectExpression!,
                    decl.Location,
                    resolver,
                    run.Stylesheet.Functions,
                    externals);

                // To evaluate, we need other globals it references to be cached. If an inner
                // reference points at an un-evaluated global, LookupVariable returns Empty —
                // which would mask cycles. So recursively force-evaluate the dependencies
                // referenced by this expression before we evaluate it.
                ForceReferencedGlobals(decl, decl.SelectExpression ?? string.Empty, decls, initialContext, run);

                DynamicContext ctx = initialContext.WithFrame(ce.SlotCount);
                int i = 0;
                foreach (QName globalName in decls.GlobalVariables.Keys)
                {
                    ctx.LocalFrame[i++] = run.LookupVariable(globalName);
                }

                List<XdmItem> items = new List<XdmItem>();
                IXdmIterator iter = ce.Root.Iterate(ctx);
                XdmItem? item;
                while ((item = iter.Next()) is not null)
                {
                    items.Add(item);
                }

                value = XdmSequence.From(items);
            }

            // The value of a variable (XTTE0570) or the default of a parameter (XTTE0600) is converted to the as type.
            SequenceType? valueType = SequenceTypes.ParseOrNull(decl.AsType, decl.Element);
            if (valueType is not null)
            {
                value = SequenceTypes.Convert(
                    value,
                    valueType,
                    decl.IsParam ? XsltErrors.XTTE0600 : XsltErrors.XTTE0570,
                    decl.Location,
                    $"The value of ${name.LocalName}");
            }

            run.Globals[name] = value;
            return value;
        }
        finally
        {
            run.GlobalsInProgress.Remove(name);
        }
    }

    /// <summary>
    /// Scan the select expression's text for <c>$prefix:local</c> / <c>$local</c> references
    /// to other globals and force-evaluate them. This is a conservative overshoot — it also
    /// flags local-variable references, but those won't appear in a top-level select — but it
    /// guarantees the "cycle detected via indirect reference" case still reaches
    /// <see cref="XsltRunState.GlobalsInProgress"/>.
    /// </summary>
    private static void ForceReferencedGlobals(
        GlobalVariableDecl decl,
        string text,
        DeclarationTable decls,
        DynamicContext initialContext,
        XsltRunState run)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '$')
            {
                continue;
            }

            int start = i + 1;
            int end = start;
            while (end < text.Length && IsNameChar(text[end]))
            {
                end++;
            }

            if (end == start)
            {
                continue;
            }

            string lexical = text.Substring(start, end - start);
            if (!LexicalQNameParser.TryResolve(lexical, decl.Element.InScopeNamespaces, out QName referenced, out _))
            {
                continue;
            }

            if (decls.GlobalVariables.ContainsKey(referenced))
            {
                Evaluate(referenced, decls, initialContext, run);
            }
        }
    }

    // Every attribute value under an element: the variable references a body may contain (select, test, ...).
    private static string CollectText(StylesheetElement element)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        AppendAttributes(element, sb);
        return sb.ToString();
    }

    private static void AppendAttributes(StylesheetElement element, System.Text.StringBuilder sb)
    {
        foreach (StylesheetAttribute attribute in element.Attributes)
        {
            sb.Append(attribute.Value).Append(' ');
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                AppendAttributes(childElement, sb);
            }
        }
    }

    private static bool IsNameChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ':';
}
