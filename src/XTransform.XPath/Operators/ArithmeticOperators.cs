// F&O §6.2 — arithmetic operations on numeric types and durations.

using System;
using System.Numerics;
using XTransform.Xdm;

namespace XTransform.XPath.Operators;

/// <summary>
/// Implementation of XPath 2.0 arithmetic operators per F&amp;O §6.2.
/// Callers must pre-process operands through <see cref="Atomization.AtomizeSingle"/> before invoking.
/// </summary>
internal static class ArithmeticOperators
{
    // ── Add ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>op:add</c> for numeric, duration, and dateTime operand pairs.
    /// F&amp;O §6.2.1, §10.5, §10.7.
    /// </summary>
    public static XdmAtomicValue Add(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        // F&O §6.2.1: xs:untypedAtomic → cast to xs:double
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // ── duration + duration ───────────────────────────────────────────────
        // F&O §10.5: duration subtypes only; mixed subtypes → XPTY0004
        if (IsDurationType(lt) && IsDurationType(rt))
        {
            return AddDurations(left, right, location);
        }

        // ── dateTime + duration ───────────────────────────────────────────────
        // F&O §10.7
        if (IsDateTimeType(lt) && IsDurationType(rt))
        {
            return AddDateTimeDuration(left, right, location);
        }

        if (IsDurationType(lt) && IsDateTimeType(rt))
        {
            // duration + dateTime = dateTime + duration (commutative per F&O §10.7)
            return AddDateTimeDuration(right, left, location);
        }

        // ── numeric + numeric ─────────────────────────────────────────────────
        XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
        XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
        XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);

        return common switch
        {
            XdmTypeCode.Integer => XdmAtomicValue.Integer(l.AsBigInteger() + r.AsBigInteger()),
            XdmTypeCode.Decimal => XdmAtomicValue.Decimal(AddDecimal(l.AsDecimal().Value, r.AsDecimal().Value, location)),
            XdmTypeCode.Float   => XdmAtomicValue.Float(l.AsFloat() + r.AsFloat()),
            XdmTypeCode.Double  => XdmAtomicValue.Double(l.AsDouble() + r.AsDouble()),
            _ => throw new InvalidOperationException($"Unexpected common numeric type: {common}"),
        };
    }

    // ── Subtract ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>op:subtract</c> for numeric, duration, and dateTime operand pairs.
    /// F&amp;O §6.2.2, §10.5, §10.7.
    /// </summary>
    public static XdmAtomicValue Subtract(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // ── duration - duration ───────────────────────────────────────────────
        if (IsDurationType(lt) && IsDurationType(rt))
        {
            return SubtractDurations(left, right, location);
        }

        // ── dateTime - duration ───────────────────────────────────────────────
        if (IsDateTimeType(lt) && IsDurationType(rt))
        {
            // dateTime - duration = dateTime + (negated duration)
            XdmAtomicValue negDuration = NegateDuration(right, location);
            return AddDateTimeDuration(left, negDuration, location);
        }

        // ── dateTime - dateTime → xs:dayTimeDuration ──────────────────────────
        // F&O §10.7.4
        if (IsDateTimeType(lt) && IsDateTimeType(rt))
        {
            return SubtractDateTimes(left, right, location);
        }

        // ── numeric - numeric ─────────────────────────────────────────────────
        XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
        XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
        XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);

        return common switch
        {
            XdmTypeCode.Integer => XdmAtomicValue.Integer(l.AsBigInteger() - r.AsBigInteger()),
            XdmTypeCode.Decimal => XdmAtomicValue.Decimal(SubtractDecimal(l.AsDecimal().Value, r.AsDecimal().Value, location)),
            XdmTypeCode.Float   => XdmAtomicValue.Float(l.AsFloat() - r.AsFloat()),
            XdmTypeCode.Double  => XdmAtomicValue.Double(l.AsDouble() - r.AsDouble()),
            _ => throw new InvalidOperationException($"Unexpected common numeric type: {common}"),
        };
    }

    // ── Multiply ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>op:multiply</c> for numeric and duration operand pairs.
    /// F&amp;O §6.2.3, §10.5.
    /// </summary>
    public static XdmAtomicValue Multiply(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // ── duration * number ─────────────────────────────────────────────────
        // F&O §10.5.3 / §10.5.9
        if (IsDurationType(lt) && NumericPromotion.IsNumeric(rt))
        {
            return MultiplyDuration(left, right, location);
        }

        // ── number * duration ─────────────────────────────────────────────────
        if (NumericPromotion.IsNumeric(lt) && IsDurationType(rt))
        {
            return MultiplyDuration(right, left, location);
        }

        // ── numeric * numeric ─────────────────────────────────────────────────
        XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
        XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
        XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);

        return common switch
        {
            XdmTypeCode.Integer => XdmAtomicValue.Integer(l.AsBigInteger() * r.AsBigInteger()),
            XdmTypeCode.Decimal => XdmAtomicValue.Decimal(MultiplyDecimal(l.AsDecimal().Value, r.AsDecimal().Value, location)),
            XdmTypeCode.Float   => XdmAtomicValue.Float(l.AsFloat() * r.AsFloat()),
            XdmTypeCode.Double  => XdmAtomicValue.Double(l.AsDouble() * r.AsDouble()),
            _ => throw new InvalidOperationException($"Unexpected common numeric type: {common}"),
        };
    }

    // ── Divide ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>op:divide</c> per F&amp;O §6.2.4.
    /// xs:integer / xs:integer yields xs:decimal.
    /// Division by zero for integer/decimal raises FOAR0001; for float/double produces ±Infinity or NaN.
    /// </summary>
    public static XdmAtomicValue Divide(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // ── duration div number ───────────────────────────────────────────────
        // F&O §10.5.4 / §10.5.10
        if (IsDurationType(lt) && NumericPromotion.IsNumeric(rt))
        {
            return DivideDurationByNumber(left, right, location);
        }

        // ── duration div duration → xs:decimal ───────────────────────────────
        // F&O §10.5.5 / §10.5.11
        if (IsDurationType(lt) && IsDurationType(rt))
        {
            return DivideDurationByDuration(left, right, location);
        }

        // ── numeric / numeric ─────────────────────────────────────────────────
        // F&O §6.2.4: if both operands are xs:integer, result type is xs:decimal
        bool bothInteger = NumericPromotion.IsIntegerFamily(lt) && NumericPromotion.IsIntegerFamily(rt);

        XdmTypeCode common;
        if (bothInteger)
        {
            common = XdmTypeCode.Decimal;
        }
        else
        {
            common = NumericPromotion.CommonNumericType(left, right, location);
        }

        XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
        XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);

        switch (common)
        {
            case XdmTypeCode.Decimal:
            {
                decimal rd = r.AsDecimal().Value;
                if (rd == 0m)
                {
                    throw new XPathDynamicException(XPathErrors.FOAR0001, "Division by zero.", location);
                }
                return XdmAtomicValue.Decimal(DivideDecimal(l.AsDecimal().Value, rd, location));
            }

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float(l.AsFloat() / r.AsFloat());  // IEEE: 0/0 → NaN, x/0 → ±Inf

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(l.AsDouble() / r.AsDouble());  // IEEE: 0/0 → NaN

            default:
                throw new InvalidOperationException($"Unexpected common numeric type: {common}");
        }
    }

    // ── IntegerDivide ─────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>idiv</c> per F&amp;O §6.2.5.
    /// Result is always xs:integer (truncated toward zero).
    /// </summary>
    public static XdmAtomicValue IntegerDivide(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // idiv by zero is always FOAR0001 regardless of type
        // For float/double: if quotient is NaN or Infinity → FOAR0002; zero divisor → FOAR0001
        if (lt == XdmTypeCode.Double || rt == XdmTypeCode.Double)
        {
            XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
            XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
            XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);
            double ld = l.AsDouble();
            double rd = r.AsDouble();
            if (rd == 0.0)
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv by zero.", location);
            }
            double q = ld / rd;
            if (double.IsNaN(q) || double.IsInfinity(q))
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv produces NaN or Infinity.", location);
            }
            return XdmAtomicValue.Integer(new BigInteger(Math.Truncate(q)));
        }

        if (lt == XdmTypeCode.Float || rt == XdmTypeCode.Float)
        {
            XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
            XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
            XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);
            float lf = l.AsFloat();
            float rf = r.AsFloat();
            if (rf == 0.0f)
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv by zero.", location);
            }
            float qf = lf / rf;
            if (float.IsNaN(qf) || float.IsInfinity(qf))
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv produces NaN or Infinity.", location);
            }
            return XdmAtomicValue.Integer(new BigInteger((double)Math.Truncate(qf)));
        }

        // Decimal / integer family
        if (lt == XdmTypeCode.Decimal || rt == XdmTypeCode.Decimal)
        {
            XdmAtomicValue lp = NumericPromotion.PromoteNumeric(left, XdmTypeCode.Decimal, location);
            XdmAtomicValue rp = NumericPromotion.PromoteNumeric(right, XdmTypeCode.Decimal, location);
            decimal rd = rp.AsDecimal().Value;
            if (rd == 0m)
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv by zero.", location);
            }
            decimal q = decimal.Truncate(lp.AsDecimal().Value / rd);
            return XdmAtomicValue.Integer(new BigInteger(q));
        }

        // Both integer family
        BigInteger li = left.AsBigInteger();
        BigInteger ri = right.AsBigInteger();
        if (ri == BigInteger.Zero)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "idiv by zero.", location);
        }

        // Truncate toward zero: BigInteger.Divide truncates toward zero
        BigInteger result = BigInteger.Divide(li, ri);
        return XdmAtomicValue.Integer(result);
    }

    // ── Modulo ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements <c>mod</c> per F&amp;O §6.2.6.
    /// Result type follows the left operand's numeric type.
    /// </summary>
    public static XdmAtomicValue Modulo(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        left = CoerceUntypedAtomic(left, location);
        right = CoerceUntypedAtomic(right, location);

        XdmTypeCode common = NumericPromotion.CommonNumericType(left, right, location);
        XdmAtomicValue l = NumericPromotion.PromoteNumeric(left, common, location);
        XdmAtomicValue r = NumericPromotion.PromoteNumeric(right, common, location);

        switch (common)
        {
            case XdmTypeCode.Integer:
            {
                BigInteger ri = r.AsBigInteger();
                if (ri == BigInteger.Zero)
                {
                    throw new XPathDynamicException(XPathErrors.FOAR0001, "mod by zero.", location);
                }
                return XdmAtomicValue.Integer(l.AsBigInteger() % ri);
            }

            case XdmTypeCode.Decimal:
            {
                decimal rd = r.AsDecimal().Value;
                if (rd == 0m)
                {
                    throw new XPathDynamicException(XPathErrors.FOAR0001, "mod by zero.", location);
                }
                return XdmAtomicValue.Decimal(l.AsDecimal().Value % rd);
            }

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float(l.AsFloat() % r.AsFloat()); // IEEE remainder

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(l.AsDouble() % r.AsDouble()); // IEEE remainder

            default:
                throw new InvalidOperationException($"Unexpected common numeric type: {common}");
        }
    }

    // ── Negate ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implements unary minus per F&amp;O §6.2.7.
    /// </summary>
    public static XdmAtomicValue Negate(XdmAtomicValue value, SourceSpan location)
    {
        value = CoerceUntypedAtomic(value, location);

        switch (value.TypeCode)
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
                return XdmAtomicValue.Integer(-value.AsBigInteger());

            case XdmTypeCode.Decimal:
                return XdmAtomicValue.Decimal(-value.AsDecimal().Value);

            case XdmTypeCode.Float:
                return XdmAtomicValue.Float(-value.AsFloat());

            case XdmTypeCode.Double:
                return XdmAtomicValue.Double(-value.AsDouble());

            case XdmTypeCode.Duration:
            case XdmTypeCode.DayTimeDuration:
            case XdmTypeCode.YearMonthDuration:
                return NegateDuration(value, location);

            default:
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"Unary minus is not defined for xs:{CastTable.GetXsLocalName(value.TypeCode)}.",
                    location);
        }
    }

    // ── Duration arithmetic helpers ───────────────────────────────────────────

    private static XdmAtomicValue AddDurations(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        // F&O §10.5: same subtype + same subtype only; mixed subtypes → XPTY0004
        if (lt == XdmTypeCode.YearMonthDuration && rt == XdmTypeCode.YearMonthDuration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            return XdmAtomicValue.YearMonthDuration(new XsdDuration(ld.Months + rd.Months, new XsdDecimal(0m)));
        }

        if (lt == XdmTypeCode.DayTimeDuration && rt == XdmTypeCode.DayTimeDuration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            decimal newSecs = ld.Seconds.Value + rd.Seconds.Value;
            return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(newSecs)));
        }

        // Both plain xs:duration
        if (lt == XdmTypeCode.Duration && rt == XdmTypeCode.Duration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            return XdmAtomicValue.Duration(new XsdDuration(
                ld.Months + rd.Months,
                new XsdDecimal(ld.Seconds.Value + rd.Seconds.Value)));
        }

        // Mixed subtype — XPTY0004
        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Cannot add duration subtypes of different kinds.",
            location);
    }

    private static XdmAtomicValue SubtractDurations(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        if (lt == XdmTypeCode.YearMonthDuration && rt == XdmTypeCode.YearMonthDuration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            return XdmAtomicValue.YearMonthDuration(new XsdDuration(ld.Months - rd.Months, new XsdDecimal(0m)));
        }

        if (lt == XdmTypeCode.DayTimeDuration && rt == XdmTypeCode.DayTimeDuration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            decimal newSecs = ld.Seconds.Value - rd.Seconds.Value;
            return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(newSecs)));
        }

        if (lt == XdmTypeCode.Duration && rt == XdmTypeCode.Duration)
        {
            XsdDuration ld = left.AsDuration();
            XsdDuration rd = right.AsDuration();
            return XdmAtomicValue.Duration(new XsdDuration(
                ld.Months - rd.Months,
                new XsdDecimal(ld.Seconds.Value - rd.Seconds.Value)));
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Cannot subtract duration subtypes of different kinds.",
            location);
    }

    private static XdmAtomicValue MultiplyDuration(XdmAtomicValue duration, XdmAtomicValue number, SourceSpan location)
    {
        XdmTypeCode dt = duration.TypeCode;

        // Promote number to double for the multiplication factor
        double factor = ToDouble(number, location);

        if (double.IsNaN(factor))
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Duration multiplication by NaN.", location);
        }

        XsdDuration dur = duration.AsDuration();

        if (dt == XdmTypeCode.YearMonthDuration)
        {
            // F&O §10.5.3: scale months, truncate
            long months = (long)Math.Round(dur.Months * factor, MidpointRounding.AwayFromZero);
            return XdmAtomicValue.YearMonthDuration(new XsdDuration((int)months, new XsdDecimal(0m)));
        }

        if (dt == XdmTypeCode.DayTimeDuration)
        {
            // F&O §10.5.9: scale seconds
            decimal newSecs = dur.Seconds.Value * (decimal)factor;
            return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(newSecs)));
        }

        // Plain xs:duration — scale both
        int newMonths = (int)Math.Round(dur.Months * factor, MidpointRounding.AwayFromZero);
        decimal newS = dur.Seconds.Value * (decimal)factor;
        return XdmAtomicValue.Duration(new XsdDuration(newMonths, new XsdDecimal(newS)));
    }

    private static XdmAtomicValue DivideDurationByNumber(XdmAtomicValue duration, XdmAtomicValue number, SourceSpan location)
    {
        XdmTypeCode dt = duration.TypeCode;
        double divisor = ToDouble(number, location);

        if (divisor == 0.0)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Division of duration by zero.", location);
        }

        if (double.IsNaN(divisor))
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Division of duration by NaN.", location);
        }

        XsdDuration dur = duration.AsDuration();

        if (dt == XdmTypeCode.YearMonthDuration)
        {
            long months = (long)Math.Round(dur.Months / divisor, MidpointRounding.AwayFromZero);
            return XdmAtomicValue.YearMonthDuration(new XsdDuration((int)months, new XsdDecimal(0m)));
        }

        if (dt == XdmTypeCode.DayTimeDuration)
        {
            decimal newSecs = dur.Seconds.Value / (decimal)divisor;
            return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(newSecs)));
        }

        int newMonths = (int)Math.Round(dur.Months / divisor, MidpointRounding.AwayFromZero);
        decimal newS = dur.Seconds.Value / (decimal)divisor;
        return XdmAtomicValue.Duration(new XsdDuration(newMonths, new XsdDecimal(newS)));
    }

    private static XdmAtomicValue DivideDurationByDuration(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        XdmTypeCode lt = left.TypeCode;
        XdmTypeCode rt = right.TypeCode;

        if (lt == XdmTypeCode.YearMonthDuration && rt == XdmTypeCode.YearMonthDuration)
        {
            int rm = right.AsDuration().Months;
            if (rm == 0)
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "Division of duration by zero duration.", location);
            }
            return XdmAtomicValue.Decimal((decimal)left.AsDuration().Months / (decimal)rm);
        }

        if (lt == XdmTypeCode.DayTimeDuration && rt == XdmTypeCode.DayTimeDuration)
        {
            decimal rs = right.AsDuration().Seconds.Value;
            if (rs == 0m)
            {
                throw new XPathDynamicException(XPathErrors.FOAR0001, "Division of duration by zero duration.", location);
            }
            return XdmAtomicValue.Decimal(left.AsDuration().Seconds.Value / rs);
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Duration div duration is only defined for matching subtypes.",
            location);
    }

    // ── dateTime / date / time + duration ────────────────────────────────────

    private static XdmAtomicValue AddDateTimeDuration(XdmAtomicValue dateTime, XdmAtomicValue duration, SourceSpan location)
    {
        // F&O §10.7 simplified: apply the duration's months and then seconds component.
        XsdDateTime dt = dateTime.AsDateTime();
        XsdDuration dur = duration.AsDuration();

        // Apply year-month part first (months offset)
        long newYear = dt.Year;
        int newMonth = dt.Month;
        int newDay = dt.Day;
        int newHour = dt.Hour;
        int newMinute = dt.Minute;
        decimal newSecond = dt.Second.Value;

        if (dur.Months != 0)
        {
            int totalMonths = (int)newYear * 12 + (newMonth - 1) + dur.Months;
            newYear = totalMonths / 12;
            newMonth = totalMonths % 12 + 1;
            if (newMonth <= 0)
            {
                newMonth += 12;
                newYear--;
            }
            // Clamp day to end of new month
            int daysInMonth = DaysInMonth(newYear, newMonth);
            if (newDay > daysInMonth)
            {
                newDay = daysInMonth;
            }
        }

        // Apply seconds part (convert to seconds offset then add)
        if (dur.Seconds.Value != 0m)
        {
            decimal totalSecs = (decimal)(newDay - 1) * 86400m
                + (decimal)newHour * 3600m
                + (decimal)newMinute * 60m
                + newSecond
                + dur.Seconds.Value;

            // Normalize day overflows
            int daysOffset = (int)(totalSecs / 86400m);
            if (totalSecs < 0m && totalSecs % 86400m != 0m)
            {
                daysOffset--;
            }
            totalSecs -= daysOffset * 86400m;

            int dayOfYear = DayOfYear(newYear, newMonth, newDay) + daysOffset;
            NormalizeYearDay(ref newYear, ref dayOfYear);
            MonthDay(newYear, dayOfYear, out newMonth, out newDay);

            long sFloor = (long)Math.Floor((double)totalSecs);
            newHour = (int)(sFloor / 3600);
            newMinute = (int)((sFloor % 3600) / 60);
            newSecond = totalSecs - (decimal)(newHour * 3600 + newMinute * 60);
        }

        XsdDateTime result = new XsdDateTime(
            dt.Kind,
            newYear,
            newMonth,
            newDay,
            newHour,
            newMinute,
            new XsdDecimal(newSecond),
            dt.TimezoneMinutes);

        return XdmAtomicValue.DateTimeValue(result);
    }

    // F&O §10.7.4: dateTime - dateTime → xs:dayTimeDuration (difference in seconds)
    private static XdmAtomicValue SubtractDateTimes(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location)
    {
        if (left.TypeCode != right.TypeCode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Cannot subtract date/times of different types.",
                location);
        }

        XsdDateTime l = left.AsDateTime();
        XsdDateTime r = right.AsDateTime();

        // Simplified: treat both as having no timezone (raw component comparison)
        decimal lSecs = DateTimeToSeconds(l);
        decimal rSecs = DateTimeToSeconds(r);
        decimal diffSecs = lSecs - rSecs;

        return XdmAtomicValue.DayTimeDuration(new XsdDuration(0, new XsdDecimal(diffSecs)));
    }

    private static decimal DateTimeToSeconds(XsdDateTime dt)
    {
        // Approximate seconds from epoch for comparison purposes
        int dayOfYear = DayOfYear(dt.Year, dt.Month == 0 ? 1 : dt.Month, dt.Day == 0 ? 1 : dt.Day);
        decimal days = (decimal)TotalDaysFromEpoch(dt.Year) + (decimal)(dayOfYear - 1);
        return days * 86400m
            + (decimal)dt.Hour * 3600m
            + (decimal)dt.Minute * 60m
            + dt.Second.Value;
    }

    private static long TotalDaysFromEpoch(long year)
    {
        // Proleptic Gregorian days from year 0
        long y = year - 1;
        return 365L * y + y / 4 - y / 100 + y / 400;
    }

    private static XdmAtomicValue NegateDuration(XdmAtomicValue value, SourceSpan location)
    {
        XsdDuration dur = value.AsDuration();
        XsdDuration negated = new XsdDuration(-dur.Months, new XsdDecimal(-dur.Seconds.Value));

        switch (value.TypeCode)
        {
            case XdmTypeCode.DayTimeDuration:
                return XdmAtomicValue.DayTimeDuration(negated);
            case XdmTypeCode.YearMonthDuration:
                return XdmAtomicValue.YearMonthDuration(negated);
            default:
                return XdmAtomicValue.Duration(negated);
        }
    }

    // ── Calendar helpers ──────────────────────────────────────────────────────

    private static int DaysInMonth(long year, int month)
    {
        if (month == 2)
        {
            return IsLeapYear(year) ? 29 : 28;
        }

        int[] days = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        return days[month];
    }

    private static bool IsLeapYear(long year)
    {
        return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
    }

    private static int DayOfYear(long year, int month, int day)
    {
        int[] cumDays = { 0, 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
        int doy = cumDays[month] + day;
        if (month > 2 && IsLeapYear(year))
        {
            doy++;
        }
        return doy;
    }

    private static void NormalizeYearDay(ref long year, ref int dayOfYear)
    {
        while (dayOfYear < 1)
        {
            year--;
            dayOfYear += IsLeapYear(year) ? 366 : 365;
        }

        while (true)
        {
            int daysInYear = IsLeapYear(year) ? 366 : 365;
            if (dayOfYear <= daysInYear)
            {
                break;
            }
            dayOfYear -= daysInYear;
            year++;
        }
    }

    private static void MonthDay(long year, int dayOfYear, out int month, out int day)
    {
        int[] cumDays = IsLeapYear(year)
            ? new[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 }
            : new[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };

        month = 1;
        while (month < 12 && dayOfYear > cumDays[month])
        {
            month++;
        }

        day = dayOfYear - cumDays[month - 1];
    }

    // ── Decimal arithmetic helpers (overflow check) ───────────────────────────

    private static decimal AddDecimal(decimal a, decimal b, SourceSpan location)
    {
        try
        {
            return a + b;
        }
        catch (OverflowException)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Decimal addition overflow.", location);
        }
    }

    private static decimal SubtractDecimal(decimal a, decimal b, SourceSpan location)
    {
        try
        {
            return a - b;
        }
        catch (OverflowException)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Decimal subtraction overflow.", location);
        }
    }

    private static decimal MultiplyDecimal(decimal a, decimal b, SourceSpan location)
    {
        try
        {
            return a * b;
        }
        catch (OverflowException)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Decimal multiplication overflow.", location);
        }
    }

    private static decimal DivideDecimal(decimal a, decimal b, SourceSpan location)
    {
        try
        {
            return a / b;
        }
        catch (OverflowException)
        {
            throw new XPathDynamicException(XPathErrors.FOAR0001, "Decimal division overflow.", location);
        }
    }

    // ── Coerce helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// F&amp;O §6.2.1: xs:untypedAtomic in an arithmetic context is cast to xs:double.
    /// </summary>
    private static XdmAtomicValue CoerceUntypedAtomic(XdmAtomicValue value, SourceSpan location)
    {
        if (value.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            return CastTable.Cast(value, XdmTypeCode.Double, location);
        }
        return value;
    }

    private static double ToDouble(XdmAtomicValue value, SourceSpan location)
    {
        value = CoerceUntypedAtomic(value, location);
        XdmAtomicValue promoted = NumericPromotion.PromoteNumeric(value, XdmTypeCode.Double, location);
        return promoted.AsDouble();
    }

    // ── Type predicates ───────────────────────────────────────────────────────

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
