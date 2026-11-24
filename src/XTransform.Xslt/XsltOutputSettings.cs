// The effective serialization parameters of one (named or unnamed) xsl:output definition,
// after merging every declaration of that name (XSLT 2.0 §20). Internal until P3-15 exposes
// it through XsltExecutable.OutputSettings.

using System;
using System.Collections.Generic;
using System.Text;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Merged <c>xsl:output</c> parameters.</summary>
internal sealed class XsltOutputSettings
{
    /// <summary>Serialization method: <c>xml</c> (default), <c>html</c>, <c>xhtml</c>, <c>text</c> or a prefixed QName.</summary>
    public string Method { get; init; } = "xml";

    /// <summary>Encoding name. Default <c>utf-8</c>.</summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary><c>indent</c>.</summary>
    public bool Indent { get; init; }

    /// <summary><c>omit-xml-declaration</c>.</summary>
    public bool OmitXmlDeclaration { get; init; }

    /// <summary><c>standalone</c>; <see langword="null"/> for <c>omit</c> / absent.</summary>
    public bool? Standalone { get; init; }

    /// <summary><c>doctype-public</c>.</summary>
    public string? DoctypePublic { get; init; }

    /// <summary><c>doctype-system</c>.</summary>
    public string? DoctypeSystem { get; init; }

    /// <summary><c>cdata-section-elements</c>, accumulated across all declarations.</summary>
    public IReadOnlyList<QName> CdataSectionElements { get; init; } = Array.Empty<QName>();

    /// <summary><c>byte-order-mark</c>; <see langword="null"/> means the encoding default.</summary>
    public bool? ByteOrderMark { get; init; }

    /// <summary>The settings used when a stylesheet has no <c>xsl:output</c>.</summary>
    public static XsltOutputSettings Default { get; } = new XsltOutputSettings();

    /// <summary>Resolves <see cref="Encoding"/>; raises <c>SESU0007</c> for an unknown name.</summary>
    public System.Text.Encoding ResolveEncoding()
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
    public XmlEmitterOptions ToXmlOptions() => new XmlEmitterOptions
    {
        Encoding = ResolveEncoding(),
        Indent = Indent,
        OmitXmlDeclaration = OmitXmlDeclaration,
        Standalone = Standalone,
        DoctypePublic = DoctypePublic,
        DoctypeSystem = DoctypeSystem,
        CdataSectionElements = CdataSectionElements,
        ByteOrderMark = ByteOrderMark,
    };

    /// <summary>Maps to <see cref="TextEmitterOptions"/> for <c>method="text"</c>.</summary>
    public TextEmitterOptions ToTextOptions() => new TextEmitterOptions
    {
        Encoding = ResolveEncoding(),
        ByteOrderMark = ByteOrderMark,
    };
}
