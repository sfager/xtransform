// Unicode general-category data for supplementary-plane code points (U+10000–U+10FFFF).
// .NET regexes work on UTF-16 code units, so \p{..} never matches a surrogate pair; the
// translator uses these ranges to add explicit surrogate-pair alternatives (F&O §7.6.1 defines
// the regex dialect over code points).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XTransform.XPath.Functions;

/// <summary>A closed range of code points.</summary>
internal readonly struct CodePointRange
{
    public CodePointRange(int first, int last)
    {
        First = first;
        Last = last;
    }

    public int First { get; }

    public int Last { get; }
}

internal static class AstralCategories
{
    private const int FirstAstral = 0x10000;
    private const int LastAstral = 0x10FFFF;

    // Immutable once built (derived from the runtime's Unicode data); built on first use.
    private static readonly Lazy<byte[]> s_categories = new Lazy<byte[]>(Build);

    /// <summary>
    /// Returns the supplementary-plane ranges whose general category is in
    /// <paramref name="categoryName"/> (<c>L</c>, <c>Lu</c>, …), or whose category is
    /// <em>not</em> in it when <paramref name="negate"/> is true. Returns null for names that are
    /// not general categories (e.g. block names such as <c>IsBasicLatin</c>).
    /// </summary>
    public static List<CodePointRange>? GetRanges(string categoryName, bool negate)
    {
        if (!TryGetCategories(categoryName, out HashSet<UnicodeCategory>? wanted))
        {
            return null;
        }

        byte[] cats = s_categories.Value;
        List<CodePointRange> ranges = new List<CodePointRange>();
        int start = -1;
        for (int cp = FirstAstral; cp <= LastAstral; cp++)
        {
            bool inSet = wanted!.Contains((UnicodeCategory)cats[cp - FirstAstral]);
            if (inSet != negate)
            {
                if (start < 0)
                {
                    start = cp;
                }
            }
            else if (start >= 0)
            {
                ranges.Add(new CodePointRange(start, cp - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            ranges.Add(new CodePointRange(start, LastAstral));
        }

        return ranges;
    }

    /// <summary>
    /// Renders <paramref name="ranges"/> as a .NET regex fragment (alternation of surrogate-pair
    /// patterns), or null when the set is empty.
    /// </summary>
    public static string? ToPattern(IReadOnlyList<CodePointRange> ranges)
    {
        // high surrogate -> low-surrogate ranges
        SortedDictionary<int, StringBuilder> byHigh = new SortedDictionary<int, StringBuilder>();
        foreach (CodePointRange r in ranges)
        {
            int first = r.First;
            int last = r.Last;
            while (first <= last)
            {
                int high = 0xD800 + ((first - 0x10000) >> 10);
                int blockEnd = (((first - 0x10000) | 0x3FF) + 0x10000);
                int segLast = Math.Min(last, blockEnd);
                int lowFirst = 0xDC00 + ((first - 0x10000) & 0x3FF);
                int lowLast = 0xDC00 + ((segLast - 0x10000) & 0x3FF);

                if (!byHigh.TryGetValue(high, out StringBuilder? lows))
                {
                    lows = new StringBuilder();
                    byHigh[high] = lows;
                }

                AppendRange(lows, lowFirst, lowLast);
                first = segLast + 1;
            }
        }

        if (byHigh.Count == 0)
        {
            return null;
        }

        // Merge consecutive high surrogates that share the same low-surrogate class.
        StringBuilder sb = new StringBuilder();
        int groupStart = -1;
        int groupEnd = -1;
        string? groupLows = null;
        foreach (KeyValuePair<int, StringBuilder> kv in byHigh)
        {
            string lows = kv.Value.ToString();
            if (groupLows is not null && kv.Key == groupEnd + 1 && string.Equals(lows, groupLows, StringComparison.Ordinal))
            {
                groupEnd = kv.Key;
                continue;
            }

            if (groupLows is not null)
            {
                AppendGroup(sb, groupStart, groupEnd, groupLows);
            }

            groupStart = kv.Key;
            groupEnd = kv.Key;
            groupLows = lows;
        }

        AppendGroup(sb, groupStart, groupEnd, groupLows!);
        return sb.ToString();
    }

    /// <summary>The union of several range lists, normalised (sorted, merged).</summary>
    public static List<CodePointRange> Union(IEnumerable<CodePointRange> ranges)
    {
        List<CodePointRange> sorted = new List<CodePointRange>(ranges);
        sorted.Sort((a, b) => a.First.CompareTo(b.First));
        List<CodePointRange> merged = new List<CodePointRange>();
        foreach (CodePointRange r in sorted)
        {
            if (merged.Count > 0 && r.First <= merged[merged.Count - 1].Last + 1)
            {
                CodePointRange prev = merged[merged.Count - 1];
                merged[merged.Count - 1] = new CodePointRange(prev.First, Math.Max(prev.Last, r.Last));
            }
            else
            {
                merged.Add(r);
            }
        }

        return merged;
    }

    private static void AppendGroup(StringBuilder sb, int highFirst, int highLast, string lows)
    {
        if (sb.Length > 0)
        {
            sb.Append('|');
        }

        sb.Append(highFirst == highLast ? Escape(highFirst) : "[" + Escape(highFirst) + "-" + Escape(highLast) + "]");
        sb.Append('[').Append(lows).Append(']');
    }

    private static void AppendRange(StringBuilder sb, int first, int last)
    {
        sb.Append(Escape(first));
        if (last > first)
        {
            sb.Append('-').Append(Escape(last));
        }
    }

    private static string Escape(int unit) => "\\u" + unit.ToString("X4", CultureInfo.InvariantCulture);

    private static bool TryGetCategories(string name, out HashSet<UnicodeCategory>? set)
    {
        set = null;
        switch (name)
        {
            case "L": set = Of(UnicodeCategory.UppercaseLetter, UnicodeCategory.LowercaseLetter, UnicodeCategory.TitlecaseLetter, UnicodeCategory.ModifierLetter, UnicodeCategory.OtherLetter); break;
            case "Lu": set = Of(UnicodeCategory.UppercaseLetter); break;
            case "Ll": set = Of(UnicodeCategory.LowercaseLetter); break;
            case "Lt": set = Of(UnicodeCategory.TitlecaseLetter); break;
            case "Lm": set = Of(UnicodeCategory.ModifierLetter); break;
            case "Lo": set = Of(UnicodeCategory.OtherLetter); break;
            case "M": set = Of(UnicodeCategory.NonSpacingMark, UnicodeCategory.SpacingCombiningMark, UnicodeCategory.EnclosingMark); break;
            case "Mn": set = Of(UnicodeCategory.NonSpacingMark); break;
            case "Mc": set = Of(UnicodeCategory.SpacingCombiningMark); break;
            case "Me": set = Of(UnicodeCategory.EnclosingMark); break;
            case "N": set = Of(UnicodeCategory.DecimalDigitNumber, UnicodeCategory.LetterNumber, UnicodeCategory.OtherNumber); break;
            case "Nd": set = Of(UnicodeCategory.DecimalDigitNumber); break;
            case "Nl": set = Of(UnicodeCategory.LetterNumber); break;
            case "No": set = Of(UnicodeCategory.OtherNumber); break;
            case "P": set = Of(UnicodeCategory.ConnectorPunctuation, UnicodeCategory.DashPunctuation, UnicodeCategory.OpenPunctuation, UnicodeCategory.ClosePunctuation, UnicodeCategory.InitialQuotePunctuation, UnicodeCategory.FinalQuotePunctuation, UnicodeCategory.OtherPunctuation); break;
            case "Pc": set = Of(UnicodeCategory.ConnectorPunctuation); break;
            case "Pd": set = Of(UnicodeCategory.DashPunctuation); break;
            case "Ps": set = Of(UnicodeCategory.OpenPunctuation); break;
            case "Pe": set = Of(UnicodeCategory.ClosePunctuation); break;
            case "Pi": set = Of(UnicodeCategory.InitialQuotePunctuation); break;
            case "Pf": set = Of(UnicodeCategory.FinalQuotePunctuation); break;
            case "Po": set = Of(UnicodeCategory.OtherPunctuation); break;
            case "S": set = Of(UnicodeCategory.MathSymbol, UnicodeCategory.CurrencySymbol, UnicodeCategory.ModifierSymbol, UnicodeCategory.OtherSymbol); break;
            case "Sm": set = Of(UnicodeCategory.MathSymbol); break;
            case "Sc": set = Of(UnicodeCategory.CurrencySymbol); break;
            case "Sk": set = Of(UnicodeCategory.ModifierSymbol); break;
            case "So": set = Of(UnicodeCategory.OtherSymbol); break;
            case "Z": set = Of(UnicodeCategory.SpaceSeparator, UnicodeCategory.LineSeparator, UnicodeCategory.ParagraphSeparator); break;
            case "Zs": set = Of(UnicodeCategory.SpaceSeparator); break;
            case "Zl": set = Of(UnicodeCategory.LineSeparator); break;
            case "Zp": set = Of(UnicodeCategory.ParagraphSeparator); break;
            case "C": set = Of(UnicodeCategory.Control, UnicodeCategory.Format, UnicodeCategory.Surrogate, UnicodeCategory.PrivateUse, UnicodeCategory.OtherNotAssigned); break;
            case "Cc": set = Of(UnicodeCategory.Control); break;
            case "Cf": set = Of(UnicodeCategory.Format); break;
            case "Co": set = Of(UnicodeCategory.PrivateUse); break;
            case "Cn": set = Of(UnicodeCategory.OtherNotAssigned); break;
            default: return false;
        }

        return true;
    }

    private static HashSet<UnicodeCategory> Of(params UnicodeCategory[] categories) => new HashSet<UnicodeCategory>(categories);

    private static byte[] Build()
    {
        byte[] cats = new byte[LastAstral - FirstAstral + 1];
        for (int cp = FirstAstral; cp <= LastAstral; cp++)
        {
            cats[cp - FirstAstral] = (byte)CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
        }

        return cats;
    }
}
