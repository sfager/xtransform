// Number words and ordinal suffixes for xsl:number (XSLT 2.0 §12.3, format tokens w, W, Ww and the
// ordinal attribute). English is supported in full; other languages fall back to English, which
// XSLT 2.0 allows ("the language it would use with lang omitted") and which is recorded in
// docs/implementation-defined.md.

using System;
using System.Globalization;

namespace XTransform.Xslt;

internal static class NumberWords
{
    private static readonly string[] s_units =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    };

    private static readonly string[] s_tens =
    {
        string.Empty, string.Empty, "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    };

    private static readonly string[] s_scales = { string.Empty, "thousand", "million", "billion", "trillion", "quadrillion", "quintillion" };

    /// <summary>The primary language subtag of <paramref name="lang"/>, lower-cased; "en" when absent.</summary>
    public static string NormaliseLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return "en";
        }

        string trimmed = lang!.Trim();
        int dash = trimmed.IndexOf('-');
        return (dash < 0 ? trimmed : trimmed.Substring(0, dash)).ToLowerInvariant();
    }

    /// <summary>English cardinal words: "zero", "twenty-one", "one hundred and five".</summary>
    public static string CardinalWords(long n, string language)
    {
        if (n == 0)
        {
            return s_units[0];
        }

        // Groups of three digits, most significant first.
        string result = string.Empty;
        int scale = 0;
        long rest = n;
        string[] groups = new string[s_scales.Length];
        while (rest > 0 && scale < s_scales.Length)
        {
            int group = (int)(rest % 1000);
            groups[scale] = group == 0 ? string.Empty : BelowThousand(group) + (scale > 0 ? " " + s_scales[scale] : string.Empty);
            rest /= 1000;
            scale++;
        }

        for (int i = scale - 1; i >= 0; i--)
        {
            if (groups[i].Length == 0)
            {
                continue;
            }

            if (result.Length > 0)
            {
                // "one thousand and five" when the final group is below one hundred.
                result += i == 0 && (n % 1000) < 100 ? " and " : " ";
            }

            result += groups[i];
        }

        return result;
    }

    /// <summary>English ordinal words: "first", "twenty-first", "one hundredth".</summary>
    public static string OrdinalWords(long n, string language, string ordinal)
    {
        string cardinal = CardinalWords(n, language);
        int cut = Math.Max(cardinal.LastIndexOf(' '), cardinal.LastIndexOf('-')) + 1;
        string head = cardinal.Substring(0, cut);
        string last = cardinal.Substring(cut);
        string ordinalLast;
        switch (last)
        {
            case "one": ordinalLast = "first"; break;
            case "two": ordinalLast = "second"; break;
            case "three": ordinalLast = "third"; break;
            case "five": ordinalLast = "fifth"; break;
            case "eight": ordinalLast = "eighth"; break;
            case "nine": ordinalLast = "ninth"; break;
            case "twelve": ordinalLast = "twelfth"; break;
            default:
                ordinalLast = last.Length > 0 && last[last.Length - 1] == 'y'
                    ? last.Remove(last.Length - 1) + "ieth"
                    : last + "th";
                break;
        }

        return head + ordinalLast;
    }

    /// <summary>The English ordinal suffix for a decimal number: "st", "nd", "rd" or "th".</summary>
    public static string OrdinalSuffix(long n, string language, string ordinal)
    {
        long mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13)
        {
            return "th";
        }

        return (n % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
    }

    private static string BelowThousand(int n)
    {
        string s = string.Empty;
        if (n >= 100)
        {
            s = s_units[n / 100] + " hundred";
            n %= 100;
            if (n > 0)
            {
                s += " and ";
            }
        }

        if (n >= 20)
        {
            s += s_tens[n / 10];
            if (n % 10 > 0)
            {
                s += "-" + s_units[n % 10];
            }
        }
        else if (n > 0)
        {
            s += s_units[n];
        }

        return s;
    }
}
