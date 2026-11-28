using System;
using System.Globalization;
using System.Text;

namespace XTransform.Xdm;

// XSD 1.0 Part 2 §3.2.7–3.2.14 — the eight date/time primitive types
/// <summary>
/// Immutable value type representing one of the eight XSD date/time primitive types.
/// Does not use <see cref="System.DateTime"/> for storage; stores raw components.
/// </summary>
public readonly struct XsdDateTime : IEquatable<XsdDateTime>
{
    private readonly XsdDateTimeKind _kind;
    private readonly long _year;
    private readonly int _month;
    private readonly int _day;
    private readonly int _hour;
    private readonly int _minute;
    private readonly XsdDecimal _second;
    private readonly int? _timezoneMinutes;

    /// <summary>
    /// Initializes a new <see cref="XsdDateTime"/> with explicit component values.
    /// </summary>
    /// <param name="kind">Which of the eight date/time types this represents.</param>
    /// <param name="year">Year component (may be negative or &gt; 9999). Year 0 is the XSD proleptic year 0.</param>
    /// <param name="month">Month (1–12), or 0 when not applicable for the given kind.</param>
    /// <param name="day">Day (1–31), or 0 when not applicable for the given kind.</param>
    /// <param name="hour">Hour (0–24), or 0 when not applicable.</param>
    /// <param name="minute">Minute (0–59), or 0 when not applicable.</param>
    /// <param name="second">Second (0–60), or 0 when not applicable.</param>
    /// <param name="timezoneMinutes">Timezone offset in minutes, or <see langword="null"/> for no timezone. Z = 0.</param>
    public XsdDateTime(
        XsdDateTimeKind kind,
        long year,
        int month,
        int day,
        int hour,
        int minute,
        XsdDecimal second,
        int? timezoneMinutes)
    {
        _kind = kind;
        _year = year;
        _month = month;
        _day = day;
        _hour = hour;
        _minute = minute;
        _second = second;
        _timezoneMinutes = timezoneMinutes;
    }

    /// <summary>Which of the eight XSD date/time types this value represents.</summary>
    public XsdDateTimeKind Kind => _kind;

    /// <summary>Year component. May be negative (proleptic Gregorian) or exceed 9999.</summary>
    public long Year => _year;

    /// <summary>Month component (1–12), or 0 when not applicable for the kind.</summary>
    public int Month => _month;

    /// <summary>Day component (1–31), or 0 when not applicable for the kind.</summary>
    public int Day => _day;

    /// <summary>Hour component (0–24), or 0 when not applicable for the kind.</summary>
    public int Hour => _hour;

    /// <summary>Minute component (0–59), or 0 when not applicable for the kind.</summary>
    public int Minute => _minute;

    /// <summary>Second component, including fractional seconds. 0 when not applicable.</summary>
    public XsdDecimal Second => _second;

    /// <summary>Timezone offset in minutes from UTC, or <see langword="null"/> when not present. Z = 0.</summary>
    public int? TimezoneMinutes => _timezoneMinutes;

    /// <summary><see langword="true"/> when a timezone designator was present in the lexical form.</summary>
    public bool HasTimezone => _timezoneMinutes.HasValue;

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses an XSD date/time lexical form for the given <paramref name="kind"/>.
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORG0001"/> on failure.
    /// </summary>
    public static XsdDateTime Parse(string lexical, XsdDateTimeKind kind)
    {
        if (!TryParse(lexical, kind, out XsdDateTime result))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0001,
                $"Invalid xs:{KindName(kind)} lexical value: '{lexical}'.",
                SourceSpan.None);
        }

        return result;
    }

    /// <summary>
    /// Tries to parse an XSD date/time lexical form for the given <paramref name="kind"/>.
    /// Returns <see langword="false"/> without throwing on failure.
    /// </summary>
    public static bool TryParse(string lexical, XsdDateTimeKind kind, out XsdDateTime result)
    {
        result = default;

        if (string.IsNullOrEmpty(lexical))
        {
            return false;
        }

        int pos = 0;

        long year = 0;
        int month = 0;
        int day = 0;
        int hour = 0;
        int minute = 0;
        XsdDecimal second = default;
        int? timezoneMinutes = null;

        switch (kind)
        {
            case XsdDateTimeKind.DateTime:
                // [-]YYYY...-MM-DDTHH:mm:ss[.fff*][tz]
                if (!TryParseYear(lexical, ref pos, out year)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out month)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out day)) { return false; }
                if (!Expect(lexical, ref pos, 'T')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out hour)) { return false; }
                if (!Expect(lexical, ref pos, ':')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out minute)) { return false; }
                if (!Expect(lexical, ref pos, ':')) { return false; }
                if (!TryParseSecond(lexical, ref pos, out second)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (!ValidateDateTimeComponents(hour, minute, second, month, day)) { return false; }
                if (day > DaysInMonth(year, month)) { return false; }
                break;

            case XsdDateTimeKind.Date:
                // [-]YYYY...-MM-DD[tz]
                if (!TryParseYear(lexical, ref pos, out year)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out month)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out day)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (month < 1 || month > 12) { return false; }
                if (day < 1 || day > 31) { return false; }
                if (day > DaysInMonth(year, month)) { return false; }
                break;

            case XsdDateTimeKind.Time:
                // HH:mm:ss[.fff*][tz]
                if (!TryParseTwoDigit(lexical, ref pos, out hour)) { return false; }
                if (!Expect(lexical, ref pos, ':')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out minute)) { return false; }
                if (!Expect(lexical, ref pos, ':')) { return false; }
                if (!TryParseSecond(lexical, ref pos, out second)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (!ValidateTimeComponents(hour, minute, second)) { return false; }
                break;

            case XsdDateTimeKind.GYearMonth:
                // [-]YYYY...-MM[tz]
                if (!TryParseYear(lexical, ref pos, out year)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out month)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (month < 1 || month > 12) { return false; }
                break;

            case XsdDateTimeKind.GYear:
                // [-]YYYY...[tz]
                if (!TryParseYear(lexical, ref pos, out year)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                break;

            case XsdDateTimeKind.GMonthDay:
                // --MM-DD[tz]
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out month)) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out day)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (month < 1 || month > 12) { return false; }
                if (day < 1 || day > 31) { return false; }
                if (day > DaysInMonth(2000, month)) { return false; }   // no year: February 29 is allowed
                break;

            case XsdDateTimeKind.GDay:
                // ---DD[tz]
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out day)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (day < 1 || day > 31) { return false; }
                break;

            case XsdDateTimeKind.GMonth:
                // --MM[tz]  (note: gMonth uses --MM, NOT --MM--)
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!Expect(lexical, ref pos, '-')) { return false; }
                if (!TryParseTwoDigit(lexical, ref pos, out month)) { return false; }
                if (!TryParseTimezone(lexical, ref pos, out timezoneMinutes)) { return false; }
                if (pos != lexical.Length) { return false; }
                if (month < 1 || month > 12) { return false; }
                break;

            default:
                return false;
        }

        result = new XsdDateTime(kind, year, month, day, hour, minute, second, timezoneMinutes);
        return true;
    }

    // -------------------------------------------------------------------------
    // Canonical serialisation
    // -------------------------------------------------------------------------

    // F&O §17.1.2 and XSD §3.2.7.2 etc.
    /// <summary>
    /// Returns the XSD canonical lexical representation for this date/time value.
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(32);

        switch (_kind)
        {
            case XsdDateTimeKind.DateTime:
                AppendYear(sb, _year);
                sb.Append('-');
                AppendTwoDigit(sb, _month);
                sb.Append('-');
                AppendTwoDigit(sb, _day);
                sb.Append('T');
                AppendTwoDigit(sb, _hour);
                sb.Append(':');
                AppendTwoDigit(sb, _minute);
                sb.Append(':');
                AppendSeconds(sb, _second);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.Date:
                AppendYear(sb, _year);
                sb.Append('-');
                AppendTwoDigit(sb, _month);
                sb.Append('-');
                AppendTwoDigit(sb, _day);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.Time:
                AppendTwoDigit(sb, _hour);
                sb.Append(':');
                AppendTwoDigit(sb, _minute);
                sb.Append(':');
                AppendSeconds(sb, _second);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.GYearMonth:
                AppendYear(sb, _year);
                sb.Append('-');
                AppendTwoDigit(sb, _month);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.GYear:
                AppendYear(sb, _year);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.GMonthDay:
                sb.Append("--");
                AppendTwoDigit(sb, _month);
                sb.Append('-');
                AppendTwoDigit(sb, _day);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.GDay:
                sb.Append("---");
                AppendTwoDigit(sb, _day);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            case XsdDateTimeKind.GMonth:
                sb.Append("--");
                AppendTwoDigit(sb, _month);
                AppendTimezone(sb, _timezoneMinutes);
                break;

            default:
                break;
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public bool Equals(XsdDateTime other)
    {
        return _kind == other._kind
            && _year == other._year
            && _month == other._month
            && _day == other._day
            && _hour == other._hour
            && _minute == other._minute
            && _second == other._second
            && _timezoneMinutes == other._timezoneMinutes;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is XsdDateTime other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int h = 17;
        h = h * 31 + _kind.GetHashCode();
        h = h * 31 + _year.GetHashCode();
        h = h * 31 + _month.GetHashCode();
        h = h * 31 + _day.GetHashCode();
        h = h * 31 + _hour.GetHashCode();
        h = h * 31 + _minute.GetHashCode();
        h = h * 31 + _second.GetHashCode();
        h = h * 31 + _timezoneMinutes.GetHashCode();
        return h;
    }

    /// <inheritdoc />
    public static bool operator ==(XsdDateTime l, XsdDateTime r) => l.Equals(r);

    /// <inheritdoc />
    public static bool operator !=(XsdDateTime l, XsdDateTime r) => !l.Equals(r);

    // -------------------------------------------------------------------------
    // Private helpers — parsing
    // -------------------------------------------------------------------------

    private static bool Expect(string s, ref int pos, char expected)
    {
        if (pos >= s.Length || s[pos] != expected)
        {
            return false;
        }

        pos++;
        return true;
    }

    // XSD 1.0 Part 2 §3.2.7 — year allows leading minus and may be more than 4 digits
    private static bool TryParseYear(string s, ref int pos, out long year)
    {
        year = 0;
        bool negative = false;

        if (pos < s.Length && s[pos] == '-')
        {
            negative = true;
            pos++;
        }

        // Must have at least 4 digits
        int start = pos;
        while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
        {
            pos++;
        }

        int length = pos - start;
        if (length < 4)
        {
            return false;
        }

        // Leading zeros only permitted for 4-digit years, and "0000" is a valid year
        // A 5+ digit year must not start with 0
        if (length > 4 && s[start] == '0')
        {
            return false;
        }

        string digits = s.Substring(start, length);
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            return false;
        }

        if (negative)
        {
            year = -year;
        }

        return true;
    }

    private static bool TryParseTwoDigit(string s, ref int pos, out int value)
    {
        value = 0;

        if (pos + 1 >= s.Length)
        {
            return false;
        }

        char c0 = s[pos];
        char c1 = s[pos + 1];

        if (c0 < '0' || c0 > '9' || c1 < '0' || c1 > '9')
        {
            return false;
        }

        value = (c0 - '0') * 10 + (c1 - '0');
        pos += 2;
        return true;
    }

    private static bool TryParseSecond(string s, ref int pos, out XsdDecimal second)
    {
        second = default;

        // Parse integer part (exactly 2 digits)
        if (pos + 1 >= s.Length)
        {
            return false;
        }

        char c0 = s[pos];
        char c1 = s[pos + 1];

        if (c0 < '0' || c0 > '9' || c1 < '0' || c1 > '9')
        {
            return false;
        }

        int intPart = (c0 - '0') * 10 + (c1 - '0');
        pos += 2;

        // Optional fractional part
        if (pos < s.Length && s[pos] == '.')
        {
            int dotPos = pos;
            pos++;
            int fracStart = pos;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
            {
                pos++;
            }

            if (pos == fracStart)
            {
                // dot with no digits after
                pos = dotPos; // back up — treat the dot as not part of seconds
                second = new XsdDecimal(intPart);
                return true;
            }

            string numStr = s.Substring(dotPos - 2, pos - (dotPos - 2));
            if (!decimal.TryParse(numStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d))
            {
                return false;
            }

            second = new XsdDecimal(d);
        }
        else
        {
            second = new XsdDecimal(intPart);
        }

        return true;
    }

    private static bool TryParseTimezone(string s, ref int pos, out int? timezoneMinutes)
    {
        timezoneMinutes = null;

        if (pos >= s.Length)
        {
            return true; // no timezone, that's fine
        }

        char c = s[pos];

        if (c == 'Z')
        {
            timezoneMinutes = 0;
            pos++;
            return true;
        }

        if (c == '+' || c == '-')
        {
            pos++;
            int tzHour, tzMin;

            if (!TryParseTwoDigit(s, ref pos, out tzHour)) { return false; }
            if (!Expect(s, ref pos, ':')) { return false; }
            if (!TryParseTwoDigit(s, ref pos, out tzMin)) { return false; }

            if (tzHour > 14 || tzMin > 59 || (tzHour == 14 && tzMin != 0))
            {
                return false;
            }

            int offset = tzHour * 60 + tzMin;
            timezoneMinutes = c == '-' ? -offset : offset;
            return true;
        }

        return true; // character not consumed, no timezone
    }

    private static bool ValidateTimeComponents(int hour, int minute, XsdDecimal second)
    {
        decimal secVal = (decimal)second;
        // 24:00:00 is valid (end of day)
        if (hour == 24)
        {
            return minute == 0 && secVal == 0m;
        }

        return hour <= 23 && minute <= 59 && secVal >= 0m && secVal < 61m;
    }

    // XML Schema Part 2 §3.2.7: the day must exist in the month (leap years included).
    private static int DaysInMonth(long year, int month)
    {
        if (month == 2)
        {
            bool leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
            return leap ? 29 : 28;
        }

        return month == 4 || month == 6 || month == 9 || month == 11 ? 30 : 31;
    }

    private static bool ValidateDateTimeComponents(int hour, int minute, XsdDecimal second, int month, int day)
    {
        if (month < 1 || month > 12) { return false; }
        if (day < 1 || day > 31) { return false; }
        return ValidateTimeComponents(hour, minute, second);
    }

    // -------------------------------------------------------------------------
    // Private helpers — serialisation
    // -------------------------------------------------------------------------

    private static void AppendYear(StringBuilder sb, long year)
    {
        if (year < 0)
        {
            sb.Append('-');
            // year 0 is "-0000"
            string s = Math.Abs(year).ToString("D4", CultureInfo.InvariantCulture);
            sb.Append(s);
        }
        else
        {
            string s = year.ToString("D4", CultureInfo.InvariantCulture);
            sb.Append(s);
        }
    }

    private static void AppendTwoDigit(StringBuilder sb, int value)
    {
        sb.Append(value.ToString("D2", CultureInfo.InvariantCulture));
    }

    private static void AppendSeconds(StringBuilder sb, XsdDecimal second)
    {
        // The seconds field must have the integer part 0-padded to 2 digits
        // but XsdDecimal.ToString() may produce "5.5" — we need "05.5"
        decimal val = (decimal)second;
        decimal intPart = Math.Floor(val);
        decimal fracPart = val - intPart;

        sb.Append(((long)intPart).ToString("D2", CultureInfo.InvariantCulture));

        if (fracPart != 0m)
        {
            // Use XsdDecimal canonical form for the fractional part string, then strip leading "0"
            string fracStr = new XsdDecimal(fracPart).ToString();
            // fracStr is "0.xxx" — append ".xxx"
            sb.Append(fracStr, 1, fracStr.Length - 1);
        }
    }

    private static void AppendTimezone(StringBuilder sb, int? timezoneMinutes)
    {
        if (!timezoneMinutes.HasValue)
        {
            return;
        }

        int tz = timezoneMinutes.Value;

        if (tz == 0)
        {
            sb.Append('Z');
            return;
        }

        if (tz > 0)
        {
            sb.Append('+');
        }
        else
        {
            sb.Append('-');
            tz = -tz;
        }

        int tzHour = tz / 60;
        int tzMin = tz % 60;
        sb.Append(tzHour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(tzMin.ToString("D2", CultureInfo.InvariantCulture));
    }

    private static string KindName(XsdDateTimeKind kind)
    {
        switch (kind)
        {
            case XsdDateTimeKind.DateTime: return "dateTime";
            case XsdDateTimeKind.Date: return "date";
            case XsdDateTimeKind.Time: return "time";
            case XsdDateTimeKind.GYearMonth: return "gYearMonth";
            case XsdDateTimeKind.GYear: return "gYear";
            case XsdDateTimeKind.GMonthDay: return "gMonthDay";
            case XsdDateTimeKind.GDay: return "gDay";
            case XsdDateTimeKind.GMonth: return "gMonth";
            default: return "unknown";
        }
    }
}
