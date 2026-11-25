// XSLT 2.0 §12.3 — number to string conversion for xsl:number: format-token parsing, decimal
// digit families, alphabetic / roman / word sequences, ordinals and digit grouping.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XTransform.Xslt;

/// <summary>Formats a sequence of non-negative integers according to an xsl:number format string.</summary>
internal static class NumberFormatter
{
    /// <summary>
    /// Formats <paramref name="numbers"/> (XSLT 2.0 §12.3). <paramref name="groupingSize"/> is 0
    /// when grouping is not requested; grouping applies only when both a separator and a size are given.
    /// </summary>
    public static string Format(
        IReadOnlyList<long> numbers,
        string format,
        string? lang,
        string? letterValue,
        string? ordinal,
        string? groupingSeparator,
        int groupingSize)
    {
        ParseFormat(format, out string prefix, out List<string> tokens, out List<string> separators, out string suffix);

        string language = NumberWords.NormaliseLanguage(lang);
        bool useGrouping = !string.IsNullOrEmpty(groupingSeparator) && groupingSize > 0;
        StringBuilder sb = new StringBuilder();
        sb.Append(prefix);

        for (int i = 0; i < numbers.Count; i++)
        {
            int tokenIndex = Math.Min(i, tokens.Count - 1);
            if (i > 0)
            {
                // The separator before the token in use; a lone token is separated by ".".
                sb.Append(tokens.Count == 1 ? "." : separators[tokenIndex]);
            }

            sb.Append(FormatOne(
                numbers[i],
                tokens[tokenIndex],
                language,
                ordinal,
                useGrouping ? groupingSeparator : null,
                groupingSize));
        }

        sb.Append(suffix);
        return sb.ToString();
    }

    // ── Format string ────────────────────────────────────────────────────────────

    private readonly struct FormatRun
    {
        public FormatRun(bool isToken, string text)
        {
            IsToken = isToken;
            Text = text;
        }

        public bool IsToken { get; }

        public string Text { get; }
    }

    /// <summary>
    /// Splits <paramref name="format"/> into alphanumeric format tokens and non-alphanumeric
    /// prefix / separator / suffix tokens (§12.3). <paramref name="separators"/>[i] is the token before
    /// format token i (empty for i = 0).
    /// </summary>
    internal static void ParseFormat(
        string format,
        out string prefix,
        out List<string> tokens,
        out List<string> separators,
        out string suffix)
    {
        prefix = string.Empty;
        suffix = string.Empty;
        tokens = new List<string>();
        separators = new List<string>();

        List<FormatRun> runs = new List<FormatRun>();
        int pos = 0;
        while (pos < format.Length)
        {
            bool alnum = IsAlphanumeric(format, pos);
            int start = pos;
            while (pos < format.Length && IsAlphanumeric(format, pos) == alnum)
            {
                pos += char.IsHighSurrogate(format[pos]) && pos + 1 < format.Length ? 2 : 1;
            }

            runs.Add(new FormatRun(alnum, format.Substring(start, pos - start)));
        }

        bool anyToken = false;
        foreach (FormatRun run in runs)
        {
            anyToken |= run.IsToken;
        }

        if (!anyToken)
        {
            // No format token: "1" is used; a lone non-alphanumeric token is both prefix and suffix.
            tokens.Add("1");
            separators.Add(string.Empty);
            if (runs.Count > 0)
            {
                prefix = runs[0].Text;
                suffix = runs[0].Text;
            }

            return;
        }

        string pendingSeparator = string.Empty;
        for (int i = 0; i < runs.Count; i++)
        {
            if (runs[i].IsToken)
            {
                tokens.Add(runs[i].Text);
                separators.Add(pendingSeparator);
                pendingSeparator = string.Empty;
            }
            else if (i == 0)
            {
                prefix = runs[i].Text;
            }
            else if (i == runs.Count - 1)
            {
                suffix = runs[i].Text;
            }
            else
            {
                pendingSeparator = runs[i].Text;
            }
        }
    }

    private static bool IsAlphanumeric(string s, int index)
    {
        UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(s, index);
        switch (cat)
        {
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.LetterNumber:
            case UnicodeCategory.OtherNumber:
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
                return true;
            default:
                return false;
        }
    }

    // ── One number ───────────────────────────────────────────────────────────────

    private static string FormatOne(long n, string token, string language, string? ordinal, string? groupSep, int groupSize)
    {
        bool ordinalRequested = !string.IsNullOrEmpty(ordinal);

        switch (token)
        {
            case "a":
            case "A":
                if (n >= 1)
                {
                    return Alphabetic(n, token == "A");
                }

                break;

            case "i":
            case "I":
                if (n >= 1 && n <= 3999)
                {
                    string roman = Roman(n);
                    return token == "I" ? roman : roman.ToLowerInvariant();
                }

                break;

            case "w":
            case "W":
            case "Ww":
            {
                string words = ordinalRequested
                    ? NumberWords.OrdinalWords(n, language, ordinal!)
                    : NumberWords.CardinalWords(n, language);
                return token == "w" ? words : token == "W" ? words.ToUpperInvariant() : TitleCase(words);
            }

            default:
            {
                // A token ending in a decimal digit with value 1 denotes that digit family.
                if (TryGetDigitFamily(token, out int zero, out int width))
                {
                    string digits = Decimal(n, zero, width, groupSep, groupSize);
                    return ordinalRequested ? digits + NumberWords.OrdinalSuffix(n, language, ordinal!) : digits;
                }

                string? special = SpecialSequences.TryFormat(token, n);
                if (special is not null)
                {
                    return special;
                }

                break;
            }
        }

        // Unsupported token or number out of range: format token "1" (§12.3).
        string fallback = Decimal(n, '0', 1, groupSep, groupSize);
        return ordinalRequested ? fallback + NumberWords.OrdinalSuffix(n, language, ordinal!) : fallback;
    }

    private static string TitleCase(string words)
    {
        StringBuilder sb = new StringBuilder(words.Length);
        bool startOfWord = true;
        foreach (char c in words)
        {
            sb.Append(startOfWord ? char.ToUpperInvariant(c) : c);
            startOfWord = c == ' ' || c == '-';
        }

        return sb.ToString();
    }

    // ── Decimal digit families ───────────────────────────────────────────────────

    /// <summary>
    /// A digit token is a run of digits from one Unicode decimal-digit family whose last digit has the
    /// value 1 and whose other digits have the value 0 (for example "1", "01", "001", Arabic-Indic one).
    /// </summary>
    private static bool TryGetDigitFamily(string token, out int zero, out int width)
    {
        zero = '0';
        width = 0;
        int i = 0;
        int lastValue = -1;
        int first = -1;
        while (i < token.Length)
        {
            int cp = char.ConvertToUtf32(token, i);
            i += char.IsSurrogatePair(token, i) ? 2 : 1;
            int digit = (int)CharUnicodeInfo.GetDecimalDigitValue(char.ConvertFromUtf32(cp), 0);
            if (digit < 0)
            {
                return false;
            }

            int family = cp - digit;
            if (first < 0)
            {
                first = family;
            }
            else if (family != first)
            {
                return false;
            }

            lastValue = digit;
            width++;
        }

        if (width == 0 || lastValue != 1)
        {
            return false;
        }

        // Every digit before the last must be zero.
        for (int j = 0, count = 0; j < token.Length; count++)
        {
            int cp = char.ConvertToUtf32(token, j);
            j += char.IsSurrogatePair(token, j) ? 2 : 1;
            if (count < width - 1 && cp != first)
            {
                return false;
            }
        }

        zero = first;
        return true;
    }

    private static string Decimal(long n, int zero, int minWidth, string? groupSep, int groupSize)
    {
        string plain = n.ToString(CultureInfo.InvariantCulture);
        StringBuilder digits = new StringBuilder();
        for (int pad = plain.Length; pad < minWidth; pad++)
        {
            digits.Append(char.ConvertFromUtf32(zero));
        }

        foreach (char c in plain)
        {
            digits.Append(char.ConvertFromUtf32(zero + (c - '0')));
        }

        if (groupSep is null || groupSize <= 0)
        {
            return digits.ToString();
        }

        // Group from the right, counting code points (digits outside the BMP use two UTF-16 units).
        List<string> cps = new List<string>();
        string all = digits.ToString();
        for (int i = 0; i < all.Length; i += char.IsSurrogatePair(all, i) ? 2 : 1)
        {
            cps.Add(char.IsSurrogatePair(all, i) ? all.Substring(i, 2) : all.Substring(i, 1));
        }

        StringBuilder grouped = new StringBuilder();
        for (int i = 0; i < cps.Count; i++)
        {
            if (i > 0 && (cps.Count - i) % groupSize == 0)
            {
                grouped.Append(groupSep);
            }

            grouped.Append(cps[i]);
        }

        return grouped.ToString();
    }

    // ── Alphabetic and roman sequences ───────────────────────────────────────────

    // a b c ... z aa ab ... (bijective base 26).
    private static string Alphabetic(long n, bool upper)
    {
        StringBuilder sb = new StringBuilder();
        long v = n;
        while (v > 0)
        {
            v--;
            sb.Insert(0, (char)((upper ? 'A' : 'a') + (int)(v % 26)));
            v /= 26;
        }

        return sb.ToString();
    }

    private static readonly int[] s_romanValues = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };

    private static readonly string[] s_romanSymbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

    private static string Roman(long n)
    {
        StringBuilder sb = new StringBuilder();
        long v = n;
        for (int i = 0; i < s_romanValues.Length; i++)
        {
            while (v >= s_romanValues[i])
            {
                sb.Append(s_romanSymbols[i]);
                v -= s_romanValues[i];
            }
        }

        return sb.ToString();
    }
}
