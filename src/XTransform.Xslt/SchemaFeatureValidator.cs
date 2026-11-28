using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// XTransform is a basic XSLT processor: it is not schema-aware. XSLT 2.0 §3.12 makes these static errors for such a
/// processor: <c>xsl:import-schema</c> (XTSE1650), a <c>type</c> attribute, and a <c>validation</c> or
/// <c>default-validation</c> attribute other than <c>strip</c> (XTSE1660). The same attributes in the XSLT namespace on a
/// literal result element are covered. <c>preserve</c> is accepted under forwards-compatible processing, where a basic
/// processor treats it like <c>strip</c> (XSLT 3.0 §3.5).
/// </summary>
internal static class SchemaFeatureValidator
{
    private static readonly HashSet<string> s_typedInstructions = new HashSet<string>(StringComparer.Ordinal)
    {
        "copy", "copy-of", "element", "attribute", "document", "result-document", "stylesheet", "transform",
    };

    /// <summary>Reports XTSE1650 and XTSE1660 for schema-aware constructs.</summary>
    public static void Validate(StylesheetAssembly assembly, DiagnosticBag bag)
    {
        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            Visit(assembly.Modules[i].Module.Root, null, bag);
        }
    }

    private static void Visit(StylesheetElement element, string? inheritedVersion, DiagnosticBag bag)
    {
        // An unrecognised xsl: element is ignored with its content under forwards-compatible processing (§3.9).
        if (element.IsUnknownXsltElement)
        {
            return;
        }

        string? version = element.DeclaredVersion.Length != 0 ? element.DeclaredVersion : inheritedVersion;
        bool forwards = IsForwardsCompatible(version);

        if (element.IsXsltElement)
        {
            string name = element.Name.LocalName;
            if (name == "import-schema")
            {
                bag.Error(XsltErrors.XTSE1650, "xsl:import-schema requires a schema-aware processor", element.Location);
            }
            else if (s_typedInstructions.Contains(name))
            {
                foreach (StylesheetAttribute attribute in element.Attributes)
                {
                    if (attribute.Name.NamespaceUri.Length == 0)
                    {
                        CheckAttribute(attribute.Name.LocalName, attribute, forwards, bag);
                    }
                }
            }
        }
        else
        {
            // A literal result element: xsl:type, xsl:validation (and xsl:default-validation).
            foreach (StylesheetAttribute attribute in element.Attributes)
            {
                if (string.Equals(attribute.Name.NamespaceUri, XsltNamespace.Uri, StringComparison.Ordinal))
                {
                    CheckAttribute(attribute.Name.LocalName, attribute, forwards, bag);
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

    private static void CheckAttribute(string localName, StylesheetAttribute attribute, bool forwards, DiagnosticBag bag)
    {
        bool schemaAware;
        if (localName == "type")
        {
            schemaAware = true;
        }
        else if (localName == "validation" || localName == "default-validation")
        {
            string value = attribute.Value.Trim();
            schemaAware = value == "strict" || value == "lax" || (value == "preserve" && !forwards);
        }
        else
        {
            return;
        }

        if (schemaAware)
        {
            bag.Error(
                XsltErrors.XTSE1660,
                $"The attribute '{localName}=\"{attribute.Value}\"' requires a schema-aware processor",
                attribute.Location);
        }
    }

    private static bool IsForwardsCompatible(string? version) =>
        !string.IsNullOrEmpty(version)
        && double.TryParse(version!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
        && v > 2.0;
}
