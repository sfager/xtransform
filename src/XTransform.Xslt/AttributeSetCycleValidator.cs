using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// XSLT 2.0 §10.2 (XTSE0720): an <c>xsl:attribute-set</c> must not reference itself, directly or indirectly, through
/// the names in its <c>use-attribute-sets</c> attribute, or through an element in its content that uses a set. Declarations
/// that share a name form one set, so their references are pooled. Names that cannot be resolved are left to the checks that report them.
/// </summary>
internal static class AttributeSetCycleValidator
{
    private static readonly char[] s_whitespace = { ' ', '\t', '\r', '\n' };

    /// <summary>Reports XTSE0720 once for every attribute set that lies on a cycle.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        Dictionary<QName, List<QName>> references = new Dictionary<QName, List<QName>>();
        Dictionary<QName, StylesheetElement> declarations = new Dictionary<QName, StylesheetElement>();

        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            StylesheetElement root = assembly.Modules[i].Module.Root;
            if (!root.IsXsltElement)
            {
                continue;
            }

            foreach (StylesheetNode child in root.Children)
            {
                if (child is StylesheetElement element && element.IsXsltElement && element.Name.LocalName == "attribute-set")
                {
                    Collect(element, references, declarations);
                }
            }
        }

        HashSet<QName> done = new HashSet<QName>();
        HashSet<QName> reported = new HashSet<QName>();
        foreach (QName name in references.Keys)
        {
            Visit(name, references, declarations, new List<QName>(), done, reported, bag);
        }
    }

    private static void Collect(
        StylesheetElement element,
        Dictionary<QName, List<QName>> references,
        Dictionary<QName, StylesheetElement> declarations)
    {
        string? nameValue = GetAttribute(element, "name");
        if (nameValue is null || !LexicalQNameParser.TryResolve(nameValue.Trim(), element.InScopeNamespaces, out QName name, out _))
        {
            return;
        }

        if (!references.TryGetValue(name, out List<QName>? used))
        {
            used = new List<QName>();
            references[name] = used;
            declarations[name] = element;
        }

        CollectUses(element, used);
    }

    // The sets an element names in use-attribute-sets (xsl:use-attribute-sets on a literal result element), and those named
    // anywhere inside its content: a set whose body builds an element that uses the set again recurses without end.
    private static void CollectUses(StylesheetElement element, List<QName> used)
    {
        string? uses = element.IsXsltElement
            ? GetAttribute(element, "use-attribute-sets")
            : GetAttribute(element, "use-attribute-sets", XsltNamespace.Uri);
        if (uses is not null)
        {
            foreach (string token in uses.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries))
            {
                if (LexicalQNameParser.TryResolve(token, element.InScopeNamespaces, out QName target, out _))
                {
                    used.Add(target);
                }
            }
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                CollectUses(childElement, used);
            }
        }
    }

    // Depth-first search; a name met again while still on the path closes a cycle, and every set on that cycle is reported.
    private static void Visit(
        QName name,
        Dictionary<QName, List<QName>> references,
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
                        XsltErrors.XTSE0720,
                        $"xsl:attribute-set '{path[i].LocalName}' references itself through use-attribute-sets",
                        declarations[path[i]].Location);
                }
            }

            return;
        }

        if (done.Contains(name) || !references.TryGetValue(name, out List<QName>? used))
        {
            return;
        }

        path.Add(name);
        foreach (QName next in used)
        {
            Visit(next, references, declarations, path, done, reported, bag);
        }

        path.RemoveAt(path.Count - 1);
        done.Add(name);
    }

    private static string? GetAttribute(StylesheetElement element, string localName, string namespaceUri = "")
    {
        foreach (StylesheetAttribute attribute in element.Attributes)
        {
            if (string.Equals(attribute.Name.NamespaceUri, namespaceUri, StringComparison.Ordinal) && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return null;
    }
}
