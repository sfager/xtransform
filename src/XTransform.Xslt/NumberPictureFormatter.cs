// XSLT 2.0 §16.4.2–§16.4.4 — fn:format-number: picture analysis and formatting.
//
// The number is handled as decimal digit strings (integer part, fractional part), so large integers and
// decimals are formatted exactly; doubles use their shortest round-trip digits.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Xslt;

internal static class NumberPictureFormatter
{
    // ── Public entry ─────────────────────────────────────────────────────────

    /// <summary>Formats <paramref name="value"/> (a numeric atomic value, or null for NaN) per <paramref name="picture"/>.</summary>
    internal static string Format(DecimalFormat format, XdmAtomicValue? value, string picture, SourceSpan location)
    {
        // The picture is analysed first so that an invalid one is reported whatever the value.
        int[] cps = ToCodePoints(picture);
        SubPicture positive;
        SubPicture negative;
        int separator = Array.IndexOf(cps, format.PatternSeparator);
        if (separator < 0)
        {
            positive = Analyse(format, cps, 0, cps.Length, location);
            negative = positive;
        }
        else
        {
            if (Array.IndexOf(cps, format.PatternSeparator, separator + 1) >= 0)
            {
                throw Invalid("the picture contains more than one pattern separator", location);
            }

            positive = Analyse(format, cps, 0, separator, location);
            negative = Analyse(format, cps, separator + 1, cps.Length, location);
        }

        bool singlePicture = separator < 0;

        // The value as sign, special-ness and digit strings.
        bool isNegative;
        bool isNaN = false;
        bool isInfinite = false;
        string integerDigits = string.Empty;
        string fractionDigits = string.Empty;
        if (value is null)
        {
            isNaN = true;
            isNegative = false;
        }
        else
        {
            Decompose(value, out isNegative, out isNaN, out isInfinite, out integerDigits, out fractionDigits);
        }

        if (isNaN)
        {
            return format.NaN;
        }

        SubPicture sub = isNegative ? negative : positive;
        string prefix = isNegative && singlePicture ? char.ConvertFromUtf32(format.MinusSign) + sub.Prefix : sub.Prefix;

        if (isInfinite)
        {
            return prefix + format.Infinity + sub.Suffix;
        }

        // Percent and per-mille scale the number (§16.4.4 step 4).
        if (sub.HasPercent)
        {
            ShiftDecimalPoint(ref integerDigits, ref fractionDigits, 2);
        }
        else if (sub.HasPerMille)
        {
            ShiftDecimalPoint(ref integerDigits, ref fractionDigits, 3);
        }

        RoundHalfEven(ref integerDigits, ref fractionDigits, sub.MaxFraction);

        // Insignificant zeros go; zero is then an empty integer part and empty fraction (§16.4.4 step 6).
        integerDigits = integerDigits.TrimStart('0');
        fractionDigits = fractionDigits.TrimEnd('0');

        StringBuilder integerPart = new StringBuilder(integerDigits);
        while (integerPart.Length < sub.MinInteger)
        {
            integerPart.Insert(0, '0');
        }

        StringBuilder fractionPart = new StringBuilder(fractionDigits);
        while (fractionPart.Length < sub.MinFraction)
        {
            fractionPart.Append('0');
        }

        StringBuilder result = new StringBuilder();
        result.Append(prefix);
        AppendGroupedInteger(result, integerPart.ToString(), sub, format);

        bool hasFraction = fractionPart.Length > 0;
        if (sub.HasDecimalSeparator && hasFraction)
        {
            AppendCodePoint(result, format.DecimalSeparator);
            AppendGroupedFraction(result, fractionPart.ToString(), sub, format);
        }

        result.Append(sub.Suffix);
        return result.ToString();
    }

    // ── Picture analysis (§16.4.3) ───────────────────────────────────────────

    private sealed class SubPicture
    {
        public string Prefix = string.Empty;
        public string Suffix = string.Empty;
        public bool HasPercent;
        public bool HasPerMille;
        public bool HasDecimalSeparator;
        public int MinInteger;
        public int MinFraction;
        public int MaxFraction;

        // Grouping positions in the integer part: an explicit set, and an interval N when they are N, 2N, 3N, ...
        public readonly HashSet<int> IntegerGrouping = new HashSet<int>();
        public int IntegerGroupingInterval;
        public readonly HashSet<int> FractionGrouping = new HashSet<int>();
    }

    private static SubPicture Analyse(DecimalFormat f, int[] all, int start, int end, SourceSpan location)
    {
        SubPicture sub = new SubPicture();
        int length = end - start;

        int decimalIndex = -1;
        int percentCount = 0;
        int perMilleCount = 0;
        bool anyDigit = false;
        int firstActive = -1;
        int lastActive = -1;
        for (int i = start; i < end; i++)
        {
            int c = all[i];
            if (c == f.DecimalSeparator)
            {
                if (decimalIndex >= 0)
                {
                    throw Invalid("a sub-picture contains more than one decimal separator", location);
                }

                decimalIndex = i;
            }
            else if (c == f.Percent)
            {
                percentCount++;
            }
            else if (c == f.PerMille)
            {
                perMilleCount++;
            }

            if (IsActive(f, c))
            {
                if (firstActive < 0)
                {
                    firstActive = i;
                }

                lastActive = i;
                if (c == f.Digit || IsZeroDigit(f, c))
                {
                    anyDigit = true;
                }
            }
        }

        if (percentCount + perMilleCount > 1)
        {
            throw Invalid("a sub-picture contains more than one percent or per-mille sign", location);
        }

        if (length == 0 || !anyDigit)
        {
            throw Invalid("a sub-picture contains no digit sign", location);
        }

        // A passive character between active ones is an error.
        for (int i = firstActive; i <= lastActive; i++)
        {
            if (!IsActive(f, all[i]))
            {
                throw Invalid("a passive character appears between active characters of the picture", location);
            }
        }

        // No grouping separator next to the decimal separator.
        if (decimalIndex >= 0)
        {
            if ((decimalIndex > start && all[decimalIndex - 1] == f.GroupingSeparator) ||
                (decimalIndex + 1 < end && all[decimalIndex + 1] == f.GroupingSeparator))
            {
                throw Invalid("a grouping separator is adjacent to the decimal separator", location);
            }
        }

        int integerEnd = decimalIndex >= 0 ? decimalIndex : lastActive + 1;
        int integerStart = firstActive;

        // Integer part: no zero digit before an optional digit; fraction: no optional digit before a zero digit.
        bool seenZero = false;
        for (int i = integerStart; i < integerEnd; i++)
        {
            if (IsZeroDigit(f, all[i]))
            {
                seenZero = true;
            }
            else if (all[i] == f.Digit && seenZero)
            {
                throw Invalid("the integer part has an optional digit after a mandatory digit", location);
            }
        }

        bool seenDigit = false;
        if (decimalIndex >= 0)
        {
            for (int i = decimalIndex + 1; i <= lastActive; i++)
            {
                if (all[i] == f.Digit)
                {
                    seenDigit = true;
                }
                else if (IsZeroDigit(f, all[i]) && seenDigit)
                {
                    throw Invalid("the fractional part has a mandatory digit after an optional digit", location);
                }
            }
        }

        sub.HasDecimalSeparator = decimalIndex >= 0;
        sub.HasPercent = percentCount > 0;
        sub.HasPerMille = perMilleCount > 0;
        sub.Prefix = Slice(all, start, firstActive);
        sub.Suffix = Slice(all, lastActive + 1, end);

        // Integer-part variables: grouping positions count digit signs to the right, within the integer part.
        List<int> positions = new List<int>();
        int zeroCount = 0;
        int rightDigits = 0;
        for (int i = integerEnd - 1; i >= integerStart; i--)
        {
            int c = all[i];
            if (c == f.GroupingSeparator)
            {
                positions.Add(rightDigits);
            }
            else if (c == f.Digit || IsZeroDigit(f, c))
            {
                rightDigits++;
                if (IsZeroDigit(f, c))
                {
                    zeroCount++;
                }
            }
        }

        positions.Sort();
        foreach (int p in positions)
        {
            sub.IntegerGrouping.Add(p);
        }

        // N, 2N, 3N, ... (including a single position) repeats at the interval N.
        if (positions.Count > 0 && positions[0] > 0)
        {
            bool regular = true;
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i] != positions[0] * (i + 1))
                {
                    regular = false;
                    break;
                }
            }

            if (regular)
            {
                sub.IntegerGroupingInterval = positions[0];
            }
        }

        sub.MinInteger = zeroCount == 0 && decimalIndex < 0 ? 1 : zeroCount;

        // Fractional-part variables: grouping positions count digit signs to the left.
        if (decimalIndex >= 0)
        {
            int leftDigits = 0;
            for (int i = decimalIndex + 1; i <= lastActive; i++)
            {
                int c = all[i];
                if (c == f.GroupingSeparator)
                {
                    sub.FractionGrouping.Add(leftDigits);
                }
                else if (c == f.Digit || IsZeroDigit(f, c))
                {
                    leftDigits++;
                    sub.MaxFraction++;
                    if (IsZeroDigit(f, c))
                    {
                        sub.MinFraction++;
                    }
                }
            }
        }

        return sub;
    }

    private static bool IsZeroDigit(DecimalFormat f, int c) => c >= f.ZeroDigit && c <= f.ZeroDigit + 9;

    private static bool IsActive(DecimalFormat f, int c) =>
        c == f.DecimalSeparator || c == f.GroupingSeparator || c == f.Digit || IsZeroDigit(f, c);

    private static XsltDynamicException Invalid(string why, SourceSpan location) =>
        new XsltDynamicException(XsltErrors.XTDE1310, "format-number: invalid picture string: " + why, location);

    // ── Value handling ───────────────────────────────────────────────────────

    // Splits a numeric value into sign and plain decimal digit strings (integer part without a sign).
    private static void Decompose(
        XdmAtomicValue value,
        out bool negative,
        out bool isNaN,
        out bool isInfinite,
        out string integerDigits,
        out string fractionDigits)
    {
        negative = false;
        isNaN = false;
        isInfinite = false;
        integerDigits = "0";
        fractionDigits = string.Empty;

        switch (value.TypeCode)
        {
            case XdmTypeCode.Double:
                DecomposeDouble(value.AsDouble(), value.AsDouble().ToString("R", CultureInfo.InvariantCulture), out negative, out isNaN, out isInfinite, out integerDigits, out fractionDigits);
                break;

            case XdmTypeCode.Float:
                DecomposeDouble(value.AsFloat(), value.AsFloat().ToString("R", CultureInfo.InvariantCulture), out negative, out isNaN, out isInfinite, out integerDigits, out fractionDigits);
                break;

            case XdmTypeCode.Decimal:
                SplitPlain(value.AsDecimal().ToString(), out negative, out integerDigits, out fractionDigits);
                break;

            default:
                // The integer family.
                BigInteger i = value.AsBigInteger();
                negative = i.Sign < 0;
                integerDigits = BigInteger.Abs(i).ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    private static void DecomposeDouble(
        double d,
        string text,
        out bool negative,
        out bool isNaN,
        out bool isInfinite,
        out string integerDigits,
        out string fractionDigits)
    {
        isNaN = double.IsNaN(d);
        isInfinite = double.IsInfinity(d);
        negative = d < 0 || (d == 0 && BitConverter.DoubleToInt64Bits(d) < 0);
        integerDigits = "0";
        fractionDigits = string.Empty;
        if (isNaN || isInfinite)
        {
            return;
        }

        // "R" gives forms such as 1.5E-07, 1E+20 and 123.456; expand the exponent into plain digits.
        string unsigned = text.TrimStart('-');
        int e = unsigned.IndexOf('E');
        int exponent = 0;
        string mantissa = unsigned;
        if (e >= 0)
        {
            exponent = ParseExponent(unsigned, e + 1);
            mantissa = unsigned.Substring(0, e);
        }

        int dot = mantissa.IndexOf('.');
        string digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);
        int pointPosition = (dot < 0 ? mantissa.Length : dot) + exponent;

        if (pointPosition <= 0)
        {
            integerDigits = "0";
            fractionDigits = new string('0', -pointPosition) + digits;
        }
        else if (pointPosition >= digits.Length)
        {
            integerDigits = digits + new string('0', pointPosition - digits.Length);
            fractionDigits = string.Empty;
        }
        else
        {
            integerDigits = digits.Substring(0, pointPosition);
            fractionDigits = digits.Substring(pointPosition);
        }
    }

    // Parses the signed exponent digits that follow the 'E' of a round-trip double string.
    private static int ParseExponent(string text, int start)
    {
        bool minus = false;
        int i = start;
        if (i < text.Length && (text[i] == '-' || text[i] == '+'))
        {
            minus = text[i] == '-';
            i++;
        }

        int value = 0;
        for (; i < text.Length; i++)
        {
            value = (value * 10) + (text[i] - '0');
        }

        return minus ? -value : value;
    }

    private static void SplitPlain(string plain, out bool negative, out string integerDigits, out string fractionDigits)
    {
        negative = plain.Length > 0 && plain[0] == '-';
        string unsigned = negative ? plain.Substring(1) : plain;
        int dot = unsigned.IndexOf('.');
        integerDigits = dot < 0 ? unsigned : unsigned.Substring(0, dot);
        fractionDigits = dot < 0 ? string.Empty : unsigned.Substring(dot + 1);
    }

    // Multiplies by 10^places by moving the decimal point.
    private static void ShiftDecimalPoint(ref string integerDigits, ref string fractionDigits, int places)
    {
        string fraction = fractionDigits.Length >= places ? fractionDigits : fractionDigits + new string('0', places - fractionDigits.Length);
        integerDigits += fraction.Substring(0, places);
        fractionDigits = fraction.Substring(places);
    }

    // fn:round-half-to-even to the given number of fractional digits (§16.4.4 step 5).
    private static void RoundHalfEven(ref string integerDigits, ref string fractionDigits, int maxFraction)
    {
        if (fractionDigits.Length <= maxFraction)
        {
            return;
        }

        string kept = fractionDigits.Substring(0, maxFraction);
        string dropped = fractionDigits.Substring(maxFraction);
        char first = dropped[0];
        bool restNonZero = false;
        for (int i = 1; i < dropped.Length; i++)
        {
            if (dropped[i] != '0')
            {
                restNonZero = true;
                break;
            }
        }

        string all = integerDigits + kept;
        bool roundUp;
        if (first > '5' || (first == '5' && restNonZero))
        {
            roundUp = true;
        }
        else if (first == '5')
        {
            // Exactly half: round to the even neighbour.
            char last = all.Length > 0 ? all[all.Length - 1] : '0';
            roundUp = (last - '0') % 2 == 1;
        }
        else
        {
            roundUp = false;
        }

        if (roundUp)
        {
            char[] chars = all.ToCharArray();
            int i = chars.Length - 1;
            while (i >= 0)
            {
                if (chars[i] == '9')
                {
                    chars[i] = '0';
                    i--;
                }
                else
                {
                    chars[i]++;
                    break;
                }
            }

            all = i < 0 ? "1" + new string(chars) : new string(chars);
        }

        int integerLength = all.Length - maxFraction;
        integerDigits = all.Substring(0, integerLength);
        fractionDigits = all.Substring(integerLength);
    }

    // ── Output ───────────────────────────────────────────────────────────────

    private static void AppendGroupedInteger(StringBuilder sb, string digits, SubPicture sub, DecimalFormat f)
    {
        int length = digits.Length;
        for (int i = 0; i < length; i++)
        {
            AppendDigit(sb, digits[i], f);
            int rightOfThis = length - 1 - i;
            if (rightOfThis > 0 && IsIntegerGroupingPosition(sub, rightOfThis))
            {
                AppendCodePoint(sb, f.GroupingSeparator);
            }
        }
    }

    private static bool IsIntegerGroupingPosition(SubPicture sub, int position) =>
        sub.IntegerGrouping.Contains(position) ||
        (sub.IntegerGroupingInterval > 0 && position % sub.IntegerGroupingInterval == 0);

    private static void AppendGroupedFraction(StringBuilder sb, string digits, SubPicture sub, DecimalFormat f)
    {
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && sub.FractionGrouping.Contains(i))
            {
                AppendCodePoint(sb, f.GroupingSeparator);
            }

            AppendDigit(sb, digits[i], f);
        }
    }

    private static void AppendDigit(StringBuilder sb, char ascii, DecimalFormat f) =>
        AppendCodePoint(sb, f.ZeroDigit + (ascii - '0'));

    private static void AppendCodePoint(StringBuilder sb, int codePoint) =>
        sb.Append(codePoint is >= 0xD800 and <= 0xDFFF ? ((char)codePoint).ToString() : char.ConvertFromUtf32(codePoint));

    // ── Code point helpers ───────────────────────────────────────────────────

    private static int[] ToCodePoints(string s)
    {
        List<int> list = new List<int>(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                list.Add(char.ConvertToUtf32(s[i], s[i + 1]));
                i++;
            }
            else
            {
                list.Add(s[i]);
            }
        }

        return list.ToArray();
    }

    private static string Slice(int[] cps, int start, int end)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = start; i < end; i++)
        {
            AppendCodePoint(sb, cps[i]);
        }

        return sb.ToString();
    }
}
