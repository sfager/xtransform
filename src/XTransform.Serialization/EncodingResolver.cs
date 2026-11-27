using System;
using System.Text;

namespace XTransform.Serialization;

/// <summary>
/// Applies the serialization <c>byte-order-mark</c> rule (Serialization 1.0 §3): by default a
/// BOM is written for UTF-16 and UTF-32 and not for UTF-8; an explicit setting overrides.
/// </summary>
internal static class EncodingResolver
{
    /// <summary>Returns an encoding instance whose preamble matches the BOM decision.</summary>
    /// <param name="encoding">The requested encoding.</param>
    /// <param name="byteOrderMark">An explicit byte-order-mark setting, or null for the default.</param>
    /// <param name="characterReferences">
    /// True for the markup methods: a character the encoding cannot represent is written as a numeric
    /// character reference instead of being replaced (Serialization §6).
    /// </param>
    public static Encoding Resolve(Encoding encoding, bool? byteOrderMark, bool characterReferences = false)
    {
        bool wide = encoding.CodePage is 1200 or 1201 or 12000 or 12001;
        bool bom = byteOrderMark ?? wide;
        switch (encoding.CodePage)
        {
            case 65001:
                return new UTF8Encoding(bom);
            case 1200:
                return new UnicodeEncoding(false, bom);
            case 1201:
                return new UnicodeEncoding(true, bom);
            case 12000:
                return new UTF32Encoding(false, bom);
            case 12001:
                return new UTF32Encoding(true, bom);
        }

        if (!characterReferences)
        {
            return encoding;
        }

        try
        {
            return Encoding.GetEncoding(encoding.CodePage, new CharacterReferenceFallback(), DecoderFallback.ReplacementFallback);
        }
        catch (ArgumentException)
        {
            // The code page is not available with a custom fallback; keep the encoding as it is.
            return encoding;
        }
    }
}
