// F&O §6.4 and §15.4 — numeric and aggregate functions.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Operators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §6.4 single-item numeric functions and §15.4 aggregate
/// functions: fn:abs, fn:ceiling, fn:floor, fn:round, fn:round-half-to-even,
/// fn:sum, fn:avg, fn:min, fn:max, fn:count, fn:number.
/// </summary>
internal static class NumericFunctions
{
    // =========================================================================
    // F&O §6.4.1 — fn:abs($arg as xs:numeric?) as xs:numeric?
    // =========================================================================

    /// <summary>Implements fn:abs($arg as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnAbs(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.1: empty → empty; preserve type family.
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:abs");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(AbsValue(av));
    }

    private static XdmAtomicValue AbsValue(XdmAtomicValue av)
    {
        // F&O §6.4.1: abs preserves type family.
        switch (av.TypeCode)
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
                return XdmAtomicValue.Integer(BigInteger.Abs(av.AsBigInteger()));

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Decimal(Math.Abs(av.AsDecimal().Value));

            case XdmTypeCode.Float:
            {
                float fv = av.AsFloat();
                return XdmAtomicValue.Float(fv < 0f ? -fv : (fv == 0f ? 0f : fv));
            }

            case XdmTypeCode.Double:
            {
                double dv = av.AsDouble();
                // F&O §6.4.1: abs(-0.0e0) → 0.0e0
                return XdmAtomicValue.Double(dv < 0.0 ? -dv : (dv == 0.0 ? 0.0 : dv));
            }

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"fn:abs requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
        }
    }

    // =========================================================================
    // F&O §6.4.2 — fn:ceiling($arg as xs:numeric?) as xs:numeric?
    // =========================================================================

    /// <summary>Implements fn:ceiling($arg as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnCeiling(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.2: empty → empty; integer family → identity; preserve type.
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:ceiling");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(CeilingValue(av));
    }

    private static XdmAtomicValue CeilingValue(XdmAtomicValue av)
    {
        switch (av.TypeCode)
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
                // F&O §6.4.2: xs:integer ceiling is identity.
                return av;

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Decimal(Math.Ceiling(av.AsDecimal().Value));

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float((float)Math.Ceiling(av.AsFloat()));

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(Math.Ceiling(av.AsDouble()));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"fn:ceiling requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
        }
    }

    // =========================================================================
    // F&O §6.4.3 — fn:floor($arg as xs:numeric?) as xs:numeric?
    // =========================================================================

    /// <summary>Implements fn:floor($arg as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnFloor(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.3: empty → empty; integer family → identity; preserve type.
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:floor");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(FloorValue(av));
    }

    private static XdmAtomicValue FloorValue(XdmAtomicValue av)
    {
        switch (av.TypeCode)
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
                // F&O §6.4.3: xs:integer floor is identity.
                return av;

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Decimal(Math.Floor(av.AsDecimal().Value));

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float((float)Math.Floor(av.AsFloat()));

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(Math.Floor(av.AsDouble()));

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"fn:floor requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
        }
    }

    // =========================================================================
    // F&O §6.4.4 — fn:round($arg as xs:numeric?) as xs:numeric?
    // =========================================================================

    /// <summary>Implements fn:round($arg as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnRound(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.4: round half towards +∞ (not banker's).
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:round");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(RoundValue(av));
    }

    private static XdmAtomicValue RoundValue(XdmAtomicValue av)
    {
        switch (av.TypeCode)
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
                // F&O §6.4.4: xs:integer round is identity.
                return av;

            case XdmTypeCode.Decimal:
            {
                // F&O §6.4.4: round half towards +∞ → Math.Floor(x + 0.5m)
                // round(2.5)=3, round(-2.5)=-2
                // xs:decimal cannot represent infinity or NaN, so no special-case needed.
                decimal d = av.AsDecimal().Value;
                return XdmAtomicValue.Decimal(Math.Floor(d + 0.5m));
            }

            case XdmTypeCode.Float:
            {
                float f = av.AsFloat();
                if (float.IsNaN(f) || float.IsInfinity(f) || f == 0f)
                {
                    return av;
                }

                // F&O §6.4.4: Floor(x + 0.5) for half-to-+∞ rounding.
                return XdmAtomicValue.Float((float)Math.Floor((double)f + 0.5));
            }

            case XdmTypeCode.Double:
            {
                double d = av.AsDouble();
                if (double.IsNaN(d) || double.IsInfinity(d) || d == 0.0)
                {
                    return av;
                }

                // F&O §6.4.4: Floor(x + 0.5) for half-to-+∞ rounding.
                return XdmAtomicValue.Double(Math.Floor(d + 0.5));
            }

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"fn:round requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
        }
    }

    // =========================================================================
    // F&O §6.4.5 — fn:round-half-to-even($arg, [$precision]) as xs:numeric?
    // =========================================================================

    /// <summary>Implements fn:round-half-to-even($arg as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnRoundHalfToEven1(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.5 1-arg: precision = 0 (round to nearest integer).
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:round-half-to-even");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(RoundHalfToEvenValue(av, 0));
    }

    /// <summary>Implements fn:round-half-to-even($arg as xs:anyAtomicType?, $precision as xs:integer) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnRoundHalfToEven2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.5 2-arg: round to $precision decimal places.
        XdmAtomicValue? av = SingleNumericOrEmpty(args[0], "fn:round-half-to-even");
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        XdmItem? precItem = args[1].FirstOrNull();
        int precision = precItem is null ? 0 : (int)((XdmAtomicValue)precItem).AsInt64();

        return new SingleItemIterator(RoundHalfToEvenValue(av, precision));
    }

    private static XdmAtomicValue RoundHalfToEvenValue(XdmAtomicValue av, int precision)
    {
        // F&O §6.4.5: banker's rounding (MidpointRounding.ToEven).
        switch (av.TypeCode)
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
            {
                if (precision >= 0)
                {
                    // Rounding an integer with non-negative precision → identity.
                    return av;
                }

                // Negative precision: round to nearest 10^|precision|.
                // e.g. round-half-to-even(355, -2) → 400 (nearest 100).
                BigInteger val = av.AsBigInteger();
                BigInteger factor = BigInteger.Pow(10, -precision);
                BigInteger half = factor / 2;
                BigInteger rem = val % factor;
                if (rem < 0)
                {
                    rem = -rem;
                }

                BigInteger absVal = BigInteger.Abs(val);
                BigInteger rounded;
                bool negative = val < BigInteger.Zero;

                if (rem < half)
                {
                    rounded = absVal - (BigInteger.Abs(val % factor));
                }
                else if (rem > half)
                {
                    rounded = absVal - (BigInteger.Abs(val % factor)) + factor;
                }
                else
                {
                    // Exactly halfway: round to even.
                    BigInteger lower = absVal - half;
                    BigInteger higher = lower + factor;
                    rounded = (lower / factor % 2 == 0) ? lower : higher;
                }

                return XdmAtomicValue.Integer(negative ? -rounded : rounded);
            }

            case XdmTypeCode.Decimal:
            {
                // xs:decimal cannot represent infinity or NaN.
                decimal d = av.AsDecimal().Value;

                // Clamp precision to decimal's supported range (0–28 for Math.Round).
                int clampedPrecision = Math.Max(0, Math.Min(28, precision));
                decimal rounded;
                if (precision < 0)
                {
                    // Round to left of decimal point.
                    decimal factor = (decimal)Math.Pow(10, -precision);
                    rounded = Math.Round(d / factor, 0, MidpointRounding.ToEven) * factor;
                }
                else
                {
                    rounded = Math.Round(d, clampedPrecision, MidpointRounding.ToEven);
                }

                return XdmAtomicValue.Decimal(rounded);
            }

            case XdmTypeCode.Float:
            {
                float f = av.AsFloat();
                if (float.IsNaN(f) || float.IsInfinity(f) || f == 0f)
                {
                    return av;
                }

                return XdmAtomicValue.Float((float)Math.Round((double)f, Math.Max(0, Math.Min(15, precision)), MidpointRounding.ToEven));
            }

            case XdmTypeCode.Double:
            {
                double d = av.AsDouble();
                if (double.IsNaN(d) || double.IsInfinity(d) || d == 0.0)
                {
                    return av;
                }

                if (precision < 0)
                {
                    double factor = Math.Pow(10, -precision);
                    return XdmAtomicValue.Double(Math.Round(d / factor, 0, MidpointRounding.ToEven) * factor);
                }

                return XdmAtomicValue.Double(Math.Round(d, Math.Max(0, Math.Min(15, precision)), MidpointRounding.ToEven));
            }

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"fn:round-half-to-even requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
        }
    }

    // =========================================================================
    // F&O §15.4.9 — fn:sum
    // =========================================================================

    /// <summary>Implements fn:sum($arg as xs:anyAtomicType*) as xs:anyAtomicType.</summary>
    internal static IXdmIterator FnSum1(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.9: empty seq → xs:integer(0).
        List<XdmAtomicValue> prepared = PrepareNumericSequence(args[0], "fn:sum", requireNumeric: false);
        ValidateSummable(prepared, "fn:sum");
        if (prepared.Count == 0)
        {
            return new SingleItemIterator(XdmAtomicValue.Integer(0L));
        }

        return new SingleItemIterator(SumPrepared(prepared));
    }

    /// <summary>Implements fn:sum($arg as xs:anyAtomicType*, $zero as xs:anyAtomicType?) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnSum2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.9: empty seq → return the second argument (which may itself be empty).
        List<XdmAtomicValue> prepared = PrepareNumericSequence(args[0], "fn:sum", requireNumeric: false);
        ValidateSummable(prepared, "fn:sum");
        if (prepared.Count == 0)
        {
            XdmAtomicValue? zero = (XdmAtomicValue?)args[1].FirstOrNull();
            if (zero is null)
            {
                return EmptyIterator.Instance;
            }

            return new SingleItemIterator(zero);
        }

        return new SingleItemIterator(SumPrepared(prepared));
    }

    private static XdmAtomicValue SumPrepared(List<XdmAtomicValue> prepared)
    {
        // Fold with Add — ArithmeticOperators.Add handles numeric promotion internally.
        XdmAtomicValue running = prepared[0];
        for (int i = 1; i < prepared.Count; i++)
        {
            running = ArithmeticOperators.Add(running, prepared[i], default);
        }

        return running;
    }

    // =========================================================================
    // F&O §15.4.4 — fn:avg
    // =========================================================================

    /// <summary>Implements fn:avg($arg as xs:anyAtomicType*) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnAvg(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.4: empty → empty sequence.
        List<XdmAtomicValue> prepared = PrepareNumericSequence(args[0], "fn:avg", requireNumeric: false);
        ValidateSummable(prepared, "fn:avg");

        if (prepared.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        if (prepared.Count == 1)
        {
            return new SingleItemIterator(prepared[0]);
        }

        // Sum then divide by count.
        XdmAtomicValue sum = prepared[0];
        for (int i = 1; i < prepared.Count; i++)
        {
            sum = ArithmeticOperators.Add(sum, prepared[i], default);
        }

        // Divide sum by count — count is xs:integer, so if sum is integer, result is decimal.
        XdmAtomicValue count = XdmAtomicValue.Integer((long)prepared.Count);
        XdmAtomicValue avg = ArithmeticOperators.Divide(sum, count, default);

        return new SingleItemIterator(avg);
    }

    // =========================================================================
    // F&O §15.4.5 / §15.4.6 — fn:min / fn:max
    // =========================================================================

    /// <summary>Implements fn:min($arg as xs:anyAtomicType*) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnMin1(DynamicContext ctx, XdmSequence[] args)
    {
        return MinMaxImpl(args[0], isMin: true);
    }

    /// <summary>Implements fn:min($arg as xs:anyAtomicType*, $collation as xs:string) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnMin2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.5: collation parameter — scope is numeric, ignore collation.
        return MinMaxImpl(args[0], isMin: true);
    }

    /// <summary>Implements fn:max($arg as xs:anyAtomicType*) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnMax1(DynamicContext ctx, XdmSequence[] args)
    {
        return MinMaxImpl(args[0], isMin: false);
    }

    /// <summary>Implements fn:max($arg as xs:anyAtomicType*, $collation as xs:string) as xs:anyAtomicType?.</summary>
    internal static IXdmIterator FnMax2(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.6: collation parameter — scope is numeric, ignore collation.
        return MinMaxImpl(args[0], isMin: false);
    }

    // fn:sum and fn:avg accept numeric values, or durations that are all xs:dayTimeDuration or all
    // xs:yearMonthDuration (F&O §15.4.4, §15.4.9).
    private static void ValidateSummable(List<XdmAtomicValue> values, string fnName)
    {
        if (values.Count == 0)
        {
            return;
        }

        bool allNumeric = true;
        foreach (XdmAtomicValue v in values)
        {
            allNumeric &= NumericPromotion.IsNumeric(v.TypeCode);
        }

        if (allNumeric)
        {
            return;
        }

        XdmTypeCode first = values[0].TypeCode;
        bool durations = first is XdmTypeCode.DayTimeDuration or XdmTypeCode.YearMonthDuration;
        foreach (XdmAtomicValue v in values)
        {
            if (!durations || v.TypeCode != first)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{fnName} requires numeric values or durations of one kind; item has type xs:{CastTable.GetXsLocalName(v.TypeCode)}.",
                    default);
            }
        }
    }

    // fn:min / fn:max over values that are not all numeric (strings, dates, times, durations): the values must be
    // comparable with each other (F&O §15.4.5, §15.4.6).
    private static SingleItemIterator MinMaxComparable(List<XdmAtomicValue> values, bool isMin, string fnName)
    {
        XdmAtomicValue extreme = values[0];
        // Only ordered types may be compared: xs:duration (as opposed to its two subtypes), the Gregorian partial
        // dates, QName, NOTATION and the binary types have no order (F&O §15.4.5).
        foreach (XdmAtomicValue v in values)
        {
            if (v.TypeCode is XdmTypeCode.Duration or XdmTypeCode.GYearMonth or XdmTypeCode.GYear or XdmTypeCode.GMonthDay
                or XdmTypeCode.GDay or XdmTypeCode.GMonth or XdmTypeCode.QName or XdmTypeCode.Notation or XdmTypeCode.HexBinary)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{fnName} cannot order values of type xs:{CastTable.GetXsLocalName(v.TypeCode)}.",
                    default);
            }
        }

        for (int i = 1; i < values.Count; i++)
        {
            int? cmp = ComparisonOperators.ValueCompare(values[i], extreme);
            if (cmp is null)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{fnName} encountered values that cannot be compared.",
                    default);
            }

            if ((isMin && cmp.Value < 0) || (!isMin && cmp.Value > 0))
            {
                extreme = values[i];
            }
        }

        return new SingleItemIterator(extreme);
    }

    private static IXdmIterator MinMaxImpl(XdmSequence seq, bool isMin)
    {
        // F&O §15.4.5/§15.4.6: empty → empty.
        string fnName = isMin ? "fn:min" : "fn:max";
        List<XdmAtomicValue> prepared = PrepareNumericSequence(seq, fnName, requireNumeric: false);

        if (prepared.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        bool allNumeric = true;
        foreach (XdmAtomicValue v in prepared)
        {
            allNumeric &= NumericPromotion.IsNumeric(v.TypeCode);
        }

        if (!allNumeric)
        {
            return MinMaxComparable(prepared, isMin, fnName);
        }

        // Determine common numeric type by folding the promotion lattice across all items.
        // We use CommonNumericTypeCode directly, expanding the running type code vs each item.
        XdmTypeCode targetType = prepared[0].TypeCode;
        for (int i = 1; i < prepared.Count; i++)
        {
            targetType = ExpandCommonNumericType(targetType, prepared[i].TypeCode);
        }

        // Promote all to the common type, then find the min/max.
        XdmAtomicValue extreme = NumericPromotion.PromoteNumeric(prepared[0], targetType, default);
        for (int i = 1; i < prepared.Count; i++)
        {
            XdmAtomicValue current = NumericPromotion.PromoteNumeric(prepared[i], targetType, default);
            int? cmp = ComparisonOperators.ValueCompare(current, extreme);
            if (cmp is null)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{fnName} encountered incomparable values.",
                    default);
            }

            if ((isMin && cmp.Value < 0) || (!isMin && cmp.Value > 0))
            {
                extreme = current;
            }
        }

        return new SingleItemIterator(extreme);
    }

    // =========================================================================
    // F&O §15.4.3 — fn:count
    // =========================================================================

    /// <summary>Implements fn:count($arg as item()*) as xs:integer.</summary>
    internal static IXdmIterator FnCount(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.4.3: returns the number of items in the sequence.
        // XdmSequence.Count is O(1) since it is pre-materialised.
        return new SingleItemIterator(XdmAtomicValue.Integer((long)args[0].Count));
    }

    // =========================================================================
    // F&O §6.4.6 — fn:number
    // =========================================================================

    /// <summary>Implements fn:number() as xs:double — uses context item.</summary>
    internal static IXdmIterator FnNumber0(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.6: 0-arg — atomize the context item.
        XdmItem? ci = ctx.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:number() with no argument requires a context item.",
                default);
        }

        // Atomize the context item.
        XdmAtomicValue av = ci.IsAtomic
            ? (XdmAtomicValue)ci
            : XdmAtomicValue.UntypedAtomic(ci.GetStringValue());

        return new SingleItemIterator(NumberValue(av));
    }

    /// <summary>Implements fn:number($arg as xs:anyAtomicType?) as xs:double.</summary>
    internal static IXdmIterator FnNumber1(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §6.4.6: 1-arg variant.
        XdmItem? item = args[0].FirstOrNull();
        if (item is null)
        {
            // F&O §6.4.6: empty sequence → NaN.
            return new SingleItemIterator(XdmAtomicValue.Double(double.NaN));
        }

        XdmAtomicValue av = (XdmAtomicValue)item;
        return new SingleItemIterator(NumberValue(av));
    }

    private static XdmAtomicValue NumberValue(XdmAtomicValue av)
    {
        // F&O §6.4.6: convert to xs:double; non-castable → NaN.
        switch (av.TypeCode)
        {
            case XdmTypeCode.Double:
                return av;

            case XdmTypeCode.Float:
                return XdmAtomicValue.Double((double)av.AsFloat());

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Double((double)av.AsDecimal().Value);

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
                return XdmAtomicValue.Double((double)(long)av.AsBigInteger());

            case XdmTypeCode.Boolean:
                // F&O §6.4.6: boolean → 1.0 or 0.0.
                return XdmAtomicValue.Double(av.AsBoolean() ? 1.0 : 0.0);

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
            {
                // Try to parse as xs:double.
                string s = av.GetStringValue().Trim();
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    return XdmAtomicValue.Double(parsed);
                }

                // Handle special XPath literals.
                if (string.Equals(s, "INF", StringComparison.Ordinal))
                {
                    return XdmAtomicValue.Double(double.PositiveInfinity);
                }

                if (string.Equals(s, "-INF", StringComparison.Ordinal))
                {
                    return XdmAtomicValue.Double(double.NegativeInfinity);
                }

                if (string.Equals(s, "NaN", StringComparison.Ordinal))
                {
                    return XdmAtomicValue.Double(double.NaN);
                }

                return XdmAtomicValue.Double(double.NaN);
            }

            default:
                // All other types → NaN per F&O §6.4.6.
                return XdmAtomicValue.Double(double.NaN);
        }
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Expands the running common numeric type to include <paramref name="next"/>.
    /// Implements the F&amp;O §6.2 promotion lattice: integer &lt; decimal &lt; float &lt; double.
    /// </summary>
    private static XdmTypeCode ExpandCommonNumericType(XdmTypeCode running, XdmTypeCode next)
    {
        // Normalise integer-family to Integer for comparison.
        XdmTypeCode ra = NumericPromotion.IsIntegerFamily(running) ? XdmTypeCode.Integer : running;
        XdmTypeCode nb = NumericPromotion.IsIntegerFamily(next) ? XdmTypeCode.Integer : next;

        // Lattice: Integer < Decimal < Float < Double
        if (ra == XdmTypeCode.Double || nb == XdmTypeCode.Double)
        {
            return XdmTypeCode.Double;
        }

        if (ra == XdmTypeCode.Float || nb == XdmTypeCode.Float)
        {
            return XdmTypeCode.Float;
        }

        if (ra == XdmTypeCode.Decimal || nb == XdmTypeCode.Decimal)
        {
            return XdmTypeCode.Decimal;
        }

        return XdmTypeCode.Integer;
    }

    /// <summary>
    /// Returns the single numeric atomic value from a ZeroOrOne sequence,
    /// or <see langword="null"/> if the sequence is empty.
    /// Raises XPTY0004 if the item is not numeric.
    /// </summary>
    private static XdmAtomicValue? SingleNumericOrEmpty(XdmSequence seq, string fnName)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return null;
        }

        XdmAtomicValue av = (XdmAtomicValue)item;

        // Cast xs:untypedAtomic to xs:double per F&O §6.2.1.
        if (av.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            av = CastTable.Cast(av, XdmTypeCode.Double, default);
        }

        if (!NumericPromotion.IsNumeric(av.TypeCode))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"{fnName} requires a numeric argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                default);
        }

        return av;
    }

    /// <summary>
    /// Materialises a sequence into a list of numeric atomic values.
    /// xs:untypedAtomic items are cast to xs:double.
    /// Raises XPTY0004 for non-numeric items.
    /// </summary>
    private static List<XdmAtomicValue> PrepareNumericSequence(XdmSequence seq, string fnName, bool requireNumeric = true)
    {
        List<XdmAtomicValue> result = new List<XdmAtomicValue>(seq.Count);
        IXdmIterator it = seq.GetIterator();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            XdmAtomicValue av = (XdmAtomicValue)item;

            // F&O §15.4: cast xs:untypedAtomic to xs:double.
            if (av.TypeCode == XdmTypeCode.UntypedAtomic)
            {
                av = CastTable.Cast(av, XdmTypeCode.Double, default);
            }

            if (requireNumeric && !NumericPromotion.IsNumeric(av.TypeCode))
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{fnName} requires a numeric sequence; item has type xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                    default);
            }

            result.Add(av);
        }

        return result;
    }
}
