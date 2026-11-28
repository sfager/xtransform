// XSLT 2.0 §20 — xsl:output declarations. Declarations with the same effective name are merged
// attribute by attribute: the value from the highest import precedence wins; two declarations
// at that precedence with different values are XTSE1560. cdata-section-elements accumulates.

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Collects and merges <c>xsl:output</c> declarations.</summary>
internal static class OutputDeclarationCollector
{
    /// <summary>Key of the unnamed (principal) output definition.</summary>
    internal static readonly QName Principal = new QName(string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Returns one merged <see cref="XsltOutputSettings"/> per output name; the unnamed
    /// definition is keyed by <see cref="Principal"/> and is always present.
    /// </summary>
    public static IReadOnlyDictionary<QName, XsltOutputSettings> Collect(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        Dictionary<QName, List<RawOutput>> byName = new Dictionary<QName, List<RawOutput>>();
        for (int m = 0; m < assembly.Modules.Count; m++)
        {
            StylesheetModuleEntry entry = assembly.Modules[m];
            StylesheetElement root = entry.Module.Root;
            if (!entry.Module.IsFullStylesheet)
            {
                continue;
            }

            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i] is StylesheetElement child &&
                    child.IsXsltElement &&
                    string.Equals(child.Name.LocalName, "output", StringComparison.Ordinal))
                {
                    AddRaw(child, entry.ImportPrecedence, byName, bag);
                }
            }
        }

        Dictionary<QName, XsltOutputSettings> result = new Dictionary<QName, XsltOutputSettings>();
        foreach (KeyValuePair<QName, List<RawOutput>> kv in byName)
        {
            result[kv.Key] = Merge(kv.Value, bag);
        }

        if (!result.ContainsKey(Principal))
        {
            result[Principal] = XsltOutputSettings.Default;
        }

        return result;
    }

    private static void AddRaw(
        StylesheetElement element,
        int precedence,
        Dictionary<QName, List<RawOutput>> byName,
        DiagnosticBag bag)
    {
        QName key = Principal;
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < element.Attributes.Count; i++)
        {
            StylesheetAttribute attr = element.Attributes[i];
            if (attr.Name.NamespaceUri.Length != 0)
            {
                continue;
            }

            if (string.Equals(attr.Name.LocalName, "name", StringComparison.Ordinal))
            {
                if (!LexicalQNameParser.TryResolve(attr.Value, element.InScopeNamespaces, out key, out string? why))
                {
                    bag.Error(XsltErrors.XTSE0020, $"Invalid xsl:output name '{attr.Value}': {why}", element.Location);
                    return;
                }

                continue;
            }

            values[attr.Name.LocalName] = attr.Value;
        }

        if (!byName.TryGetValue(key, out List<RawOutput>? list))
        {
            list = new List<RawOutput>();
            byName[key] = list;
        }

        list.Add(new RawOutput(element, precedence, values));
    }

    private static XsltOutputSettings Merge(List<RawOutput> decls, DiagnosticBag bag)
    {
        Dictionary<string, string> effective = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, RawOutput> source = new Dictionary<string, RawOutput>(StringComparer.Ordinal);

        // Highest precedence wins per attribute; equal precedence with different values is XTSE1560.
        foreach (RawOutput decl in decls)
        {
            foreach (KeyValuePair<string, string> attr in decl.Values)
            {
                if (IsAccumulating(attr.Key))
                {
                    continue;
                }

                if (!source.TryGetValue(attr.Key, out RawOutput? current) || decl.Precedence > current.Precedence)
                {
                    effective[attr.Key] = attr.Value;
                    source[attr.Key] = decl;
                }
                else if (decl.Precedence == current.Precedence &&
                         !string.Equals(effective[attr.Key], attr.Value, StringComparison.Ordinal))
                {
                    bag.Error(
                        XsltErrors.XTSE1560,
                        $"xsl:output attribute '{attr.Key}' has conflicting values '{effective[attr.Key]}' and '{attr.Value}' at the same import precedence",
                        decl.Element.Location);
                }
            }
        }

        List<QName> cdata = new List<QName>();
        foreach (RawOutput decl in decls)
        {
            if (!decl.Values.TryGetValue("cdata-section-elements", out string? list))
            {
                continue;
            }

            foreach (string token in list.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (LexicalQNameParser.TryResolve(token, decl.Element.InScopeNamespaces, out QName name, out string? why))
                {
                    cdata.Add(name);
                }
                else
                {
                    bag.Error(XsltErrors.XTSE0020, $"Invalid cdata-section-elements entry '{token}': {why}", decl.Element.Location);
                }
            }
        }

        return new XsltOutputSettings
        {
            Method = ParseMethod(effective, source, bag),
            Encoding = effective.TryGetValue("encoding", out string? enc) ? enc : "utf-8",
            Version = effective.TryGetValue("version", out string? outputVersion) && outputVersion.Trim() == "1.1" ? "1.1" : "1.0",
            Indent = ParseBool(effective, source, "indent", bag) ?? false,
            OmitXmlDeclaration = ParseBool(effective, source, "omit-xml-declaration", bag) ?? false,
            Standalone = ParseStandalone(effective, source, bag),
            DoctypePublic = effective.TryGetValue("doctype-public", out string? pub) ? pub : null,
            DoctypeSystem = effective.TryGetValue("doctype-system", out string? sys) ? sys : null,
            CdataSectionElements = cdata,
            ByteOrderMark = ParseBool(effective, source, "byte-order-mark", bag),
        };
    }

    private static bool IsAccumulating(string attribute) =>
        string.Equals(attribute, "cdata-section-elements", StringComparison.Ordinal) ||
        string.Equals(attribute, "use-character-maps", StringComparison.Ordinal);

    private static string ParseMethod(
        Dictionary<string, string> effective,
        Dictionary<string, RawOutput> source,
        DiagnosticBag bag)
    {
        if (!effective.TryGetValue("method", out string? method))
        {
            return "xml";
        }

        string value = method.Trim();
        if (value is "xml" or "html" or "xhtml" or "text" || value.IndexOf(':') > 0)
        {
            return value;
        }

        bag.Error(
            XsltErrors.XTSE0020,
            $"xsl:output method must be xml, html, xhtml, text or a prefixed QName, got '{value}'",
            source["method"].Element.Location);
        return "xml";
    }

    private static bool? ParseBool(
        Dictionary<string, string> effective,
        Dictionary<string, RawOutput> source,
        string attribute,
        DiagnosticBag bag)
    {
        if (!effective.TryGetValue(attribute, out string? raw))
        {
            return null;
        }

        switch (raw.Trim())
        {
            case "yes":
            case "true":
            case "1":
                return true;
            case "no":
            case "false":
            case "0":
                return false;
            default:
                bag.Error(
                    XsltErrors.XTSE0020,
                    $"xsl:output {attribute} must be yes or no, got '{raw}'",
                    source[attribute].Element.Location);
                return null;
        }
    }

    private static bool? ParseStandalone(
        Dictionary<string, string> effective,
        Dictionary<string, RawOutput> source,
        DiagnosticBag bag)
    {
        if (effective.TryGetValue("standalone", out string? raw) &&
            string.Equals(raw.Trim(), "omit", StringComparison.Ordinal))
        {
            return null;
        }

        return ParseBool(effective, source, "standalone", bag);
    }

    private sealed class RawOutput
    {
        public RawOutput(StylesheetElement element, int precedence, Dictionary<string, string> values)
        {
            Element = element;
            Precedence = precedence;
            Values = values;
        }

        public StylesheetElement Element { get; }
        public int Precedence { get; }
        public Dictionary<string, string> Values { get; }
    }
}
