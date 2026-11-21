using System;
using System.Globalization;
using System.Text;

namespace XTransform.Xdm;

// XSD 1.0 Part 2 §3.2.6 — duration
/// <summary>
/// Immutable value type representing an xs:duration.
/// Stored as total months (years*12 + months) and total seconds (days*86400 + … + fractional seconds).
/// </summary>
public readonly struct XsdDuration : IEquatable<XsdDuration>
{
    private readonly int _months;
    private readonly XsdDecimal _seconds;

    /// <summary>
    /// Initializes a new <see cref="XsdDuration"/> from pre-computed months and seconds components.
    /// </summary>
    /// <param name="months">Total months (positive or negative).</param>
    /// <param name="seconds">Total seconds (positive or negative); must have the same sign as <paramref name="months"/> or be zero.</param>
    public XsdDuration(int months, XsdDecimal seconds)
    {
        _months = months;
        _seconds = seconds;
    }

    /// <summary>Total months component (years * 12 + months from lexical form). May be negative.</summary>
    public int Months => _months;

    /// <summary>Total seconds component (days * 86400 + hours * 3600 + minutes * 60 + seconds). May be negative.</summary>
    public XsdDecimal Seconds => _seconds;

    // XSD 1.0 Part 2 §3.2.6.1 — lexical representation
    // Format: -?P(\d+Y)?(\d+M)?(\d+D)?(T(\d+H)?(\d+M)?(\d+(\.\d+)?S)?)?
    /// <summary>
    /// Parses an XSD duration lexical form.
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORG0001"/> on failure.
    /// </summary>
    public static XsdDuration Parse(string lexical)
    {
        if (!TryParse(lexical, out XsdDuration result))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0001,
                $"Invalid xs:duration lexical value: '{lexical}'.",
                SourceSpan.None);
        }

        return result;
    }

    /// <summary>
    /// Tries to parse an XSD duration lexical form.
    /// Returns <see langword="false"/> without throwing on failure.
    /// </summary>
    public static bool TryParse(string lexical, out XsdDuration result)
    {
        result = default;

        if (string.IsNullOrEmpty(lexical))
        {
            return false;
        }

        int pos = 0;

        // Optional leading minus sign
        bool negative = false;
        if (pos < lexical.Length && lexical[pos] == '-')
        {
            negative = true;
            pos++;
        }

        // Required 'P'
        if (pos >= lexical.Length || lexical[pos] != 'P')
        {
            return false;
        }

        pos++;

        // Parse date part components: Y, M, D
        long years = 0;
        long months = 0;
        long days = 0;
        long hours = 0;
        long minutes = 0;
        decimal seconds = 0m;
        bool hasComponent = false;
        bool inTimePart = false;

        while (pos < lexical.Length)
        {
            if (lexical[pos] == 'T')
            {
                if (inTimePart)
                {
                    return false; // duplicate T
                }

                inTimePart = true;
                pos++;
                // T must be followed by at least one component (checked after loop)
                continue;
            }

            // Parse a number (integer or decimal for S)
            int numStart = pos;
            while (pos < lexical.Length && lexical[pos] >= '0' && lexical[pos] <= '9')
            {
                pos++;
            }

            if (pos == numStart)
            {
                return false; // expected a digit
            }

            // Check for decimal point (only allowed before 'S')
            bool hasFraction = false;
            if (pos < lexical.Length && lexical[pos] == '.')
            {
                hasFraction = true;
                pos++; // skip '.'
                int fracStart = pos;
                while (pos < lexical.Length && lexical[pos] >= '0' && lexical[pos] <= '9')
                {
                    pos++;
                }

                if (pos == fracStart)
                {
                    return false; // '.' not followed by digits
                }
            }

            if (pos >= lexical.Length)
            {
                return false; // no designator
            }

            char designator = lexical[pos];
            pos++;

            if (hasFraction && designator != 'S')
            {
                return false; // decimal only valid for seconds
            }

            string numStr = lexical.Substring(numStart, (hasFraction ? pos - 1 : pos - 1) - numStart + 0);
            // Re-extract the number string without the designator
            int numLen = (pos - 1) - numStart; // length before designator
            numStr = lexical.Substring(numStart, numLen);

            if (!inTimePart)
            {
                // Date designators: Y, M, D
                switch (designator)
                {
                    case 'Y':
                        if (!long.TryParse(numStr, NumberStyles.None, CultureInfo.InvariantCulture, out years))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    case 'M':
                        if (!long.TryParse(numStr, NumberStyles.None, CultureInfo.InvariantCulture, out months))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    case 'D':
                        if (!long.TryParse(numStr, NumberStyles.None, CultureInfo.InvariantCulture, out days))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    default:
                        return false;
                }
            }
            else
            {
                // Time designators: H, M, S
                switch (designator)
                {
                    case 'H':
                        if (!long.TryParse(numStr, NumberStyles.None, CultureInfo.InvariantCulture, out hours))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    case 'M':
                        if (!long.TryParse(numStr, NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    case 'S':
                        if (!decimal.TryParse(numStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out seconds))
                        {
                            return false;
                        }

                        hasComponent = true;
                        break;
                    default:
                        return false;
                }
            }
        }

        // If T was present but no H/M/S followed, that's invalid
        if (inTimePart && !hasComponent)
        {
            return false;
        }

        // At least one component must be present
        if (!hasComponent)
        {
            return false;
        }

        // Check that total months fits in int
        long totalMonthsLong = years * 12L + months;
        if (totalMonthsLong > int.MaxValue || totalMonthsLong < int.MinValue)
        {
            return false;
        }

        int totalMonths = (int)totalMonthsLong;
        decimal totalSeconds = days * 86400m + hours * 3600m + minutes * 60m + seconds;

        if (negative)
        {
            totalMonths = -totalMonths;
            totalSeconds = -totalSeconds;
        }

        result = new XsdDuration(totalMonths, new XsdDecimal(totalSeconds));
        return true;
    }

    // XSD 1.0 Part 2 §3.2.6.2 and F&O §17.1.2 — canonical representation
    /// <summary>
    /// Returns the XSD canonical lexical representation per XSD §3.2.6.2.
    /// Zero duration is <c>"PT0S"</c>.
    /// </summary>
    public override string ToString()
    {
        // Zero duration
        if (_months == 0 && (decimal)_seconds == 0m)
        {
            return "PT0S";
        }

        bool negative = _months < 0 || ((decimal)_seconds < 0m && _months == 0);

        int absMonths = Math.Abs(_months);
        decimal absSeconds = Math.Abs((decimal)_seconds);

        int years = absMonths / 12;
        int months = absMonths % 12;

        // Decompose absSeconds into days, hours, minutes, fractional seconds
        decimal totalSecs = absSeconds;
        long totalSecsTrunc = (long)Math.Floor(totalSecs);
        long daysD = totalSecsTrunc / 86400;
        long remaining = totalSecsTrunc % 86400;
        long hoursH = remaining / 3600;
        long remaining2 = remaining % 3600;
        long minutesM = remaining2 / 60;
        decimal secsFrac = totalSecs - (daysD * 86400m + hoursH * 3600m + minutesM * 60m);

        StringBuilder sb = new StringBuilder();

        if (negative)
        {
            sb.Append('-');
        }

        sb.Append('P');

        if (years > 0)
        {
            sb.Append(years.ToString(CultureInfo.InvariantCulture));
            sb.Append('Y');
        }

        if (months > 0)
        {
            sb.Append(months.ToString(CultureInfo.InvariantCulture));
            sb.Append('M');
        }

        if (daysD > 0)
        {
            sb.Append(daysD.ToString(CultureInfo.InvariantCulture));
            sb.Append('D');
        }

        // Time part: add T{H}H{M}M{S}S if any time component is non-zero
        bool hasTimePart = hoursH > 0 || minutesM > 0 || secsFrac != 0m;
        if (hasTimePart)
        {
            sb.Append('T');

            if (hoursH > 0)
            {
                sb.Append(hoursH.ToString(CultureInfo.InvariantCulture));
                sb.Append('H');
            }

            if (minutesM > 0)
            {
                sb.Append(minutesM.ToString(CultureInfo.InvariantCulture));
                sb.Append('M');
            }

            if (secsFrac != 0m)
            {
                // Use canonical XsdDecimal form for seconds
                string secsStr = new XsdDecimal(secsFrac).ToString();
                sb.Append(secsStr);
                sb.Append('S');
            }
        }
        else if (years == 0 && months == 0 && daysD == 0)
        {
            // All zero but we checked above — shouldn't reach here
            sb.Append("T0S");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public bool Equals(XsdDuration other) => _months == other._months && _seconds == other._seconds;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is XsdDuration other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int h = 17;
        h = h * 31 + _months.GetHashCode();
        h = h * 31 + _seconds.GetHashCode();
        return h;
    }

    /// <inheritdoc />
    public static bool operator ==(XsdDuration l, XsdDuration r) => l.Equals(r);

    /// <inheritdoc />
    public static bool operator !=(XsdDuration l, XsdDuration r) => !l.Equals(r);
}
