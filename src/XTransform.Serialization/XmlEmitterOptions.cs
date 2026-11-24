using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Serialization parameters for <see cref="XmlEmitter"/> (XSLT and XQuery Serialization 1.0,
/// method="xml"). The public <c>XsltOutputSettings</c> built in P3-14/P3-15 maps onto this.
/// </summary>
internal sealed class XmlEmitterOptions
{
    /// <summary>Output encoding; also names the encoding in the XML declaration. Default UTF-8.</summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    /// <summary><c>indent="yes"</c>: add whitespace between child nodes of elements that contain no text.</summary>
    public bool Indent { get; init; }

    /// <summary><c>omit-xml-declaration="yes"</c>.</summary>
    public bool OmitXmlDeclaration { get; init; }

    /// <summary><c>standalone</c>: <see langword="null"/> means "omit".</summary>
    public bool? Standalone { get; init; }

    /// <summary><c>doctype-public</c>; only used together with <see cref="DoctypeSystem"/>.</summary>
    public string? DoctypePublic { get; init; }

    /// <summary><c>doctype-system</c>; when set, a DOCTYPE is written before the first root element.</summary>
    public string? DoctypeSystem { get; init; }

    /// <summary><c>cdata-section-elements</c>: text children of these elements are written as CDATA.</summary>
    public IReadOnlyCollection<QName>? CdataSectionElements { get; init; }

    /// <summary>
    /// <c>byte-order-mark</c>. <see langword="null"/> means "default": a BOM for UTF-16 / UTF-32,
    /// none for UTF-8. Only honoured when writing to a stream (<see cref="XmlEmitter.Create"/>).
    /// </summary>
    public bool? ByteOrderMark { get; init; }
}
