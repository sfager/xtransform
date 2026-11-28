// XSD built-in type names (String, Decimal, Integer, Long, Int, Short, Double, Float, Boolean,
// Float) are mandated by the XSD 1.0 Part 2 §3 spec; CA1720 does not apply.
#pragma warning disable CA1720

using System;
using System.Globalization;
using System.Numerics;

namespace XTransform.Xdm;

/// <summary>
/// An immutable XDM atomic value carrying a typed payload.
/// Corresponds to xs:anyAtomicType and all its subtypes defined in XSD 1.0 Part 2.
/// </summary>
/// <remarks>
/// XPath 2.0 §2.5 defines atomic values. Each instance holds a <see cref="XdmTypeCode"/>
/// and an <see langword="object"/> payload chosen by that type code. Instances are
/// safe to share across threads.
/// </remarks>
public sealed class XdmAtomicValue : XdmItem, IEquatable<XdmAtomicValue>
{
    // -------------------------------------------------------------------------
    // Cached singletons
    // -------------------------------------------------------------------------

    private static readonly XdmAtomicValue s_true = new XdmAtomicValue(XdmTypeCode.Boolean, true);
    private static readonly XdmAtomicValue s_false = new XdmAtomicValue(XdmTypeCode.Boolean, false);

    // -------------------------------------------------------------------------
    // Storage
    // -------------------------------------------------------------------------

    private readonly XdmTypeCode _typeCode;
    private readonly object _payload;

    private XdmAtomicValue(XdmTypeCode typeCode, object payload)
    {
        _typeCode = typeCode;
        _payload = payload;
    }

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    /// <summary>The XSD atomic type code of this value.</summary>
    public XdmTypeCode TypeCode => _typeCode;

    /// <inheritdoc />
    public override bool IsNode => false;

    /// <inheritdoc />
    public override bool IsAtomic => true;

    // -------------------------------------------------------------------------
    // Factories
    // -------------------------------------------------------------------------

    /// <summary>Creates an <c>xs:string</c> atomic value.</summary>
    /// <param name="value">The string content. Must not be <see langword="null"/>.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.String"/>.</returns>
    public static XdmAtomicValue String(string value)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (value is null) { throw new ArgumentNullException(nameof(value)); }
#pragma warning restore CA1510
        return new XdmAtomicValue(XdmTypeCode.String, value);
    }

    /// <summary>Creates an <c>xs:untypedAtomic</c> atomic value.</summary>
    /// <param name="value">The string content. Must not be <see langword="null"/>.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.UntypedAtomic"/>.</returns>
    public static XdmAtomicValue UntypedAtomic(string value)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (value is null) { throw new ArgumentNullException(nameof(value)); }
#pragma warning restore CA1510
        return new XdmAtomicValue(XdmTypeCode.UntypedAtomic, value);
    }

    /// <summary>Creates an <c>xs:anyURI</c> atomic value.</summary>
    /// <param name="value">The URI string. Must not be <see langword="null"/>.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.AnyUri"/>.</returns>
    public static XdmAtomicValue AnyUri(string value)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (value is null) { throw new ArgumentNullException(nameof(value)); }
#pragma warning restore CA1510
        return new XdmAtomicValue(XdmTypeCode.AnyUri, value);
    }

    /// <summary>
    /// Returns the cached <c>xs:boolean</c> instance for the given value.
    /// Two calls with the same argument always return a reference-identical instance.
    /// </summary>
    /// <param name="value">Boolean value.</param>
    /// <returns>The shared <see cref="XdmAtomicValue"/> for <see langword="true"/> or <see langword="false"/>.</returns>
    public static XdmAtomicValue Boolean(bool value)
    {
        return value ? s_true : s_false;
    }

    /// <summary>Creates an <c>xs:integer</c> atomic value from a <see cref="long"/>.</summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Integer"/>.</returns>
    public static XdmAtomicValue Integer(long value)
    {
        return new XdmAtomicValue(XdmTypeCode.Integer, value);
    }

    /// <summary>
    /// Creates an <c>xs:integer</c> atomic value from a <see cref="BigInteger"/>.
    /// If the value fits in a <see cref="long"/>, a <see langword="long"/> payload is used.
    /// </summary>
    /// <param name="value">The integer value (arbitrary precision).</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Integer"/>.</returns>
    public static XdmAtomicValue Integer(BigInteger value)
    {
        // Shrink to long when possible to avoid BigInteger boxing overhead.
        if (value >= long.MinValue && value <= long.MaxValue)
        {
            return new XdmAtomicValue(XdmTypeCode.Integer, (long)value);
        }
        return new XdmAtomicValue(XdmTypeCode.Integer, value);
    }

    /// <summary>Creates an <c>xs:decimal</c> atomic value from a CLR <see cref="decimal"/>.</summary>
    /// <param name="value">The decimal value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Decimal"/>.</returns>
    public static XdmAtomicValue Decimal(decimal value)
    {
        return new XdmAtomicValue(XdmTypeCode.Decimal, new XsdDecimal(value));
    }

    /// <summary>Creates an <c>xs:decimal</c> atomic value from an <see cref="XsdDecimal"/>.</summary>
    /// <param name="value">The XSD decimal value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Decimal"/>.</returns>
    public static XdmAtomicValue Decimal(XsdDecimal value)
    {
        return new XdmAtomicValue(XdmTypeCode.Decimal, value);
    }

    /// <summary>Creates an <c>xs:double</c> atomic value.</summary>
    /// <param name="value">The double-precision floating-point value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Double"/>.</returns>
    public static XdmAtomicValue Double(double value)
    {
        return new XdmAtomicValue(XdmTypeCode.Double, value);
    }

    /// <summary>Creates an <c>xs:float</c> atomic value.</summary>
    /// <param name="value">The single-precision floating-point value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Float"/>.</returns>
    public static XdmAtomicValue Float(float value)
    {
        return new XdmAtomicValue(XdmTypeCode.Float, value);
    }

    /// <summary>Creates an <c>xs:QName</c> atomic value.</summary>
    /// <param name="value">The QName. Must not be <see langword="null"/>.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.QName"/>.</returns>
    public static XdmAtomicValue QNameValue(QName value)
    {
        return new XdmAtomicValue(XdmTypeCode.QName, value);
    }

    /// <summary>
    /// Creates a date/time atomic value. The <see cref="XdmTypeCode"/> is inferred from
    /// <see cref="XsdDateTime.Kind"/>.
    /// </summary>
    /// <param name="value">The XSD date/time value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with the inferred date/time type code.</returns>
    public static XdmAtomicValue DateTimeValue(XsdDateTime value)
    {
        XdmTypeCode code = KindToTypeCode(value.Kind);
        return new XdmAtomicValue(code, value);
    }

    /// <summary>Creates an <c>xs:duration</c> atomic value.</summary>
    /// <param name="value">The XSD duration value.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Duration"/>.</returns>
    public static XdmAtomicValue Duration(XsdDuration value)
    {
        return new XdmAtomicValue(XdmTypeCode.Duration, value);
    }

    /// <summary>Creates an <c>xs:dayTimeDuration</c> atomic value.</summary>
    /// <param name="value">The XSD duration value (months component must be zero).</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.DayTimeDuration"/>.</returns>
    public static XdmAtomicValue DayTimeDuration(XsdDuration value)
    {
        return new XdmAtomicValue(XdmTypeCode.DayTimeDuration, value);
    }

    /// <summary>Creates an <c>xs:yearMonthDuration</c> atomic value.</summary>
    /// <param name="value">The XSD duration value (seconds component must be zero).</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.YearMonthDuration"/>.</returns>
    public static XdmAtomicValue YearMonthDuration(XsdDuration value)
    {
        return new XdmAtomicValue(XdmTypeCode.YearMonthDuration, value);
    }

    /// <summary>Creates an <c>xs:hexBinary</c> atomic value.</summary>
    /// <param name="value">The binary data.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.HexBinary"/>.</returns>
    public static XdmAtomicValue HexBinary(ReadOnlyMemory<byte> value)
    {
        return new XdmAtomicValue(XdmTypeCode.HexBinary, value);
    }

    /// <summary>Creates an <c>xs:base64Binary</c> atomic value.</summary>
    /// <param name="value">The binary data.</param>
    /// <returns>A new <see cref="XdmAtomicValue"/> with <see cref="XdmTypeCode.Base64Binary"/>.</returns>
    public static XdmAtomicValue Base64Binary(ReadOnlyMemory<byte> value)
    {
        return new XdmAtomicValue(XdmTypeCode.Base64Binary, value);
    }

    // -------------------------------------------------------------------------
    // Parse / TryParse
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a lexical string as an atomic value of the given type.
    /// </summary>
    /// <param name="lexical">The lexical string to parse. Must not be <see langword="null"/>.</param>
    /// <param name="type">The target XSD type code.</param>
    /// <returns>The parsed <see cref="XdmAtomicValue"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lexical"/> is <see langword="null"/>.</exception>
    /// <exception cref="XPathDynamicException">
    /// Thrown with error code <see cref="XPathErrors.FORG0001"/> when the lexical form is not valid for <paramref name="type"/>.
    /// </exception>
    public static XdmAtomicValue Parse(string lexical, XdmTypeCode type)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (lexical is null) { throw new ArgumentNullException(nameof(lexical)); }
#pragma warning restore CA1510

        if (!TryParseCore(lexical, type, out XdmAtomicValue? result))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0001,
                $"Invalid lexical value '{lexical}' for type xs:{CastTable.GetXsLocalName(type)}.",
                SourceSpan.None);
        }

        // Validate derived-type facets (range checks for integer-family, pattern checks for string-family)
        if (!CastTable.ValidateDerivedFacets(result!, type, out string? facetError))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0001,
                facetError ?? $"Value is not valid for type xs:{CastTable.GetXsLocalName(type)}.",
                SourceSpan.None);
        }

        return result!;
    }

    /// <summary>
    /// Tries to parse a lexical string as an atomic value of the given type.
    /// </summary>
    /// <param name="lexical">The lexical string. Must not be <see langword="null"/>.</param>
    /// <param name="type">The target XSD type code.</param>
    /// <param name="value">When successful, the parsed value; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> on invalid lexical form.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lexical"/> is <see langword="null"/>.</exception>
    public static bool TryParse(string lexical, XdmTypeCode type, out XdmAtomicValue? value)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (lexical is null) { throw new ArgumentNullException(nameof(lexical)); }
#pragma warning restore CA1510

        if (!TryParseCore(lexical, type, out value))
        {
            return false;
        }
        // Validate derived-type facets after a successful core parse
        if (!CastTable.ValidateDerivedFacets(value!, type, out _))
        {
            value = null;
            return false;
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // GetStringValue
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>Returns the XSD canonical lexical representation per F&amp;O §17.1.2.</remarks>
    public override string GetStringValue()
    {
        // F&O §17.1.2 — canonical representations by type
        switch (_typeCode)
        {
            // String family, UntypedAtomic, AnyUri → stored string as-is
            case XdmTypeCode.String:
            case XdmTypeCode.NormalizedString:
            case XdmTypeCode.Token:
            case XdmTypeCode.Language:
            case XdmTypeCode.NmToken:
            case XdmTypeCode.Name:
            case XdmTypeCode.NcName:
            case XdmTypeCode.Id:
            case XdmTypeCode.IdRef:
            case XdmTypeCode.Entity:
            case XdmTypeCode.UntypedAtomic:
            case XdmTypeCode.AnyUri:
                return (string)_payload;

            case XdmTypeCode.Boolean:
                return (bool)_payload ? "true" : "false";

            case XdmTypeCode.Integer:
            case XdmTypeCode.NonPositiveInteger:
            case XdmTypeCode.NegativeInteger:
            case XdmTypeCode.Long:
            case XdmTypeCode.Int:
            case XdmTypeCode.Short:
            case XdmTypeCode.Byte:
            case XdmTypeCode.NonNegativeInteger:
            case XdmTypeCode.UnsignedLong:
            case XdmTypeCode.UnsignedInt:
            case XdmTypeCode.UnsignedShort:
            case XdmTypeCode.UnsignedByte:
            case XdmTypeCode.PositiveInteger:
                if (_payload is long lv)
                {
                    return lv.ToString(CultureInfo.InvariantCulture);
                }
                return ((BigInteger)_payload).ToString(CultureInfo.InvariantCulture);

            case XdmTypeCode.Decimal:
                return ((XsdDecimal)_payload).ToString();

            case XdmTypeCode.Double:
                return FormatDouble((double)_payload);

            case XdmTypeCode.Float:
                return FormatFloat((float)_payload);

            case XdmTypeCode.Duration:
            case XdmTypeCode.DayTimeDuration:
            case XdmTypeCode.YearMonthDuration:
                return ((XsdDuration)_payload).ToString();

            case XdmTypeCode.DateTime:
            case XdmTypeCode.Date:
            case XdmTypeCode.Time:
            case XdmTypeCode.GYearMonth:
            case XdmTypeCode.GYear:
            case XdmTypeCode.GMonthDay:
            case XdmTypeCode.GDay:
            case XdmTypeCode.GMonth:
                return ((XsdDateTime)_payload).ToString();

            case XdmTypeCode.HexBinary:
                return FormatHexBinary(((ReadOnlyMemory<byte>)_payload).ToArray());

            case XdmTypeCode.Base64Binary:
                return Convert.ToBase64String(((ReadOnlyMemory<byte>)_payload).ToArray());

            case XdmTypeCode.QName:
            case XdmTypeCode.Notation:
                return ((QName)_payload).ToString();

            default:
                return string.Empty;
        }
    }

    // -------------------------------------------------------------------------
    // Typed accessors
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the string value for string-family types, xs:untypedAtomic, and xs:anyURI.
    /// </summary>
    /// <returns>The stored string value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not in the string family.</exception>
    public string AsString()
    {
        if (!IsStringFamily(_typeCode))
        {
            ThrowInvalidCast("string family, untypedAtomic or anyURI");
        }
        return (string)_payload;
    }

    /// <summary>Returns the boolean value for <c>xs:boolean</c>.</summary>
    /// <returns>The boolean value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not <see cref="XdmTypeCode.Boolean"/>.</exception>
    public bool AsBoolean()
    {
        if (_typeCode != XdmTypeCode.Boolean)
        {
            ThrowInvalidCast("boolean");
        }
        return (bool)_payload;
    }

    /// <summary>
    /// Returns the integer value as a <see cref="long"/> for integer-family types
    /// when the payload fits in a <see langword="long"/>.
    /// </summary>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not an integer type.</exception>
    /// <exception cref="OverflowException">Thrown when the value exceeds <see cref="long"/> range.</exception>
    public long AsInt64()
    {
        if (!IsIntegerType(_typeCode))
        {
            ThrowInvalidCast("integer family");
        }
        if (_payload is long lv)
        {
            return lv;
        }
        // BigInteger payload — may overflow
        return (long)(BigInteger)_payload;
    }

    /// <summary>
    /// Returns the integer value as a <see cref="BigInteger"/> for integer-family types.
    /// Long payloads are promoted automatically.
    /// </summary>
    /// <returns>The integer value as <see cref="BigInteger"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not an integer type.</exception>
    public BigInteger AsBigInteger()
    {
        if (!IsIntegerType(_typeCode))
        {
            ThrowInvalidCast("integer family");
        }
        if (_payload is long lv)
        {
            return new BigInteger(lv);
        }
        return (BigInteger)_payload;
    }

    /// <summary>
    /// Returns the value as <see cref="XsdDecimal"/>. Integer-family values are promoted.
    /// </summary>
    /// <returns>The decimal value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not Decimal or integer family.</exception>
    public XsdDecimal AsDecimal()
    {
        if (_typeCode == XdmTypeCode.Decimal)
        {
            return (XsdDecimal)_payload;
        }
        if (IsIntegerType(_typeCode))
        {
            if (_payload is long lv)
            {
                return new XsdDecimal((decimal)lv);
            }
            // BigInteger → decimal (may throw OverflowException if out of range)
            return new XsdDecimal((decimal)(BigInteger)_payload);
        }
        ThrowInvalidCast("decimal or integer family");
        return default; // unreachable
    }

    /// <summary>Returns the double value for <c>xs:double</c>.</summary>
    /// <returns>The double value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not <see cref="XdmTypeCode.Double"/>.</exception>
    public double AsDouble()
    {
        if (_typeCode != XdmTypeCode.Double)
        {
            ThrowInvalidCast("double");
        }
        return (double)_payload;
    }

    /// <summary>Returns the float value for <c>xs:float</c>.</summary>
    /// <returns>The float value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not <see cref="XdmTypeCode.Float"/>.</exception>
    public float AsFloat()
    {
        if (_typeCode != XdmTypeCode.Float)
        {
            ThrowInvalidCast("float");
        }
        return (float)_payload;
    }

    /// <summary>Returns the QName for <c>xs:QName</c> and <c>xs:NOTATION</c>.</summary>
    /// <returns>The QName value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not QName or Notation.</exception>
    public QName AsQName()
    {
        if (_typeCode != XdmTypeCode.QName && _typeCode != XdmTypeCode.Notation)
        {
            ThrowInvalidCast("QName or NOTATION");
        }
        return (QName)_payload;
    }

    /// <summary>Returns the date/time value for any of the eight date/time types.</summary>
    /// <returns>The <see cref="XsdDateTime"/> value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not a date/time type.</exception>
    public XsdDateTime AsDateTime()
    {
        if (!IsDateTimeType(_typeCode))
        {
            ThrowInvalidCast("date/time family");
        }
        return (XsdDateTime)_payload;
    }

    /// <summary>Returns the duration value for any of the three duration types.</summary>
    /// <returns>The <see cref="XsdDuration"/> value.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not a duration type.</exception>
    public XsdDuration AsDuration()
    {
        if (!IsDurationType(_typeCode))
        {
            ThrowInvalidCast("duration family");
        }
        return (XsdDuration)_payload;
    }

    /// <summary>Returns the binary data for <c>xs:hexBinary</c>.</summary>
    /// <returns>The raw bytes.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not <see cref="XdmTypeCode.HexBinary"/>.</exception>
    public ReadOnlyMemory<byte> AsHexBinary()
    {
        if (_typeCode != XdmTypeCode.HexBinary)
        {
            ThrowInvalidCast("hexBinary");
        }
        return (ReadOnlyMemory<byte>)_payload;
    }

    /// <summary>Returns the binary data for <c>xs:base64Binary</c>.</summary>
    /// <returns>The raw bytes.</returns>
    /// <exception cref="InvalidCastException">Thrown when the type code is not <see cref="XdmTypeCode.Base64Binary"/>.</exception>
    public ReadOnlyMemory<byte> AsBase64Binary()
    {
        if (_typeCode != XdmTypeCode.Base64Binary)
        {
            ThrowInvalidCast("base64Binary");
        }
        return (ReadOnlyMemory<byte>)_payload;
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compares this value to another using op:*-equal semantics for the common primitive type.
    /// Values with different primitive types are always unequal.
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when both values have the same primitive type and are equal per op:*-equal.</returns>
    public bool Equals(XdmAtomicValue? other)
    {
        if (other is null) { return false; }
        if (ReferenceEquals(this, other)) { return true; }

        XdmTypeCode myPrim = CastTable.PrimitiveTypeOf(_typeCode);
        XdmTypeCode otPrim = CastTable.PrimitiveTypeOf(other._typeCode);

        // Different primitives → not equal (identity semantics from interfaces.md)
        if (myPrim != otPrim) { return false; }

        // Same primitive — compare by primitive type
        switch (myPrim)
        {
            case XdmTypeCode.String:
                return string.Equals((string)_payload, (string)other._payload, StringComparison.Ordinal);

            case XdmTypeCode.UntypedAtomic:
                return string.Equals((string)_payload, (string)other._payload, StringComparison.Ordinal);

            case XdmTypeCode.AnyUri:
                return string.Equals((string)_payload, (string)other._payload, StringComparison.Ordinal);

            case XdmTypeCode.Boolean:
                return (bool)_payload == (bool)other._payload;

            case XdmTypeCode.Decimal:
                // Integer family shares Decimal primitive — promote both to XsdDecimal
                XsdDecimal myDec = ToXsdDecimalForCompare(_payload);
                XsdDecimal otDec = ToXsdDecimalForCompare(other._payload);
                return myDec == otDec;

            case XdmTypeCode.Double:
                {
                    // op:numeric-equal: NaN != NaN
                    double dv = (double)_payload;
                    double ov = (double)other._payload;
#pragma warning disable CS1718 // Comparing to same variable — intentional NaN check per op:numeric-equal
                    return dv == ov && !double.IsNaN(dv);
#pragma warning restore CS1718
                }

            case XdmTypeCode.Float:
                {
                    // op:numeric-equal: NaN != NaN
                    float fv = (float)_payload;
                    float ov = (float)other._payload;
#pragma warning disable CS1718 // Comparing to same variable — intentional NaN check per op:numeric-equal
                    return fv == ov && !float.IsNaN(fv);
#pragma warning restore CS1718
                }

            case XdmTypeCode.Duration:
                return ((XsdDuration)_payload).Equals((XsdDuration)other._payload);

            case XdmTypeCode.DateTime:
            case XdmTypeCode.Date:
            case XdmTypeCode.Time:
            case XdmTypeCode.GYearMonth:
            case XdmTypeCode.GYear:
            case XdmTypeCode.GMonthDay:
            case XdmTypeCode.GDay:
            case XdmTypeCode.GMonth:
                // Raw component comparison (cross-timezone comparison deferred to P1-06)
                return ((XsdDateTime)_payload).Equals((XsdDateTime)other._payload);

            case XdmTypeCode.HexBinary:
            case XdmTypeCode.Base64Binary:
                return ((ReadOnlyMemory<byte>)_payload).Span.SequenceEqual(((ReadOnlyMemory<byte>)other._payload).Span);

            case XdmTypeCode.QName:
            case XdmTypeCode.Notation:
                return ((QName)_payload).Equals((QName)other._payload);

            default:
                return false;
        }
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as XdmAtomicValue);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        XdmTypeCode prim = CastTable.PrimitiveTypeOf(_typeCode);
        int typeHash = (int)prim;

        switch (prim)
        {
            case XdmTypeCode.String:
            case XdmTypeCode.UntypedAtomic:
            case XdmTypeCode.AnyUri:
                return HashCombine(typeHash, StringComparer.Ordinal.GetHashCode((string)_payload));

            case XdmTypeCode.Boolean:
                return HashCombine(typeHash, ((bool)_payload).GetHashCode());

            case XdmTypeCode.Decimal:
                return HashCombine(typeHash, ToXsdDecimalForCompare(_payload).GetHashCode());

            case XdmTypeCode.Double:
                {
                    double dv = (double)_payload;
                    // NaN == NaN is false but all NaNs hash the same (consistent with Equals)
                    return HashCombine(typeHash, dv.GetHashCode());
                }

            case XdmTypeCode.Float:
                {
                    float fv = (float)_payload;
                    return HashCombine(typeHash, fv.GetHashCode());
                }

            case XdmTypeCode.Duration:
                return HashCombine(typeHash, ((XsdDuration)_payload).GetHashCode());

            case XdmTypeCode.DateTime:
            case XdmTypeCode.Date:
            case XdmTypeCode.Time:
            case XdmTypeCode.GYearMonth:
            case XdmTypeCode.GYear:
            case XdmTypeCode.GMonthDay:
            case XdmTypeCode.GDay:
            case XdmTypeCode.GMonth:
                return HashCombine(typeHash, ((XsdDateTime)_payload).GetHashCode());

            case XdmTypeCode.HexBinary:
            case XdmTypeCode.Base64Binary:
                {
                    ReadOnlySpan<byte> span = ((ReadOnlyMemory<byte>)_payload).Span;
                    int h = 17;
                    foreach (byte b in span)
                    {
                        h = h * 31 + b;
                    }
                    return HashCombine(typeHash, h);
                }

            case XdmTypeCode.QName:
            case XdmTypeCode.Notation:
                return HashCombine(typeHash, ((QName)_payload).GetHashCode());

            default:
                return typeHash;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(XdmAtomicValue? left, XdmAtomicValue? right)
    {
        if (left is null) { return right is null; }
        return left.Equals(right);
    }

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(XdmAtomicValue? left, XdmAtomicValue? right)
    {
        return !(left == right);
    }

    // -------------------------------------------------------------------------
    // Private helpers — parse dispatch
    // -------------------------------------------------------------------------

    private static bool TryParseCore(string lexical, XdmTypeCode type, out XdmAtomicValue? value)
    {
        value = null;

        // XSD 1.0 Part 2 §4.3.6: whiteSpace facet normalization before parsing
        string normalized = NormalizeWhitespace(lexical, type);

        switch (type)
        {
            // String family — store normalized value; lexical validation deferred to P1-06
            case XdmTypeCode.String:
                value = new XdmAtomicValue(XdmTypeCode.String, normalized);
                return true;

            case XdmTypeCode.NormalizedString:
                value = new XdmAtomicValue(XdmTypeCode.NormalizedString, normalized);
                return true;

            case XdmTypeCode.Token:
                value = new XdmAtomicValue(XdmTypeCode.Token, normalized);
                return true;

            case XdmTypeCode.Language:
                value = new XdmAtomicValue(XdmTypeCode.Language, normalized);
                return true;

            case XdmTypeCode.NmToken:
                value = new XdmAtomicValue(XdmTypeCode.NmToken, normalized);
                return true;

            case XdmTypeCode.Name:
                value = new XdmAtomicValue(XdmTypeCode.Name, normalized);
                return true;

            case XdmTypeCode.NcName:
                value = new XdmAtomicValue(XdmTypeCode.NcName, normalized);
                return true;

            case XdmTypeCode.Id:
                value = new XdmAtomicValue(XdmTypeCode.Id, normalized);
                return true;

            case XdmTypeCode.IdRef:
                value = new XdmAtomicValue(XdmTypeCode.IdRef, normalized);
                return true;

            case XdmTypeCode.Entity:
                value = new XdmAtomicValue(XdmTypeCode.Entity, normalized);
                return true;

            case XdmTypeCode.UntypedAtomic:
                value = new XdmAtomicValue(XdmTypeCode.UntypedAtomic, normalized);
                return true;

            case XdmTypeCode.AnyUri:
                value = new XdmAtomicValue(XdmTypeCode.AnyUri, normalized);
                return true;

            case XdmTypeCode.Boolean:
                // XSD 1.0 Part 2 §3.2.2.1 — lexical: "true"/"false"/"1"/"0"
                return TryParseBoolean(normalized, out value);

            case XdmTypeCode.Integer:
            case XdmTypeCode.NonPositiveInteger:
            case XdmTypeCode.NegativeInteger:
            case XdmTypeCode.Long:
            case XdmTypeCode.Int:
            case XdmTypeCode.Short:
            case XdmTypeCode.Byte:
            case XdmTypeCode.NonNegativeInteger:
            case XdmTypeCode.UnsignedLong:
            case XdmTypeCode.UnsignedInt:
            case XdmTypeCode.UnsignedShort:
            case XdmTypeCode.UnsignedByte:
            case XdmTypeCode.PositiveInteger:
                // XSD 1.0 Part 2 §3.3.13 — integer: optional sign, digits
                return TryParseInteger(normalized, type, out value);

            case XdmTypeCode.Decimal:
                {
                    if (!XsdDecimal.TryParse(normalized, out XsdDecimal d)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.Decimal, d);
                    return true;
                }

            case XdmTypeCode.Double:
                return TryParseDouble(normalized, out value);

            case XdmTypeCode.Float:
                return TryParseFloat(normalized, out value);

            case XdmTypeCode.Duration:
                {
                    if (!XsdDuration.TryParse(normalized, out XsdDuration d)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.Duration, d);
                    return true;
                }

            case XdmTypeCode.DayTimeDuration:
                {
                    // XPath 2.0 §2.6.2: dayTimeDuration may not have months component
                    if (!XsdDuration.TryParse(normalized, out XsdDuration d)) { return false; }
                    if (d.Months != 0) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.DayTimeDuration, d);
                    return true;
                }

            case XdmTypeCode.YearMonthDuration:
                {
                    // XPath 2.0 §2.6.2: yearMonthDuration may not have seconds component
                    if (!XsdDuration.TryParse(normalized, out XsdDuration d)) { return false; }
                    if ((decimal)d.Seconds != 0m) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.YearMonthDuration, d);
                    return true;
                }

            case XdmTypeCode.DateTime:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.DateTime, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.DateTime, dt);
                    return true;
                }

            case XdmTypeCode.Date:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.Date, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.Date, dt);
                    return true;
                }

            case XdmTypeCode.Time:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.Time, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.Time, dt);
                    return true;
                }

            case XdmTypeCode.GYearMonth:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.GYearMonth, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.GYearMonth, dt);
                    return true;
                }

            case XdmTypeCode.GYear:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.GYear, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.GYear, dt);
                    return true;
                }

            case XdmTypeCode.GMonthDay:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.GMonthDay, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.GMonthDay, dt);
                    return true;
                }

            case XdmTypeCode.GDay:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.GDay, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.GDay, dt);
                    return true;
                }

            case XdmTypeCode.GMonth:
                {
                    if (!XsdDateTime.TryParse(normalized, XsdDateTimeKind.GMonth, out XsdDateTime dt)) { return false; }
                    value = new XdmAtomicValue(XdmTypeCode.GMonth, dt);
                    return true;
                }

            case XdmTypeCode.HexBinary:
                return TryParseHexBinary(normalized, out value);

            case XdmTypeCode.Base64Binary:
                return TryParseBase64Binary(normalized, out value);

            case XdmTypeCode.QName:
                return TryParseQName(normalized, XdmTypeCode.QName, out value);

            case XdmTypeCode.Notation:
                return TryParseQName(normalized, XdmTypeCode.Notation, out value);

            case XdmTypeCode.AnyAtomicType:
                // F&O §17.1.1: cannot cast to abstract type
                return false;

            default:
                return false;
        }
    }

    // XSD 1.0 Part 2 §3.2.2.1 — boolean lexical: "true"/"false"/"1"/"0"
    private static bool TryParseBoolean(string s, out XdmAtomicValue? value)
    {
        value = null;
        if (string.Equals(s, "true", StringComparison.Ordinal) ||
            string.Equals(s, "1", StringComparison.Ordinal))
        {
            value = s_true;
            return true;
        }
        if (string.Equals(s, "false", StringComparison.Ordinal) ||
            string.Equals(s, "0", StringComparison.Ordinal))
        {
            value = s_false;
            return true;
        }
        return false;
    }

    // XSD 1.0 Part 2 §3.3.13 — integer lexical: [+|-]?[0-9]+
    private static bool TryParseInteger(string s, XdmTypeCode type, out XdmAtomicValue? value)
    {
        value = null;

        if (s.Length == 0) { return false; }

        int start = 0;
        if (s[0] == '+' || s[0] == '-')
        {
            start = 1;
        }

        if (start >= s.Length) { return false; }

        // All remaining characters must be digits
        for (int i = start; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9') { return false; }
        }

        // Parse as BigInteger for arbitrary precision, then shrink
        if (!BigInteger.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger bigVal))
        {
            return false;
        }

        object payload;
        if (bigVal >= long.MinValue && bigVal <= long.MaxValue)
        {
            payload = (long)bigVal;
        }
        else
        {
            payload = bigVal;
        }

        value = new XdmAtomicValue(type, payload);
        return true;
    }

    // F&O §17.1.2 — double lexical: "INF"/"-INF"/"NaN" or standard float literal
    private static bool TryParseDouble(string s, out XdmAtomicValue? value)
    {
        value = null;

        if (string.Equals(s, "INF", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Double, double.PositiveInfinity);
            return true;
        }
        if (string.Equals(s, "-INF", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Double, double.NegativeInfinity);
            return true;
        }
        if (string.Equals(s, "NaN", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Double, double.NaN);
            return true;
        }

        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return false;
        }

        // .NET Framework parses "-0" as positive zero; XML Schema says it is negative zero.
        if (d == 0 && s.Length > 0 && s[0] == '-')
        {
            d = -0.0;
        }

        value = new XdmAtomicValue(XdmTypeCode.Double, d);
        return true;
    }

    // F&O §17.1.2 — float lexical
    private static bool TryParseFloat(string s, out XdmAtomicValue? value)
    {
        value = null;

        if (string.Equals(s, "INF", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Float, float.PositiveInfinity);
            return true;
        }
        if (string.Equals(s, "-INF", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Float, float.NegativeInfinity);
            return true;
        }
        if (string.Equals(s, "NaN", StringComparison.Ordinal))
        {
            value = new XdmAtomicValue(XdmTypeCode.Float, float.NaN);
            return true;
        }

        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            return false;
        }

        if (f == 0 && s.Length > 0 && s[0] == '-')
        {
            f = -0.0f;
        }

        value = new XdmAtomicValue(XdmTypeCode.Float, f);
        return true;
    }

    // XSD 1.0 Part 2 §3.2.15 — hexBinary: even number of hex digits
    private static bool TryParseHexBinary(string s, out XdmAtomicValue? value)
    {
        value = null;

        if (s.Length == 0)
        {
            value = new XdmAtomicValue(XdmTypeCode.HexBinary, ReadOnlyMemory<byte>.Empty);
            return true;
        }

        if ((s.Length & 1) != 0) { return false; } // must be even length

        byte[] bytes = new byte[s.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            int hi = HexDigit(s[i * 2]);
            int lo = HexDigit(s[i * 2 + 1]);
            if (hi < 0 || lo < 0) { return false; }
            bytes[i] = (byte)((hi << 4) | lo);
        }

        value = new XdmAtomicValue(XdmTypeCode.HexBinary, new ReadOnlyMemory<byte>(bytes));
        return true;
    }

    private static int HexDigit(char c)
    {
        if (c >= '0' && c <= '9') { return c - '0'; }
        if (c >= 'a' && c <= 'f') { return c - 'a' + 10; }
        if (c >= 'A' && c <= 'F') { return c - 'A' + 10; }
        return -1;
    }

    // XSD 1.0 Part 2 §3.2.16 — base64Binary
    private static bool TryParseBase64Binary(string s, out XdmAtomicValue? value)
    {
        value = null;
        try
        {
            byte[] bytes = Convert.FromBase64String(s);
            value = new XdmAtomicValue(XdmTypeCode.Base64Binary, new ReadOnlyMemory<byte>(bytes));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // xs:QName lexical: local or prefix:local — namespace URI deferred to evaluator
    private static bool TryParseQName(string s, XdmTypeCode type, out XdmAtomicValue? value)
    {
        value = null;

        if (string.IsNullOrEmpty(s)) { return false; }

        int colon = s.IndexOf(':');
        if (colon < 0)
        {
            // No prefix — must be a valid NCName
            if (!IsValidNcName(s)) { return false; }
            value = new XdmAtomicValue(type, new QName(string.Empty, string.Empty, s));
            return true;
        }

        // prefix:local
        string prefix = s.Substring(0, colon);
        string local = s.Substring(colon + 1);

        if (!IsValidNcName(prefix) || !IsValidNcName(local)) { return false; }

        value = new XdmAtomicValue(type, new QName(prefix, string.Empty, local));
        return true;
    }

    // -------------------------------------------------------------------------
    // Private helpers — whitespace normalization
    // -------------------------------------------------------------------------

    // XSD 1.0 Part 2 §4.3.6 — whiteSpace facet
    private static string NormalizeWhitespace(string s, XdmTypeCode type)
    {
        if (type == XdmTypeCode.String)
        {
            // "preserve" — no change
            return s;
        }

        if (type == XdmTypeCode.NormalizedString)
        {
            // "replace" — tab/LF/CR → space (no trim, no merge)
            return ReplaceWhitespace(s);
        }

        // "collapse" for all other types: replace + trim + merge internal runs
        return CollapseWhitespace(s);
    }

    private static string ReplaceWhitespace(string s)
    {
        bool needsChange = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\t' || c == '\n' || c == '\r')
            {
                needsChange = true;
                break;
            }
        }

        if (!needsChange) { return s; }

        char[] buf = s.ToCharArray();
        for (int i = 0; i < buf.Length; i++)
        {
            char c = buf[i];
            if (c == '\t' || c == '\n' || c == '\r')
            {
                buf[i] = ' ';
            }
        }

        return new string(buf);
    }

    private static string CollapseWhitespace(string s)
    {
        // XSD 1.0 Part 2 §4.3.6 "collapse": replace + trim leading/trailing + merge internal runs
        // Simple eager approach: build the result directly.
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
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
                    // Record that we have pending whitespace to emit if more content follows
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

        // needSpace at end means trailing whitespace — do NOT emit it (trim)
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Private helpers — formatting
    // -------------------------------------------------------------------------

    // Helper: netstandard2.0 compatible char-in-string check
    // CA2249 fires on net8.0 for `IndexOf(char) >= 0`; suppress since we target netstandard2.0 API surface.
#pragma warning disable CA2249 // string.IndexOf → string.Contains: not available in netstandard2.0 API surface we write against
    private static bool StringContainsChar(string s, char c) => s.IndexOf(c) >= 0;
#pragma warning restore CA2249

    // F&O §17.1.2 — canonical double representation
    private static string FormatDouble(double d)
    {
        if (double.IsNaN(d)) { return "NaN"; }
        if (double.IsPositiveInfinity(d)) { return "INF"; }
        if (double.IsNegativeInfinity(d)) { return "-INF"; }

        // Both +0 and -0 → "0"
        if (d == 0.0) { return "0"; }

        double abs = Math.Abs(d);

        // F&O §17.1.2: decimal notation when abs value in [1e-6, 1e21)
        if (abs >= 1e-6 && abs < 1e21)
        {
            // Use "R" for round-trip fidelity, then strip unnecessary trailing zeros
            string r = d.ToString("R", CultureInfo.InvariantCulture);
            // "R" may produce scientific notation for small numbers — reformat if needed
            if (StringContainsChar(r, 'E') || StringContainsChar(r, 'e'))
            {
                // Force decimal notation via G17 and strip trailing zeros
                r = d.ToString("G17", CultureInfo.InvariantCulture);
                if (StringContainsChar(r, 'E') || StringContainsChar(r, 'e'))
                {
                    // Still scientific — use the actual decimal form
                    r = d.ToString("0.####################", CultureInfo.InvariantCulture);
                }
            }
            return StripTrailingZerosDecimal(r);
        }

        // Scientific notation: {mantissa}E{exponent}
        return FormatScientific(d);
    }

    // F&O §17.1.2 — canonical float representation
    private static string FormatFloat(float f)
    {
        if (float.IsNaN(f)) { return "NaN"; }
        if (float.IsPositiveInfinity(f)) { return "INF"; }
        if (float.IsNegativeInfinity(f)) { return "-INF"; }

        if (f == 0.0f) { return "0"; }

        float abs = Math.Abs(f);

        if (abs >= 1e-6f && abs < 1e21f)
        {
            string r = f.ToString("R", CultureInfo.InvariantCulture);
            if (StringContainsChar(r, 'E') || StringContainsChar(r, 'e'))
            {
                r = f.ToString("G9", CultureInfo.InvariantCulture);
                if (StringContainsChar(r, 'E') || StringContainsChar(r, 'e'))
                {
                    r = f.ToString("0.########", CultureInfo.InvariantCulture);
                }
            }
            return StripTrailingZerosDecimal(r);
        }

        return FormatScientificFloat(f);
    }

    private static string StripTrailingZerosDecimal(string s)
    {
        int dot = s.IndexOf('.');
        if (dot < 0) { return s; }

        int end = s.Length - 1;
        while (end > dot && s[end] == '0')
        {
            end--;
        }
        // Keep the decimal point if there are still fractional digits; remove it if not
        if (s[end] == '.')
        {
            return s.Substring(0, end);
        }
        return s.Substring(0, end + 1);
    }

    private static string FormatScientific(double d)
    {
        // Format as scientific notation with single digit before decimal in mantissa
        // Exponent: no leading zero, no '+' on positive
        string r = d.ToString("R", CultureInfo.InvariantCulture);

        // Parse via standard scientific formatting
        // Use G17 to get enough precision
        string g = d.ToString("E15", CultureInfo.InvariantCulture);
        // "E15" gives e.g. "1.000000000000000E+022" — parse mantissa and exponent
        int eIdx = g.IndexOf('E');
        if (eIdx < 0) { return r; } // fallback

        string mantissaStr = g.Substring(0, eIdx);
        string expStr = g.Substring(eIdx + 1);

        int exp = int.Parse(expStr, CultureInfo.InvariantCulture);
        double mantissaVal = double.Parse(mantissaStr, CultureInfo.InvariantCulture);

        // Strip trailing zeros from mantissa
        string mantissaFormatted = StripTrailingZerosDecimal(mantissaStr);

        return mantissaFormatted + "E" + exp.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatScientificFloat(float f)
    {
        string g = f.ToString("E8", CultureInfo.InvariantCulture);
        int eIdx = g.IndexOf('E');
        if (eIdx < 0) { return f.ToString("R", CultureInfo.InvariantCulture); }

        string mantissaStr = g.Substring(0, eIdx);
        string expStr = g.Substring(eIdx + 1);

        int exp = int.Parse(expStr, CultureInfo.InvariantCulture);
        string mantissaFormatted = StripTrailingZerosDecimal(mantissaStr);

        return mantissaFormatted + "E" + exp.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatHexBinary(byte[] bytes)
    {
        if (bytes.Length == 0) { return string.Empty; }
#if NET5_0_OR_GREATER
        return Convert.ToHexString(bytes);
#else
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif
    }

    // -------------------------------------------------------------------------
    // Private helpers — type predicates
    // -------------------------------------------------------------------------

    private static bool IsStringFamily(XdmTypeCode tc)
    {
        return CastTable.PrimitiveTypeOf(tc) == XdmTypeCode.String
            || tc == XdmTypeCode.UntypedAtomic
            || tc == XdmTypeCode.AnyUri;
    }

    private static bool IsIntegerType(XdmTypeCode tc)
    {
        return tc == XdmTypeCode.Integer
            || tc == XdmTypeCode.NonPositiveInteger
            || tc == XdmTypeCode.NegativeInteger
            || tc == XdmTypeCode.Long
            || tc == XdmTypeCode.Int
            || tc == XdmTypeCode.Short
            || tc == XdmTypeCode.Byte
            || tc == XdmTypeCode.NonNegativeInteger
            || tc == XdmTypeCode.UnsignedLong
            || tc == XdmTypeCode.UnsignedInt
            || tc == XdmTypeCode.UnsignedShort
            || tc == XdmTypeCode.UnsignedByte
            || tc == XdmTypeCode.PositiveInteger;
    }

    private static bool IsDateTimeType(XdmTypeCode tc)
    {
        return tc == XdmTypeCode.DateTime
            || tc == XdmTypeCode.Date
            || tc == XdmTypeCode.Time
            || tc == XdmTypeCode.GYearMonth
            || tc == XdmTypeCode.GYear
            || tc == XdmTypeCode.GMonthDay
            || tc == XdmTypeCode.GDay
            || tc == XdmTypeCode.GMonth;
    }

    private static bool IsDurationType(XdmTypeCode tc)
    {
        return tc == XdmTypeCode.Duration
            || tc == XdmTypeCode.DayTimeDuration
            || tc == XdmTypeCode.YearMonthDuration;
    }

    private static bool IsValidNcName(string s)
    {
        // XML Namespaces §6: NCName ::= Name - (Char* ':' Char*)
        // Simplified check: first char must be letter or '_'; rest letter/digit/'-'/'.'/'_'
        if (s.Length == 0) { return false; }

        char first = s[0];
        if (!IsNcNameStartChar(first)) { return false; }

        for (int i = 1; i < s.Length; i++)
        {
            if (!IsNcNameChar(s[i])) { return false; }
        }

        return true;
    }

    private static bool IsNcNameStartChar(char c)
    {
        return c == '_'
            || (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || c > 0x7F; // simplified: allow high Unicode
    }

    private static bool IsNcNameChar(char c)
    {
        return c == '_' || c == '-' || c == '.'
            || (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9')
            || c > 0x7F;
    }

    private static XdmTypeCode KindToTypeCode(XsdDateTimeKind kind)
    {
        switch (kind)
        {
            case XsdDateTimeKind.DateTime: return XdmTypeCode.DateTime;
            case XsdDateTimeKind.Date: return XdmTypeCode.Date;
            case XsdDateTimeKind.Time: return XdmTypeCode.Time;
            case XsdDateTimeKind.GYearMonth: return XdmTypeCode.GYearMonth;
            case XsdDateTimeKind.GYear: return XdmTypeCode.GYear;
            case XsdDateTimeKind.GMonthDay: return XdmTypeCode.GMonthDay;
            case XsdDateTimeKind.GDay: return XdmTypeCode.GDay;
            case XsdDateTimeKind.GMonth: return XdmTypeCode.GMonth;
            default: return XdmTypeCode.DateTime;
        }
    }

    private static XsdDecimal ToXsdDecimalForCompare(object payload)
    {
        if (payload is XsdDecimal xd) { return xd; }
        if (payload is long lv) { return new XsdDecimal((decimal)lv); }
        // BigInteger — may overflow for truly huge integers, but that is acceptable
        return new XsdDecimal((decimal)(BigInteger)payload);
    }

    private static int HashCombine(int h1, int h2)
    {
        return (h1 * 31) + h2;
    }

    private void ThrowInvalidCast(string expected)
    {
        throw new InvalidCastException(
            $"Cannot access xs:{CastTable.GetXsLocalName(_typeCode)} value as {expected}.");
    }

    // -------------------------------------------------------------------------
    // Internal helper for derived-type casts (CastTable.Cast.cs)
    // -------------------------------------------------------------------------

    // Creates a new XdmAtomicValue with the same payload but a different TypeCode.
    // Used when casting to a derived type (e.g. xs:integer → xs:byte) where the
    // payload is already correct but the type code needs updating.
    internal XdmAtomicValue WithTypeCode(XdmTypeCode newTypeCode)
    {
        if (newTypeCode == _typeCode)
        {
            return this;
        }
        return new XdmAtomicValue(newTypeCode, _payload);
    }
}

#pragma warning restore CA1720
