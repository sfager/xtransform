using System;
using System.IO;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Loads input documents for a transformation into the XDM tree model.</summary>
internal static class XsltInput
{
    /// <summary>The most characters one document may gain from expanding entities, which stops an entity expansion attack.</summary>
    internal const long MaxCharactersFromEntities = 10_000_000;

    /// <summary>
    /// Reader settings for source documents: the DTD is parsed, so internal entities are declared, but nothing is resolved externally
    /// (no external DTD subset, no external entity) and entity expansion is capped.
    /// </summary>
    public static XmlReaderSettings CreateReaderSettings() => new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Parse,
        XmlResolver = null,
        MaxCharactersFromEntities = MaxCharactersFromEntities,
    };

    /// <summary>Creates a reader over a source document, which may declare XML 1.1.</summary>
    public static XmlReader CreateReader(Stream stream, string? baseUri) => Xml11Source.CreateReader(stream, CreateReaderSettings(), baseUri);

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
        using XmlReader reader = Xml11Source.CreateReader(stream, CreateReaderSettings(), null);
        return Load(reader, pool, whitespace);
    }
}
