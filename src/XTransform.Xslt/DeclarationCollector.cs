// XSLT 2.0 §3.6 — declaration collection pass.
// Walks every module in a linked StylesheetAssembly in ascending import-precedence order,
// classifies top-level xsl:* declarations and detects import-precedence-sensitive duplicates:
//   XTSE0630 — duplicate global variables at the same precedence
//   XTSE0660 — duplicate named templates at the same precedence
//   XTSE0740 — xsl:function name must have a prefix

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Builds a <see cref="DeclarationTable"/> from a linked <see cref="StylesheetAssembly"/>.
/// </summary>
internal static class DeclarationCollector
{
    /// <summary>Sentinel value representing the default (unnamed) mode.</summary>
    internal static readonly QName DefaultMode = new QName(string.Empty, string.Empty, string.Empty);


    /// <summary>Runs the declaration-collection pass and appends diagnostics to <paramref name="bag"/>.</summary>
    public static DeclarationTable Collect(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        Dictionary<QName, GlobalVariableDecl> globals = new Dictionary<QName, GlobalVariableDecl>();
        Dictionary<QName, TemplateDecl> namedTemplates = new Dictionary<QName, TemplateDecl>();
        Dictionary<FunctionKey, FunctionDecl> functions = new Dictionary<FunctionKey, FunctionDecl>();
        List<TemplateDecl> templates = new List<TemplateDecl>();
        HashSet<QName> declaredModes = new HashSet<QName>();

        // Modules are iterated in load order (post-order). Entries with the same precedence
        // can conflict; higher precedence overrides lower. We process in ascending order and
        // overwrite with higher precedence; same-precedence overwrites raise an error.
        for (int m = 0; m < assembly.Modules.Count; m++)
        {
            StylesheetModuleEntry entry = assembly.Modules[m];
            StylesheetElement root = entry.Module.Root;
            if (!root.IsXsltElement ||
                (!string.Equals(root.Name.LocalName, "stylesheet", StringComparison.Ordinal) &&
                 !string.Equals(root.Name.LocalName, "transform", StringComparison.Ordinal)))
            {
                // Simplified stylesheet — root is a literal result element acting as the body
                // of an implicit xsl:template match="/". No top-level decls to collect here.
                continue;
            }

            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i] is not StylesheetElement child || !child.IsXsltElement)
                {
                    continue;
                }

                switch (child.Name.LocalName)
                {
                    case "variable":
                    case "param":
                        CollectGlobalVariable(child, entry.ImportPrecedence, globals, bag);
                        break;

                    case "template":
                        CollectTemplate(child, entry.ImportPrecedence, templates, namedTemplates, declaredModes, bag);
                        break;

                    case "function":
                        CollectFunction(child, entry.ImportPrecedence, functions, bag);
                        break;

                    // Other top-level declarations (key, attribute-set, decimal-format,
                    // character-map, namespace-alias, output, strip-space, preserve-space,
                    // import-schema) are collected in later tasks — P3-04 stays tight.
                    default:
                        break;
                }
            }
        }

        return new DeclarationTable(globals, templates, namedTemplates, functions, declaredModes);
    }

    // ── xsl:variable / xsl:param ────────────────────────────────────────────

    private static void CollectGlobalVariable(
        StylesheetElement element,
        int precedence,
        Dictionary<QName, GlobalVariableDecl> globals,
        DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, $"xsl:{element.Name.LocalName} requires a name attribute", element.Location);
            return;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid variable name '{nameAttr}': {why}", element.Location);
            return;
        }

        bool isParam = string.Equals(element.Name.LocalName, "param", StringComparison.Ordinal);
        GlobalVariableDecl decl = new GlobalVariableDecl(
            element,
            precedence,
            name,
            GetAttribute(element, "select"),
            isParam,
            isRequired: string.Equals(GetAttribute(element, "required"), "yes", StringComparison.Ordinal),
            asType: GetAttribute(element, "as"));

        if (globals.TryGetValue(name, out GlobalVariableDecl? existing))
        {
            if (existing.ImportPrecedence == precedence)
            {
                bag.Error(
                    XsltErrors.XTSE0630,
                    $"Duplicate global {(isParam ? "parameter" : "variable")} '{name.ClarkName}' at the same import precedence",
                    element.Location);
                return;
            }

            // Higher precedence wins silently (XSLT 2.0 §9.3).
            if (existing.ImportPrecedence > precedence)
            {
                return;
            }
        }

        globals[name] = decl;
    }

    // ── xsl:template ────────────────────────────────────────────────────────

    private static void CollectTemplate(
        StylesheetElement element,
        int precedence,
        List<TemplateDecl> templates,
        Dictionary<QName, TemplateDecl> namedTemplates,
        HashSet<QName> declaredModes,
        DiagnosticBag bag)
    {
        QName? name = null;
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is not null)
        {
            if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName n, out string? why))
            {
                bag.Error(XsltErrors.XTSE0010, $"Invalid template name '{nameAttr}': {why}", element.Location);
            }
            else
            {
                name = n;
            }
        }

        List<QName> modes = new List<QName>();
        string? modeAttr = GetAttribute(element, "mode");
        if (modeAttr is not null)
        {
            foreach (string token in modeAttr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, "#default", StringComparison.Ordinal) ||
                    string.Equals(token, "#all", StringComparison.Ordinal))
                {
                    // Default mode is represented by the empty-string QName; #all is handled later.
                    modes.Add(DefaultMode);
                    continue;
                }

                if (!LexicalQNameParser.TryResolve(token, element.InScopeNamespaces, out QName modeQ, out string? why))
                {
                    bag.Error(XsltErrors.XTSE0010, $"Invalid mode '{token}': {why}", element.Location);
                    continue;
                }

                modes.Add(modeQ);
            }
        }

        TemplateDecl decl = new TemplateDecl(
            element,
            precedence,
            name,
            matchPattern: GetAttribute(element, "match"),
            modes,
            priority: GetAttribute(element, "priority"));

        templates.Add(decl);

        // Track declared modes — a template that declares @mode makes that mode valid for
        // apply-templates (XTSE0680 check in NameBindingValidator).
        for (int i = 0; i < modes.Count; i++)
        {
            declaredModes.Add(modes[i]);
        }

        if (name is QName nq)
        {
            if (namedTemplates.TryGetValue(nq, out TemplateDecl? existing))
            {
                if (existing.ImportPrecedence == precedence)
                {
                    bag.Error(
                        XsltErrors.XTSE0660,
                        $"Duplicate named template '{nq.ClarkName}' at the same import precedence",
                        element.Location);
                    return;
                }

                if (existing.ImportPrecedence > precedence)
                {
                    return;
                }
            }

            namedTemplates[nq] = decl;
        }
    }

    // ── xsl:function ────────────────────────────────────────────────────────

    private static void CollectFunction(
        StylesheetElement element,
        int precedence,
        Dictionary<FunctionKey, FunctionDecl> functions,
        DiagnosticBag bag)
    {
        string? nameAttr = GetAttribute(element, "name");
        if (nameAttr is null)
        {
            bag.Error(XsltErrors.XTSE0010, "xsl:function requires a name attribute", element.Location);
            return;
        }

        if (!LexicalQNameParser.HasPrefix(nameAttr))
        {
            bag.Error(
                XsltErrors.XTSE0740,
                $"xsl:function name '{nameAttr}' must have a prefix (user functions live in a namespace)",
                element.Location);
            return;
        }

        if (!LexicalQNameParser.TryResolve(nameAttr, element.InScopeNamespaces, out QName name, out string? why))
        {
            bag.Error(XsltErrors.XTSE0010, $"Invalid function name '{nameAttr}': {why}", element.Location);
            return;
        }

        int arity = 0;
        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is StylesheetElement child &&
                child.IsXsltElement &&
                string.Equals(child.Name.LocalName, "param", StringComparison.Ordinal))
            {
                arity++;
            }
        }

        FunctionDecl decl = new FunctionDecl(element, precedence, name, arity, GetAttribute(element, "as"));

        FunctionKey key = new FunctionKey(name, arity);
        if (functions.TryGetValue(key, out FunctionDecl? existing))
        {
            if (existing.ImportPrecedence == precedence)
            {
                bag.Error(
                    XsltErrors.XTSE0660,
                    $"Duplicate xsl:function '{name.ClarkName}#{arity}' at the same import precedence",
                    element.Location);
                return;
            }

            if (existing.ImportPrecedence > precedence)
            {
                return;
            }
        }

        functions[key] = decl;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
