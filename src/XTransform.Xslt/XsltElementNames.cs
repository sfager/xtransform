using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// The set of XSLT 2.0 element local names. Used by the stylesheet reader to detect
/// unknown <c>xsl:*</c> elements (XSLT 2.0 §3.7; outside forwards-compatible mode these
/// raise <c>XTSE0805</c>).
/// </summary>
internal static class XsltElementNames
{
    private static readonly HashSet<string> s_known = new HashSet<string>(System.StringComparer.Ordinal)
    {
        // Top-level
        "stylesheet", "transform", "include", "import",
        "output", "template", "variable", "param", "function", "key", "mode",
        "attribute-set", "decimal-format", "character-map", "namespace-alias",
        "strip-space", "preserve-space", "import-schema",
        // Instructions
        "apply-templates", "apply-imports", "call-template",
        "if", "choose", "when", "otherwise",
        "for-each", "for-each-group",
        "value-of", "text", "sequence",
        "copy", "copy-of",
        "element", "attribute", "comment", "processing-instruction", "document", "namespace",
        "with-param",
        "result-document", "message",
        "analyze-string", "matching-substring", "non-matching-substring",
        "number", "sort", "perform-sort",
        "next-match", "fallback",
        // Character-map children
        "output-character",
    };

    /// <summary>True when <paramref name="localName"/> is an XSLT 2.0 element name.</summary>
    public static bool IsKnown(string localName) => s_known.Contains(localName);
}
