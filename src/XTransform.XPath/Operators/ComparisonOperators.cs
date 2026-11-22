// F&O §10 — value comparison (op:*-equal, op:*-lt, etc.).
// XPath 2.0 §3.5.1 — value comparison semantics.

using System;
using System.Numerics;
using XTransform.Xdm;

namespace XTransform.XPath.Operators;

/// <summary>
/// Implementation of XPath 2.0 value-comparison operators per F&amp;O §10.
/// </summary>
internal static class ComparisonOperators
{
    /// <summary>
    /// op:*-equal per F&amp;O §10. Returns <see langword="null"/> for incomparable types
    /// (caller decides whether to raise XPTY0004 or return false for general comparison).
    /// Used by both value and general comparisons after type coercion.
    /// </summary>
    public static bool? ValueEqual(XdmAtomicValue left, XdmAtomicValue right)
    {
        // Coerce untyped to string for equals (default string coercion)
        // Note: general comparison handles untypedAtomic specially; here both are already coerced.
        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        XdmTypeCode lp = CastTable.PrimitiveTypeOf(lt);
        XdmTypeCode rp = CastTable.PrimitiveTypeOf(rt);

        // Both numeric → promote and compare
        if (NumericPromotion.IsNumeric(lt) && NumericPromotion.IsNumeric(rt))
        {
            XdmTypeCode common = SourceSpan.None is var _ ? CommonNumericCompareType(lt, rt) : CommonNumericCompareType(lt, rt);
            XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, SourceSpan.None);
            XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, SourceSpan.None);
            return NumericEqual(l, r);
        }

        // Both string family (using ordinal compare)
        if (lp == XdmTypeCode.String && rp == XdmTypeCode.String)
        {
            return string.Equals(left.GetStringValue(), right.GetStringValue(), StringComparison.Ordinal);
        }

        // UntypedAtomic vs UntypedAtomic → string compare
        if (lt == XdmTypeCode.UntypedAtomic && rt == XdmTypeCode.UntypedAtomic)
        {
            return string.Equals(left.GetStringValue(), right.GetStringValue(), StringComparison.Ordinal);
        }

        // anyURI vs anyURI
        if (lt == XdmTypeCode.AnyUri && rt == XdmTypeCode.AnyUri)
        {
            return string.Equals(left.GetStringValue(), right.GetStringValue(), StringComparison.Ordinal);
        }

        // anyURI vs string (permitted by F&O)
        if ((lt == XdmTypeCode.AnyUri || lp == XdmTypeCode.String)
            && (rt == XdmTypeCode.AnyUri || rp == XdmTypeCode.String))
        {
            return string.Equals(left.GetStringValue(), right.GetStringValue(), StringComparison.Ordinal);
        }

        // Both boolean
        if (lt == XdmTypeCode.Boolean && rt == XdmTypeCode.Boolean)
        {
            return left.AsBoolean() == right.AsBoolean();
        }

        // Both QName — component-wise
        if (lt == XdmTypeCode.QName && rt == XdmTypeCode.QName)
        {
            return left.AsQName().Equals(right.AsQName());
        }

        // Both duration family
        if (IsDurationType(lt) && IsDurationType(rt))
        {
            if (lt != rt)
            {
                // Different subtypes can be compared for equality if same effective type
                // per F&O §10.3: xs:duration equality: same months AND same seconds
                XsdDuration ld = left.AsDuration();
                XsdDuration rd = right.AsDuration();
                return ld.Months == rd.Months && ld.Seconds.Value == rd.Seconds.Value;
            }
            XsdDuration ldur = left.AsDuration();
            XsdDuration rdur = right.AsDuration();
            return ldur.Months == rdur.Months && ldur.Seconds.Value == rdur.Seconds.Value;
        }

        // Both dateTime family (same primitive type)
        if (lp == rp && IsDateTimeType(lt))
        {
            return left.AsDateTime().Equals(right.AsDateTime());
        }

        // Both hexBinary / base64Binary
        if (lt == XdmTypeCode.HexBinary && rt == XdmTypeCode.HexBinary)
        {
            return left.AsHexBinary().Span.SequenceEqual(right.AsHexBinary().Span);
        }

        if (lt == XdmTypeCode.Base64Binary && rt == XdmTypeCode.Base64Binary)
        {
            return left.AsBase64Binary().Span.SequenceEqual(right.AsBase64Binary().Span);
        }

        // Incomparable
        return null;
    }

    /// <summary>
    /// op:*-lt / op:*-gt / op:*-le / op:*-ge as a signed comparison result (-1, 0, +1).
    /// Returns <see langword="null"/> for incomparable types.
    /// </summary>
    public static int? ValueCompare(XdmAtomicValue left, XdmAtomicValue right)
    {
        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        XdmTypeCode lp = CastTable.PrimitiveTypeOf(lt);
        XdmTypeCode rp = CastTable.PrimitiveTypeOf(rt);

        // Both numeric
        if (NumericPromotion.IsNumeric(lt) && NumericPromotion.IsNumeric(rt))
        {
            XdmTypeCode common = CommonNumericCompareType(lt, rt);
            XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, SourceSpan.None);
            XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, SourceSpan.None);
            return NumericCompare(l, r);
        }

        // Both string
        if (lp == XdmTypeCode.String && rp == XdmTypeCode.String)
        {
            return string.CompareOrdinal(left.GetStringValue(), right.GetStringValue());
        }

        // Both untypedAtomic
        if (lt == XdmTypeCode.UntypedAtomic && rt == XdmTypeCode.UntypedAtomic)
        {
            return string.CompareOrdinal(left.GetStringValue(), right.GetStringValue());
        }

        // anyURI
        if (lt == XdmTypeCode.AnyUri && rt == XdmTypeCode.AnyUri)
        {
            return string.CompareOrdinal(left.GetStringValue(), right.GetStringValue());
        }

        // Both boolean
        if (lt == XdmTypeCode.Boolean && rt == XdmTypeCode.Boolean)
        {
            bool lb = left.AsBoolean();
            bool rb = right.AsBoolean();
            if (lb == rb) { return 0; }
            return lb ? 1 : -1;  // false < true
        }

        // QName — not orderable
        if (lt == XdmTypeCode.QName || rt == XdmTypeCode.QName)
        {
            return null;
        }

        // Duration family
        if (IsDurationType(lt) && IsDurationType(rt))
        {
            if (lt == XdmTypeCode.YearMonthDuration && rt == XdmTypeCode.YearMonthDuration)
            {
                return left.AsDuration().Months.CompareTo(right.AsDuration().Months);
            }

            if (lt == XdmTypeCode.DayTimeDuration && rt == XdmTypeCode.DayTimeDuration)
            {
                return left.AsDuration().Seconds.Value.CompareTo(right.AsDuration().Seconds.Value);
            }

            // Plain xs:duration is a partial order — not comparable
            return null;
        }

        // DateTime family — same kind
        if (lp == rp && IsDateTimeType(lt))
        {
            return CompareDateTimes(left.AsDateTime(), right.AsDateTime());
        }

        // Binary
        if (lt == XdmTypeCode.HexBinary && rt == XdmTypeCode.HexBinary)
        {
            return CompareSpans(left.AsHexBinary().Span, right.AsHexBinary().Span);
        }

        if (lt == XdmTypeCode.Base64Binary && rt == XdmTypeCode.Base64Binary)
        {
            return CompareSpans(left.AsBase64Binary().Span, right.AsBase64Binary().Span);
        }

        return null;
    }

    /// <summary>
    /// Coerces an atomic value for comparison to a target primitive type.
    /// Returns <see langword="null"/> when coercion is not applicable.
    /// </summary>
    public static XdmAtomicValue? CoerceForComparison(XdmAtomicValue value, XdmTypeCode targetPrimitive, SourceSpan location)
    {
        if (value.TypeCode == targetPrimitive || CastTable.IsSubtypeOf(value.TypeCode, targetPrimitive))
        {
            return value;
        }

        if (!CastTable.TryCast(value, targetPrimitive, out XdmAtomicValue? result))
        {
            return null;
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static XdmTypeCode CommonNumericCompareType(XdmTypeCode lt, XdmTypeCode rt)
    {
        if (lt == XdmTypeCode.Double || rt == XdmTypeCode.Double)
        {
            return XdmTypeCode.Double;
        }
        if (lt == XdmTypeCode.Float || rt == XdmTypeCode.Float)
        {
            return XdmTypeCode.Float;
        }
        if (lt == XdmTypeCode.Decimal || rt == XdmTypeCode.Decimal)
        {
            return XdmTypeCode.Decimal;
        }
        return XdmTypeCode.Integer;
    }

    private static bool NumericEqual(XdmAtomicValue l, XdmAtomicValue r)
    {
        switch (l.TypeCode)
        {
            case XdmTypeCode.Integer:
                return l.AsBigInteger() == r.AsBigInteger();

            case XdmTypeCode.Decimal:
                return l.AsDecimal().Value == r.AsDecimal().Value;

            case XdmTypeCode.Float:
            {
                float lf = l.AsFloat();
                float rf = r.AsFloat();
#pragma warning disable CS1718 // NaN check intentional
                return lf == rf && !float.IsNaN(lf);
#pragma warning restore CS1718
            }

            case XdmTypeCode.Double:
            {
                double ld = l.AsDouble();
                double rd = r.AsDouble();
#pragma warning disable CS1718 // NaN check intentional
                return ld == rd && !double.IsNaN(ld);
#pragma warning restore CS1718
            }

            default:
                return false;
        }
    }

    private static int NumericCompare(XdmAtomicValue l, XdmAtomicValue r)
    {
        switch (l.TypeCode)
        {
            case XdmTypeCode.Integer:
                return l.AsBigInteger().CompareTo(r.AsBigInteger());

            case XdmTypeCode.Decimal:
                return l.AsDecimal().Value.CompareTo(r.AsDecimal().Value);

            case XdmTypeCode.Float:
                return l.AsFloat().CompareTo(r.AsFloat());

            case XdmTypeCode.Double:
                return l.AsDouble().CompareTo(r.AsDouble());

            default:
                return 0;
        }
    }

    private static int CompareDateTimes(XsdDateTime l, XsdDateTime r)
    {
        // Simplified component-by-component comparison (no TZ normalization for P2-05)
        int c = l.Year.CompareTo(r.Year);
        if (c != 0) { return c; }
        c = l.Month.CompareTo(r.Month);
        if (c != 0) { return c; }
        c = l.Day.CompareTo(r.Day);
        if (c != 0) { return c; }
        c = l.Hour.CompareTo(r.Hour);
        if (c != 0) { return c; }
        c = l.Minute.CompareTo(r.Minute);
        if (c != 0) { return c; }
        return l.Second.Value.CompareTo(r.Second.Value);
    }

    private static int CompareSpans(ReadOnlySpan<byte> l, ReadOnlySpan<byte> r)
    {
        int minLen = l.Length < r.Length ? l.Length : r.Length;
        for (int i = 0; i < minLen; i++)
        {
            int c = l[i].CompareTo(r[i]);
            if (c != 0) { return c; }
        }
        return l.Length.CompareTo(r.Length);
    }

    private static bool IsDurationType(XdmTypeCode tc)
    {
        return tc == XdmTypeCode.Duration
            || tc == XdmTypeCode.DayTimeDuration
            || tc == XdmTypeCode.YearMonthDuration;
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
}
