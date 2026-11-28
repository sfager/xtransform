// XSLT 2.0 §3.7 — a stylesheet module whose document element is a literal result element is a simplified
// stylesheet: shorthand for an xsl:stylesheet with one xsl:template match="/" whose body is that element.

using System;
using XTransform.Xdm;

namespace XTransform.Xslt;

internal static class SimplifiedStylesheet
{
    /// <summary>True when <paramref name="root"/> is xsl:stylesheet or xsl:transform.</summary>
    public static bool IsStylesheetElement(StylesheetElement root) =>
        root.IsXsltElement &&
        (string.Equals(root.Name.LocalName, "stylesheet", StringComparison.Ordinal) ||
         string.Equals(root.Name.LocalName, "transform", StringComparison.Ordinal));

    /// <summary>
    /// Creates the implicit template of a simplified stylesheet whose root is <paramref name="root"/>. Returns null when the
    /// element has no <c>xsl:version</c> attribute, which is <c>XTSE0150</c>.
    /// </summary>
    public static StylesheetElement? CreateTemplate(StylesheetElement root)
    {
        bool hasXslVersion = false;
        for (int i = 0; i < root.Attributes.Count; i++)
        {
            StylesheetAttribute a = root.Attributes[i];
            if (string.Equals(a.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal) &&
                string.Equals(a.Name.LocalName, "version", StringComparison.Ordinal))
            {
                hasXslVersion = true;
                break;
            }
        }

        if (!hasXslVersion)
        {
            return null;
        }

        StylesheetElement template = new StylesheetElement(
            new QName("xsl", XsltNamespace.Uri, "template"), root.InScopeNamespaces, root.Location);
        template.AppendAttribute(new StylesheetAttribute(new QName(string.Empty, string.Empty, "match"), "/", root.Location));
        template.AppendChild(root);
        return template;
    }
}
