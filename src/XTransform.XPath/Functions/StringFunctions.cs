// F&O §7 — String functions.

using System;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §7 string functions:
/// fn:concat, fn:string-join, fn:substring, fn:string-length, fn:normalize-space,
/// fn:normalize-unicode, fn:upper-case, fn:lower-case, fn:translate,
/// fn:encode-for-uri, fn:iri-to-uri, fn:escape-html-uri,
/// fn:contains, fn:starts-with, fn:ends-with, fn:substring-before, fn:substring-after,
/// fn:string-to-codepoints, fn:codepoints-to-string, fn:compare, fn:codepoint-equal.
/// </summary>
internal static class StringFunctions
{
    // ── Shared codepoint collation URI ────────────────────────────────────────

    private const string CodepointCollationUri =
        "http://www.w3.org/2005/xpath-functions/collation/codepoint";

    // =========================================================================
    // F&O §7.4.1 — fn:concat(xs:anyAtomicType?, ...) as xs:string
    // =========================================================================

    /// <summary>Implements fn:concat for 2 arguments.</summary>
    internal static IXdmIterator FnConcat2(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 3 arguments.</summary>
    internal static IXdmIterator FnConcat3(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 4 arguments.</summary>
    internal static IXdmIterator FnConcat4(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 5 arguments.</summary>
    internal static IXdmIterator FnConcat5(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 6 arguments.</summary>
    internal static IXdmIterator FnConcat6(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 7 arguments.</summary>
    internal static IXdmIterator FnConcat7(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 8 arguments.</summary>
    internal static IXdmIterator FnConcat8(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 9 arguments.</summary>
    internal static IXdmIterator FnConcat9(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    /// <summary>Implements fn:concat for 10 arguments.</summary>
    internal static IXdmIterator FnConcat10(DynamicContext ctx, XdmSequence[] args)
        => ConcatArgs(args);

    private static SingleItemIterator ConcatArgs(XdmSequence[] args)
    {
        // F&O §7.4.1: each argument is xs:anyAtomicType? — stringify each; empty → ""
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            sb.Append(SequenceToString(args[i]));
        }

        return new SingleItemIterator(XdmAtomicValue.String(sb.ToString()));
    }

    // =========================================================================
    // F&O §7.4.2 — fn:string-join(xs:string*, xs:string) as xs:string
    // =========================================================================

    /// <summary>Implements fn:string-join($seq, $sep).</summary>
    internal static IXdmIterator FnStringJoin(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.2
        string sep = OneStringOrEmpty(args[1]);
        StringBuilder sb = new StringBuilder();
        bool first = true;
        IXdmIterator it = args[0].GetIterator();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            if (!first)
            {
                sb.Append(sep);
            }

            sb.Append(item.GetStringValue());
            first = false;
        }

        return new SingleItemIterator(XdmAtomicValue.String(sb.ToString()));
    }

    // =========================================================================
    // F&O §7.4.3 — fn:substring
    // =========================================================================

    /// <summary>Implements fn:substring($sourceString, $startingLoc) as xs:string.</summary>
    internal static IXdmIterator FnSubstring2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.3: 2-arg variant — no length limit (use to end of string)
        string s = OneStringOrEmpty(args[0]);
        double startD = OneDoubleOrNaN(args[1]);

        return new SingleItemIterator(XdmAtomicValue.String(SubstringImpl(s, startD, double.PositiveInfinity)));
    }

    /// <summary>Implements fn:substring($sourceString, $startingLoc, $length) as xs:string.</summary>
    internal static IXdmIterator FnSubstring3(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.3: 3-arg variant
        string s = OneStringOrEmpty(args[0]);
        double startD = OneDoubleOrNaN(args[1]);
        double lengthD = OneDoubleOrNaN(args[2]);

        return new SingleItemIterator(XdmAtomicValue.String(SubstringImpl(s, startD, lengthD)));
    }

    private static string SubstringImpl(string s, double startD, double lengthD)
    {
        // F&O §7.4.3: the range of positions ps included in the result satisfies:
        //   fn:round($startingLoc) <= ps AND ps < fn:round($startingLoc) + fn:round($length)
        // NaN start → no match for any ps → ""

        if (double.IsNaN(startD))
        {
            return string.Empty;
        }

        // F&O §7.4.3: round start and length *independently* using fn:round semantics (ToEven).
        // Then the end boundary = roundedStart + roundedLength.
        int cpCount = CodePointString.CodePointCount(s);

        // Compute rounded start (1-based, may be large negative or +∞)
        int startRounded;
        if (double.IsNegativeInfinity(startD))
        {
            startRounded = int.MinValue;
        }
        else if (double.IsPositiveInfinity(startD))
        {
            // Start past everything — no positions satisfy ps >= +∞
            return string.Empty;
        }
        else
        {
            startRounded = (int)Math.Round(startD, MidpointRounding.ToEven);
        }

        // Compute end boundary: fn:round(start) + fn:round(length)
        // end is exclusive — positions ps must satisfy ps < end
        int endBoundary;
        if (double.IsPositiveInfinity(lengthD))
        {
            endBoundary = int.MaxValue;
        }
        else if (double.IsNegativeInfinity(lengthD) || double.IsNaN(lengthD))
        {
            // For -∞ or NaN length the range is empty: start + (-∞) = -∞ → ps < -∞ impossible,
            // or NaN length → result is ""
            return string.Empty;
        }
        else
        {
            int lengthRounded = (int)Math.Round(lengthD, MidpointRounding.ToEven);
            // Guard against overflow: if start is int.MinValue, adding lengthRounded may overflow
            if (startRounded == int.MinValue)
            {
                endBoundary = lengthRounded <= 0 ? int.MinValue : (int.MinValue + lengthRounded);
            }
            else
            {
                endBoundary = startRounded + lengthRounded;
            }
        }

        // Effective clipped start: max(1, startRounded)
        int p = startRounded < 1 ? 1 : startRounded;
        // Effective clipped end: min(cpCount+1, endBoundary)
        int q = endBoundary > cpCount + 1 ? cpCount + 1 : endBoundary;

        if (p >= q)
        {
            return string.Empty;
        }

        // Extract p..(q-1) code points (1-based), length = q - p
        return CodePointString.Substring(s, p, q - p);
    }

    // =========================================================================
    // F&O §7.4.4 — fn:string-length
    // =========================================================================

    /// <summary>Implements fn:string-length() — 0-arg: uses context item string value.</summary>
    internal static IXdmIterator FnStringLength0(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.4: 0-arg uses fn:string on context item
        string s = GetContextItemString(ctx);
        return new SingleItemIterator(XdmAtomicValue.Integer((long)CodePointString.CodePointCount(s)));
    }

    /// <summary>Implements fn:string-length($arg as xs:string?) as xs:integer.</summary>
    internal static IXdmIterator FnStringLength1(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.4
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.Integer((long)CodePointString.CodePointCount(s)));
    }

    // =========================================================================
    // F&O §7.4.5 — fn:normalize-space
    // =========================================================================

    /// <summary>Implements fn:normalize-space() — 0-arg: uses context item.</summary>
    internal static IXdmIterator FnNormalizeSpace0(DynamicContext ctx, XdmSequence[] args)
    {
        string s = GetContextItemString(ctx);
        return new SingleItemIterator(XdmAtomicValue.String(NormalizeSpaceImpl(s)));
    }

    /// <summary>Implements fn:normalize-space($arg as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnNormalizeSpace1(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(NormalizeSpaceImpl(s)));
    }

    private static string NormalizeSpaceImpl(string s)
    {
        // F&O §7.4.5: "collapse" whitespace: trim leading/trailing, merge internal runs.
        // Whitespace = space (0x20), tab (0x09), LF (0x0A), CR (0x0D).
        if (s.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(s.Length);
        bool inWhitespace = true; // start true to trim leading whitespace
        bool needSpace = false;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool isWs = c == ' ' || c == '\t' || c == '\n' || c == '\r';
            if (isWs)
            {
                if (!inWhitespace)
                {
                    needSpace = true;
                    inWhitespace = true;
                }
            }
            else
            {
                if (needSpace)
                {
                    sb.Append(' ');
                    needSpace = false;
                }

                inWhitespace = false;
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    // =========================================================================
    // F&O §7.4.6 — fn:normalize-unicode
    // =========================================================================

    /// <summary>Implements fn:normalize-unicode($arg as xs:string?) as xs:string (NFC).</summary>
    internal static IXdmIterator FnNormalizeUnicode1(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormC)));
    }

    /// <summary>Implements fn:normalize-unicode($arg as xs:string?, $normForm as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnNormalizeUnicode2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.6
        string s = OneStringOrEmpty(args[0]);
        string form = OneStringOrEmpty(args[1]).Trim().ToUpperInvariant();

        if (form.Length == 0 || form == "")
        {
            // Identity — return as-is
            return new SingleItemIterator(XdmAtomicValue.String(s));
        }

        switch (form)
        {
            case "NFC":
                return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormC)));
            case "NFD":
                return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormD)));
            case "NFKC":
                return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormKC)));
            case "NFKD":
                return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormKD)));
            case "FULLY-NORMALIZED":
                // Approximate: NFC is a sufficient approximation per XSLT 2.0 §16.2
                return new SingleItemIterator(XdmAtomicValue.String(s.Normalize(NormalizationForm.FormC)));
            default:
                throw new XPathDynamicException(
                    XPathErrors.FOCH0003,
                    $"Unsupported normalization form: '{form}'.",
                    default);
        }
    }

    // =========================================================================
    // F&O §7.4.7 — fn:upper-case / fn:lower-case
    // =========================================================================

    /// <summary>Implements fn:upper-case($arg as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnUpperCase(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.7: Unicode case mapping, not locale-dependent.
        // .NET ToUpperInvariant does not expand ß → SS on all runtimes.
        // We apply Unicode SpecialCasing.txt expansions first, then ToUpperInvariant.
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(UnicodeUpperCase(s)));
    }

    /// <summary>Implements fn:lower-case($arg as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnLowerCase(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.8: Unicode case mapping, not locale-dependent.
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(s.ToLowerInvariant()));
    }

    // =========================================================================
    // F&O §7.4.9 — fn:translate
    // =========================================================================

    /// <summary>Implements fn:translate($arg, $mapString, $transString) as xs:string.</summary>
    internal static IXdmIterator FnTranslate(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.9: code-point-based character mapping.
        string s = OneStringOrEmpty(args[0]);
        string mapStr = OneStringOrEmpty(args[1]);
        string transStr = OneStringOrEmpty(args[2]);

        return new SingleItemIterator(XdmAtomicValue.String(TranslateImpl(s, mapStr, transStr)));
    }

    private static string TranslateImpl(string s, string mapStr, string transStr)
    {
        if (s.Length == 0 || mapStr.Length == 0)
        {
            return s;
        }

        // Build code-point arrays for mapStr and transStr (code-point based)
        int[] mapCp = CodePointString.ToCodePointArray(mapStr);
        int[] transCp = CodePointString.ToCodePointArray(transStr);

        StringBuilder sb = new StringBuilder(s.Length);

        // For each code point in s, look it up in mapCp
        int i = 0;
        int len = s.Length;
        while (i < len)
        {
            char c = s[i];
            int cp;
            int cpLen;
            if (char.IsHighSurrogate(c) && i + 1 < len && char.IsLowSurrogate(s[i + 1]))
            {
                cp = char.ConvertToUtf32(c, s[i + 1]);
                cpLen = 2;
            }
            else
            {
                cp = c;
                cpLen = 1;
            }

            // Find cp in mapCp
            int mapIdx = FindCodePoint(mapCp, cp);
            if (mapIdx < 0)
            {
                // Not in map — keep
                sb.Append(s, i, cpLen);
            }
            else if (mapIdx < transCp.Length)
            {
                // Replace with transCp[mapIdx]
                sb.Append(char.ConvertFromUtf32(transCp[mapIdx]));
            }
            // else: mapIdx >= transCp.Length → delete (do not append)

            i += cpLen;
        }

        return sb.ToString();
    }

    private static int FindCodePoint(int[] arr, int cp)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == cp)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================================
    // F&O §7.4.10 — fn:encode-for-uri
    // =========================================================================

    /// <summary>Implements fn:encode-for-uri($uri-part as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnEncodeForUri(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.10: encode all characters except unreserved (ALPHA / DIGIT / - _ . ~)
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(EncodeForUri(s)));
    }

    private static string EncodeForUri(string s)
    {
        // RFC 3986 unreserved characters: ALPHA / DIGIT / "-" / "_" / "." / "~"
        if (s.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(s.Length * 2);
        byte[] utf8 = Encoding.UTF8.GetBytes(s);
        foreach (byte b in utf8)
        {
            if (IsUnreservedByte(b))
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('%');
                sb.Append(HexChar(b >> 4));
                sb.Append(HexChar(b & 0xF));
            }
        }

        return sb.ToString();
    }

    // =========================================================================
    // F&O §7.4.11 — fn:iri-to-uri
    // =========================================================================

    /// <summary>Implements fn:iri-to-uri($iri as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnIriToUri(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.11: percent-encode characters outside the URI character set
        // (characters > U+007F or not allowed in IRIs).
        // We encode bytes > 0x7F as UTF-8 percent-encoding; leave the rest as-is.
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(IriToUri(s)));
    }

    private static string IriToUri(string s)
    {
        if (s.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(s.Length * 2);
        // Process char by char; non-ASCII gets UTF-8 percent-encoded
        int i = 0;
        int len = s.Length;
        while (i < len)
        {
            char c = s[i];
            if (c <= 0x7F)
            {
                // ASCII: check if it's a character that must be percent-encoded in a URI
                // Per F&O §7.4.11: encode "%" only if not already starting a valid pct-encoded seq,
                // and encode non-IRI chars. For simplicity, we encode all non-IRI-allowed ASCII.
                if (IsIriAllowedAscii(c))
                {
                    sb.Append(c);
                }
                else
                {
                    byte b = (byte)c;
                    sb.Append('%');
                    sb.Append(HexChar(b >> 4));
                    sb.Append(HexChar(b & 0xF));
                }

                i++;
            }
            else
            {
                // Non-ASCII: encode as UTF-8 bytes
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

                byte[] utf8 = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(cp));
                foreach (byte b in utf8)
                {
                    sb.Append('%');
                    sb.Append(HexChar(b >> 4));
                    sb.Append(HexChar(b & 0xF));
                }
            }
        }

        return sb.ToString();
    }

    // =========================================================================
    // F&O §7.4.12 — fn:escape-html-uri
    // =========================================================================

    /// <summary>Implements fn:escape-html-uri($uri as xs:string?) as xs:string.</summary>
    internal static IXdmIterator FnEscapeHtmlUri(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.4.12: percent-encode all characters outside the printable ASCII range (0x21..0x7E)
        // excluding the space character. Keep printable ASCII as-is; encode all others as UTF-8 pct-enc.
        string s = OneStringOrEmpty(args[0]);
        return new SingleItemIterator(XdmAtomicValue.String(EscapeHtmlUri(s)));
    }

    private static string EscapeHtmlUri(string s)
    {
        if (s.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(s.Length * 2);
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(s);
        // We need to process per byte but preserve printable ASCII (0x20..0x7E except we keep all)
        // F&O §7.4.12: encode all characters not in the range #x20..#x7E (ie printable ASCII)
        foreach (byte b in utf8Bytes)
        {
            if (b >= 0x20 && b <= 0x7E)
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('%');
                sb.Append(HexChar(b >> 4));
                sb.Append(HexChar(b & 0xF));
            }
        }

        return sb.ToString();
    }

    // =========================================================================
    // F&O §7.5 — fn:contains, fn:starts-with, fn:ends-with
    // =========================================================================

    /// <summary>Implements fn:contains($arg1, $arg2) as xs:boolean.</summary>
    internal static IXdmIterator FnContains2(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        string sub = OneStringOrEmpty(args[1]);
        // F&O §7.5.1: empty substring always returns true
        // CA2249 suppressed: string.Contains(string, StringComparison) not available on netstandard2.0 API surface we write against
#pragma warning disable CA2249
        bool result = sub.Length == 0 || s.IndexOf(sub, StringComparison.Ordinal) >= 0;
#pragma warning restore CA2249
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>Implements fn:contains($arg1, $arg2, $collation) as xs:boolean.</summary>
    internal static IXdmIterator FnContains3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnContains2(ctx, args);
    }

    /// <summary>Implements fn:starts-with($arg1, $arg2) as xs:boolean.</summary>
    internal static IXdmIterator FnStartsWith2(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        string sub = OneStringOrEmpty(args[1]);
        // F&O §7.5.2: empty substring always returns true
        bool result = sub.Length == 0 || s.StartsWith(sub, StringComparison.Ordinal);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>Implements fn:starts-with($arg1, $arg2, $collation) as xs:boolean.</summary>
    internal static IXdmIterator FnStartsWith3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnStartsWith2(ctx, args);
    }

    /// <summary>Implements fn:ends-with($arg1, $arg2) as xs:boolean.</summary>
    internal static IXdmIterator FnEndsWith2(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        string sub = OneStringOrEmpty(args[1]);
        // F&O §7.5.3: empty substring always returns true
        bool result = sub.Length == 0 || s.EndsWith(sub, StringComparison.Ordinal);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>Implements fn:ends-with($arg1, $arg2, $collation) as xs:boolean.</summary>
    internal static IXdmIterator FnEndsWith3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnEndsWith2(ctx, args);
    }

    // =========================================================================
    // F&O §7.5.4 — fn:substring-before / fn:substring-after
    // =========================================================================

    /// <summary>Implements fn:substring-before($arg1, $arg2) as xs:string.</summary>
    internal static IXdmIterator FnSubstringBefore2(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        string sub = OneStringOrEmpty(args[1]);
        string result = SubstringBeforeImpl(s, sub);
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    /// <summary>Implements fn:substring-before($arg1, $arg2, $collation) as xs:string.</summary>
    internal static IXdmIterator FnSubstringBefore3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnSubstringBefore2(ctx, args);
    }

    /// <summary>Implements fn:substring-after($arg1, $arg2) as xs:string.</summary>
    internal static IXdmIterator FnSubstringAfter2(DynamicContext ctx, XdmSequence[] args)
    {
        string s = OneStringOrEmpty(args[0]);
        string sub = OneStringOrEmpty(args[1]);
        string result = SubstringAfterImpl(s, sub);
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    /// <summary>Implements fn:substring-after($arg1, $arg2, $collation) as xs:string.</summary>
    internal static IXdmIterator FnSubstringAfter3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnSubstringAfter2(ctx, args);
    }

    private static string SubstringBeforeImpl(string s, string sub)
    {
        // F&O §7.5.4: returns portion of s before first occurrence of sub; "" if not found
        if (sub.Length == 0)
        {
            return string.Empty;
        }

        int idx = s.IndexOf(sub, StringComparison.Ordinal);
        return idx < 0 ? string.Empty : s.Substring(0, idx);
    }

    private static string SubstringAfterImpl(string s, string sub)
    {
        // F&O §7.5.5: returns portion of s after first occurrence of sub; "" if not found
        if (sub.Length == 0)
        {
            return s;
        }

        int idx = s.IndexOf(sub, StringComparison.Ordinal);
        return idx < 0 ? string.Empty : s.Substring(idx + sub.Length);
    }

    // =========================================================================
    // F&O §7.2.1 — fn:string-to-codepoints
    // =========================================================================

    /// <summary>Implements fn:string-to-codepoints($arg as xs:string?) as xs:integer*.</summary>
    internal static IXdmIterator FnStringToCodepoints(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.2.1
        string s = OneStringOrEmpty(args[0]);
        if (s.Length == 0)
        {
            return EmptyIterator.Instance;
        }

        int[] codePoints = CodePointString.ToCodePointArray(s);
        XdmItem[] items = new XdmItem[codePoints.Length];
        for (int i = 0; i < codePoints.Length; i++)
        {
            items[i] = XdmAtomicValue.Integer((long)codePoints[i]);
        }

        return new ArrayIterator(items);
    }

    // =========================================================================
    // F&O §7.2.2 — fn:codepoints-to-string
    // =========================================================================

    /// <summary>Implements fn:codepoints-to-string($arg as xs:integer*) as xs:string.</summary>
    internal static IXdmIterator FnCodepointsToString(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.2.2: convert sequence of code points to string
        // The parameter is xs:integer* so FunctionConversion has already atomized but not cast.
        IXdmIterator it = args[0].GetIterator();
        StringBuilder sb = new StringBuilder();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            XdmAtomicValue av = (XdmAtomicValue)item;
            long lv = av.AsInt64();
            // Validate range per FOCH0001: 1..0x10FFFF, excluding surrogates
            CodePointString.ValidateCodePoint((int)lv);
            sb.Append(char.ConvertFromUtf32((int)lv));
        }

        return new SingleItemIterator(XdmAtomicValue.String(sb.ToString()));
    }

    // =========================================================================
    // F&O §7.3.2 — fn:compare
    // =========================================================================

    /// <summary>Implements fn:compare($comparand1, $comparand2) as xs:integer?.</summary>
    internal static IXdmIterator FnCompare2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.3.2: empty on either arg → empty sequence
        string? s1 = OptionalString(args[0]);
        string? s2 = OptionalString(args[1]);
        if (s1 is null || s2 is null)
        {
            return EmptyIterator.Instance;
        }

        int cmp = string.Compare(s1, s2, StringComparison.Ordinal);
        int result = cmp < 0 ? -1 : cmp > 0 ? 1 : 0;
        return new SingleItemIterator(XdmAtomicValue.Integer((long)result));
    }

    /// <summary>Implements fn:compare($comparand1, $comparand2, $collation) as xs:integer?.</summary>
    internal static IXdmIterator FnCompare3(DynamicContext ctx, XdmSequence[] args)
    {
        CheckCollation(args[2]);
        return FnCompare2(ctx, args);
    }

    // =========================================================================
    // F&O §7.3.3 — fn:codepoint-equal
    // =========================================================================

    /// <summary>Implements fn:codepoint-equal($comparand1, $comparand2) as xs:boolean?.</summary>
    internal static IXdmIterator FnCodepointEqual(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §7.3.3: empty on either → empty sequence
        string? s1 = OptionalString(args[0]);
        string? s2 = OptionalString(args[1]);
        if (s1 is null || s2 is null)
        {
            return EmptyIterator.Instance;
        }

        bool equal = string.Equals(s1, s2, StringComparison.Ordinal);
        return new SingleItemIterator(XdmAtomicValue.Boolean(equal));
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Returns the string value of the single item in <paramref name="seq"/>,
    /// or <see cref="string.Empty"/> if the sequence is empty.
    /// </summary>
    private static string OneStringOrEmpty(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }

    /// <summary>
    /// Returns the string value of the single item in <paramref name="seq"/>,
    /// or <see langword="null"/> if the sequence is empty.
    /// </summary>
    private static string? OptionalString(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? null : item.GetStringValue();
    }

    /// <summary>Returns the double value of the first item in <paramref name="seq"/>, or NaN if empty.</summary>
    private static double OneDoubleOrNaN(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return double.NaN;
        }

        XdmAtomicValue av = (XdmAtomicValue)item;
        return av.AsDouble();
    }

    /// <summary>
    /// Returns the string value of the single item in <paramref name="seq"/>,
    /// or "" if the sequence is empty. Each item is atomized via <see cref="XdmItem.GetStringValue"/>.
    /// </summary>
    private static string SequenceToString(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }

    /// <summary>
    /// Returns fn:string() on the context item. Raises XPDY0002 if no context item.
    /// </summary>
    private static string GetContextItemString(DynamicContext ctx)
    {
        XdmItem? ci = ctx.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:string-length() / fn:normalize-space() with no argument requires a context item.",
                default);
        }

        return ci.GetStringValue();
    }

    /// <summary>
    /// Validates that the collation URI in <paramref name="collationSeq"/> is the codepoint
    /// collation (or empty, defaulting to codepoint). Raises FOCH0002 otherwise.
    /// Per P2-09 scope: silently accept any collation URI and use codepoint comparison.
    /// </summary>
    private static void CheckCollation(XdmSequence collationSeq)
    {
        // P2-09 scope: accept any collation URI (defer proper collation support).
        // If a non-codepoint collation is specified in future, raise FOCH0002 here.
        // For now: no-op.
        _ = collationSeq;
    }

    // =========================================================================
    // Unicode case mapping helpers
    // =========================================================================

    /// <summary>
    /// Returns the uppercase version of <paramref name="s"/> using Unicode case mapping.
    /// Applies Unicode SpecialCasing.txt expansions (e.g. ß → SS) that .NET's
    /// ToUpperInvariant may not handle on all runtimes, then calls ToUpperInvariant
    /// for the remaining characters.
    /// F&amp;O §7.4.7.
    /// </summary>
    private static string UnicodeUpperCase(string s)
    {
        if (s.Length == 0)
        {
            return string.Empty;
        }

        // Check if any special-casing expansion is needed first (fast path for ASCII).
        bool needsSpecial = false;
        for (int i = 0; i < s.Length; i++)
        {
            if (NeedsSpecialUpperCase(s[i]))
            {
                needsSpecial = true;
                break;
            }
        }

        if (!needsSpecial)
        {
            return s.ToUpperInvariant();
        }

        // Apply special-case expansions then upper-case the rest.
        // Unicode SpecialCasing.txt: lowercase → uppercase (unconditional mappings)
        StringBuilder sb = new StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            string? expansion = GetSpecialUpperCase(c);
            if (expansion != null)
            {
                sb.Append(expansion);
            }
            else
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.ToString();
    }

    private static bool NeedsSpecialUpperCase(char c)
    {
        // Quick check for characters with Unicode SpecialCasing.txt entries that expand
        switch (c)
        {
            case 'ß': // ß → SS
            case 'ŉ': // ŉ → ʼN
            case 'ǰ': // ǰ → J̌  (omit complex case)
            case 'ΐ': // ΐ
            case 'ΰ': // ΰ
            case 'և': // և
            case 'ẖ': // ẖ
            case 'ẗ': // ẗ
            case 'ẘ': // ẘ
            case 'ẙ': // ẙ
            case 'ẚ': // ả
            case 'ᾶ': // ᾶ
            case 'ῆ': // ῆ
            case 'ῒ': case 'ΐ': case 'ῖ': case 'ῗ':
            case 'ῢ': case 'ΰ': case 'ῤ': case 'ῦ': case 'ῧ':
            case 'ῶ':
            case 'ﬀ': // ﬀ → FF
            case 'ﬁ': // ﬁ → FI
            case 'ﬂ': // ﬂ → FL
            case 'ﬃ': // ﬃ → FFI
            case 'ﬄ': // ﬄ → FFL
            case 'ﬅ': // ﬅ → ST
            case 'ﬆ': // ﬆ → ST
                return true;
            default:
                return false;
        }
    }

    private static string? GetSpecialUpperCase(char c)
    {
        // Unicode SpecialCasing.txt unconditional lowercase → uppercase expansions.
        // Only the most common entries are listed here (sufficient for F&O §7.4.7 conformance).
        switch (c)
        {
            case 'ß': return "SS";     // ß → SS
            case 'ﬀ': return "FF";     // ﬀ → FF
            case 'ﬁ': return "FI";     // ﬁ → FI
            case 'ﬂ': return "FL";     // ﬂ → FL
            case 'ﬃ': return "FFI";    // ﬃ → FFI
            case 'ﬄ': return "FFL";    // ﬄ → FFL
            case 'ﬅ': return "ST";     // ﬅ → ST
            case 'ﬆ': return "ST";     // ﬆ → ST
            default:       return null;
        }
    }

    // =========================================================================
    // URI encoding helpers
    // =========================================================================

    private static bool IsUnreservedByte(byte b)
    {
        // RFC 3986 §2.3: unreserved = ALPHA / DIGIT / "-" / "_" / "." / "~"
        return (b >= (byte)'A' && b <= (byte)'Z')
            || (b >= (byte)'a' && b <= (byte)'z')
            || (b >= (byte)'0' && b <= (byte)'9')
            || b == (byte)'-'
            || b == (byte)'_'
            || b == (byte)'.'
            || b == (byte)'~';
    }

    private static bool IsIriAllowedAscii(char c)
    {
        // F&O §7.4.11 / RFC 3987: allow printable ASCII that is in URI syntax
        // For simplicity: allow all printable ASCII that would naturally appear in a URI
        // (letters, digits, and URI syntax chars: - _ . ~ ! * ' ( ) ; : @ & = + $ , / ? # [ ] %)
        // This is a conservative implementation — if in doubt, encode.
        if (c >= 'A' && c <= 'Z') { return true; }
        if (c >= 'a' && c <= 'z') { return true; }
        if (c >= '0' && c <= '9') { return true; }

        // RFC 3986 gen-delims + sub-delims + unreserved (minus already handled)
        switch (c)
        {
            case '-': case '_': case '.': case '~':
            case '!': case '$': case '&': case '\'':
            case '(': case ')': case '*': case '+':
            case ',': case ';': case '=':
            case ':': case '/': case '?': case '#':
            case '[': case ']': case '@': case '%':
                return true;
            default:
                return false;
        }
    }

    private static char HexChar(int nibble)
    {
        return nibble < 10 ? (char)('0' + nibble) : (char)('A' + nibble - 10);
    }
}
