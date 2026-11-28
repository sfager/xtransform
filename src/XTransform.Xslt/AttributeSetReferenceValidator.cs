using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// XSLT 2.0 §10.2 (XTSE0710): each name in a <c>use-attribute-sets</c> attribute of <c>xsl:copy</c>, <c>xsl:element</c> or
/// <c>xsl:attribute-set</c> (or <c>xsl:use-attribute-sets</c> on a literal result element) must be the name of an
/// <c>xsl:attribute-set</c> declared in the stylesheet. A prefix with no namespace declaration is XTSE0280.
/// </summary>
internal static class AttributeSetReferenceValidator
{
    private static readonly char[] s_whitespace = { ' ', '\t', '\r', '\n' };

    /// <summary>Reports XTSE0710 for a reference to an attribute set that is not declared.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        HashSet<QName> declared = new HashSet<QName>();
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            StylesheetElement root = assembly.Modules[i].Module.Root;
            if (!root.IsXsltElement)
            {
                continue;
            }

            foreach (StylesheetNode child in root.Children)
            {
                if (child is StylesheetElement element
                    && element.IsXsltElement
                    && element.Name.LocalName == "attribute-set"
                    && GetAttribute(element, "name", string.Empty) is string name
                    && LexicalQNameParser.TryResolve(name.Trim(), element.InScopeNamespaces, out QName qname, out _))
                {
                    declared.Add(qname);
                }
            }
        }

        HashSet<StylesheetElement> visited = new HashSet<StylesheetElement>();
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            Visit(assembly.Modules[i].Module.Root, null, declared, visited, bag);
        }
    }

    private static void Visit(
        StylesheetElement element,
        string? inheritedVersion,
        HashSet<QName> declared,
        HashSet<StylesheetElement> visited,
        DiagnosticBag bag)
    {
        // An included module's declarations are shared with the module that includes it; check each element once.
        if (element.IsUnknownXsltElement || !visited.Add(element))
        {
            return;
        }

        string? version = element.DeclaredVersion.Length != 0 ? element.DeclaredVersion : inheritedVersion;
        if (!IsForwardsCompatible(version))
        {
            string? uses = element.IsXsltElement
                ? (IsUsingElement(element.Name.LocalName) ? GetAttribute(element, "use-attribute-sets", string.Empty) : null)
                : GetAttribute(element, "use-attribute-sets", XsltNamespace.Uri);
            if (uses is not null)
            {
                CheckReferences(element, uses, declared, bag);
            }
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                Visit(childElement, version, declared, visited, bag);
            }
        }
    }

    private static void CheckReferences(StylesheetElement element, string uses, HashSet<QName> declared, DiagnosticBag bag)
    {
        foreach (string token in uses.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = token.IndexOf(':');
            string prefixPart = colon > 0 ? token.Substring(0, colon) : string.Empty;
            string localPart = colon > 0 ? token.Substring(colon + 1) : token;

            if (!IsNCName(localPart) || (colon >= 0 && !IsNCName(prefixPart)))
            {
                bag.Error(XsltErrors.XTSE0710, $"'{token}' is not the name of an attribute set", element.Location);
            }
            else if (!LexicalQNameParser.TryResolve(token, element.InScopeNamespaces, out QName name, out _))
            {
                bag.Error(XsltErrors.XTSE0280, $"The prefix '{prefixPart}' in the attribute set name '{token}' is not declared", element.Location);
            }
            else if (!declared.Contains(name))
            {
                bag.Error(XsltErrors.XTSE0710, $"No xsl:attribute-set is named '{token}'", element.Location);
            }
        }
    }

    private static bool IsUsingElement(string localName) =>
        localName == "copy" || localName == "element" || localName == "attribute-set";

    private static bool IsNCName(string value) => LexicalNames.IsNCName(value);

    private static string? GetAttribute(StylesheetElement element, string localName, string namespaceUri)
    {
        foreach (StylesheetAttribute attribute in element.Attributes)
        {
            if (string.Equals(attribute.Name.NamespaceUri, namespaceUri, StringComparison.Ordinal)
                && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static bool IsForwardsCompatible(string? version) =>
        !string.IsNullOrEmpty(version)
        && double.TryParse(version!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
        && v > 2.0;
}
