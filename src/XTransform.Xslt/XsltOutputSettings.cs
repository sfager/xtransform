// The effective serialization parameters of one (named or unnamed) xsl:output definition,
// after merging every declaration of that name (XSLT 2.0 §20). Exposed publicly through
// XsltExecutable.OutputSettings.

using System;
using System.Collections.Generic;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform;

/// <summary>
/// Serialization parameters (design.md, Stage 4): one property per <c>xsl:output</c>
/// attribute the engine honours, after merging every declaration of the same name.
/// </summary>
public sealed class XsltOutputSettings
{
    /// <summary>Serialization method: <c>xml</c> (default), <c>html</c>, <c>xhtml</c>, <c>text</c> or a prefixed QName.</summary>
    public string Method { get; init; } = "xml";

    /// <summary>Encoding name. Default <c>utf-8</c>.</summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary><c>version</c>: <c>1.1</c> escapes the control characters XML 1.0 forbids as character references; any other value is <c>1.0</c>.</summary>
    public string Version { get; init; } = "1.0";

    /// <summary><c>indent</c>.</summary>
    public bool Indent { get; init; }

    /// <summary><c>omit-xml-declaration</c>.</summary>
    public bool OmitXmlDeclaration { get; init; }

    /// <summary><c>standalone</c>; <see langword="null"/> for <c>omit</c> or absent.</summary>
    public bool? Standalone { get; init; }

    /// <summary><c>doctype-public</c>.</summary>
    public string? DoctypePublic { get; init; }

    /// <summary><c>doctype-system</c>.</summary>
    public string? DoctypeSystem { get; init; }

    /// <summary><c>cdata-section-elements</c>, accumulated across all declarations.</summary>
    public IReadOnlyList<QName> CdataSectionElements { get; init; } = Array.Empty<QName>();

    /// <summary><c>byte-order-mark</c>; <see langword="null"/> means the encoding's default.</summary>
    public bool? ByteOrderMark { get; init; }

    /// <summary>The settings used when a stylesheet has no <c>xsl:output</c>.</summary>
    internal static XsltOutputSettings Default { get; } = new XsltOutputSettings();

    /// <summary>Resolves <see cref="Encoding"/>; raises <c>SESU0007</c> for an unknown name.</summary>
    internal System.Text.Encoding ResolveEncoding()
    {
        try
        {
            return System.Text.Encoding.GetEncoding(Encoding);
        }
        catch (ArgumentException)
        {
            throw new XsltDynamicException(
                XsltErrors.SESU0007,
                $"Output encoding '{Encoding}' is not supported",
                SourceSpan.None);
        }
    }

    /// <summary>Maps to <see cref="XmlEmitterOptions"/> for <c>method="xml"</c>.</summary>
    internal XmlEmitterOptions ToXmlOptions() => new XmlEmitterOptions
    {
        Encoding = ResolveEncoding(),
        Indent = Indent,
        Version = Version,
        OmitXmlDeclaration = OmitXmlDeclaration,
        Standalone = Standalone,
        DoctypePublic = DoctypePublic,
        DoctypeSystem = DoctypeSystem,
        CdataSectionElements = CdataSectionElements,
        ByteOrderMark = ByteOrderMark,
    };

    /// <summary>Maps to <see cref="TextEmitterOptions"/> for <c>method="text"</c>.</summary>
    internal TextEmitterOptions ToTextOptions() => new TextEmitterOptions
    {
        Encoding = ResolveEncoding(),
        ByteOrderMark = ByteOrderMark,
    };
}
