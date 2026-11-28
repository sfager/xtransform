using System;
using System.IO;
using System.Text;
using System.Xml;

namespace XTransform.Xslt;

/// <summary>
/// Reads documents that declare XML 1.1, which <see cref="XmlReader"/> refuses. The declaration is read as 1.0 and the character
/// checks are switched off, so the characters 1.1 adds (character references to control characters, the wider name characters
/// the reader accepts) get through. Only seekable byte streams are examined, so a stream that cannot seek is read as it comes.
/// </summary>
internal static class Xml11Source
{
    private const int PeekLength = 128;

    /// <summary>Creates a reader over <paramref name="stream"/> with <paramref name="settings"/> (changed when the document declares XML 1.1).</summary>
    public static XmlReader CreateReader(Stream stream, XmlReaderSettings settings, string? baseUri)
    {
        if (TryReadAsXml10(stream, out Stream? patched))
        {
            settings.CheckCharacters = false;
            return XmlReader.Create(patched!, settings, baseUri);
        }

        return XmlReader.Create(stream, settings, baseUri);
    }

    // An XML declaration with version="1.1" at the start of the stream, as ASCII or UTF-8 (with or without a byte order mark).
    private static bool TryReadAsXml10(Stream stream, out Stream? patched)
    {
        patched = null;
        if (!stream.CanSeek || !stream.CanRead)
        {
            return false;
        }

        long start = stream.Position;
        byte[] head = new byte[PeekLength];
        int read = stream.Read(head, 0, head.Length);
        stream.Position = start;

        string text = Encoding.GetEncoding(28591).GetString(head, 0, read);
        int declaration = text.StartsWith("\u00EF\u00BB\u00BF", StringComparison.Ordinal) ? 3 : 0;
        if (string.CompareOrdinal(text, declaration, "<?xml", 0, 5) != 0)
        {
            return false;
        }

        int version = text.IndexOf("version", declaration, StringComparison.Ordinal);
        int end = text.IndexOf("?>", declaration, StringComparison.Ordinal);
        if (version < 0 || (end >= 0 && version > end))
        {
            return false;
        }

        int quote = version + "version".Length;
        while (quote < read && (text[quote] == ' ' || text[quote] == '=' || text[quote] == '\t'))
        {
            quote++;
        }

        if (quote + 4 >= read || (text[quote] != '"' && text[quote] != '\'') || string.CompareOrdinal(text, quote + 1, "1.1", 0, 3) != 0)
        {
            return false;
        }

        MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        byte[] bytes = copy.GetBuffer();
        bytes[quote + 3] = (byte)'0';
        copy.Position = 0;
        patched = copy;
        return true;
    }
}
