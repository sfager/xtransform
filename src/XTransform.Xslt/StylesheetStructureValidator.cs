using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Checks the content model of every XSLT element (XSLT 2.0 §3.6, §3.7, §6, §11, §18): which children and which
/// text an element may contain, where <c>xsl:param</c> and <c>xsl:sort</c> may appear, and the attributes an
/// element requires. A violation is XTSE0010, and XTSE0120 for text directly inside <c>xsl:stylesheet</c>.
/// The check is off inside a forwards-compatible region (effective version above 2.0, §3.9). Elements the reader
/// already rejected as unknown are not checked again.
/// </summary>
internal static class StylesheetStructureValidator
{
    private static readonly HashSet<string> s_declarations = new HashSet<string>(StringComparer.Ordinal)
    {
        "import", "include", "strip-space", "preserve-space", "output", "key", "decimal-format", "namespace-alias",
        "attribute-set", "character-map", "import-schema", "param", "variable", "function", "template", "mode",
    };

    // Elements allowed in a sequence constructor (XSLT 2.0 §5.7, the "instruction" category), plus xsl:variable and xsl:fallback.
    private static readonly HashSet<string> s_instructions = new HashSet<string>(StringComparer.Ordinal)
    {
        "apply-templates", "apply-imports", "call-template", "next-match", "for-each", "for-each-group", "if", "choose",
        "value-of", "text", "sequence", "copy", "copy-of", "element", "attribute", "comment", "processing-instruction",
        "document", "namespace", "result-document", "message", "analyze-string", "number", "perform-sort", "variable",
        "fallback",
    };

    // Elements whose content is a sequence constructor, optionally preceded by xsl:param or xsl:sort elements.
    private static readonly Dictionary<string, string?> s_sequenceConstructorWithLeading = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["template"] = "param",
        ["function"] = "param",
        ["for-each"] = "sort",
        ["for-each-group"] = "sort",
        ["perform-sort"] = "sort",
        ["variable"] = null,
        ["param"] = null,
        ["with-param"] = null,
        ["if"] = null,
        ["when"] = null,
        ["otherwise"] = null,
        ["copy"] = null,
        ["element"] = null,
        ["attribute"] = null,
        ["comment"] = null,
        ["processing-instruction"] = null,
        ["document"] = null,
        ["message"] = null,
        ["namespace"] = null,
        ["result-document"] = null,
        ["fallback"] = null,
        ["matching-substring"] = null,
        ["non-matching-substring"] = null,
        ["sort"] = null,
        ["value-of"] = null,
        ["key"] = null,
    };

    // Elements whose children are restricted to the listed element names and that take no text.
    private static readonly Dictionary<string, string[]> s_elementOnly = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["apply-templates"] = new[] { "sort", "with-param" },
        ["call-template"] = new[] { "with-param" },
        ["apply-imports"] = new[] { "with-param" },
        ["next-match"] = new[] { "with-param", "fallback" },
        ["choose"] = new[] { "when", "otherwise" },
        ["analyze-string"] = new[] { "matching-substring", "non-matching-substring", "fallback" },
        ["attribute-set"] = new[] { "attribute" },
        ["character-map"] = new[] { "output-character" },
        ["sequence"] = new[] { "fallback" },
        ["output"] = Array.Empty<string>(),
        ["include"] = Array.Empty<string>(),
        ["import"] = Array.Empty<string>(),
        ["strip-space"] = Array.Empty<string>(),
        ["preserve-space"] = Array.Empty<string>(),
        ["decimal-format"] = Array.Empty<string>(),
        ["namespace-alias"] = Array.Empty<string>(),
        ["number"] = Array.Empty<string>(),
        ["copy-of"] = Array.Empty<string>(),
        ["output-character"] = Array.Empty<string>(),
    };

    // Attributes an element must have (XSLT 2.0 element syntax summaries).
    private static readonly Dictionary<string, string[]> s_required = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["stylesheet"] = new[] { "version" },
        ["transform"] = new[] { "version" },
        ["include"] = new[] { "href" },
        ["import"] = new[] { "href" },
        ["strip-space"] = new[] { "elements" },
        ["preserve-space"] = new[] { "elements" },
        ["key"] = new[] { "name", "match" },
        ["function"] = new[] { "name" },
        ["variable"] = new[] { "name" },
        ["param"] = new[] { "name" },
        ["with-param"] = new[] { "name" },
        ["call-template"] = new[] { "name" },
        ["attribute-set"] = new[] { "name" },
        ["character-map"] = new[] { "name" },
        ["output-character"] = new[] { "character", "string" },
        ["namespace-alias"] = new[] { "stylesheet-prefix", "result-prefix" },
        ["if"] = new[] { "test" },
        ["when"] = new[] { "test" },
        ["for-each"] = new[] { "select" },
        ["for-each-group"] = new[] { "select" },
        ["copy-of"] = new[] { "select" },
        ["element"] = new[] { "name" },
        ["attribute"] = new[] { "name" },
        ["processing-instruction"] = new[] { "name" },
        ["analyze-string"] = new[] { "select", "regex" },
    };

    /// <summary>Reports XTSE0010 / XTSE0120 for content-model violations in every module.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            Visit(assembly.Modules[i].Module.Root, null, bag);
        }
    }

    private static void Visit(StylesheetElement element, string? inheritedVersion, DiagnosticBag bag)
    {
        string? version = element.DeclaredVersion.Length != 0 ? element.DeclaredVersion : inheritedVersion;
        bool check = !IsForwardsCompatible(version);

        if (check)
        {
            if (element.IsXsltElement && XsltElementNames.IsKnown(element.Name.LocalName))
            {
                CheckRequiredAttributes(element, bag);
                CheckXsltContent(element, bag);
            }
            else if (!element.IsXsltElement)
            {
                // A literal result element (or an extension element): its content is a sequence constructor.
                CheckSequenceConstructor(element, bag);
            }
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                Visit(childElement, version, bag);
            }
        }
    }

    private static void CheckRequiredAttributes(StylesheetElement element, DiagnosticBag bag)
    {
        if (!s_required.TryGetValue(element.Name.LocalName, out string[]? required))
        {
            return;
        }

        foreach (string name in required)
        {
            if (!HasAttribute(element, name))
            {
                bag.Error(
                    XsltErrors.XTSE0010,
                    $"xsl:{element.Name.LocalName} requires the attribute '{name}'",
                    element.Location);
            }
        }
    }

    private static void CheckXsltContent(StylesheetElement element, DiagnosticBag bag)
    {
        string name = element.Name.LocalName;

        if (name == "stylesheet" || name == "transform")
        {
            CheckTopLevel(element, bag);
        }
        else if (s_sequenceConstructorWithLeading.TryGetValue(name, out string? leading))
        {
            CheckSequenceConstructor(element, bag, leading, name == "perform-sort");
        }
        else if (name == "text")
        {
            foreach (StylesheetNode child in element.Children)
            {
                if (child is StylesheetElement childElement)
                {
                    Report(bag, childElement, $"xsl:text may contain only text, not xsl:{childElement.Name.LocalName}");
                }
            }
        }
        else if (s_elementOnly.TryGetValue(name, out string[]? allowed))
        {
            CheckElementOnly(element, allowed, bag);
        }
    }

    // XSLT 2.0 §3.6: the children of xsl:stylesheet are declarations (or user-defined data elements).
    private static void CheckTopLevel(StylesheetElement root, DiagnosticBag bag)
    {
        foreach (StylesheetNode child in root.Children)
        {
            if (child is StylesheetText text)
            {
                if (!text.IsWhitespaceOnly)
                {
                    bag.Error(XsltErrors.XTSE0120, "Text is not allowed directly inside xsl:" + root.Name.LocalName, text.Location);
                }
            }
            else if (child is StylesheetElement element
                && element.IsXsltElement
                && XsltElementNames.IsKnown(element.Name.LocalName)
                && !s_declarations.Contains(element.Name.LocalName))
            {
                Report(bag, element, $"xsl:{element.Name.LocalName} is not allowed as a top-level element");
            }
        }
    }

    // A sequence constructor, optionally preceded by xsl:param or xsl:sort children (XSLT 2.0 §5.7, §9.1, §10.1, §11.1, §13.1).
    private static void CheckSequenceConstructor(StylesheetElement element, DiagnosticBag bag, string? leading = null, bool requireLeading = false)
    {
        bool inLeading = leading is not null;
        int leadingCount = 0;

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetText text)
            {
                if (!text.IsWhitespaceOnly)
                {
                    inLeading = false;
                }

                continue;
            }

            if (child is not StylesheetElement childElement || !childElement.IsXsltElement)
            {
                inLeading = false;
                continue;
            }

            string childName = childElement.Name.LocalName;
            if (!XsltElementNames.IsKnown(childName))
            {
                continue;
            }

            if (inLeading && childName == leading)
            {
                leadingCount++;
                continue;
            }

            inLeading = false;
            if (!s_instructions.Contains(childName))
            {
                Report(bag, childElement, $"xsl:{childName} is not allowed here (inside {Describe(element)})");
            }
        }

        if (requireLeading && leadingCount == 0)
        {
            Report(bag, element, $"{Describe(element)} requires at least one xsl:{leading}");
        }
    }

    private static void CheckElementOnly(StylesheetElement element, string[] allowed, DiagnosticBag bag)
    {
        string name = element.Name.LocalName;
        int whens = 0;
        bool otherwiseSeen = false;
        bool matching = false;
        bool nonMatching = false;

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetText text)
            {
                if (!text.IsWhitespaceOnly)
                {
                    Report(bag, element, $"Text is not allowed inside {Describe(element)}");
                }

                continue;
            }

            if (child is not StylesheetElement childElement)
            {
                continue;
            }

            string childName = childElement.Name.LocalName;
            if (childElement.IsXsltElement && !XsltElementNames.IsKnown(childName))
            {
                continue;
            }

            if (!childElement.IsXsltElement || Array.IndexOf(allowed, childName) < 0)
            {
                Report(bag, childElement, $"{(childElement.IsXsltElement ? "xsl:" + childName : childName)} is not allowed inside {Describe(element)}");
                continue;
            }

            if (name == "choose")
            {
                if (childName == "when")
                {
                    whens++;
                    if (otherwiseSeen)
                    {
                        Report(bag, childElement, "xsl:when must come before xsl:otherwise");
                    }
                }
                else if (otherwiseSeen)
                {
                    Report(bag, childElement, "xsl:choose may contain only one xsl:otherwise");
                }
                else
                {
                    otherwiseSeen = true;
                }
            }
            else if (name == "analyze-string")
            {
                if (childName == "matching-substring")
                {
                    if (matching || nonMatching)
                    {
                        Report(bag, childElement, "xsl:matching-substring must come first and appear once");
                    }

                    matching = true;
                }
                else if (childName == "non-matching-substring")
                {
                    if (nonMatching)
                    {
                        Report(bag, childElement, "xsl:non-matching-substring may appear only once");
                    }

                    nonMatching = true;
                }
            }
        }

        if (name == "choose" && whens == 0)
        {
            Report(bag, element, "xsl:choose requires at least one xsl:when");
        }
    }

    private static void Report(DiagnosticBag bag, StylesheetElement at, string message) =>
        bag.Error(XsltErrors.XTSE0010, message, at.Location);

    private static string Describe(StylesheetElement element) =>
        element.IsXsltElement ? "xsl:" + element.Name.LocalName : "<" + element.Name.LocalName + ">";

    private static bool HasAttribute(StylesheetElement element, string localName)
    {
        foreach (StylesheetAttribute attribute in element.Attributes)
        {
            if (attribute.Name.NamespaceUri.Length == 0 && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForwardsCompatible(string? version) =>
        !string.IsNullOrEmpty(version)
        && double.TryParse(version!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
        && v > 2.0;
}
