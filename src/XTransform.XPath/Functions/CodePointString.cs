// F&O §7 — Unicode code-point aware string utilities.

using System.Text;
using XTransform.Xdm;

namespace XTransform.XPath.Functions;

/// <summary>
/// Unicode code-point aware string utilities used by fn: string functions.
/// XPath 2.0 F&amp;O §7 specifies indexing by Unicode code points, not UTF-16 units.
/// A non-BMP character is one code point but two UTF-16 units (a surrogate pair).
/// </summary>
internal static class CodePointString
{
    /// <summary>Returns the number of Unicode code points in <paramref name="s"/>.</summary>
    /// <param name="s">The string to measure. Must not be <see langword="null"/>.</param>
    /// <returns>The number of Unicode code points.</returns>
    internal static int CodePointCount(string s)
    {
        int count = 0;
        int i = 0;
        int len = s.Length;
        while (i < len)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < len && char.IsLowSurrogate(s[i + 1]))
            {
                i += 2;
            }
            else
            {
                i++;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Returns the 1-based substring of <paramref name="s"/> starting at code-point position
    /// <paramref name="startCp"/> (XPath's rounded start) running for <paramref name="lengthCp"/>
    /// code points. Out-of-range start is clipped; negative or zero length returns "".
    /// </summary>
    /// <param name="s">The source string.</param>
    /// <param name="startCp">1-based start code-point position (after rounding per F&amp;O §7.4.3).</param>
    /// <param name="lengthCp">Number of code points to include; &lt;= 0 means zero characters.</param>
    /// <returns>The extracted substring.</returns>
    internal static string Substring(string s, int startCp, int lengthCp)
    {
        if (s.Length == 0)
        {
            return string.Empty;
        }

        if (lengthCp <= 0)
        {
            return string.Empty;
        }

        // Clip to 1
        int effectiveStart = startCp < 1 ? 1 : startCp;
        // Find the UTF-16 offset of effectiveStart
        int utf16Start = Utf16OffsetFor(s, effectiveStart);
        if (utf16Start < 0)
        {
            // start is past end
            return string.Empty;
        }

        // Now count lengthCp code points from utf16Start
        int i = utf16Start;
        int len = s.Length;
        int cpTaken = 0;
        while (i < len && cpTaken < lengthCp)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < len && char.IsLowSurrogate(s[i + 1]))
            {
                i += 2;
            }
            else
            {
                i++;
            }

            cpTaken++;
        }

        return s.Substring(utf16Start, i - utf16Start);
    }

    /// <summary>Returns the 0-based UTF-16 offset for the given 1-based code-point position; -1 if past end.</summary>
    /// <param name="s">The source string.</param>
    /// <param name="oneBasedCpPos">1-based code-point position.</param>
    /// <returns>The 0-based UTF-16 offset, or -1 if position is past the end of the string.</returns>
    internal static int Utf16OffsetFor(string s, int oneBasedCpPos)
    {
        if (oneBasedCpPos < 1)
        {
            return 0;
        }

        int cpIndex = 0;
        int i = 0;
        int len = s.Length;

        while (i < len)
        {
            cpIndex++;
            if (cpIndex == oneBasedCpPos)
            {
                return i;
            }

            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < len && char.IsLowSurrogate(s[i + 1]))
            {
                i += 2;
            }
            else
            {
                i++;
            }
        }

        // oneBasedCpPos is past the end
        return -1;
    }

    /// <summary>
    /// Returns an array of all code points in <paramref name="s"/>.
    /// Non-BMP characters yield their full code point value (not two surrogate values).
    /// </summary>
    /// <param name="s">The source string. Must not be <see langword="null"/>.</param>
    /// <returns>An array of Unicode code-point integers.</returns>
    internal static int[] ToCodePointArray(string s)
    {
        if (s.Length == 0)
        {
            return System.Array.Empty<int>();
        }

        // Pre-size with an upper bound (s.Length code points at most)
        int[] buf = new int[s.Length];
        int idx = 0;
        int i = 0;
        int len = s.Length;
        while (i < len)
        {
            char c = s[i];
            int cp;
            if (char.IsHighSurrogate(c) && i + 1 < len && char.IsLowSurrogate(s[i + 1]))
            {
                cp = char.ConvertToUtf32(c, s[i + 1]);
                i += 2;
            }
            else
            {
                cp = c;
                i++;
            }

            buf[idx++] = cp;
        }

        if (idx == buf.Length)
        {
            return buf;
        }

        int[] result = new int[idx];
        System.Array.Copy(buf, result, idx);
        return result;
    }

    /// <summary>Validates that <paramref name="cp"/> is a legal Unicode scalar value.</summary>
    /// <param name="cp">The code point to validate.</param>
    /// <exception cref="XPathDynamicException">Thrown with FOCH0001 if invalid.</exception>
    internal static void ValidateCodePoint(int cp)
    {
        // F&O §7.2.1 FOCH0001: values < 1, > 0x10FFFF, or in surrogate range are invalid
        if (cp < 1 || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
        {
            throw new XPathDynamicException(
                XPathErrors.FOCH0001,
                $"Invalid Unicode code point: {cp}.",
                default);
        }
    }
}
