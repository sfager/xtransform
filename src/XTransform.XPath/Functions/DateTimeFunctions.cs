// F&O §10 — date/time and duration accessor/adjustment functions.

using System;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §10 date/time component extractors, duration component
/// extractors, timezone adjustment functions, and fn:dateTime.
/// </summary>
internal static class DateTimeFunctions
{
    // =========================================================================
    // Helpers — single-item extraction
    // =========================================================================

    /// <summary>
    /// Returns the single xs:dateTime (or xs:date, xs:time) from a ZeroOrOne sequence,
    /// or <see langword="null"/> if the sequence is empty.
    /// Raises XPTY0004 if the item is not in the date/time family.
    /// </summary>
    private static XdmAtomicValue? SingleDateTimeOrEmpty(XdmSequence seq, string fnName)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return null;
        }

        XdmAtomicValue av = (XdmAtomicValue)item;
        if (!IsDateTimeFamily(av.TypeCode))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"{fnName} requires a date/time argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                default);
        }

        return av;
    }

    /// <summary>
    /// Returns the single xs:duration from a ZeroOrOne sequence, or <see langword="null"/>
    /// if the sequence is empty. Raises XPTY0004 if the item is not a duration.
    /// </summary>
    private static XdmAtomicValue? SingleDurationOrEmpty(XdmSequence seq, string fnName)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return null;
        }

        XdmAtomicValue av = (XdmAtomicValue)item;
        if (!IsDurationFamily(av.TypeCode))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"{fnName} requires a duration argument; got xs:{CastTable.GetXsLocalName(av.TypeCode)}.",
                default);
        }

        return av;
    }

    private static bool IsDateTimeFamily(XdmTypeCode tc)
    {
        switch (tc)
        {
            case XdmTypeCode.DateTime:
            case XdmTypeCode.Date:
            case XdmTypeCode.Time:
            case XdmTypeCode.GYearMonth:
            case XdmTypeCode.GYear:
            case XdmTypeCode.GMonthDay:
            case XdmTypeCode.GDay:
            case XdmTypeCode.GMonth:
                return true;
            default:
                return false;
        }
    }

    private static bool IsDurationFamily(XdmTypeCode tc)
    {
        switch (tc)
        {
            case XdmTypeCode.Duration:
            case XdmTypeCode.DayTimeDuration:
            case XdmTypeCode.YearMonthDuration:
                return true;
            default:
                return false;
        }
    }

    // =========================================================================
    // F&O §10.5 — Component extraction from xs:dateTime
    // =========================================================================

    /// <summary>Implements fn:year-from-dateTime($arg as xs:dateTime?) as xs:integer?.</summary>
    // F&O §10.5.1
    internal static IXdmIterator FnYearFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:year-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer(av.AsDateTime().Year));
    }

    /// <summary>Implements fn:month-from-dateTime($arg as xs:dateTime?) as xs:integer?.</summary>
    // F&O §10.5.2
    internal static IXdmIterator FnMonthFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:month-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Month));
    }

    /// <summary>Implements fn:day-from-dateTime($arg as xs:dateTime?) as xs:integer?.</summary>
    // F&O §10.5.3
    internal static IXdmIterator FnDayFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:day-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Day));
    }

    /// <summary>Implements fn:hours-from-dateTime($arg as xs:dateTime?) as xs:integer?.</summary>
    // F&O §10.5.4
    internal static IXdmIterator FnHoursFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:hours-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Hour));
    }

    /// <summary>Implements fn:minutes-from-dateTime($arg as xs:dateTime?) as xs:integer?.</summary>
    // F&O §10.5.5
    internal static IXdmIterator FnMinutesFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:minutes-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Minute));
    }

    /// <summary>Implements fn:seconds-from-dateTime($arg as xs:dateTime?) as xs:decimal?.</summary>
    // F&O §10.5.6
    internal static IXdmIterator FnSecondsFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:seconds-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Decimal(av.AsDateTime().Second));
    }

    /// <summary>Implements fn:timezone-from-dateTime($arg as xs:dateTime?) as xs:dayTimeDuration?.</summary>
    // F&O §10.5.7
    internal static IXdmIterator FnTimezoneFromDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:timezone-from-dateTime");
        if (av is null) { return EmptyIterator.Instance; }
        return TimezoneResult(av.AsDateTime());
    }

    // =========================================================================
    // F&O §10.5 — Component extraction from xs:date
    // =========================================================================

    /// <summary>Implements fn:year-from-date($arg as xs:date?) as xs:integer?.</summary>
    // F&O §10.5.8
    internal static IXdmIterator FnYearFromDate(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:year-from-date");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer(av.AsDateTime().Year));
    }

    /// <summary>Implements fn:month-from-date($arg as xs:date?) as xs:integer?.</summary>
    // F&O §10.5.9
    internal static IXdmIterator FnMonthFromDate(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:month-from-date");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Month));
    }

    /// <summary>Implements fn:day-from-date($arg as xs:date?) as xs:integer?.</summary>
    // F&O §10.5.10
    internal static IXdmIterator FnDayFromDate(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:day-from-date");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Day));
    }

    /// <summary>Implements fn:timezone-from-date($arg as xs:date?) as xs:dayTimeDuration?.</summary>
    // F&O §10.5.11
    internal static IXdmIterator FnTimezoneFromDate(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:timezone-from-date");
        if (av is null) { return EmptyIterator.Instance; }
        return TimezoneResult(av.AsDateTime());
    }

    // =========================================================================
    // F&O §10.5 — Component extraction from xs:time
    // =========================================================================

    /// <summary>Implements fn:hours-from-time($arg as xs:time?) as xs:integer?.</summary>
    // F&O §10.5.12
    internal static IXdmIterator FnHoursFromTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:hours-from-time");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Hour));
    }

    /// <summary>Implements fn:minutes-from-time($arg as xs:time?) as xs:integer?.</summary>
    // F&O §10.5.13
    internal static IXdmIterator FnMinutesFromTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:minutes-from-time");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Integer((long)av.AsDateTime().Minute));
    }

    /// <summary>Implements fn:seconds-from-time($arg as xs:time?) as xs:decimal?.</summary>
    // F&O §10.5.14
    internal static IXdmIterator FnSecondsFromTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:seconds-from-time");
        if (av is null) { return EmptyIterator.Instance; }
        return new SingleItemIterator(XdmAtomicValue.Decimal(av.AsDateTime().Second));
    }

    /// <summary>Implements fn:timezone-from-time($arg as xs:time?) as xs:dayTimeDuration?.</summary>
    // F&O §10.5.15
    internal static IXdmIterator FnTimezoneFromTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:timezone-from-time");
        if (av is null) { return EmptyIterator.Instance; }
        return TimezoneResult(av.AsDateTime());
    }

    // =========================================================================
    // F&O §10.6 — Component extraction from xs:duration
    // =========================================================================

    /// <summary>Implements fn:years-from-duration($arg as xs:duration?) as xs:integer?.</summary>
    // F&O §10.6.1
    internal static IXdmIterator FnYearsFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:years-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.1: truncate toward zero; sign follows duration sign.
        int months = av.AsDuration().Months;
        long years = months / 12L;
        return new SingleItemIterator(XdmAtomicValue.Integer(years));
    }

    /// <summary>Implements fn:months-from-duration($arg as xs:duration?) as xs:integer?.</summary>
    // F&O §10.6.2
    internal static IXdmIterator FnMonthsFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:months-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.2: months component in -11..11.
        int months = av.AsDuration().Months;
        long monthComp = months % 12L;
        return new SingleItemIterator(XdmAtomicValue.Integer(monthComp));
    }

    /// <summary>Implements fn:days-from-duration($arg as xs:duration?) as xs:integer?.</summary>
    // F&O §10.6.3
    internal static IXdmIterator FnDaysFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:days-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.3: whole days truncated toward zero.
        decimal seconds = av.AsDuration().Seconds.Value;
        long days = (long)decimal.Truncate(seconds / 86400m);
        return new SingleItemIterator(XdmAtomicValue.Integer(days));
    }

    /// <summary>Implements fn:hours-from-duration($arg as xs:duration?) as xs:integer?.</summary>
    // F&O §10.6.4
    internal static IXdmIterator FnHoursFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:hours-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.4: sign-preserving modulo. hours component is -23..23.
        decimal seconds = av.AsDuration().Seconds.Value;
        // Truncate to whole seconds for integer parts.
        decimal dayRemainder = seconds - decimal.Truncate(seconds / 86400m) * 86400m;
        long hours = (long)decimal.Truncate(dayRemainder / 3600m);
        return new SingleItemIterator(XdmAtomicValue.Integer(hours));
    }

    /// <summary>Implements fn:minutes-from-duration($arg as xs:duration?) as xs:integer?.</summary>
    // F&O §10.6.5
    internal static IXdmIterator FnMinutesFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:minutes-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.5: sign-preserving modulo. minutes component is -59..59.
        decimal seconds = av.AsDuration().Seconds.Value;
        decimal dayRemainder = seconds - decimal.Truncate(seconds / 86400m) * 86400m;
        decimal hourRemainder = dayRemainder - decimal.Truncate(dayRemainder / 3600m) * 3600m;
        long minutes = (long)decimal.Truncate(hourRemainder / 60m);
        return new SingleItemIterator(XdmAtomicValue.Integer(minutes));
    }

    /// <summary>Implements fn:seconds-from-duration($arg as xs:duration?) as xs:decimal?.</summary>
    // F&O §10.6.6
    internal static IXdmIterator FnSecondsFromDuration(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDurationOrEmpty(args[0], "fn:seconds-from-duration");
        if (av is null) { return EmptyIterator.Instance; }
        // F&O §10.6.6: seconds component (with fractional part), sign-preserving. Range -60..60.
        decimal seconds = av.AsDuration().Seconds.Value;
        decimal dayRemainder = seconds - decimal.Truncate(seconds / 86400m) * 86400m;
        decimal hourRemainder = dayRemainder - decimal.Truncate(dayRemainder / 3600m) * 3600m;
        decimal minuteRemainder = hourRemainder - decimal.Truncate(hourRemainder / 60m) * 60m;
        return new SingleItemIterator(XdmAtomicValue.Decimal(minuteRemainder));
    }

    // =========================================================================
    // F&O §10.7 — Timezone adjustment functions
    // =========================================================================

    /// <summary>Implements fn:adjust-dateTime-to-timezone($arg as xs:dateTime?) as xs:dateTime?.</summary>
    // F&O §10.7.6 (1-arg: use implicit timezone)
    internal static IXdmIterator FnAdjustDateTimeToTimezone1(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-dateTime-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int implicitTz = ctx.Run.ImplicitTimezoneMinutes;
        XsdDateTime result = AdjustDateTimeToTimezone(av.AsDateTime(), implicitTz, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    /// <summary>Implements fn:adjust-dateTime-to-timezone($arg as xs:dateTime?, $tz as xs:dayTimeDuration?) as xs:dateTime?.</summary>
    // F&O §10.7.6 (2-arg)
    internal static IXdmIterator FnAdjustDateTimeToTimezone2(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-dateTime-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int? newTzMinutes = GetTzMinutesArg(args[1], "fn:adjust-dateTime-to-timezone");
        XsdDateTime result = AdjustDateTimeToTimezone(av.AsDateTime(), newTzMinutes, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    /// <summary>Implements fn:adjust-date-to-timezone($arg as xs:date?) as xs:date?.</summary>
    // F&O §10.7.7 (1-arg: use implicit timezone)
    internal static IXdmIterator FnAdjustDateToTimezone1(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-date-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int implicitTz = ctx.Run.ImplicitTimezoneMinutes;
        // F&O §10.7.7: treat date as 00:00:00, adjust as dateTime, then extract date.
        XsdDateTime result = AdjustDateToTimezone(av.AsDateTime(), implicitTz, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    /// <summary>Implements fn:adjust-date-to-timezone($arg as xs:date?, $tz as xs:dayTimeDuration?) as xs:date?.</summary>
    // F&O §10.7.7 (2-arg)
    internal static IXdmIterator FnAdjustDateToTimezone2(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-date-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int? newTzMinutes = GetTzMinutesArg(args[1], "fn:adjust-date-to-timezone");
        XsdDateTime result = AdjustDateToTimezone(av.AsDateTime(), newTzMinutes, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    /// <summary>Implements fn:adjust-time-to-timezone($arg as xs:time?) as xs:time?.</summary>
    // F&O §10.7.8 (1-arg: use implicit timezone)
    internal static IXdmIterator FnAdjustTimeToTimezone1(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-time-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int implicitTz = ctx.Run.ImplicitTimezoneMinutes;
        XsdDateTime result = AdjustTimeToTimezone(av.AsDateTime(), implicitTz, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    /// <summary>Implements fn:adjust-time-to-timezone($arg as xs:time?, $tz as xs:dayTimeDuration?) as xs:time?.</summary>
    // F&O §10.7.8 (2-arg)
    internal static IXdmIterator FnAdjustTimeToTimezone2(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = SingleDateTimeOrEmpty(args[0], "fn:adjust-time-to-timezone");
        if (av is null) { return EmptyIterator.Instance; }
        int? newTzMinutes = GetTzMinutesArg(args[1], "fn:adjust-time-to-timezone");
        XsdDateTime result = AdjustTimeToTimezone(av.AsDateTime(), newTzMinutes, hasNewTz: true);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    // =========================================================================
    // F&O §10.8.1 — fn:dateTime
    // =========================================================================

    /// <summary>Implements fn:dateTime($date as xs:date?, $time as xs:time?) as xs:dateTime?.</summary>
    // F&O §10.8.1
    internal static IXdmIterator FnDateTime(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? dateAv = SingleDateTimeOrEmpty(args[0], "fn:dateTime");
        XdmAtomicValue? timeAv = SingleDateTimeOrEmpty(args[1], "fn:dateTime");

        // F&O §10.8.1: if either is empty → empty.
        if (dateAv is null || timeAv is null) { return EmptyIterator.Instance; }

        XsdDateTime dateVal = dateAv.AsDateTime();
        XsdDateTime timeVal = timeAv.AsDateTime();

        // F&O §10.8.1: if both have timezones, they must be equal; else FORG0008.
        int? resultTz;
        if (dateVal.HasTimezone && timeVal.HasTimezone)
        {
            if (dateVal.TimezoneMinutes!.Value != timeVal.TimezoneMinutes!.Value)
            {
                throw new XPathDynamicException(
                    XPathErrors.FORG0008,
                    "fn:dateTime: $date and $time have different timezone designators.",
                    default);
            }

            resultTz = dateVal.TimezoneMinutes;
        }
        else
        {
            // Take whichever has a timezone, or neither.
            resultTz = dateVal.HasTimezone ? dateVal.TimezoneMinutes : timeVal.TimezoneMinutes;
        }

        // Build result: year/month/day from date; hour/minute/second from time.
        XsdDateTime result = new XsdDateTime(
            XsdDateTimeKind.DateTime,
            dateVal.Year,
            dateVal.Month,
            dateVal.Day,
            timeVal.Hour,
            timeVal.Minute,
            timeVal.Second,
            resultTz);

        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(result));
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Builds an xs:dayTimeDuration iterator representing the timezone offset of <paramref name="dt"/>.
    /// Returns empty if the value has no timezone.
    /// </summary>
    private static IXdmIterator TimezoneResult(XsdDateTime dt)
    {
        if (!dt.HasTimezone) { return EmptyIterator.Instance; }
        // F&O §10.5.7: timezone = xs:dayTimeDuration with offset in seconds.
        int offsetSeconds = dt.TimezoneMinutes!.Value * 60;
        XsdDuration tzDuration = new XsdDuration(0, new XsdDecimal(offsetSeconds));
        return new SingleItemIterator(XdmAtomicValue.DayTimeDuration(tzDuration));
    }

    /// <summary>
    /// Reads the optional timezone argument (second arg to adjust-* functions).
    /// Returns the offset in minutes, or null to strip the timezone.
    /// For 1-arg overloads pass the implicit-timezone directly.
    /// </summary>
    private static int? GetTzMinutesArg(XdmSequence tzSeq, string fnName)
    {
        XdmItem? item = tzSeq.FirstOrNull();
        if (item is null)
        {
            // Empty sequence → strip timezone.
            return null;
        }

        XdmAtomicValue tzAv = (XdmAtomicValue)item;
        if (tzAv.TypeCode != XdmTypeCode.DayTimeDuration)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"{fnName}: timezone argument must be xs:dayTimeDuration?; got xs:{CastTable.GetXsLocalName(tzAv.TypeCode)}.",
                default);
        }

        // F&O §10.7: validate −14:00..+14:00 (±840 minutes).
        decimal secs = tzAv.AsDuration().Seconds.Value;
        // Convert seconds to minutes; must be whole minutes.
        decimal minutesDec = secs / 60m;
        int minutes = (int)decimal.Truncate(minutesDec);
        if (minutes < -840 || minutes > 840)
        {
            throw new XPathDynamicException(
                XPathErrors.FODT0003,
                $"{fnName}: timezone offset {minutes} minutes is outside the valid range −14:00..+14:00.",
                default);
        }

        return minutes;
    }

    /// <summary>
    /// Adjusts an xs:dateTime value to a new timezone offset.
    /// <paramref name="newTzMinutes"/> is null to strip the timezone.
    /// </summary>
    // F&O §10.7.6
    private static XsdDateTime AdjustDateTimeToTimezone(
        XsdDateTime dt, int? newTzMinutes, bool hasNewTz)
    {
        if (newTzMinutes is null)
        {
            // Strip timezone: remove it without shifting.
            return new XsdDateTime(dt.Kind, dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, dt.Second, timezoneMinutes: null);
        }

        int targetTz = newTzMinutes.Value;

        if (!dt.HasTimezone)
        {
            // No existing timezone → attach the new timezone without shifting.
            return new XsdDateTime(dt.Kind, dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, dt.Second, timezoneMinutes: targetTz);
        }

        // Both have timezones → shift the local time so the absolute moment is preserved.
        // Delta = targetTz - sourceTz minutes.
        int delta = targetTz - dt.TimezoneMinutes!.Value;
        return ShiftDateTime(dt, delta, targetTz);
    }

    /// <summary>
    /// Adjusts an xs:date to a new timezone.
    /// Internally treats it as 00:00:00 of that date, shifts, and extracts the date back.
    /// </summary>
    // F&O §10.7.7
    private static XsdDateTime AdjustDateToTimezone(
        XsdDateTime dt, int? newTzMinutes, bool hasNewTz)
    {
        if (newTzMinutes is null)
        {
            return new XsdDateTime(XsdDateTimeKind.Date, dt.Year, dt.Month, dt.Day,
                0, 0, default, timezoneMinutes: null);
        }

        int targetTz = newTzMinutes.Value;

        if (!dt.HasTimezone)
        {
            return new XsdDateTime(XsdDateTimeKind.Date, dt.Year, dt.Month, dt.Day,
                0, 0, default, timezoneMinutes: targetTz);
        }

        int delta = targetTz - dt.TimezoneMinutes!.Value;
        // Treat as midnight, shift, then extract date.
        XsdDateTime asDateTime = new XsdDateTime(
            XsdDateTimeKind.DateTime, dt.Year, dt.Month, dt.Day,
            0, 0, default, dt.TimezoneMinutes);
        XsdDateTime shifted = ShiftDateTime(asDateTime, delta, targetTz);
        return new XsdDateTime(XsdDateTimeKind.Date,
            shifted.Year, shifted.Month, shifted.Day,
            0, 0, default, shifted.TimezoneMinutes);
    }

    /// <summary>
    /// Adjusts an xs:time to a new timezone.
    /// Internally uses a fixed date (2000-01-01), shifts, and extracts the time back.
    /// </summary>
    // F&O §10.7.8
    private static XsdDateTime AdjustTimeToTimezone(
        XsdDateTime dt, int? newTzMinutes, bool hasNewTz)
    {
        if (newTzMinutes is null)
        {
            return new XsdDateTime(XsdDateTimeKind.Time, 0, 0, 0,
                dt.Hour, dt.Minute, dt.Second, timezoneMinutes: null);
        }

        int targetTz = newTzMinutes.Value;

        if (!dt.HasTimezone)
        {
            return new XsdDateTime(XsdDateTimeKind.Time, 0, 0, 0,
                dt.Hour, dt.Minute, dt.Second, timezoneMinutes: targetTz);
        }

        int delta = targetTz - dt.TimezoneMinutes!.Value;
        // Use fixed reference date to allow date crossing.
        XsdDateTime asDateTime = new XsdDateTime(
            XsdDateTimeKind.DateTime, 2000, 1, 1,
            dt.Hour, dt.Minute, dt.Second, dt.TimezoneMinutes);
        XsdDateTime shifted = ShiftDateTime(asDateTime, delta, targetTz);
        return new XsdDateTime(XsdDateTimeKind.Time, 0, 0, 0,
            shifted.Hour, shifted.Minute, shifted.Second, shifted.TimezoneMinutes);
    }

    /// <summary>
    /// Shifts an xs:dateTime by <paramref name="deltaMinutes"/> and sets the timezone to
    /// <paramref name="newTzMinutes"/>. Handles wrapping across day, month, year boundaries.
    /// </summary>
    private static XsdDateTime ShiftDateTime(XsdDateTime dt, int deltaMinutes, int newTzMinutes)
    {
        if (deltaMinutes == 0)
        {
            return new XsdDateTime(dt.Kind, dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, dt.Second, timezoneMinutes: newTzMinutes);
        }

        // Decompose existing seconds into whole-second and fractional parts.
        decimal secondVal = dt.Second.Value;
        int wholeSecs = (int)decimal.Truncate(secondVal);
        decimal fracSecs = secondVal - wholeSecs;

        // Convert to total minutes from some epoch origin (we only need relative arithmetic).
        // Use total minutes of the time-of-day: hour*60 + minute, then add delta.
        int totalMinutes = dt.Hour * 60 + dt.Minute + deltaMinutes;

        // Handle wrapping: totalMinutes may be negative or >= 24*60.
        // We need to also track day overflow.
        // Compute day offset and new time-of-day in minutes.
        int minutesPerDay = 24 * 60;
        int dayOffset;
        int newMinutes;
        if (totalMinutes >= 0)
        {
            dayOffset = totalMinutes / minutesPerDay;
            newMinutes = totalMinutes % minutesPerDay;
        }
        else
        {
            // For negative: e.g. -30 → dayOffset=-1, newMinutes=1410 (23*60+30)
            dayOffset = (totalMinutes - (minutesPerDay - 1)) / minutesPerDay;
            newMinutes = totalMinutes - dayOffset * minutesPerDay;
        }

        int newHour = newMinutes / 60;
        int newMinute = newMinutes % 60;

        // Apply day offset to the date.
        long newYear = dt.Year;
        int newMonth = dt.Month;
        int newDay = dt.Day + dayOffset;

        // Normalize the date by advancing/retreating months.
        NormalizeDate(ref newYear, ref newMonth, ref newDay);

        return new XsdDateTime(dt.Kind, newYear, newMonth, newDay,
            newHour, newMinute, new XsdDecimal(wholeSecs + fracSecs),
            timezoneMinutes: newTzMinutes);
    }

    /// <summary>
    /// Normalizes year/month/day when day overflows or underflows the month's valid range.
    /// Uses the proleptic Gregorian calendar.
    /// </summary>
    private static void NormalizeDate(ref long year, ref int month, ref int day)
    {
        // Forward overflow: while day > days-in-month, subtract and advance month.
        while (day > DaysInMonth(year, month))
        {
            day -= DaysInMonth(year, month);
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        // Backward underflow: while day < 1, retreat month and add days.
        while (day < 1)
        {
            month--;
            if (month < 1)
            {
                month = 12;
                year--;
            }

            day += DaysInMonth(year, month);
        }
    }

    /// <summary>
    /// Returns the number of days in <paramref name="month"/> of <paramref name="year"/>
    /// using the proleptic Gregorian calendar.
    /// Year 0 (XSD proleptic) = 1 BC → leap year.
    /// </summary>
    private static int DaysInMonth(long year, int month)
    {
        switch (month)
        {
            case 1: case 3: case 5: case 7: case 8: case 10: case 12:
                return 31;
            case 4: case 6: case 9: case 11:
                return 30;
            case 2:
                return IsLeapYear(year) ? 29 : 28;
            default:
                return 30; // shouldn't happen
        }
    }

    /// <summary>
    /// Returns whether <paramref name="year"/> is a leap year in the proleptic Gregorian calendar.
    /// XSD year 0 = astronomical year 0, so proleptic rules apply directly.
    /// </summary>
    private static bool IsLeapYear(long year)
    {
        // Proleptic Gregorian: divisible by 4, except centuries not divisible by 400.
        return (year % 4 == 0) && ((year % 100 != 0) || (year % 400 == 0));
    }
}
