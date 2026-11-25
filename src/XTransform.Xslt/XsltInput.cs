using System;
using System.IO;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Loads input documents for a transformation into the XDM tree model.</summary>
internal static class XsltInput
{
    /// <summary>Reader settings for source documents: DTDs are ignored, nothing is resolved externally.</summary>
    public static XmlReaderSettings CreateReaderSettings() => new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
    };

    /// <summary>Builds a document node from <paramref name="reader"/> using <paramref name="pool"/>.</summary>
    public static XdmNode Load(XmlReader reader, NamePool pool, WhitespaceTable? whitespace = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }
#pragma warning restore CA1510

        // xsl:strip-space applies to source documents, not to temporary trees (XSLT 2.0 §4.3).
        Func<string, string, bool>? strip = whitespace is not null && whitespace.HasStripRules ? whitespace.ShouldStrip : null;
        TinyTree tree = TinyTreeBuilder.Load(reader, pool, strip);
        return new XdmNode(NodeHandle.ForNode(tree, 0));
    }

    /// <summary>Parses <paramref name="stream"/> (left open) into a document node.</summary>
    public static XdmNode Load(Stream stream, NamePool pool, WhitespaceTable? whitespace = null)
    {
        using XmlReader reader = XmlReader.Create(stream, CreateReaderSettings());
        return Load(reader, pool, whitespace);
    }
}
