using System;
using System.Globalization;

namespace XTransform.Xdm;

// XSD 1.0 Part 2 §3.2.3 — decimal
/// <summary>
/// Immutable value type representing an xs:decimal.
/// Wraps <see cref="decimal"/> with XSD lexical parsing and canonical serialisation.
/// </summary>
public readonly struct XsdDecimal : IEquatable<XsdDecimal>, IComparable<XsdDecimal>
{
    private readonly decimal _value;

    /// <summary>Initializes a new <see cref="XsdDecimal"/> from a CLR <see cref="decimal"/>.</summary>
    public XsdDecimal(decimal value)
    {
        // Normalise -0 to 0
        _value = value == 0m ? 0m : value;
    }

    /// <summary>The underlying CLR decimal value.</summary>
    public decimal Value => _value;

    // XSD 1.0 Part 2 §3.2.3.1 — lexical representation: (\+|-)?[0-9]+(\.[0-9]*)?
    /// <summary>
    /// Parses an XSD decimal lexical form.
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORG0001"/> on failure.
    /// </summary>
    public static XsdDecimal Parse(string lexical)
    {
        if (!TryParse(lexical, out XsdDecimal result))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0001,
                $"Invalid xs:decimal lexical value: '{lexical}'.",
                SourceSpan.None);
        }

        return result;
    }

    /// <summary>
    /// Tries to parse an XSD decimal lexical form.
    /// Returns <see langword="false"/> without throwing on failure.
    /// </summary>
    public static bool TryParse(string lexical, out XsdDecimal result)
    {
        result = default;

        if (string.IsNullOrEmpty(lexical))
        {
            return false;
        }

        // XSD lexical form must not contain exponent notation.
        // Manual scan instead of string.IndexOf/Contains for netstandard2.0 compatibility.
        bool hasExponent = false;
        for (int i = 0; i < lexical.Length; i++)
        {
            char ch = lexical[i];
            if (ch == 'E' || ch == 'e')
            {
                hasExponent = true;
                break;
            }
        }

        if (hasExponent)
        {
            return false;
        }

        // Validate the lexical form: optional leading sign, then digits, optional decimal point with digits
        int start = 0;
        if (lexical[0] == '+' || lexical[0] == '-')
        {
            start = 1;
        }

        if (start >= lexical.Length)
        {
            return false;
        }

        bool hasDigit = false;
        bool hasDot = false;
        for (int i = start; i < lexical.Length; i++)
        {
            char c = lexical[i];
            if (c >= '0' && c <= '9')
            {
                hasDigit = true;
            }
            else if (c == '.' && !hasDot)
            {
                hasDot = true;
            }
            else
            {
                return false;
            }
        }

        if (!hasDigit)
        {
            return false;
        }

        if (!decimal.TryParse(
                lexical,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            return false;
        }

        result = new XsdDecimal(value);
        return true;
    }

    // F&O §17.1.2 — canonical representation: no trailing zeros after decimal point; no decimal point if integer
    /// <summary>
    /// Returns the XSD canonical lexical representation:
    /// no trailing zeros after the decimal point; no decimal point if the value is an integer.
    /// <c>-0</c> is normalised to <c>"0"</c>.
    /// </summary>
    public override string ToString()
    {
        // -0 → "0" is handled by the constructor normalising to 0m.
        string raw = _value.ToString(CultureInfo.InvariantCulture);

        if (raw.IndexOf('.') < 0)
        {
            return raw;
        }

        // Strip trailing zeros after the decimal point, then the point itself if nothing remains.
        int end = raw.Length - 1;
        while (end > 0 && raw[end] == '0')
        {
            end--;
        }

        if (raw[end] == '.')
        {
            end--;
        }

        return raw.Substring(0, end + 1);
    }

    /// <inheritdoc />
    public bool Equals(XsdDecimal other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is XsdDecimal other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(XsdDecimal other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public static bool operator ==(XsdDecimal l, XsdDecimal r) => l.Equals(r);

    /// <inheritdoc />
    public static bool operator !=(XsdDecimal l, XsdDecimal r) => !l.Equals(r);

    /// <inheritdoc />
    public static bool operator <(XsdDecimal l, XsdDecimal r) => l._value < r._value;

    /// <inheritdoc />
    public static bool operator >(XsdDecimal l, XsdDecimal r) => l._value > r._value;

    /// <inheritdoc />
    public static bool operator <=(XsdDecimal l, XsdDecimal r) => l._value <= r._value;

    /// <inheritdoc />
    public static bool operator >=(XsdDecimal l, XsdDecimal r) => l._value >= r._value;

    /// <summary>Implicitly converts a <see cref="decimal"/> to an <see cref="XsdDecimal"/>.</summary>
    public static implicit operator XsdDecimal(decimal value) => new XsdDecimal(value);

    /// <summary>Explicitly converts an <see cref="XsdDecimal"/> to a <see cref="decimal"/>.</summary>
    public static explicit operator decimal(XsdDecimal value) => value._value;
}
