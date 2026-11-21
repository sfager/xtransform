// F&O §17.1 — cast rules and matrix implementation.
// CastTable is an internal partial class; this file adds Cast/TryCast/TryPromote/IsCastAllowed.

using System;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace XTransform.Xdm;

internal static partial class CastTable
{
    // -------------------------------------------------------------------------
    // Cast-primitive lookup (F&O §17.1 "quasi-primitive" scheme)
    // -------------------------------------------------------------------------

    // F&O §17.1: for casting, integer-family collapses to Integer (not Decimal),
    // string-derived collapses to String, duration sub-types keep their own identity.
    // DayTimeDuration and YearMonthDuration are treated as separate cast-primitives.
    internal static XdmTypeCode CastPrimitiveOf(XdmTypeCode type)
    {
        switch (type)
        {
            // Integer family → Integer
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
                return XdmTypeCode.Integer;

            // String-derived → String
            case XdmTypeCode.NormalizedString:
            case XdmTypeCode.Token:
            case XdmTypeCode.Language:
            case XdmTypeCode.NmToken:
            case XdmTypeCode.Name:
            case XdmTypeCode.NcName:
            case XdmTypeCode.Id:
            case XdmTypeCode.IdRef:
            case XdmTypeCode.Entity:
                return XdmTypeCode.String;

            // Everything else maps to itself (Integer, Decimal, Double, Float, Boolean,
            // Duration, DayTimeDuration, YearMonthDuration, DateTime, Date, Time,
            // GYearMonth, GYear, GMonthDay, GDay, GMonth, HexBinary, Base64Binary,
            // AnyUri, QName, Notation, String, UntypedAtomic, AnyAtomicType)
            default:
                return type;
        }
    }

    // -------------------------------------------------------------------------
    // Cast-allowed matrix (F&O §17.1)
    // -------------------------------------------------------------------------

    // Index order for the matrix (24 quasi-primitives + 1 for AnyAtomicType guard).
    // We index via CastPrimitiveOf(), which returns values in the XdmTypeCode range.
    // The matrix is bool[maxCode+1, maxCode+1]; false = N, true = Y or M.

    // We use the max enum value (Notation = 44) to size the array.
    private const int MatrixSize = 45;

    private static readonly bool[,] s_castMatrix = BuildCastMatrix();

    // F&O §17.1 — the full cast-allowed matrix over quasi-primitives.
    private static bool[,] BuildCastMatrix()
    {
        bool[,] m = new bool[MatrixSize, MatrixSize];

        // Helper indices
        int ua   = (int)XdmTypeCode.UntypedAtomic;
        int str  = (int)XdmTypeCode.String;
        int bl   = (int)XdmTypeCode.Boolean;
        int dec  = (int)XdmTypeCode.Decimal;
        int dbl  = (int)XdmTypeCode.Double;
        int flt  = (int)XdmTypeCode.Float;
        int dur  = (int)XdmTypeCode.Duration;
        int dtd  = (int)XdmTypeCode.DayTimeDuration;
        int ymd  = (int)XdmTypeCode.YearMonthDuration;
        int dt   = (int)XdmTypeCode.DateTime;
        int d    = (int)XdmTypeCode.Date;
        int t    = (int)XdmTypeCode.Time;
        int gym  = (int)XdmTypeCode.GYearMonth;
        int gy   = (int)XdmTypeCode.GYear;
        int gmd  = (int)XdmTypeCode.GMonthDay;
        int gd   = (int)XdmTypeCode.GDay;
        int gm   = (int)XdmTypeCode.GMonth;
        int hb   = (int)XdmTypeCode.HexBinary;
        int b64  = (int)XdmTypeCode.Base64Binary;
        int uri  = (int)XdmTypeCode.AnyUri;
        int qn   = (int)XdmTypeCode.QName;
        int not  = (int)XdmTypeCode.Notation;
        int intg = (int)XdmTypeCode.Integer;
        // AnyAtomicType = 0, never allowed (stays false)

        // Rules applied below; comments cite F&O §17.1 table.

        // --- From UntypedAtomic → all except AnyAtomicType and Notation: M (via lexical) ---
        // Note: Notation is excluded (F&O says Notation cannot be a cast target from string)
        int[] allExceptAatAndNot = new int[]
        {
            ua, str, bl, intg, dec, dbl, flt,
            dur, dtd, ymd,
            dt, d, t, gym, gy, gmd, gd, gm,
            hb, b64, uri, qn
        };
        foreach (int tgt in allExceptAatAndNot)
        {
            m[ua, tgt] = true;
        }

        // --- From String → all except AnyAtomicType and Notation: M ---
        foreach (int tgt in allExceptAatAndNot)
        {
            m[str, tgt] = true;
        }

        // --- To String: all sources (except AnyAtomicType) → Y ---
        int[] allSources = new int[]
        {
            ua, str, bl, intg, dec, dbl, flt,
            dur, dtd, ymd,
            dt, d, t, gym, gy, gmd, gd, gm,
            hb, b64, uri, qn, not
        };
        foreach (int src in allSources)
        {
            m[src, str] = true;
        }

        // --- To UntypedAtomic: all sources → Y ---
        foreach (int src in allSources)
        {
            m[src, ua] = true;
        }

        // --- Numeric conversions ---
        // Integer → Decimal Y
        m[intg, dec] = true;
        // Integer → Float Y
        m[intg, flt] = true;
        // Integer → Double Y
        m[intg, dbl] = true;
        // Decimal → Integer M
        m[dec, intg] = true;
        // Decimal → Float Y
        m[dec, flt] = true;
        // Decimal → Double Y
        m[dec, dbl] = true;
        // Float → Integer M
        m[flt, intg] = true;
        // Float → Decimal M
        m[flt, dec] = true;
        // Float → Double Y
        m[flt, dbl] = true;
        // Double → Integer M
        m[dbl, intg] = true;
        // Double → Decimal M
        m[dbl, dec] = true;
        // Double → Float Y
        m[dbl, flt] = true;

        // --- Boolean conversions ---
        // Numeric → Boolean Y
        m[intg, bl] = true;
        m[dec, bl] = true;
        m[flt, bl] = true;
        m[dbl, bl] = true;
        // Boolean → Numeric Y
        m[bl, intg] = true;
        m[bl, dec] = true;
        m[bl, flt] = true;
        m[bl, dbl] = true;

        // --- Duration family ---
        // Duration → DayTimeDuration Y
        m[dur, dtd] = true;
        // Duration → YearMonthDuration Y
        m[dur, ymd] = true;
        // DayTimeDuration → Duration Y
        m[dtd, dur] = true;
        // YearMonthDuration → Duration Y
        m[ymd, dur] = true;
        // DayTimeDuration → YearMonthDuration Y
        m[dtd, ymd] = true;
        // YearMonthDuration → DayTimeDuration Y
        m[ymd, dtd] = true;

        // --- DateTime extractions ---
        // DateTime → Date, Time, GYearMonth, GYear, GMonthDay, GDay, GMonth Y
        m[dt, d]   = true;
        m[dt, t]   = true;
        m[dt, gym] = true;
        m[dt, gy]  = true;
        m[dt, gmd] = true;
        m[dt, gd]  = true;
        m[dt, gm]  = true;
        // Date → DateTime Y
        m[d, dt]   = true;
        // Date → G-types Y
        m[d, gym]  = true;
        m[d, gy]   = true;
        m[d, gmd]  = true;
        m[d, gd]   = true;
        m[d, gm]   = true;
        // Time → nothing other than Time/String/UntypedAtomic (already covered above) — no extra rows
        // GYearMonth → GYear, GMonth Y
        m[gym, gy] = true;
        m[gym, gm] = true;
        // GMonthDay → GMonth, GDay Y
        m[gmd, gm] = true;
        m[gmd, gd] = true;

        // --- Binary conversions ---
        m[hb, b64]  = true;
        m[b64, hb]  = true;

        // --- AnyURI → String Y (already set above by "To String" loop) ---
        // --- String → AnyURI M (already set above by "From String" loop) ---

        // --- QName / Notation ---
        // QName → String / UntypedAtomic already covered
        // Notation → String / UntypedAtomic already covered
        // String → QName M already covered
        // Notation → Notation: set below (identity)
        // All QName <-> non-string non-self: N (stays false)

        // --- Identity (same cast-primitive): Y ---
        int[] allPrimitives = new int[]
        {
            ua, str, bl, intg, dec, dbl, flt,
            dur, dtd, ymd,
            dt, d, t, gym, gy, gmd, gd, gm,
            hb, b64, uri, qn, not
        };
        foreach (int p in allPrimitives)
        {
            m[p, p] = true;
        }

        return m;
    }

    /// <summary>
    /// Returns whether a cast from <paramref name="source"/> to <paramref name="target"/> is
    /// defined in the F&amp;O §17.1 cast matrix (Y or M cells).
    /// N cells return <see langword="false"/>; AnyAtomicType as either side always returns <see langword="false"/>.
    /// </summary>
    internal static bool IsPrimitiveCastAllowed(XdmTypeCode source, XdmTypeCode target)
    {
        int s = (int)source;
        int t = (int)target;
        if (s < 0 || s >= MatrixSize || t < 0 || t >= MatrixSize)
        {
            return false;
        }
        return s_castMatrix[s, t];
    }

    /// <summary>
    /// Returns whether a cast from <paramref name="source"/> to <paramref name="target"/> is allowed,
    /// resolving both through <see cref="CastPrimitiveOf"/> first.
    /// </summary>
    internal static bool IsCastAllowed(XdmTypeCode source, XdmTypeCode target)
    {
        if (source == XdmTypeCode.AnyAtomicType || target == XdmTypeCode.AnyAtomicType)
        {
            return false;
        }
        return IsPrimitiveCastAllowed(CastPrimitiveOf(source), CastPrimitiveOf(target));
    }

    // -------------------------------------------------------------------------
    // Cast — public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Casts <paramref name="value"/> to <paramref name="target"/>, following F&amp;O §17.1.
    /// </summary>
    /// <param name="value">Source value. Must not be <see langword="null"/>.</param>
    /// <param name="target">Target type code.</param>
    /// <param name="location">Source location for error reporting.</param>
    /// <returns>The cast result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="XPathDynamicException">
    /// Thrown with <see cref="XPathErrors.XPTY0004"/> for N-cell casts or when target is xs:anyAtomicType.
    /// Thrown with <see cref="XPathErrors.FORG0001"/> when the value is not valid for the target type.
    /// </exception>
    internal static XdmAtomicValue Cast(XdmAtomicValue value, XdmTypeCode target, SourceSpan location)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#pragma warning restore CA1510

        if (target == XdmTypeCode.AnyAtomicType)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Cannot cast to xs:anyAtomicType (abstract type).",
                location);
        }

        // F&O §17.1: identity cast
        if (value.TypeCode == target)
        {
            return value;
        }

        XdmTypeCode sourcePrim = CastPrimitiveOf(value.TypeCode);
        XdmTypeCode targetPrim = CastPrimitiveOf(target);

        if (!IsPrimitiveCastAllowed(sourcePrim, targetPrim))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Cast from xs:{GetXsLocalName(value.TypeCode)} to xs:{GetXsLocalName(target)} is not allowed.",
                location);
        }

        // Perform the primitive cast
        XdmAtomicValue primResult = CastBetweenPrimitives(value, targetPrim, location);

        // If target is a derived type, apply facet validation and retype
        if (target != targetPrim)
        {
            XdmAtomicValue derived = primResult.WithTypeCode(target);
            if (!ValidateDerivedFacets(derived, target, out string? errorMessage))
            {
                throw new XPathDynamicException(
                    XPathErrors.FORG0001,
                    errorMessage ?? $"Value is not valid for type xs:{GetXsLocalName(target)}.",
                    location);
            }
            return derived;
        }

        return primResult;
    }

    // -------------------------------------------------------------------------
    // TryCast
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tries to cast <paramref name="value"/> to <paramref name="target"/>.
    /// Never throws; returns <see langword="false"/> on any failure.
    /// </summary>
    /// <param name="value">Source value (may be <see langword="null"/>).</param>
    /// <param name="target">Target type code.</param>
    /// <param name="result">On success, the cast result; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on success.</returns>
    internal static bool TryCast(XdmAtomicValue? value, XdmTypeCode target, out XdmAtomicValue? result)
    {
        result = null;
        if (value is null)
        {
            return false;
        }
        try
        {
            result = Cast(value, target, SourceSpan.None);
            return true;
        }
        catch (XPathDynamicException)
        {
            return false;
        }
        catch (ArgumentNullException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // TryPromote — F&O §3.1.5 function conversion rules
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies F&amp;O §3.1.5 function conversion rules: numeric promotion and xs:anyURI→xs:string.
    /// Also handles subtype→supertype and identity.
    /// </summary>
    /// <param name="value">Source value.</param>
    /// <param name="target">Target type code.</param>
    /// <param name="result">On success, the promoted value; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when promotion is applicable and succeeds.</returns>
    internal static bool TryPromote(XdmAtomicValue? value, XdmTypeCode target, out XdmAtomicValue? result)
    {
        result = null;

        if (value is null)
        {
            return false;
        }

        // Identity
        if (value.TypeCode == target)
        {
            result = value;
            return true;
        }

        // Subtype → supertype (upcast)
        if (IsSubtypeOf(value.TypeCode, target))
        {
            result = value.WithTypeCode(target);
            return true;
        }

        // F&O §3.1.5: numeric promotion — xs:integer → xs:decimal
        if (target == XdmTypeCode.Decimal && IsIntegerTypeCode(value.TypeCode))
        {
            result = TryCast(value, XdmTypeCode.Decimal, out XdmAtomicValue? r) ? r : null;
            return result is not null;
        }

        // F&O §3.1.5: xs:integer or xs:decimal → xs:float
        if (target == XdmTypeCode.Float
            && (IsIntegerTypeCode(value.TypeCode) || value.TypeCode == XdmTypeCode.Decimal))
        {
            result = TryCast(value, XdmTypeCode.Float, out XdmAtomicValue? r) ? r : null;
            return result is not null;
        }

        // F&O §3.1.5: xs:integer, xs:decimal, or xs:float → xs:double
        if (target == XdmTypeCode.Double
            && (IsIntegerTypeCode(value.TypeCode)
                || value.TypeCode == XdmTypeCode.Decimal
                || value.TypeCode == XdmTypeCode.Float))
        {
            result = TryCast(value, XdmTypeCode.Double, out XdmAtomicValue? r) ? r : null;
            return result is not null;
        }

        // F&O §3.1.5: xs:anyURI → xs:string
        if (target == XdmTypeCode.String && value.TypeCode == XdmTypeCode.AnyUri)
        {
            result = XdmAtomicValue.String(value.AsString());
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // ValidateDerivedFacets
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates that <paramref name="value"/> satisfies the XSD facets of <paramref name="target"/>
    /// (a derived type). Called after a successful primitive cast.
    /// </summary>
    /// <param name="value">The value to validate (TypeCode should equal target).</param>
    /// <param name="target">The derived target type.</param>
    /// <param name="errorMessage">When returning false, a human-readable explanation.</param>
    /// <returns><see langword="true"/> when the value satisfies all facets.</returns>
    internal static bool ValidateDerivedFacets(XdmAtomicValue value, XdmTypeCode target, out string? errorMessage)
    {
        errorMessage = null;

        switch (target)
        {
            // ---- Integer-family range checks ----
            case XdmTypeCode.NonPositiveInteger:
            {
                BigInteger v = value.AsBigInteger();
                if (v > BigInteger.Zero)
                {
                    errorMessage = $"Value {v} is not valid for xs:nonPositiveInteger (must be <= 0).";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.NegativeInteger:
            {
                BigInteger v = value.AsBigInteger();
                if (v >= BigInteger.Zero)
                {
                    errorMessage = $"Value {v} is not valid for xs:negativeInteger (must be < 0).";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Long:
            {
                BigInteger v = value.AsBigInteger();
                if (v < new BigInteger(long.MinValue) || v > new BigInteger(long.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:long.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Int:
            {
                BigInteger v = value.AsBigInteger();
                if (v < new BigInteger(int.MinValue) || v > new BigInteger(int.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:int.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Short:
            {
                BigInteger v = value.AsBigInteger();
                if (v < new BigInteger(short.MinValue) || v > new BigInteger(short.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:short.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Byte:
            {
                BigInteger v = value.AsBigInteger();
                if (v < new BigInteger(sbyte.MinValue) || v > new BigInteger(sbyte.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:byte.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.NonNegativeInteger:
            {
                BigInteger v = value.AsBigInteger();
                if (v < BigInteger.Zero)
                {
                    errorMessage = $"Value {v} is not valid for xs:nonNegativeInteger (must be >= 0).";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.UnsignedLong:
            {
                BigInteger v = value.AsBigInteger();
                BigInteger max = new BigInteger(ulong.MaxValue); // 18446744073709551615
                if (v < BigInteger.Zero || v > max)
                {
                    errorMessage = $"Value {v} is out of range for xs:unsignedLong.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.UnsignedInt:
            {
                BigInteger v = value.AsBigInteger();
                if (v < BigInteger.Zero || v > new BigInteger(uint.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:unsignedInt.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.UnsignedShort:
            {
                BigInteger v = value.AsBigInteger();
                if (v < BigInteger.Zero || v > new BigInteger(ushort.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:unsignedShort.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.UnsignedByte:
            {
                BigInteger v = value.AsBigInteger();
                if (v < BigInteger.Zero || v > new BigInteger(byte.MaxValue))
                {
                    errorMessage = $"Value {v} is out of range for xs:unsignedByte.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.PositiveInteger:
            {
                BigInteger v = value.AsBigInteger();
                if (v <= BigInteger.Zero)
                {
                    errorMessage = $"Value {v} is not valid for xs:positiveInteger (must be > 0).";
                    return false;
                }
                return true;
            }

            // ---- String-derived facet checks ----
            // Note: the value here is already whitespace-normalized per NormalizeWhitespace.
            // Facet validation checks the stored string against lexical constraints.

            case XdmTypeCode.NormalizedString:
            {
                string s = value.AsString();
                if (ContainsControlWhitespace(s))
                {
                    errorMessage = "xs:normalizedString must not contain tab (\\t), LF (\\n), or CR (\\r).";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Token:
            {
                string s = value.AsString();
                if (ContainsControlWhitespace(s))
                {
                    errorMessage = "xs:token must not contain \\t, \\n, or \\r.";
                    return false;
                }
                if (s.Length > 0 && (s[0] == ' ' || s[s.Length - 1] == ' '))
                {
                    errorMessage = "xs:token must not have leading or trailing spaces.";
                    return false;
                }
                if (ContainsDoubleSpace(s))
                {
                    errorMessage = "xs:token must not contain consecutive spaces.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.Language:
            {
                string s = value.AsString();
                // XSD language: [a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*
                if (!IsValidLanguage(s))
                {
                    errorMessage = $"'{s}' is not a valid xs:language value.";
                    return false;
                }
                return true;
            }

            case XdmTypeCode.NmToken:
            {
                string s = value.AsString();
                try
                {
                    XmlConvert.VerifyNMTOKEN(s);
                    return true;
                }
                catch (XmlException)
                {
                    errorMessage = $"'{s}' is not a valid xs:NMTOKEN value.";
                    return false;
                }
            }

            case XdmTypeCode.Name:
            {
                string s = value.AsString();
                try
                {
                    XmlConvert.VerifyName(s);
                    return true;
                }
                catch (XmlException)
                {
                    errorMessage = $"'{s}' is not a valid xs:Name value.";
                    return false;
                }
            }

            case XdmTypeCode.NcName:
            case XdmTypeCode.Id:
            case XdmTypeCode.IdRef:
            case XdmTypeCode.Entity:
            {
                string s = value.AsString();
                try
                {
                    XmlConvert.VerifyNCName(s);
                    return true;
                }
                catch (XmlException)
                {
                    errorMessage = $"'{s}' is not a valid xs:{GetXsLocalName(target)} value.";
                    return false;
                }
            }

            default:
                // No additional facets for this type
                return true;
        }
    }

    // -------------------------------------------------------------------------
    // CastBetweenPrimitives — the actual conversion dispatch
    // -------------------------------------------------------------------------

    // F&O §17.1 — cast between quasi-primitive types.
    private static XdmAtomicValue CastBetweenPrimitives(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XdmTypeCode sourcePrim = CastPrimitiveOf(value.TypeCode);

        // Identity at primitive level
        if (sourcePrim == targetPrim)
        {
            return value.WithTypeCode(targetPrim);
        }

        // --- String-like source → any: lexical parse ---
        if (sourcePrim == XdmTypeCode.String
            || sourcePrim == XdmTypeCode.UntypedAtomic
            || sourcePrim == XdmTypeCode.AnyUri)
        {
            // F&O §17.1: Notation cannot be a cast target from string
            if (targetPrim == XdmTypeCode.Notation)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    "xs:NOTATION cannot be used as a cast target from string (F&O §17.1).",
                    location);
            }
            try
            {
                return XdmAtomicValue.Parse(value.AsString(), targetPrim);
            }
            catch (XPathDynamicException)
            {
                throw;
            }
        }

        // --- Any → String / UntypedAtomic ---
        if (targetPrim == XdmTypeCode.String)
        {
            return XdmAtomicValue.String(value.GetStringValue());
        }
        if (targetPrim == XdmTypeCode.UntypedAtomic)
        {
            return XdmAtomicValue.UntypedAtomic(value.GetStringValue());
        }
        if (targetPrim == XdmTypeCode.AnyUri)
        {
            // Only String/UntypedAtomic/AnyUri → AnyUri allowed; already handled above.
            throw new InvalidOperationException("unreachable: non-string to AnyUri not in matrix");
        }

        // --- Specific primitive→primitive conversions ---
        // F&O §17.1
        switch (sourcePrim)
        {
            case XdmTypeCode.Integer:
                return CastIntegerTo(value, targetPrim, location);

            case XdmTypeCode.Decimal:
                return CastDecimalTo(value, targetPrim, location);

            case XdmTypeCode.Float:
                return CastFloatTo(value, targetPrim, location);

            case XdmTypeCode.Double:
                return CastDoubleTo(value, targetPrim, location);

            case XdmTypeCode.Boolean:
                return CastBooleanTo(value, targetPrim, location);

            case XdmTypeCode.Duration:
                return CastDurationTo(value, targetPrim, location);

            case XdmTypeCode.DayTimeDuration:
                return CastDayTimeDurationTo(value, targetPrim, location);

            case XdmTypeCode.YearMonthDuration:
                return CastYearMonthDurationTo(value, targetPrim, location);

            case XdmTypeCode.DateTime:
                return CastDateTimeTo(value, targetPrim, location);

            case XdmTypeCode.Date:
                return CastDateTo(value, targetPrim, location);

            case XdmTypeCode.GYearMonth:
                return CastGYearMonthTo(value, targetPrim, location);

            case XdmTypeCode.GMonthDay:
                return CastGMonthDayTo(value, targetPrim, location);

            case XdmTypeCode.HexBinary:
                if (targetPrim == XdmTypeCode.Base64Binary)
                {
                    return XdmAtomicValue.Base64Binary(value.AsHexBinary());
                }
                break;

            case XdmTypeCode.Base64Binary:
                if (targetPrim == XdmTypeCode.HexBinary)
                {
                    return XdmAtomicValue.HexBinary(value.AsBase64Binary());
                }
                break;
        }

        // Any remaining cells that reach here are N-cells (should not happen if matrix is correct)
        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            $"Cast from xs:{GetXsLocalName(value.TypeCode)} to xs:{GetXsLocalName(targetPrim)} is not allowed.",
            location);
    }

    // -------------------------------------------------------------------------
    // Integer casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastIntegerTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        BigInteger intVal = value.AsBigInteger();

        switch (targetPrim)
        {
            case XdmTypeCode.Decimal:
            {
                // BigInteger → decimal (may overflow if value > ~7.9e28)
                decimal d;
                try
                {
                    d = (decimal)intVal;
                }
                catch (OverflowException)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Integer value {intVal} overflows xs:decimal range.",
                        location);
                }
                return XdmAtomicValue.Decimal(d);
            }

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float((float)(double)intVal);

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double((double)intVal);

            case XdmTypeCode.Boolean:
                return XdmAtomicValue.Boolean(intVal != BigInteger.Zero);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:integer to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Decimal casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastDecimalTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        decimal decVal = (decimal)value.AsDecimal();

        switch (targetPrim)
        {
            case XdmTypeCode.Integer:
            {
                decimal truncated = decimal.Truncate(decVal);
                BigInteger bigVal;
                try
                {
                    bigVal = new BigInteger(truncated);
                }
                catch (OverflowException)
                {
                    // decimal.Truncate should not overflow BigInteger, but guard anyway
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Decimal value {decVal} cannot be converted to xs:integer.",
                        location);
                }
                return XdmAtomicValue.Integer(bigVal);
            }

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float((float)decVal);

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double((double)decVal);

            case XdmTypeCode.Boolean:
                return XdmAtomicValue.Boolean(decVal != 0m);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:decimal to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Float casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastFloatTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        float fVal = value.AsFloat();

        switch (targetPrim)
        {
            case XdmTypeCode.Integer:
            {
                if (float.IsNaN(fVal) || float.IsInfinity(fVal))
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Cannot cast xs:float value '{fVal}' to xs:integer.",
                        location);
                }
                double truncated = Math.Truncate((double)fVal);
                return XdmAtomicValue.Integer(new BigInteger(truncated));
            }

            case XdmTypeCode.Decimal:
            {
                if (float.IsNaN(fVal) || float.IsInfinity(fVal))
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Cannot cast xs:float value '{fVal}' to xs:decimal.",
                        location);
                }
                decimal d;
                try
                {
                    d = (decimal)fVal;
                }
                catch (OverflowException)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"xs:float value {fVal} overflows xs:decimal range.",
                        location);
                }
                return XdmAtomicValue.Decimal(d);
            }

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double((double)fVal);

            case XdmTypeCode.Boolean:
                return XdmAtomicValue.Boolean(!float.IsNaN(fVal) && fVal != 0.0f);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:float to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Double casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastDoubleTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        double dVal = value.AsDouble();

        switch (targetPrim)
        {
            case XdmTypeCode.Integer:
            {
                if (double.IsNaN(dVal) || double.IsInfinity(dVal))
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Cannot cast xs:double value '{dVal}' to xs:integer.",
                        location);
                }
                return XdmAtomicValue.Integer(new BigInteger(Math.Truncate(dVal)));
            }

            case XdmTypeCode.Decimal:
            {
                if (double.IsNaN(dVal) || double.IsInfinity(dVal))
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"Cannot cast xs:double value '{dVal}' to xs:decimal.",
                        location);
                }
                decimal d;
                try
                {
                    d = (decimal)dVal;
                }
                catch (OverflowException)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORG0001,
                        $"xs:double value {dVal} overflows xs:decimal range.",
                        location);
                }
                return XdmAtomicValue.Decimal(d);
            }

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float((float)dVal);

            case XdmTypeCode.Boolean:
                return XdmAtomicValue.Boolean(!double.IsNaN(dVal) && dVal != 0.0);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:double to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Boolean casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastBooleanTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        bool b = value.AsBoolean();

        switch (targetPrim)
        {
            case XdmTypeCode.Integer:
                return XdmAtomicValue.Integer(b ? 1L : 0L);

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Decimal(b ? 1m : 0m);

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float(b ? 1.0f : 0.0f);

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(b ? 1.0 : 0.0);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:boolean to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Duration family casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastDurationTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDuration dur = value.AsDuration();

        switch (targetPrim)
        {
            case XdmTypeCode.DayTimeDuration:
                // F&O §17.1: drop months component
                return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, dur.Seconds));

            case XdmTypeCode.YearMonthDuration:
                // F&O §17.1: drop seconds component
                return XdmAtomicValue.YearMonthDuration(new XsdDuration(dur.Months, new XsdDecimal(0m)));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:duration to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    private static XdmAtomicValue CastDayTimeDurationTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDuration dur = value.AsDuration();

        switch (targetPrim)
        {
            case XdmTypeCode.Duration:
                // DayTimeDuration IS a duration — just retype
                return value.WithTypeCode(XdmTypeCode.Duration);

            case XdmTypeCode.YearMonthDuration:
                // F&O §17.1: produces zero months
                return XdmAtomicValue.YearMonthDuration(new XsdDuration(0, new XsdDecimal(0m)));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:dayTimeDuration to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    private static XdmAtomicValue CastYearMonthDurationTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        switch (targetPrim)
        {
            case XdmTypeCode.Duration:
                return value.WithTypeCode(XdmTypeCode.Duration);

            case XdmTypeCode.DayTimeDuration:
                // F&O §17.1: produces zero seconds
                return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(0m)));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:yearMonthDuration to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Date/time casts
    // -------------------------------------------------------------------------

    private static XdmAtomicValue CastDateTimeTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDateTime dt = value.AsDateTime();

        switch (targetPrim)
        {
            case XdmTypeCode.Date:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.Date, dt.Year, dt.Month, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.Time:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.Time, 0, 0, 0, dt.Hour, dt.Minute, dt.Second, dt.TimezoneMinutes));

            case XdmTypeCode.GYearMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GYearMonth, dt.Year, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GYear:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GYear, dt.Year, 0, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GMonthDay:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonthDay, 0, dt.Month, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GDay:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GDay, 0, 0, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonth, 0, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:dateTime to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    private static XdmAtomicValue CastDateTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDateTime dt = value.AsDateTime();

        switch (targetPrim)
        {
            case XdmTypeCode.DateTime:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.DateTime, dt.Year, dt.Month, dt.Day, 0, 0, new XsdDecimal(0m), dt.TimezoneMinutes));

            case XdmTypeCode.GYearMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GYearMonth, dt.Year, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GYear:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GYear, dt.Year, 0, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GMonthDay:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonthDay, 0, dt.Month, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GDay:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GDay, 0, 0, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonth, 0, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:date to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    private static XdmAtomicValue CastGYearMonthTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDateTime dt = value.AsDateTime();

        switch (targetPrim)
        {
            case XdmTypeCode.GYear:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GYear, dt.Year, 0, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonth, 0, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:gYearMonth to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    private static XdmAtomicValue CastGMonthDayTo(XdmAtomicValue value, XdmTypeCode targetPrim, SourceSpan location)
    {
        XsdDateTime dt = value.AsDateTime();

        switch (targetPrim)
        {
            case XdmTypeCode.GMonth:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GMonth, 0, dt.Month, 0, 0, 0, default, dt.TimezoneMinutes));

            case XdmTypeCode.GDay:
                return XdmAtomicValue.DateTimeValue(
                    new XsdDateTime(XsdDateTimeKind.GDay, 0, 0, dt.Day, 0, 0, default, dt.TimezoneMinutes));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Cast from xs:gMonthDay to xs:{GetXsLocalName(targetPrim)} is not allowed.",
                    location);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool IsIntegerTypeCode(XdmTypeCode tc)
    {
        switch (tc)
        {
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
                return true;
            default:
                return false;
        }
    }

    private static bool ContainsControlWhitespace(string s)
    {
        foreach (char c in s)
        {
            if (c == '\t' || c == '\n' || c == '\r')
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsDoubleSpace(string s)
    {
        for (int i = 0; i + 1 < s.Length; i++)
        {
            if (s[i] == ' ' && s[i + 1] == ' ')
            {
                return true;
            }
        }
        return false;
    }

    // XSD language: [a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*
#pragma warning disable SYSLIB1045 // GeneratedRegex: not available on netstandard2.0
    private static readonly Regex s_languageRegex = new Regex(
        @"^[a-zA-Z]{1,8}(-[a-zA-Z0-9]{1,8})*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

    private static bool IsValidLanguage(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        return s_languageRegex.IsMatch(s);
    }
}
