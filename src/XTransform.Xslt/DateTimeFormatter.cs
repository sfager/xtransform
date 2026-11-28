// XSLT 2.0 §16.5 — format-dateTime, format-date and format-time: picture parsing and formatting.
//
// The picture is literal text and [markers]. A marker is a component specifier, up to two presentation
// modifiers and a width modifier. English is the only language implemented; other languages, calendars and
// countries fall back to English / the Gregorian calendar (recorded in docs/implementation-defined.md).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Xslt;

internal static class DateTimeFormatter
{
    private static readonly string[] s_months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    // Monday first: the numeric day of week is ISO (Monday = 1 … Sunday = 7).
    private static readonly string[] s_days =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    };

    // ── Entry point ──────────────────────────────────────────────────────────

    /// <summary>Formats <paramref name="value"/> (kind date, time or dateTime) per <paramref name="picture"/>.</summary>
    internal static string Format(
        XsdDateTime value,
        string picture,
        string? language,
        string? calendar,
        string? country,
        SourceSpan location)
    {
        string lang = ResolveLanguage(language);
        string calendarName = ResolveCalendar(calendar, location, out bool calendarFallback);
        StringBuilder sb = new StringBuilder();

        // A language or calendar that is valid but not supported is identified in the output (§16.5.2).
        if (!string.IsNullOrEmpty(language) && lang != "en")
        {
            sb.Append("[Language: en]");
        }

        if (calendarFallback)
        {
            sb.Append("[Calendar: ").Append(calendarName).Append(']');
        }

        int i = 0;
        while (i < picture.Length)
        {
            char c = picture[i];
            if (c == '[')
            {
                if (i + 1 < picture.Length && picture[i + 1] == '[')
                {
                    sb.Append('[');
                    i += 2;
                    continue;
                }

                int close = picture.IndexOf(']', i + 1);
                if (close < 0)
                {
                    throw Invalid("a '[' has no closing ']'", location);
                }

                sb.Append(FormatMarker(value, picture.Substring(i + 1, close - i - 1), lang, calendarName, location));
                i = close + 1;
            }
            else if (c == ']')
            {
                if (i + 1 < picture.Length && picture[i + 1] == ']')
                {
                    sb.Append(']');
                    i += 2;
                    continue;
                }

                throw Invalid("a ']' without a matching '['", location);
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static XsltDynamicException Invalid(string why, SourceSpan location) =>
        new XsltDynamicException(XsltErrors.XTDE1340, "Invalid date/time picture string: " + why, location);

    // ── Language and calendar (§16.5.2) ──────────────────────────────────────

    private static string ResolveLanguage(string? language) =>
        string.IsNullOrEmpty(language) ? "en" : NumberWords.NormaliseLanguage(language);

    // Unprefixed calendar names come from the designator table; only the Gregorian ones (AD, CE, ISO) are
    // supported, anything else valid falls back to AD and is reported in the output.
    private static string ResolveCalendar(string? calendar, SourceSpan location, out bool fallback)
    {
        fallback = false;
        if (string.IsNullOrEmpty(calendar))
        {
            return "AD";
        }

        string lexical = calendar!.Trim();
        int colon = lexical.IndexOf(':');
        string local = colon < 0 ? lexical : lexical.Substring(colon + 1);
        string prefix = colon < 0 ? string.Empty : lexical.Substring(0, colon);
        if (!IsNcName(local) || (colon >= 0 && !IsNcName(prefix)))
        {
            throw Invalid($"'{calendar}' is not a valid calendar name", location);
        }

        if (colon < 0 && (local == "AD" || local == "CE" || local == "ISO"))
        {
            return local;
        }

        fallback = true;
        return "AD";
    }

    private static bool IsNcName(string text) =>
        text.Length > 0 && XdmAtomicValue.TryParse(text, XdmTypeCode.NcName, out _);

    // ── One marker ───────────────────────────────────────────────────────────

    private static string FormatMarker(XsdDateTime v, string rawMarker, string lang, string calendar, SourceSpan location)
    {
        // Whitespace inside a marker is ignored.
        StringBuilder compact = new StringBuilder(rawMarker.Length);
        foreach (char ch in rawMarker)
        {
            if (!char.IsWhiteSpace(ch))
            {
                compact.Append(ch);
            }
        }

        string marker = compact.ToString();
        if (marker.Length == 0)
        {
            throw Invalid("an empty marker", location);
        }

        char component = marker[0];
        if ("YMDdFWwHhPmsfZzCE".IndexOf(component) < 0)
        {
            throw Invalid($"'{component}' is not a component specifier", location);
        }

        // Modifiers, then the optional width: "Y0001", "MNn", "D1o", "Y,2-2".
        string rest = marker.Substring(1);
        string modifiers = rest;
        string? widthText = null;
        int comma = rest.IndexOf(',');
        if (comma >= 0)
        {
            modifiers = rest.Substring(0, comma);
            widthText = rest.Substring(comma + 1);
        }

        bool ordinal = false;
        bool traditional = false;
        if (modifiers.Length > 0 && (modifiers[modifiers.Length - 1] == 'o' || modifiers[modifiers.Length - 1] == 't'))
        {
            ordinal = modifiers[modifiers.Length - 1] == 'o';
            traditional = !ordinal;
            modifiers = modifiers.Substring(0, modifiers.Length - 1);
        }

        string token = modifiers.Length == 0 ? DefaultModifier(component) : modifiers;

        int minWidth = 1;
        int maxWidth = int.MaxValue;
        bool explicitWidth = false;
        if (widthText is not null)
        {
            ParseWidth(widthText, out minWidth, out maxWidth, location);
            explicitWidth = true;
        }

        // A format token with leading zeros ("01", "0001") fixes the width, unless a width is given.
        bool digitToken = TryGetDigitToken(token, out int tokenDigits, out int zeroDigit);
        if (digitToken && !explicitWidth && tokenDigits > 1)
        {
            minWidth = tokenDigits;
            maxWidth = tokenDigits;
        }

        bool isNameToken = token == "n" || token == "N" || token == "Nn";
        _ = traditional;

        return FormatComponent(v, component, token, isNameToken, ordinal, minWidth, maxWidth, lang, calendar, zeroDigit, location);
    }

    private static string DefaultModifier(char component) => component switch
    {
        'm' or 's' => "01",
        'F' or 'P' or 'C' or 'E' => "n",
        _ => "1",
    };

    private static void ParseWidth(string text, out int min, out int max, SourceSpan location)
    {
        min = 1;
        max = int.MaxValue;
        int dash = text.IndexOf('-');
        string minText = dash < 0 ? text : text.Substring(0, dash);
        string? maxText = dash < 0 ? null : text.Substring(dash + 1);

        if (!TryParseWidthValue(minText, out int parsedMin))
        {
            throw Invalid($"'{text}' is not a valid width modifier", location);
        }

        min = parsedMin == int.MaxValue ? 1 : parsedMin;
        if (maxText is not null && !TryParseWidthValue(maxText, out max))
        {
            throw Invalid($"'{text}' is not a valid width modifier", location);
        }

        if (max < min)
        {
            throw Invalid($"the maximum width is smaller than the minimum in '{text}'", location);
        }
    }

    // A width value is an integer greater than zero, or "*" (no limit).
    private static bool TryParseWidthValue(string text, out int value)
    {
        value = int.MaxValue;
        if (text == "*")
        {
            return true;
        }

        if (text.Length == 0 || text.Length > 6)
        {
            return false;
        }

        int n = 0;
        foreach (char c in text)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }

            n = (n * 10) + (c - '0');
        }

        if (n == 0)
        {
            return false;
        }

        value = n;
        return true;
    }

    // A decimal-digit format token: every character is a digit of one family and the last has value 1 ("1",
    // "01", "001", or the same in another script). Returns its length and the zero digit of the family.
    private static bool TryGetDigitToken(string token, out int length, out int zeroDigit)
    {
        length = 0;
        zeroDigit = '0';
        int lastValue = -1;
        int lastCodePoint = 0;
        for (int i = 0; i < token.Length; i++)
        {
            int cp = char.ConvertToUtf32(token, i);
            if (char.IsHighSurrogate(token[i]))
            {
                i++;
            }

            string s = char.ConvertFromUtf32(cp);
            if (CharUnicodeInfo.GetUnicodeCategory(s, 0) != UnicodeCategory.DecimalDigitNumber)
            {
                return false;
            }

            lastValue = CharUnicodeInfo.GetDecimalDigitValue(s, 0);
            lastCodePoint = cp;
            length++;
        }

        if (length == 0 || lastValue != 1)
        {
            return false;
        }

        zeroDigit = lastCodePoint - 1;
        return true;
    }

    // ── Components ───────────────────────────────────────────────────────────

    private static string FormatComponent(
        XsdDateTime v,
        char component,
        string token,
        bool isNameToken,
        bool ordinal,
        int minWidth,
        int maxWidth,
        string lang,
        string calendar,
        int zeroDigit,
        SourceSpan location)
    {
        bool hasDate = v.Kind is XsdDateTimeKind.Date or XsdDateTimeKind.DateTime;
        bool hasTime = v.Kind is XsdDateTimeKind.Time or XsdDateTimeKind.DateTime;

        // XTDE1350: a component the value does not have.
        bool dateComponent = component is 'Y' or 'M' or 'D' or 'd' or 'F' or 'W' or 'w' or 'C' or 'E';
        bool timeComponent = component is 'H' or 'h' or 'P' or 'm' or 's' or 'f';
        if ((dateComponent && !hasDate) || (timeComponent && !hasTime))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1350,
                $"The component '{component}' is not available in a value of this type",
                location);
        }

        switch (component)
        {
            case 'Y':
                return Numeric(Math.Abs(v.Year), token, ordinal, lang, minWidth, maxWidth, zeroDigit, truncateHighOrder: true);
            case 'M':
                return isNameToken ? Name(s_months[v.Month - 1], token, minWidth, maxWidth)
                                   : Numeric(v.Month, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'D':
                return Numeric(v.Day, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'd':
                return Numeric(DayOfYear(v), token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'F':
            {
                int dow = DayOfWeek(v);
                return isNameToken ? Name(s_days[dow - 1], token, minWidth, maxWidth)
                                   : Numeric(dow, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            }

            case 'W':
                return Numeric(IsoWeekOfYear(v), token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'w':
                return Numeric(WeekOfMonth(v), token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'H':
                return Numeric(v.Hour, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'h':
            {
                int h12 = v.Hour % 12;
                return Numeric(h12 == 0 ? 12 : h12, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            }

            case 'P':
                return Name(v.Hour < 12 ? "am" : "pm", isNameToken ? token : "n", minWidth, maxWidth);
            case 'm':
                return Numeric(v.Minute, token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 's':
                return Numeric(WholeSeconds(v), token, ordinal, lang, minWidth, maxWidth, zeroDigit);
            case 'f':
                return FractionalSeconds(v, minWidth, maxWidth, zeroDigit);
            case 'Z':
            case 'z':
                return TimeZone(v, component == 'z', token == "0", minWidth, maxWidth);
            case 'C':
                return Name(calendar, isNameToken ? token : "n", minWidth, maxWidth);
            default:
                // 'E': the era.
                return Name(v.Year < 0 ? "BC" : "AD", isNameToken ? token : "n", minWidth, maxWidth);
        }
    }

    // A name in the case the modifier asks for: N upper, n lower, Nn title. Too long is shortened: three letters
    // is the first three of the name, four letters uses the conventional abbreviation (Tues, Weds, Thur, Sept),
    // anything else is right-truncated. Too short is padded with trailing spaces (§16.5.1).
    private static string Name(string name, string token, int minWidth, int maxWidth)
    {
        string shortened = name.Length > maxWidth ? Abbreviate(name, maxWidth) : name;
        string text = token switch
        {
            "N" => shortened.ToUpperInvariant(),
            "Nn" => char.ToUpperInvariant(shortened[0]) + shortened.Substring(1).ToLowerInvariant(),
            _ => shortened.ToLowerInvariant(),
        };

        return text.Length < minWidth ? text + new string(' ', minWidth - text.Length) : text;
    }

    // Conventional English abbreviations, used when the maximum width is 5 or less; wider limits truncate.
    private static string Abbreviate(string name, int maxWidth)
    {
        if (maxWidth <= 5)
        {
            string conventional = name switch
            {
                "Monday" => "Mon",
                "Tuesday" => "Tues",
                "Wednesday" => "Weds",
                "Thursday" => "Thurs",
                "Friday" => "Fri",
                "Saturday" => "Sat",
                "Sunday" => "Sun",
                "June" => "June",
                "July" => "July",
                "September" => "Sept",
                _ => name.Substring(0, Math.Min(3, name.Length)),
            };

            return conventional.Length <= maxWidth ? conventional : conventional.Substring(0, maxWidth);
        }

        return name.Substring(0, maxWidth);
    }

    private static string Numeric(
        long n,
        string token,
        bool ordinal,
        string lang,
        int minWidth,
        int maxWidth,
        int zeroDigit,
        bool truncateHighOrder = false)
    {
        // A name token on a numeric-only component, or an unknown token, uses the default numeric form.
        string t = token is "n" or "N" or "Nn" ? "1" : token;
        string text = NumberFormatter.Format(new[] { n }, t, lang, null, ordinal ? "yes" : null, null, 0);

        bool allDigits = IsAllDigits(text);

        // Y: the maximum width drops high-order digits (2003 with a maximum of 2 is "03").
        if (truncateHighOrder && allDigits && CodePointLength(text) > maxWidth)
        {
            text = Tail(text, maxWidth);
        }

        int length = CodePointLength(text);
        if (length < minWidth)
        {
            // Decimals get leading zeros, anything else trailing spaces.
            text = allDigits
                ? Repeat(zeroDigit, minWidth - length) + text
                : text + new string(' ', minWidth - length);
        }

        return text;
    }

    private static string FractionalSeconds(XsdDateTime v, int minWidth, int maxWidth, int zeroDigit)
    {
        string plain = v.Second.ToString();
        int dot = plain.IndexOf('.');
        string digits = dot < 0 ? string.Empty : plain.Substring(dot + 1);

        if (digits.Length > maxWidth)
        {
            digits = RoundFraction(digits, maxWidth);
        }

        if (digits.Length == 0)
        {
            digits = "0";
        }

        if (digits.Length < minWidth)
        {
            digits += new string('0', minWidth - digits.Length);
        }

        return MapDigits(digits, zeroDigit);
    }

    // Rounds a digit string to the given length (half to even); a carry out of the top is truncated.
    private static string RoundFraction(string digits, int length)
    {
        string kept = digits.Substring(0, length);
        char next = digits[length];
        bool restNonZero = false;
        for (int i = length + 1; i < digits.Length; i++)
        {
            if (digits[i] != '0')
            {
                restNonZero = true;
                break;
            }
        }

        bool up = next > '5' || (next == '5' && (restNonZero || (length > 0 && ((kept[length - 1] - '0') % 2) == 1)));
        if (!up || length == 0)
        {
            return kept;
        }

        char[] chars = kept.ToCharArray();
        for (int i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] == '9')
            {
                chars[i] = '0';
            }
            else
            {
                chars[i]++;
                return new string(chars);
            }
        }

        // The carry would reach the seconds; keep the largest value that fits.
        return new string('9', length);
    }

    private static string MapDigits(string ascii, int zeroDigit)
    {
        if (zeroDigit == '0')
        {
            return ascii;
        }

        StringBuilder sb = new StringBuilder();
        foreach (char c in ascii)
        {
            sb.Append(char.ConvertFromUtf32(zeroDigit + (c - '0')));
        }

        return sb.ToString();
    }

    // ── Timezone ─────────────────────────────────────────────────────────────

    // Z is the offset from UTC and z the same with a "GMT" prefix. Both are "+hh:mm" by default; a maximum width of
    // 2 or less gives just the hours when the offset is a whole number of hours (§16.5.1).
    private static string TimeZone(XsdDateTime v, bool gmtPrefix, bool shortForm, int minWidth, int maxWidth)
    {
        if (v.TimezoneMinutes is null)
        {
            return string.Empty;
        }

        int total = v.TimezoneMinutes.Value;
        string sign = total < 0 ? "-" : "+";
        int abs = Math.Abs(total);
        int hours = abs / 60;
        int minutes = abs % 60;

        string body;
        if (shortForm || (minutes == 0 && maxWidth <= 2))
        {
            // Hours without padding, and minutes only when there are some ("-9:30", "-14").
            body = hours.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(minWidth, 1), '0')
                + (minutes != 0 ? ":" + minutes.ToString("00", CultureInfo.InvariantCulture) : string.Empty);
        }
        else
        {
            body = hours.ToString("00", CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture);
        }

        return (gmtPrefix ? "GMT" : string.Empty) + sign + body;
    }

    // ── Calendar arithmetic (proleptic Gregorian; XSD years have no year zero) ──

    private static long AstronomicalYear(XsdDateTime v) => v.Year < 0 ? v.Year + 1 : v.Year;

    private static long DaysFromCivil(long year, int month, int day)
    {
        long y = month <= 2 ? year - 1 : year;
        long era = (y >= 0 ? y : y - 399) / 400;
        long yoe = y - (era * 400);
        long doy = ((153 * (month + (month > 2 ? -3 : 9))) + 2) / 5 + day - 1;
        long doe = (yoe * 365) + (yoe / 4) - (yoe / 100) + doy;
        return (era * 146097) + doe - 719468;
    }

    // ISO day of week: Monday = 1 … Sunday = 7.
    private static int DayOfWeek(XsdDateTime v)
    {
        long days = DaysFromCivil(AstronomicalYear(v), v.Month, v.Day);
        long dow = ((days + 3) % 7 + 7) % 7; // 1970-01-01 was a Thursday
        return (int)dow + 1;
    }

    private static bool IsLeap(long year) => (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    private static int DayOfYear(XsdDateTime v)
    {
        long y = AstronomicalYear(v);
        return (int)(DaysFromCivil(y, v.Month, v.Day) - DaysFromCivil(y, 1, 1)) + 1;
    }

    private static int WeeksInIsoYear(long year)
    {
        static long P(long y) => ((y + (y / 4) - (y / 100) + (y / 400)) % 7 + 7) % 7;
        return P(year) == 4 || P(year - 1) == 3 ? 53 : 52;
    }

    private static int IsoWeekOfYear(XsdDateTime v)
    {
        long y = AstronomicalYear(v);
        int week = (DayOfYear(v) - DayOfWeek(v) + 10) / 7;
        if (week < 1)
        {
            return WeeksInIsoYear(y - 1);
        }

        if (week > WeeksInIsoYear(y))
        {
            return 1;
        }

        return week;
    }

    // Week in the month: week 1 is the Monday-to-Sunday week that holds the month's first Thursday, so it
    // starts on the Monday on or before that Thursday. Days before it belong to the previous month's last week;
    // days after the month's end stay in this month's numbering (a week can spill into the next month).
    private static int WeekOfMonth(XsdDateTime v)
    {
        long year = AstronomicalYear(v);
        int month = v.Month;
        long days = DaysFromCivil(year, month, v.Day);
        long week1Start = WeekOneStart(year, month);
        if (days < week1Start)
        {
            if (month == 1)
            {
                year--;
                month = 12;
            }
            else
            {
                month--;
            }

            week1Start = WeekOneStart(year, month);
        }

        return (int)((days - week1Start) / 7) + 1;
    }

    private static long WeekOneStart(long year, int month)
    {
        long first = DaysFromCivil(year, month, 1);
        long firstDow = ((first + 3) % 7 + 7) % 7; // Monday = 0
        long firstThursday = first + (((3 - firstDow) % 7) + 7) % 7;
        return firstThursday - 3;
    }

    private static long WholeSeconds(XsdDateTime v)
    {
        string plain = v.Second.ToString();
        int dot = plain.IndexOf('.');
        return long.Parse(dot < 0 ? plain : plain.Substring(0, dot), CultureInfo.InvariantCulture);
    }

    // ── String helpers (code points) ─────────────────────────────────────────

    private static bool IsAllDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            int cp = char.ConvertToUtf32(text, i);
            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0) != UnicodeCategory.DecimalDigitNumber)
            {
                return false;
            }
        }

        return true;
    }

    private static int CodePointLength(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length)
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static string Tail(string text, int codePoints)
    {
        List<string> parts = new List<string>();
        for (int i = 0; i < text.Length; i++)
        {
            int len = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            parts.Add(text.Substring(i, len));
            i += len - 1;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = Math.Max(0, parts.Count - codePoints); i < parts.Count; i++)
        {
            sb.Append(parts[i]);
        }

        return sb.ToString();
    }

    private static string Repeat(int codePoint, int count)
    {
        StringBuilder sb = new StringBuilder();
        string s = char.ConvertFromUtf32(codePoint);
        for (int i = 0; i < count; i++)
        {
            sb.Append(s);
        }

        return sb.ToString();
    }
}
