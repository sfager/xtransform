// XSLT 2.0 stylesheet reader. Loads an XML stylesheet into a StylesheetModule
// AST that preserves source locations and in-scope namespaces per element.
// XSLT 2.0 §3.6 (stylesheet element), §3.7 (forwards-compatible processing).

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Reads an XSLT stylesheet module into a <see cref="StylesheetModule"/> AST.
/// Does not resolve <c>xsl:include</c> / <c>xsl:import</c> (P3-03), parse AVTs (P3-02),
/// evaluate <c>use-when</c> (P3-02), or strip whitespace (P3-02).
/// </summary>
internal static class StylesheetReader
{
    /// <summary>
    /// Reads a stylesheet from an <see cref="XmlReader"/>. <paramref name="baseUri"/> overrides
    /// the reader's reported base URI when supplied.
    /// </summary>
    public static StylesheetModule Read(XmlReader reader, Uri? baseUri = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }
#pragma warning restore CA1510

        string? moduleUri = baseUri?.OriginalString ?? reader.BaseURI;
        if (string.IsNullOrEmpty(moduleUri))
        {
            moduleUri = null;
        }

        DiagnosticBag diagnostics = new DiagnosticBag();
        StylesheetElement? root = null;

        // Advance to the root element.
        while (reader.NodeType != XmlNodeType.Element)
        {
            if (!reader.Read())
            {
                throw new XsltStaticException(new Diagnostic[]
                {
                    new Diagnostic(
                        DiagnosticSeverity.Error,
                        XsltErrors.XTSE0010,
                        "Stylesheet module is empty",
                        new SourceSpan(moduleUri, 1, 1)),
                });
            }
        }

        root = ReadElement(reader, NamespaceBindings.Root, effectiveVersion: null, xpathDefaultNamespace: string.Empty, System.Array.Empty<string>(), System.Array.Empty<string>(), moduleUri, diagnostics);

        // The reader may have more siblings at the top level if the document is malformed;
        // XmlReader will have thrown already if it's not well-formed XML.
        if (diagnostics.HasErrors)
        {
            throw new XsltStaticException(diagnostics.ToList());
        }

        return new StylesheetModule(moduleUri, root, diagnostics.ToList());
    }

    /// <summary>Convenience overload reading from a <see cref="TextReader"/>.</summary>
    public static StylesheetModule Read(TextReader source, Uri? baseUri = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#pragma warning restore CA1510

        XmlReaderSettings settings = CreateSettings();
        using XmlReader reader = XmlReader.Create(source, settings, baseUri?.OriginalString);
        return Read(reader, baseUri);
    }

    /// <summary>Convenience overload reading from a <see cref="Stream"/>.</summary>
    public static StylesheetModule Read(Stream source, Uri? baseUri = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#pragma warning restore CA1510

        XmlReaderSettings settings = CreateSettings();
        using XmlReader reader = XmlReader.Create(source, settings, baseUri?.OriginalString);
        return Read(reader, baseUri);
    }

    /// <summary>XmlReaderSettings consistent across overloads: DTD off, line info on, close input.</summary>
    private static XmlReaderSettings CreateSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CloseInput = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
        };
    }

    // ── Element reading ──────────────────────────────────────────────────────

    private static StylesheetElement ReadElement(
        XmlReader reader,
        NamespaceBindings parentScope,
        string? effectiveVersion,
        string xpathDefaultNamespace,
        IReadOnlyCollection<string> inheritedExcluded,
        IReadOnlyCollection<string> inheritedExtension,
        string? moduleUri,
        DiagnosticBag diagnostics)
    {
        IXmlLineInfo lineInfo = reader as IXmlLineInfo ?? NullLineInfo.Instance;
        SourceSpan location = new SourceSpan(moduleUri, lineInfo.LineNumber, lineInfo.LinePosition);

        // Gather namespace declarations first so attributes can be resolved in the new scope.
        Dictionary<string, string> nsDecls = new Dictionary<string, string>(System.StringComparer.Ordinal);
        List<RawAttribute> rawAttrs = new List<RawAttribute>();

        bool hasAttrs = reader.MoveToFirstAttribute();
        while (hasAttrs)
        {
            string prefix = reader.Prefix;
            string localName = reader.LocalName;
            string ns = reader.NamespaceURI;
            string value = reader.Value;
            SourceSpan attrLoc = new SourceSpan(moduleUri, lineInfo.LineNumber, lineInfo.LinePosition);

            if (string.Equals(ns, XsltNamespace.XmlnsUri, System.StringComparison.Ordinal))
            {
                // Namespace declaration. "xmlns" (no prefix on the attribute) binds the default ns.
                string boundPrefix = string.Equals(prefix, "xmlns", System.StringComparison.Ordinal)
                    ? localName
                    : string.Empty;
                nsDecls[boundPrefix] = value;
            }
            else
            {
                rawAttrs.Add(new RawAttribute(prefix, localName, ns, value, attrLoc));
            }

            hasAttrs = reader.MoveToNextAttribute();
        }

        reader.MoveToElement();

        NamespaceBindings inScope = parentScope.Extend(nsDecls);

        // Resolve the element name. XmlReader reports a resolved NamespaceURI which we trust;
        // the local name stays as is. We use NamespaceUri+LocalName for QName (prefix retained for diagnostics).
        QName name = new QName(reader.Prefix, reader.NamespaceURI, reader.LocalName);

        StylesheetElement element = new StylesheetElement(name, inScope, location);

        // Attach non-namespace attributes.
        foreach (RawAttribute raw in rawAttrs)
        {
            QName attrName = new QName(raw.Prefix, raw.NamespaceUri, raw.LocalName);
            element.AppendAttribute(new StylesheetAttribute(attrName, raw.Value, raw.Location));
        }

        // xpath-default-namespace (on an XSLT element, or xsl:xpath-default-namespace on a literal result element)
        // applies to the element and its descendants until overridden (XSLT 2.0 §5.2).
        string? xpathDefaultHere = name.NamespaceUri == XsltNamespace.Uri
            ? FindAttribute(element, string.Empty, "xpath-default-namespace")
            : FindAttribute(element, XsltNamespace.Uri, "xpath-default-namespace");
        element.XPathDefaultNamespace = xpathDefaultHere is null ? xpathDefaultNamespace : xpathDefaultHere.Trim();

        ReadNamespaceDesignations(element, inheritedExcluded, inheritedExtension, diagnostics);

        // Effective version: prefer this element's declared version (xsl:version on a literal
        // result element, or version on xsl:stylesheet/xsl:transform), else inherit from ancestor.
        // XSLT 2.0 §3.7: forwards-compatible processing applies when the effective version > 2.0,
        // backwards-compatible when < 2.0.
        string? declaredHere =
            FindAttribute(element, string.Empty, "version") ??
            FindAttribute(element, XsltNamespace.Uri, "version");
        string? currentEffective = declaredHere ?? effectiveVersion;
        if (!string.IsNullOrEmpty(declaredHere))
        {
            element.DeclaredVersion = declaredHere!;
        }

        // Handle unknown xsl:* elements (XTSE0010 outside forwards-compatible mode;
        // XTSE0150 warning inside). XSLT 2.0 §3.7.
        if (element.IsUnknownXsltElement)
        {
            // Only an effective version above 2.0 is forwards-compatible; a lower one is backwards-compatible and still rejects an
            // unknown element (XSLT 2.0 §3.9).
            bool forwardsCompatible = !string.IsNullOrEmpty(currentEffective) &&
                                      double.TryParse(currentEffective!.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double numericVersion) &&
                                      numericVersion > 2.0;
            if (forwardsCompatible)
            {
                diagnostics.Warning(
                    XsltErrors.XTSE0150,
                    $"Unknown XSLT element xsl:{element.Name.LocalName} ignored under forwards-compatible processing",
                    location);
            }
            else
            {
                diagnostics.Error(
                    XsltErrors.XTSE0010,
                    $"Unknown XSLT element xsl:{element.Name.LocalName}",
                    location);
            }
        }

        if (reader.IsEmptyElement)
        {
            // No children; advance past the empty element.
            reader.Read();
            return element;
        }

        reader.Read(); // Move to first child or end tag.

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    StylesheetElement child = ReadElement(reader, inScope, currentEffective, element.XPathDefaultNamespace, element.ExcludedNamespaceUris, element.ExtensionNamespaceUris, moduleUri, diagnostics);

                    // XTSE0130: when a child of xsl:stylesheet/xsl:transform is in no namespace.
                    if (IsTopLevelContainer(element) &&
                        child.Name.NamespaceUri.Length == 0)
                    {
                        diagnostics.Error(
                            XsltErrors.XTSE0130,
                            $"Child element '{child.Name.LocalName}' of xsl:{element.Name.LocalName} must have a non-null namespace",
                            child.Location);
                    }

                    element.AppendChild(child);
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    SourceSpan textLoc = new SourceSpan(moduleUri, lineInfo.LineNumber, lineInfo.LinePosition);
                    element.AppendChild(new StylesheetText(reader.Value, textLoc));
                    reader.Read();
                    break;

                case XmlNodeType.Comment:
                case XmlNodeType.ProcessingInstruction:
                    // Discarded per settings (IgnoreComments / IgnoreProcessingInstructions).
                    reader.Read();
                    break;

                case XmlNodeType.EntityReference:
                    reader.ResolveEntity();
                    reader.Read();
                    break;

                default:
                    reader.Read();
                    break;
            }
        }

        reader.Read(); // Past the end tag.
        return element;
    }

    /// <summary>True when <paramref name="element"/> is xsl:stylesheet or xsl:transform.</summary>
    private static bool IsTopLevelContainer(StylesheetElement element)
    {
        if (!element.IsXsltElement)
        {
            return false;
        }

        string local = element.Name.LocalName;
        return string.Equals(local, "stylesheet", System.StringComparison.Ordinal) ||
               string.Equals(local, "transform", System.StringComparison.Ordinal);
    }

    // XSLT 2.0 §11.1.3, §18.2: exclude-result-prefixes and extension-element-prefixes name prefixes whose namespaces are left out of
    // the result; the sets are inherited by every descendant. On an XSLT element the attribute has no namespace, on any other element it is in the XSLT namespace.
    private static void ReadNamespaceDesignations(
        StylesheetElement element,
        IReadOnlyCollection<string> inheritedExcluded,
        IReadOnlyCollection<string> inheritedExtension,
        DiagnosticBag diagnostics)
    {
        string attributeNamespace = element.IsXsltElement ? string.Empty : XsltNamespace.Uri;
        string? excludeValue = FindAttribute(element, attributeNamespace, "exclude-result-prefixes");
        string? extensionValue = FindAttribute(element, attributeNamespace, "extension-element-prefixes");
        if (excludeValue is null && extensionValue is null)
        {
            element.ExcludedNamespaceUris = inheritedExcluded;
            element.ExtensionNamespaceUris = inheritedExtension;
            return;
        }

        HashSet<string> excluded = new HashSet<string>(inheritedExcluded, System.StringComparer.Ordinal);
        HashSet<string> extension = new HashSet<string>(inheritedExtension, System.StringComparer.Ordinal);
        char[] separators = { ' ', '\t', '\r', '\n' };

        if (excludeValue is not null)
        {
            foreach (string token in excludeValue.Split(separators, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "#all")
                {
                    foreach (string uri in element.InScopeNamespaces.AsDictionary().Values)
                    {
                        excluded.Add(uri);
                    }
                }
                else if (TryResolveDesignation(element, token, XsltErrors.XTSE0808, diagnostics, out string uri))
                {
                    excluded.Add(uri);
                }
            }
        }

        if (extensionValue is not null)
        {
            foreach (string token in extensionValue.Split(separators, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryResolveDesignation(element, token, XsltErrors.XTSE1430, diagnostics, out string uri))
                {
                    extension.Add(uri);
                    excluded.Add(uri);
                }
            }
        }

        element.ExcludedNamespaceUris = excluded;
        element.ExtensionNamespaceUris = extension;
    }

    private static bool TryResolveDesignation(StylesheetElement element, string token, QName unboundCode, DiagnosticBag diagnostics, out string uri)
    {
        uri = string.Empty;
        string prefix = token == "#default" ? string.Empty : token;
        string? bound = element.InScopeNamespaces.Resolve(prefix);
        if (bound is null || (token == "#default" && bound.Length == 0))
        {
            diagnostics.Error(
                token == "#default" ? XsltErrors.XTSE0809 : unboundCode,
                token == "#default" ? "#default is used but there is no default namespace in scope" : $"The prefix '{token}' is not bound to a namespace in scope",
                element.Location);
            return false;
        }

        uri = bound;
        return true;
    }

    private static string? FindAttribute(StylesheetElement element, string namespaceUri, string localName)
    {
        foreach (StylesheetAttribute attr in element.Attributes)
        {
            if (string.Equals(attr.Name.LocalName, localName, System.StringComparison.Ordinal) &&
                string.Equals(attr.Name.NamespaceUri, namespaceUri, System.StringComparison.Ordinal))
            {
                return attr.Value;
            }
        }

        return null;
    }

    private readonly struct RawAttribute
    {
        public RawAttribute(string prefix, string localName, string namespaceUri, string value, SourceSpan location)
        {
            Prefix = prefix;
            LocalName = localName;
            NamespaceUri = namespaceUri;
            Value = value;
            Location = location;
        }

        public string Prefix { get; }
        public string LocalName { get; }
        public string NamespaceUri { get; }
        public string Value { get; }
        public SourceSpan Location { get; }
    }

    private sealed class NullLineInfo : IXmlLineInfo
    {
        public static readonly NullLineInfo Instance = new NullLineInfo();
        public bool HasLineInfo() => false;
        public int LineNumber => 0;
        public int LinePosition => 0;
    }
}
