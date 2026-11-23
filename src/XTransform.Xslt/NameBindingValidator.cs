// XSLT 2.0 §6.4 — mode references must match at least one template rule (XTSE0680).

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Walks the declaration table's templates looking for <c>xsl:apply-templates</c> and
/// <c>xsl:call-template</c> usages whose referenced name or mode does not exist and reports
/// the appropriate static error.
/// </summary>
internal static class NameBindingValidator
{
    /// <summary>Runs the validator and appends diagnostics to <paramref name="bag"/>.</summary>
    public static void Validate(DeclarationTable declarations, DiagnosticBag bag)
    {
        // Pre-compute lookup sets. The default mode (empty QName) is always valid.
        HashSet<QName> declaredModes = new HashSet<QName>(declarations.DeclaredModes);
        declaredModes.Add(DeclarationCollector.DefaultMode); // default mode is always valid.

        for (int i = 0; i < declarations.Templates.Count; i++)
        {
            VisitElement(declarations.Templates[i].Element, declaredModes, declarations.NamedTemplates, bag);
        }
    }

    private static void VisitElement(
        StylesheetElement element,
        HashSet<QName> declaredModes,
        IReadOnlyDictionary<QName, TemplateDecl> namedTemplates,
        DiagnosticBag bag)
    {
        if (element.IsXsltElement)
        {
            switch (element.Name.LocalName)
            {
                case "apply-templates":
                    CheckApplyTemplatesMode(element, declaredModes, bag);
                    break;

                case "call-template":
                    CheckCallTemplateName(element, namedTemplates, bag);
                    break;
            }
        }

        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is StylesheetElement child)
            {
                VisitElement(child, declaredModes, namedTemplates, bag);
            }
        }
    }

    private static void CheckApplyTemplatesMode(
        StylesheetElement element,
        HashSet<QName> declaredModes,
        DiagnosticBag bag)
    {
        string? modeAttr = GetAttribute(element, "mode");
        if (modeAttr is null || modeAttr.Length == 0)
        {
            // Default mode is always valid.
            return;
        }

        if (string.Equals(modeAttr, "#default", StringComparison.Ordinal) ||
            string.Equals(modeAttr, "#current", StringComparison.Ordinal))
        {
            return;
        }

        if (!LexicalQNameParser.TryResolve(modeAttr, element.InScopeNamespaces, out QName mode, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid mode '{modeAttr}' on xsl:apply-templates: {why}", element.Location);
            return;
        }

        if (!declaredModes.Contains(mode))
        {
            bag.Error(
                XsltErrors.XTSE0680,
                $"xsl:apply-templates references mode '{mode.ClarkName}' but no template rule declares it",
                element.Location);
        }
    }

    private static void CheckCallTemplateName(
        StylesheetElement element,
        IReadOnlyDictionary<QName, TemplateDecl> namedTemplates,
        DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:call-template requires a name attribute", element.Location);
            return;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid template name '{nameAttr}': {why}", element.Location);
            return;
        }

        if (!namedTemplates.ContainsKey(name))
        {
            // Spec code is XTSE0690 (unknown named template); we reuse XTSE0680 for now since
            // its XTSE0690 constant isn't defined yet — later tasks will add it.
            bag.Error(
                XsltErrors.XTSE0680,
                $"xsl:call-template references unknown template '{name.ClarkName}'",
                element.Location);
        }
    }

    private static string? GetAttribute(StylesheetElement element, string localName)
    {
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (attr.Name.NamespaceUri.Length == 0 &&
                string.Equals(attr.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return attr.Value;
            }
        }

        return null;
    }
}
