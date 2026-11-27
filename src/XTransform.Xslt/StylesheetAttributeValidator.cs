using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Rejects attributes that an XSLT element is not allowed to have. XSLT 2.0 §3.1: an attribute with no namespace
/// or in the XSLT namespace that is not defined for the element is a static error (XTSE0090); attributes in other
/// namespaces are extension attributes and are ignored. Inside a forwards-compatible region (effective version
/// above 2.0, §3.9) the check is off.
/// </summary>
internal static class StylesheetAttributeValidator
{
    // Standard attributes (XSLT 2.0 §3.5), allowed on every XSLT element.
    private static readonly string[] s_standard =
    {
        "default-collation", "exclude-result-prefixes", "extension-element-prefixes", "use-when", "version",
        "xpath-default-namespace",
    };

    // The serialization attributes of xsl:output; xsl:result-document takes them too (§20, §19.1).
    private const string OutputAttributes =
        "method byte-order-mark cdata-section-elements doctype-public doctype-system encoding escape-uri-attributes " +
        "include-content-type indent media-type normalization-form omit-xml-declaration standalone undeclare-prefixes " +
        "use-character-maps";

    private static readonly char[] s_space = { ' ' };

    // The XSLT-namespace attributes a literal result element may carry: the standard attributes and four more (§3.5, §11.1.1).
    private static readonly HashSet<string> s_literalResultElement = new HashSet<string>(s_standard, StringComparer.Ordinal)
    {
        "use-attribute-sets", "type", "validation", "inherit-namespaces",
    };

    private static readonly Dictionary<string, HashSet<string>> s_allowed = Build();

    /// <summary>Reports XTSE0090 for each attribute of an XSLT element that the element does not define.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            StylesheetElement root = assembly.Modules[i].Module.Root;
            Visit(root, null, bag);
        }
    }

    private static void Visit(StylesheetElement element, string? inheritedVersion, DiagnosticBag bag)
    {
        // An element in the XSLT namespace that XSLT 2.0 does not define is ignored with its content under forwards-compatible
        // processing (§3.9); the reader reports it otherwise.
        if (element.IsUnknownXsltElement)
        {
            return;
        }

        string? version = inheritedVersion;
        if (element.DeclaredVersion.Length != 0)
        {
            version = element.DeclaredVersion;
        }

        if (element.IsXsltElement && !IsForwardsCompatible(version) && s_allowed.TryGetValue(element.Name.LocalName, out HashSet<string>? allowed))
        {
            foreach (StylesheetAttribute attribute in element.Attributes)
            {
                string ns = attribute.Name.NamespaceUri;
                if (ns.Length == 0)
                {
                    if (!allowed.Contains(attribute.Name.LocalName))
                    {
                        bag.Error(
                            XsltErrors.XTSE0090,
                            $"xsl:{element.Name.LocalName}: attribute '{attribute.Name.LocalName}' is not allowed",
                            attribute.Location);
                    }
                }
                else if (string.Equals(ns, XsltNamespace.Uri, StringComparison.Ordinal))
                {
                    bag.Error(
                        XsltErrors.XTSE0090,
                        $"xsl:{element.Name.LocalName}: attribute 'xsl:{attribute.Name.LocalName}' is in the XSLT namespace and is not allowed on an XSLT element",
                        attribute.Location);
                }
            }
        }

        else if (!element.IsXsltElement && !IsForwardsCompatible(version))
        {
            // XSLT 2.0 §11.1.1 (XTSE0805): an attribute of a literal result element in the XSLT namespace must be one the
            // specification defines.
            foreach (StylesheetAttribute attribute in element.Attributes)
            {
                if (string.Equals(attribute.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal)
                    && !s_literalResultElement.Contains(attribute.Name.LocalName))
                {
                    bag.Error(
                        XsltErrors.XTSE0805,
                        $"<{element.Name.LocalName}>: attribute 'xsl:{attribute.Name.LocalName}' is not defined for a literal result element",
                        attribute.Location);
                }
            }
        }

        foreach (StylesheetNode child in element.Children)
        {
            if (child is StylesheetElement childElement)
            {
                Visit(childElement, version, bag);
            }
        }
    }

    // XSLT 2.0 §3.9: forwards-compatible processing applies when the effective version is greater than 2.0.
    private static bool IsForwardsCompatible(string? version) =>
        !string.IsNullOrEmpty(version)
        && double.TryParse(version!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
        && v > 2.0;

    private static Dictionary<string, HashSet<string>> Build()
    {
        Dictionary<string, HashSet<string>> table = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        void Add(string elements, string attributes)
        {
            foreach (string element in elements.Split(' '))
            {
                HashSet<string> set = new HashSet<string>(s_standard, StringComparer.Ordinal);
                foreach (string attribute in attributes.Split(s_space, StringSplitOptions.RemoveEmptyEntries))
                {
                    set.Add(attribute);
                }

                table[element] = set;
            }
        }

        // Declarations (XSLT 2.0 §3.6 - §3.10, §6.5, §9, §10, §16, §18, §20).
        Add("stylesheet transform", "id default-validation input-type-annotations");
        Add("include import", "href");
        Add("import-schema", "namespace schema-location");
        Add("output", "name " + OutputAttributes);
        Add("template", "match name priority mode as");
        Add("variable", "name select as");
        // xsl:with-param does not allow required (XTSE0090); that is reported where call parameters are compiled, so it is allowed here.
        Add("param", "name select as required tunnel");
        Add("with-param", "name select as tunnel required");
        Add("function", "name as override");
        Add("key", "name match use collation");
        Add("attribute-set", "name use-attribute-sets");
        Add("character-map", "name use-character-maps");
        Add("output-character", "character string");
        Add("namespace-alias", "stylesheet-prefix result-prefix");
        Add("strip-space preserve-space", "elements");

        // Instructions.
        Add("apply-templates", "select mode");
        Add("apply-imports next-match choose otherwise fallback matching-substring non-matching-substring", string.Empty);
        Add("call-template", "name");
        Add("if when", "test");
        Add("for-each perform-sort sequence", "select");
        Add("for-each-group", "select group-by group-adjacent group-starting-with group-ending-with collation");
        Add("value-of", "select separator disable-output-escaping");
        Add("text", "disable-output-escaping");
        Add("copy", "copy-namespaces inherit-namespaces use-attribute-sets type validation");
        Add("copy-of", "select copy-namespaces type validation");
        Add("element", "name namespace inherit-namespaces use-attribute-sets type validation");
        Add("attribute", "name namespace select separator type validation");
        Add("namespace", "name select");
        Add("comment", "select");
        Add("processing-instruction", "name select");
        Add("document", "validation type");
        Add("result-document", "format href validation type output-version " + OutputAttributes);
        Add("message", "select terminate");
        Add("analyze-string", "select regex flags");
        Add("number", "value select level count from format lang letter-value ordinal grouping-separator grouping-size");
        Add("sort", "select lang order collation stable case-order data-type");

        // xsl:decimal-format is checked where it is collected (DeclarationCollector), which also reads its name.
        return table;
    }
}
