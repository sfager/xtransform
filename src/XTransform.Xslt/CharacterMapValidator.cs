using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Static checks on character maps (XSLT 2.0 §20.1): two maps with one name and import precedence (XTSE1580), a name in
/// <c>use-character-maps</c> that no map bears (XTSE1590), and a map that reaches itself through
/// <c>use-character-maps</c> (XTSE1600). The method of <c>xsl:output</c> must be a known name or a prefixed QName (XTSE1570, §20).
/// </summary>
internal static class CharacterMapValidator
{
    private static readonly char[] s_whitespace = { ' ', '\t', '\r', '\n' };

    /// <summary>Reports XTSE1570, XTSE1580, XTSE1590 and XTSE1600.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        Dictionary<QName, List<QName>> uses = new Dictionary<QName, List<QName>>();
        Dictionary<QName, StylesheetElement> declarations = new Dictionary<QName, StylesheetElement>();
        Dictionary<QName, int> precedence = new Dictionary<QName, int>();
        HashSet<StylesheetElement> seen = new HashSet<StylesheetElement>();
        // The elements that name character maps in use-character-maps, and the attribute value of each (net462 has no ValueTuple).
        List<StylesheetElement> referencingElements = new List<StylesheetElement>();
        List<string> referencedNames = new List<string>();

        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            StylesheetElement root = assembly.Modules[i].Module.Root;
            int modulePrecedence = assembly.Modules[i].ImportPrecedence;
            if (!root.IsXsltElement)
            {
                continue;
            }

            foreach (StylesheetNode child in root.Children)
            {
                if (child is not StylesheetElement element || !element.IsXsltElement || !seen.Add(element))
                {
                    continue;
                }

                if (element.Name.LocalName == "output")
                {
                    CheckMethod(element, bag);
                    string? outputUses = GetAttribute(element, "use-character-maps");
                    if (outputUses is not null)
                    {
                        referencingElements.Add(element);
                        referencedNames.Add(outputUses);
                    }
                }
                else if (element.Name.LocalName == "character-map")
                {
                    Collect(element, modulePrecedence, uses, declarations, precedence, bag);
                    string? mapUses = GetAttribute(element, "use-character-maps");
                    if (mapUses is not null)
                    {
                        referencingElements.Add(element);
                        referencedNames.Add(mapUses);
                    }
                }
            }
        }

        for (int r = 0; r < referencingElements.Count; r++)
        {
            StylesheetElement element = referencingElements[r];
            foreach (string token in referencedNames[r].Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries))
            {
                if (LexicalQNameParser.TryResolve(token, element.InScopeNamespaces, out QName name, out _) && !uses.ContainsKey(name))
                {
                    bag.Error(XsltErrors.XTSE1590, $"'{token}' in use-character-maps is not the name of a character map", element.Location);
                }
            }
        }

        HashSet<QName> done = new HashSet<QName>();
        HashSet<QName> reported = new HashSet<QName>();
        foreach (QName name in uses.Keys)
        {
            Visit(name, uses, declarations, new List<QName>(), done, reported, bag);
        }
    }

    // XSLT 2.0 §20: the serialization method is xml, html, xhtml or text, or a QName with a prefix.
    private static void CheckMethod(StylesheetElement output, DiagnosticBag bag)
    {
        string? method = GetAttribute(output, "method");
        if (method is null)
        {
            return;
        }

        method = method.Trim();
        bool known = method == "xml" || method == "html" || method == "xhtml" || method == "text";
        bool prefixed = method.IndexOf(':') > 0 && LexicalNames.IsQName(method);
        if (!known && !prefixed)
        {
            bag.Error(XsltErrors.XTSE1570, $"'{method}' is not a valid output method", output.Location);
        }
    }

    private static void Collect(
        StylesheetElement element,
        int modulePrecedence,
        Dictionary<QName, List<QName>> uses,
        Dictionary<QName, StylesheetElement> declarations,
        Dictionary<QName, int> precedence,
        DiagnosticBag bag)
    {
        string? nameValue = GetAttribute(element, "name");
        if (nameValue is null || !LexicalQNameParser.TryResolve(nameValue.Trim(), element.InScopeNamespaces, out QName name, out _))
        {
            return;
        }

        if (declarations.ContainsKey(name))
        {
            if (precedence[name] == modulePrecedence)
            {
                bag.Error(XsltErrors.XTSE1580, $"Two xsl:character-map declarations are named '{nameValue}' at the same import precedence", element.Location);
            }

            // A map at a higher import precedence replaces one at a lower precedence.
            if (precedence[name] >= modulePrecedence)
            {
                return;
            }
        }

        declarations[name] = element;
        precedence[name] = modulePrecedence;
        List<QName> used = new List<QName>();
        uses[name] = used;
        string? tokens = GetAttribute(element, "use-character-maps");
        if (tokens is null)
        {
            return;
        }

        foreach (string token in tokens.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            if (LexicalQNameParser.TryResolve(token, element.InScopeNamespaces, out QName target, out _))
            {
                used.Add(target);
            }
        }
    }

    private static void Visit(
        QName name,
        Dictionary<QName, List<QName>> uses,
        Dictionary<QName, StylesheetElement> declarations,
        List<QName> path,
        HashSet<QName> done,
        HashSet<QName> reported,
        DiagnosticBag bag)
    {
        int onPath = path.IndexOf(name);
        if (onPath >= 0)
        {
            for (int i = onPath; i < path.Count; i++)
            {
                if (reported.Add(path[i]))
                {
                    bag.Error(
                        XsltErrors.XTSE1600,
                        $"xsl:character-map '{path[i].LocalName}' reaches itself through use-character-maps",
                        declarations[path[i]].Location);
                }
            }

            return;
        }

        if (done.Contains(name) || !uses.TryGetValue(name, out List<QName>? used))
        {
            return;
        }

        path.Add(name);
        foreach (QName next in used)
        {
            Visit(next, uses, declarations, path, done, reported, bag);
        }

        path.RemoveAt(path.Count - 1);
        done.Add(name);
    }

    private static string? GetAttribute(StylesheetElement element, string localName)
    {
        foreach (StylesheetAttribute attribute in element.Attributes)
        {
            if (attribute.Name.NamespaceUri.Length == 0 && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return null;
    }
}
