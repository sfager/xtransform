// XSLT 2.0 §3.12 (use-when), §4.4 (whitespace in the stylesheet), §5.6.1 (AVTs).
// A post-reader pass over the StylesheetModule AST.

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Options controlling <see cref="StylesheetProcessor"/>. Defaults mirror the XSLT 2.0 spec.
/// </summary>
internal sealed class StylesheetProcessorOptions
{
    /// <summary>Whether to evaluate <c>[xsl:]use-when</c> and prune elements that evaluate false.</summary>
    public bool EvaluateUseWhen { get; init; } = true;

    /// <summary>Whether to parse AVTs on applicable attributes.</summary>
    public bool ParseAvts { get; init; } = true;

    /// <summary>Whether to strip whitespace-only text nodes per XSLT 2.0 §4.4.</summary>
    public bool StripWhitespace { get; init; } = true;
}

/// <summary>
/// Post-reader pass that evaluates <c>use-when</c>, parses attribute value templates,
/// and strips whitespace-only text nodes according to XSLT 2.0 §4.4. Mutates the
/// <see cref="StylesheetModule"/> in place.
/// </summary>
internal static class StylesheetProcessor
{
    /// <summary>
    /// Runs the processor on <paramref name="module"/>. Any diagnostics produced are appended
    /// to <see cref="StylesheetModule.Diagnostics"/>. Fatal errors are collected and thrown as
    /// a single <see cref="XsltStaticException"/>.
    /// </summary>
    public static StylesheetModule Process(StylesheetModule module, StylesheetProcessorOptions? options = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }
#pragma warning restore CA1510

        options ??= new StylesheetProcessorOptions();

        DiagnosticBag bag = new DiagnosticBag();
        foreach (Diagnostic d in module.Diagnostics)
        {
            bag.Add(d);
        }

        // Use-when prunes the tree first; AVT parsing and whitespace stripping then run on
        // what remains. xml:space="preserve" on the root element propagates to descendants.
        if (options.EvaluateUseWhen)
        {
            if (!EvaluateUseWhen(module.Root, bag))
            {
                // Root itself evaluated to false — exceedingly rare but legal; replace with an
                // empty synthetic root. Simpler: treat as a hard error so downstream passes
                // always see a root element.
                bag.Error(
                    XsltErrors.XTSE0010,
                    "Root stylesheet element was removed by use-when",
                    module.Root.Location);
            }
        }

        if (options.ParseAvts)
        {
            ParseAvts(module.Root, bag);
        }

        if (options.StripWhitespace)
        {
            StripWhitespace(module.Root, preserve: false);
        }

        if (bag.HasErrors)
        {
            throw new XsltStaticException(bag.ToList());
        }

        return new StylesheetModule(module.ModuleUri, module.Root, bag.ToList());
    }

    // ── use-when ─────────────────────────────────────────────────────────────

    // Returns true when the element survives (either no use-when, or evaluated true).
    // Recursively processes children regardless of the current element's outcome, so nested
    // use-when attributes are always evaluated (and collected diagnostics stay visible).
    private static bool EvaluateUseWhen(StylesheetElement element, DiagnosticBag bag)
    {
        string? useWhen = FindUseWhen(element);
        bool keep = true;
        if (useWhen is not null)
        {
            try
            {
                keep = UseWhenEvaluator.Evaluate(useWhen, element.InScopeNamespaces, element.Location);
            }
            catch (XTransformException ex)
            {
                bag.Error(ex.ErrorCode, ex.Message, element.Location);
                keep = true; // keep the element so later passes report their own diagnostics
            }
        }

        if (!keep)
        {
            return false;
        }

        List<StylesheetNode> retained = new List<StylesheetNode>(element.Children.Count);
        for (int i = 0; i < element.Children.Count; i++)
        {
            StylesheetNode child = element.Children[i];
            if (child is StylesheetElement childElement)
            {
                if (EvaluateUseWhen(childElement, bag))
                {
                    retained.Add(childElement);
                }
            }
            else
            {
                retained.Add(child);
            }
        }

        if (retained.Count != element.Children.Count)
        {
            element.ReplaceChildren(retained);
        }

        return true;
    }

    private static string? FindUseWhen(StylesheetElement element)
    {
        // On xsl:* elements, use-when is in the null namespace; on non-xsl elements, it must
        // be xsl:use-when (XSLT 2.0 §3.12).
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            bool match = string.Equals(attr.Name.LocalName, "use-when", StringComparison.Ordinal) &&
                         (element.IsXsltElement
                             ? attr.Name.NamespaceUri.Length == 0
                             : string.Equals(attr.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal));
            if (match)
            {
                return attr.Value;
            }
        }

        return null;
    }

    // ── AVT parsing ──────────────────────────────────────────────────────────

    private static void ParseAvts(StylesheetElement element, DiagnosticBag bag)
    {
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (!IsAvtEligible(element, attr))
            {
                continue;
            }

            try
            {
                attr.ParsedTemplate = AttributeValueTemplate.Parse(attr.Value, attr.Location);
            }
            catch (XsltStaticException ex)
            {
                foreach (Diagnostic d in ex.Errors)
                {
                    bag.Add(d);
                }
            }
        }

        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is StylesheetElement child)
            {
                ParseAvts(child, bag);
            }
        }
    }

    private static bool IsAvtEligible(StylesheetElement element, StylesheetAttribute attr)
    {
        // Namespace declarations are already filtered out by the reader. XML-namespaced
        // attributes (e.g. xml:space) are not AVTs.
        if (string.Equals(attr.Name.NamespaceUri, XsltNamespace.XmlUri, StringComparison.Ordinal))
        {
            return false;
        }

        if (element.IsXsltElement)
        {
            // Only attributes on well-known AVT-bearing xsl:* instructions. Everything else
            // (match, select, test, use, use-when …) is XPath or a plain string.
            return attr.Name.NamespaceUri.Length == 0 &&
                   XsltAvtAttributes.IsAvt(element.Name.LocalName, attr.Name.LocalName);
        }

        // Literal result element: every attribute not in the XSLT namespace is an AVT (xsl:*
        // attributes on an LRE — e.g. xsl:version, xsl:use-attribute-sets — are control
        // attributes, not AVTs).
        return !string.Equals(attr.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal);
    }

    // ── Whitespace stripping (XSLT 2.0 §4.4) ─────────────────────────────────

    private static void StripWhitespace(StylesheetElement element, bool preserve)
    {
        bool effectivePreserve = preserve;

        // xml:space on this element overrides the inherited policy.
        string? xmlSpace = FindAttributeValue(element, XsltNamespace.XmlUri, "space");
        if (xmlSpace is not null)
        {
            effectivePreserve = string.Equals(xmlSpace, "preserve", StringComparison.Ordinal);
        }

        bool isTextElement = element.IsXsltElement &&
                             string.Equals(element.Name.LocalName, "text", StringComparison.Ordinal);

        // xsl:text preserves all whitespace in its content regardless of ancestor policy.
        bool preserveHere = effectivePreserve || isTextElement;

        List<StylesheetNode> retained = new List<StylesheetNode>(element.Children.Count);
        for (int i = 0; i < element.Children.Count; i++)
        {
            StylesheetNode child = element.Children[i];
            if (child is StylesheetText text && text.IsWhitespaceOnly && !preserveHere)
            {
                continue;
            }

            retained.Add(child);

            if (child is StylesheetElement childElement)
            {
                StripWhitespace(childElement, preserveHere);
            }
        }

        if (retained.Count != element.Children.Count)
        {
            element.ReplaceChildren(retained);
        }
    }

    private static string? FindAttributeValue(StylesheetElement element, string namespaceUri, string localName)
    {
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (string.Equals(attr.Name.LocalName, localName, StringComparison.Ordinal) &&
                string.Equals(attr.Name.NamespaceUri, namespaceUri, StringComparison.Ordinal))
            {
                return attr.Value;
            }
        }

        return null;
    }
}
