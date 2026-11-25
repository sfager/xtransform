using System.Text;

namespace XTransform.Serialization;

/// <summary>
/// Applies the serialization <c>byte-order-mark</c> rule (Serialization 1.0 §3): by default a
/// BOM is written for UTF-16 and UTF-32 and not for UTF-8; an explicit setting overrides.
/// </summary>
internal static class EncodingResolver
{
    /// <summary>Returns an encoding instance whose preamble matches the BOM decision.</summary>
    public static Encoding Resolve(Encoding encoding, bool? byteOrderMark)
    {
        bool wide = encoding.CodePage is 1200 or 1201 or 12000 or 12001;
        bool bom = byteOrderMark ?? wide;
        return encoding.CodePage switch
        {
            65001 => new UTF8Encoding(bom),
            1200 => new UnicodeEncoding(false, bom),
            1201 => new UnicodeEncoding(true, bom),
            12000 => new UTF32Encoding(false, bom),
            12001 => new UTF32Encoding(true, bom),
            _ => encoding,
        };
    }
}
