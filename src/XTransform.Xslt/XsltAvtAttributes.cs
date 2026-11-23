using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// Registry of XSLT 2.0 attributes on <c>xsl:*</c> elements that are attribute value templates.
/// Per XSLT 2.0 §3.7, most <c>xsl:*</c> attributes take plain strings or XPath expressions;
/// a specific subset (listed here) are AVTs. Every attribute on a literal result element is
/// treated as an AVT by the caller — this table is only consulted for <c>xsl:*</c> elements.
/// </summary>
internal static class XsltAvtAttributes
{
    // Keyed by "elementLocalName/attributeLocalName". The attribute name has no prefix (AVTs
    // live in the null namespace on xsl:* elements).
    private static readonly HashSet<string> s_avtAttributes = new HashSet<string>(System.StringComparer.Ordinal)
    {
        "element/name",
        "element/namespace",
        "attribute/name",
        "attribute/namespace",
        "processing-instruction/name",
        "namespace/name",
        "value-of/separator",
        "number/format",
        "number/lang",
        "number/letter-value",
        "number/ordinal",
        "number/grouping-separator",
        "number/grouping-size",
        "sort/lang",
        "sort/data-type",
        "sort/order",
        "sort/case-order",
        "sort/stable",
        "sort/collation",
        "result-document/href",
        "result-document/format",
        "result-document/method",
        "result-document/byte-order-mark",
        "result-document/cdata-section-elements",
        "result-document/doctype-public",
        "result-document/doctype-system",
        "result-document/encoding",
        "result-document/escape-uri-attributes",
        "result-document/include-content-type",
        "result-document/indent",
        "result-document/media-type",
        "result-document/normalization-form",
        "result-document/omit-xml-declaration",
        "result-document/output-version",
        "result-document/standalone",
        "result-document/undeclare-prefixes",
        "result-document/use-character-maps",
        "message/terminate",
    };

    /// <summary>
    /// True when the attribute named <paramref name="attributeLocalName"/> on <c>xsl:<paramref name="elementLocalName"/></c>
    /// is an attribute value template.
    /// </summary>
    public static bool IsAvt(string elementLocalName, string attributeLocalName)
    {
        return s_avtAttributes.Contains(elementLocalName + "/" + attributeLocalName);
    }
}
