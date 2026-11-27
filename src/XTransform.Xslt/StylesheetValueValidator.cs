using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Checks the lexical form of attribute values that the XSLT 2.0 schema types as a QName, a <c>yes</c> / <c>no</c>
/// flag or an enumeration (XSLT 2.0 §3.1, §6.5, §9, §10, §16, §20; XTSE0020). Attributes that are attribute value
/// templates are checked only when they contain no braces. In a forwards-compatible region the flags also accept
/// the XSLT 3.0 spellings (<c>true</c>, <c>false</c>, <c>1</c>, <c>0</c>).
/// </summary>
internal static class StylesheetValueValidator
{
    private enum Kind
    {
        QName,
        YesNo,
        Enum,
        TemplateMode,
        ApplyMode,
    }

    private sealed class Rule
    {
        public Rule(Kind kind, bool avt = false, params string[] values)
        {
            Kind = kind;
            Avt = avt;
            Values = values;
        }

        public Kind Kind { get; }

        public bool Avt { get; }

        public string[] Values { get; }
    }

    private static readonly char[] s_braces = { '{', '}' };

    private static readonly Dictionary<string, Dictionary<string, Rule>> s_rules = Build();

    /// <summary>Reports XTSE0020 for each attribute value that is not valid for its type.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            Visit(assembly.Modules[i].Module.Root, null, null, bag);
        }
    }

    private static void Visit(StylesheetElement element, StylesheetElement? parent, string? inheritedVersion, DiagnosticBag bag)
    {
        // An element in the XSLT namespace that XSLT 2.0 does not define is ignored with its content under forwards-compatible
        // processing (§3.9); the reader reports it otherwise.
        if (element.IsUnknownXsltElement)
        {
            return;
        }

        string? version = element.DeclaredVersion.Length != 0 ? element.DeclaredVersion : inheritedVersion;
        bool forwards = IsForwardsCompatible(version);

        if (element.IsXsltElement)
        {
            string name = element.Name.LocalName;
            if (s_rules.TryGetValue(name, out Dictionary<string, Rule>? rules))
            {
                foreach (StylesheetAttribute attribute in element.Attributes)
                {
                    if (attribute.Name.NamespaceUri.Length == 0 && rules.TryGetValue(attribute.Name.LocalName, out Rule? rule))
                    {
                        CheckValue(element, attribute, rule, forwards, bag);
                    }
                }
            }

            // XSLT 2.0 §10.1.2: tunnel parameters exist only on template parameters.
            if (name == "param" && !forwards && !IsTemplateParam(parent))
            {
                string? tunnel = GetAttribute(element, "tunnel");
                if (tunnel is not null && IsYes(tunnel))
                {
                    bag.Error(XsltErrors.XTSE0020, "tunnel=\"yes\" is allowed only on a parameter of a template", element.Location);
                }
            }
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                Visit(childElement, element, version, bag);
            }
        }
    }

    private static void CheckValue(StylesheetElement element, StylesheetAttribute attribute, Rule rule, bool forwards, DiagnosticBag bag)
    {
        string value = attribute.Value.Trim();
        if (rule.Avt && value.IndexOfAny(s_braces) >= 0)
        {
            return;
        }

        bool valid = rule.Kind switch
        {
            Kind.QName => IsQName(value),
            Kind.YesNo => IsYesNo(value, forwards),
            Kind.Enum => Array.IndexOf(rule.Values, value) >= 0,
            Kind.TemplateMode => IsTemplateMode(value),
            Kind.ApplyMode => value == "#default" || value == "#current" || IsQName(value),
            _ => true,
        };

        if (!valid)
        {
            bag.Error(
                rule.Kind == Kind.TemplateMode ? XsltErrors.XTSE0550 : XsltErrors.XTSE0020,
                $"xsl:{element.Name.LocalName}: '{attribute.Value}' is not a valid value for the attribute '{attribute.Name.LocalName}'",
                attribute.Location);
        }
    }

    private static bool IsQName(string value)
    {
        int colon = value.IndexOf(':');
        if (colon < 0)
        {
            return IsNCName(value);
        }

        return IsNCName(value.Substring(0, colon)) && IsNCName(value.Substring(colon + 1));
    }

    private static bool IsNCName(string value)
    {
        if (value.Length == 0 || !XmlConvert.IsStartNCNameChar(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!XmlConvert.IsNCNameChar(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    // XSLT 2.0 §6.5 (XTSE0550): "#all" alone, or a non-empty whitespace-separated list of distinct QNames and "#default".
    private static bool IsTemplateMode(string value)
    {
        string[] tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string token in tokens)
        {
            if ((token != "#all" && token != "#default" && !IsQName(token)) || !seen.Add(token))
            {
                return false;
            }
        }

        return !seen.Contains("#all") || tokens.Length == 1;
    }

    private static bool IsYesNo(string value, bool forwards) =>
        value == "yes" || value == "no" || (forwards && (value == "true" || value == "false" || value == "1" || value == "0"));

    private static bool IsYes(string value)
    {
        string v = value.Trim();
        return v == "yes" || v == "true" || v == "1";
    }

    private static bool IsTemplateParam(StylesheetElement? parent) =>
        parent is not null && parent.IsXsltElement && parent.Name.LocalName == "template";

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

    private static bool IsForwardsCompatible(string? version) =>
        !string.IsNullOrEmpty(version)
        && double.TryParse(version!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
        && v > 2.0;

    private static Dictionary<string, Dictionary<string, Rule>> Build()
    {
        Dictionary<string, Dictionary<string, Rule>> table = new Dictionary<string, Dictionary<string, Rule>>(StringComparer.Ordinal);

        void Add(string elements, string attribute, Rule rule)
        {
            foreach (string element in elements.Split(' '))
            {
                if (!table.TryGetValue(element, out Dictionary<string, Rule>? rules))
                {
                    rules = new Dictionary<string, Rule>(StringComparer.Ordinal);
                    table[element] = rules;
                }

                rules[attribute] = rule;
            }
        }

        // QName-valued names (XSLT 2.0 §3.1: a name that is not an AVT is a lexical QName).
        Add("attribute-set key template call-template variable param with-param function decimal-format character-map output", "name", new Rule(Kind.QName));
        Add("template", "mode", new Rule(Kind.TemplateMode));
        Add("apply-templates", "mode", new Rule(Kind.ApplyMode));

        // Flags (yes | no).
        Add("param with-param", "tunnel", new Rule(Kind.YesNo));
        Add("param", "required", new Rule(Kind.YesNo));
        Add("copy copy-of", "copy-namespaces", new Rule(Kind.YesNo));
        Add("copy element", "inherit-namespaces", new Rule(Kind.YesNo));
        Add("value-of text", "disable-output-escaping", new Rule(Kind.YesNo));
        Add("message", "terminate", new Rule(Kind.YesNo, avt: true));
        Add("sort", "stable", new Rule(Kind.YesNo, avt: true));
        Add("output", "omit-xml-declaration", new Rule(Kind.YesNo));
        Add("output", "indent", new Rule(Kind.YesNo));
        Add("output", "byte-order-mark", new Rule(Kind.YesNo));
        Add("output", "escape-uri-attributes", new Rule(Kind.YesNo));
        Add("output", "include-content-type", new Rule(Kind.YesNo));
        Add("output", "undeclare-prefixes", new Rule(Kind.YesNo));
        Add("result-document", "omit-xml-declaration", new Rule(Kind.YesNo, avt: true));
        Add("result-document", "indent", new Rule(Kind.YesNo, avt: true));
        Add("result-document", "byte-order-mark", new Rule(Kind.YesNo, avt: true));
        Add("result-document", "escape-uri-attributes", new Rule(Kind.YesNo, avt: true));
        Add("result-document", "include-content-type", new Rule(Kind.YesNo, avt: true));
        Add("result-document", "undeclare-prefixes", new Rule(Kind.YesNo, avt: true));

        // Enumerations.
        Add("output", "standalone", new Rule(Kind.Enum, false, "yes", "no", "omit"));
        Add("result-document", "standalone", new Rule(Kind.Enum, true, "yes", "no", "omit"));
        Add("sort", "order", new Rule(Kind.Enum, true, "ascending", "descending"));
        Add("sort", "case-order", new Rule(Kind.Enum, true, "upper-first", "lower-first"));
        Add("number", "level", new Rule(Kind.Enum, false, "single", "multiple", "any"));
        Add("copy copy-of element attribute document result-document", "validation", new Rule(Kind.Enum, false, "strict", "lax", "preserve", "strip"));

        return table;
    }
}
