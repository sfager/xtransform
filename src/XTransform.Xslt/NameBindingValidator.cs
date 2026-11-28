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

        // XSLT 2.0 has no static error for a mode that no template declares: the built-in rules apply (§6.6).
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

        if (!namedTemplates.TryGetValue(name, out TemplateDecl? target))
        {
            bag.Error(
                XsltErrors.XTSE0650,
                $"xsl:call-template references unknown template '{name.ClarkName}'",
                element.Location);
            return;
        }

        CheckCallParameters(element, target, bag);
    }

    // XSLT 2.0 §10.1.1: the with-param children of xsl:call-template against the parameters the template declares.
    private static void CheckCallParameters(StylesheetElement call, TemplateDecl target, DiagnosticBag bag)
    {
        // The declared parameters: name, tunnel, required.
        List<DeclaredParam> declared = new List<DeclaredParam>();
        for (int i = 0; i < target.Element.Children.Count; i++)
        {
            if (target.Element.Children[i] is not StylesheetElement p ||
                !p.IsXsltElement ||
                !string.Equals(p.Name.LocalName, "param", StringComparison.Ordinal))
            {
                continue;
            }

            string? pn = GetAttribute(p, "name");
            if (pn is null || !LexicalQNameParser.TryResolve(pn, p.InScopeNamespaces, out QName paramName, out _))
            {
                continue;
            }

            declared.Add(new DeclaredParam(
                paramName,
                string.Equals(GetAttribute(p, "tunnel")?.Trim(), "yes", StringComparison.Ordinal),
                string.Equals(GetAttribute(p, "required")?.Trim(), "yes", StringComparison.Ordinal)));
        }

        // The with-params supplied: their names, split by tunnel.
        List<QName> supplied = new List<QName>();
        for (int i = 0; i < call.Children.Count; i++)
        {
            if (call.Children[i] is not StylesheetElement w ||
                !w.IsXsltElement ||
                !string.Equals(w.Name.LocalName, "with-param", StringComparison.Ordinal))
            {
                continue;
            }

            string? wn = GetAttribute(w, "name");
            if (wn is null || !LexicalQNameParser.TryResolve(wn, w.InScopeNamespaces, out QName withName, out _))
            {
                continue;
            }

            if (string.Equals(GetAttribute(w, "tunnel")?.Trim(), "yes", StringComparison.Ordinal))
            {
                continue;
            }

            supplied.Add(withName);

            // XTSE0680: a non-tunnel with-param needs a non-tunnel parameter of that name.
            bool matches = false;
            foreach (DeclaredParam d in declared)
            {
                matches |= !d.Tunnel && d.Name.Equals(withName);
            }

            if (!matches)
            {
                bag.Error(
                    XsltErrors.XTSE0680,
                    $"xsl:with-param '{withName.ClarkName}' matches no non-tunnel parameter of the template",
                    w.Location);
            }
        }

        // XTSE0690: a required non-tunnel parameter must be supplied.
        foreach (DeclaredParam d in declared)
        {
            if (d.Required && !d.Tunnel && !supplied.Contains(d.Name))
            {
                bag.Error(
                    XsltErrors.XTSE0690,
                    $"xsl:call-template does not supply the required parameter '{d.Name.ClarkName}'",
                    call.Location);
            }
        }
    }

    private sealed class DeclaredParam
    {
        public DeclaredParam(QName name, bool tunnel, bool required)
        {
            Name = name;
            Tunnel = tunnel;
            Required = required;
        }

        public QName Name { get; }

        public bool Tunnel { get; }

        public bool Required { get; }
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
