// P2-11: Date/time and duration function tests (F&O §10), plus xs: constructor tests (F&O §17).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §10 date/time component extractors, duration component extractors,
/// timezone adjustment functions, fn:dateTime, and F&amp;O §17 xs: constructor functions.
/// </summary>
[TestFixture]
internal sealed class DateTimeFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static List<XdmItem> DrainExpr(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = ce.Root.Iterate(NewRoot());
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return items;
    }

    private static XdmAtomicValue SingleAtomic(string expr)
    {
        List<XdmItem> items = DrainExpr(expr);
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton result for: {expr}");
        return (XdmAtomicValue)items[0];
    }

    private static bool IsEmpty(string expr)
    {
        List<XdmItem> items = DrainExpr(expr);
        return items.Count == 0;
    }

    private static long IntegerResult(string expr) => SingleAtomic(expr).AsInt64();

    private static decimal DecimalResult(string expr) => SingleAtomic(expr).AsDecimal().Value;

    // =========================================================================
    // xs: constructor functions — F&O §17
    // =========================================================================

    [Test]
    public void XsIntegerConstructor_FromString_ReturnsInteger()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("xs:integer('42')");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(result.AsInt64(), Is.EqualTo(42L));
    }

    [Test]
    public void XsDateTimeConstructor_FromString_RoundTrips()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("xs:dateTime('2024-07-15T12:00:00')");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DateTime));
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Year, Is.EqualTo(2024L));
        Assert.That(dt.Month, Is.EqualTo(7));
        Assert.That(dt.Day, Is.EqualTo(15));
        Assert.That(dt.Hour, Is.EqualTo(12));
    }

    [Test]
    public void XsDurationConstructor_P1Y_Has12Months()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("xs:duration('P1Y')");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Duration));
        Assert.That(result.AsDuration().Months, Is.EqualTo(12));
    }

    [Test]
    public void XsDurationConstructor_Empty_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("xs:duration(())"), Is.True);
    }

    [Test]
    public void XsYearMonthDurationConstructor_FromString_ReturnsYearMonthDuration()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("xs:yearMonthDuration('P1Y2M')");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.YearMonthDuration));
        Assert.That(result.AsDuration().Months, Is.EqualTo(14));
    }

    [Test]
    public void XsBooleanConstructor_FromString_ReturnsBoolean()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("xs:boolean('true')");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Boolean));
        Assert.That(result.AsBoolean(), Is.True);
    }

    // =========================================================================
    // fn:year-from-dateTime — F&O §10.5.1
    // =========================================================================

    [Test]
    public void FnYearFromDateTime_ValidDateTime_ReturnsYear()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:year-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))"),
            Is.EqualTo(2024L));
    }

    [Test]
    public void FnYearFromDateTime_NegativeYear_ReturnsNegativeYear()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:year-from-dateTime(xs:dateTime('-0001-01-01T00:00:00'))"),
            Is.EqualTo(-1L));
    }

    [Test]
    public void FnYearFromDateTime_Empty_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:year-from-dateTime(())"), Is.True);
    }

    // =========================================================================
    // fn:month-from-dateTime — F&O §10.5.2
    // =========================================================================

    [Test]
    public void FnMonthFromDateTime_ValidDateTime_ReturnsMonth()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:month-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))"),
            Is.EqualTo(7L));
    }

    // =========================================================================
    // fn:day-from-dateTime — F&O §10.5.3
    // =========================================================================

    [Test]
    public void FnDayFromDateTime_ValidDateTime_ReturnsDay()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:day-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))"),
            Is.EqualTo(15L));
    }

    // =========================================================================
    // fn:hours-from-dateTime — F&O §10.5.4
    // =========================================================================

    [Test]
    public void FnHoursFromDateTime_ValidDateTime_ReturnsHour()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:hours-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))"),
            Is.EqualTo(12L));
    }

    // =========================================================================
    // fn:minutes-from-dateTime — F&O §10.5.5
    // =========================================================================

    [Test]
    public void FnMinutesFromDateTime_ValidDateTime_ReturnsMinutes()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:minutes-from-dateTime(xs:dateTime('2024-07-15T12:30:45'))"),
            Is.EqualTo(30L));
    }

    // =========================================================================
    // fn:seconds-from-dateTime — F&O §10.5.6
    // =========================================================================

    [Test]
    public void FnSecondsFromDateTime_WithFractional_ReturnsDecimalSeconds()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:seconds-from-dateTime(xs:dateTime('2024-07-15T12:30:45.5'))");

        // Assert — must be xs:decimal per F&O §10.5.6
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(45.5m));
    }

    // =========================================================================
    // fn:timezone-from-dateTime — F&O §10.5.7
    // =========================================================================

    [Test]
    public void FnTimezoneFromDateTime_WithTimezone_ReturnsDayTimeDuration()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:timezone-from-dateTime(xs:dateTime('2024-07-15T12:00:00+01:00'))");

        // Assert — xs:dayTimeDuration representing PT1H
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
        // +01:00 = 60 minutes = 3600 seconds
        Assert.That(result.AsDuration().Seconds.Value, Is.EqualTo(3600m));
    }

    [Test]
    public void FnTimezoneFromDateTime_NoTimezone_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:timezone-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))"), Is.True);
    }

    [Test]
    public void FnTimezoneFromDateTime_UTC_ReturnsPT0S()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:timezone-from-dateTime(xs:dateTime('2024-07-15T12:00:00Z'))");

        // Assert — UTC = PT0S (0 seconds offset)
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
        Assert.That(result.AsDuration().Seconds.Value, Is.EqualTo(0m));
    }

    // =========================================================================
    // fn:year-from-date, fn:month-from-date, fn:day-from-date — F&O §10.5.8-10
    // =========================================================================

    [Test]
    public void FnYearFromDate_ValidDate_ReturnsYear()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:year-from-date(xs:date('2024-07-15'))"),
            Is.EqualTo(2024L));
    }

    [Test]
    public void FnMonthFromDate_ValidDate_ReturnsMonth()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:month-from-date(xs:date('2024-07-15'))"),
            Is.EqualTo(7L));
    }

    [Test]
    public void FnDayFromDate_ValidDate_ReturnsDay()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:day-from-date(xs:date('2024-07-15'))"),
            Is.EqualTo(15L));
    }

    // =========================================================================
    // fn:hours-from-time, fn:minutes-from-time, fn:seconds-from-time — F&O §10.5.12-14
    // =========================================================================

    [Test]
    public void FnHoursFromTime_ValidTime_ReturnsHours()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:hours-from-time(xs:time('12:30:45'))"),
            Is.EqualTo(12L));
    }

    [Test]
    public void FnMinutesFromTime_ValidTime_ReturnsMinutes()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:minutes-from-time(xs:time('12:30:45'))"),
            Is.EqualTo(30L));
    }

    [Test]
    public void FnSecondsFromTime_ValidTime_ReturnsSeconds()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:seconds-from-time(xs:time('12:30:45'))");

        // Assert — xs:decimal per spec
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(45m));
    }

    // =========================================================================
    // Duration component extractors — F&O §10.6
    // =========================================================================

    [Test]
    public void FnHoursFromDuration_PT30H_Returns6()
    {
        // Arrange / Act / Assert
        // F&O §10.6.4: 30 hours = 1 day + 6 hours → hours component = 6.
        Assert.That(IntegerResult("fn:hours-from-duration(xs:duration('PT30H'))"),
            Is.EqualTo(6L));
    }

    [Test]
    public void FnDaysFromDuration_PT72H_Returns3()
    {
        // Arrange / Act / Assert
        // F&O §10.6.3: 72 hours = 3 days exactly.
        Assert.That(IntegerResult("fn:days-from-duration(xs:duration('PT72H'))"),
            Is.EqualTo(3L));
    }

    [Test]
    public void FnYearsFromDuration_P14M_Returns1()
    {
        // Arrange / Act / Assert
        // 14 months = 1 year + 2 months → years component = 1.
        Assert.That(IntegerResult("fn:years-from-duration(xs:yearMonthDuration('P14M'))"),
            Is.EqualTo(1L));
    }

    [Test]
    public void FnMonthsFromDuration_P14M_Returns2()
    {
        // Arrange / Act / Assert
        // 14 months = 1 year + 2 months → months component = 2.
        Assert.That(IntegerResult("fn:months-from-duration(xs:yearMonthDuration('P14M'))"),
            Is.EqualTo(2L));
    }

    [Test]
    public void FnDaysFromDuration_P1DT2H_Returns1()
    {
        // Arrange / Act / Assert
        Assert.That(IntegerResult("fn:days-from-duration(xs:duration('P1DT2H'))"),
            Is.EqualTo(1L));
    }

    [Test]
    public void FnHoursFromDuration_NegativeDuration_ReturnsNegativeHours()
    {
        // Arrange / Act / Assert
        // F&O §10.6.4: -PT30H → -6 (sign follows duration sign).
        Assert.That(IntegerResult("fn:hours-from-duration(xs:duration('-PT30H'))"),
            Is.EqualTo(-6L));
    }

    [Test]
    public void FnSecondsFromDuration_PT90S_Returns30Decimal()
    {
        // Arrange / Act
        // 90 seconds = 1 minute + 30 seconds → seconds component = 30.
        XdmAtomicValue result = SingleAtomic("fn:seconds-from-duration(xs:duration('PT90S'))");

        // Assert — xs:decimal per spec
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(30m));
    }

    [Test]
    public void FnMinutesFromDuration_PT90S_Returns1()
    {
        // Arrange / Act / Assert
        // 90 seconds = 1 minute + 30 seconds → minutes component = 1.
        Assert.That(IntegerResult("fn:minutes-from-duration(xs:duration('PT90S'))"),
            Is.EqualTo(1L));
    }

    [Test]
    public void FnYearsFromDuration_Empty_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:years-from-duration(())"), Is.True);
    }

    // =========================================================================
    // fn:adjust-dateTime-to-timezone — F&O §10.7.6
    // =========================================================================

    [Test]
    public void FnAdjustDateTimeToTimezone_UTCToPlus1_ShiftsCorrectly()
    {
        // Arrange / Act
        // 2024-07-15T12:00:00Z adjusted to +01:00 = 2024-07-15T13:00:00+01:00
        XdmAtomicValue result = SingleAtomic(
            "fn:adjust-dateTime-to-timezone(xs:dateTime('2024-07-15T12:00:00Z'), xs:dayTimeDuration('PT1H'))");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DateTime));
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Year, Is.EqualTo(2024L));
        Assert.That(dt.Month, Is.EqualTo(7));
        Assert.That(dt.Day, Is.EqualTo(15));
        Assert.That(dt.Hour, Is.EqualTo(13));
        Assert.That(dt.Minute, Is.EqualTo(0));
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(60));
    }

    [Test]
    public void FnAdjustDateTimeToTimezone_NoTzToPlus1_AttachesTzWithoutShift()
    {
        // Arrange / Act
        // dt without TZ; attach +01:00 without shifting
        XdmAtomicValue result = SingleAtomic(
            "fn:adjust-dateTime-to-timezone(xs:dateTime('2024-07-15T12:00:00'), xs:dayTimeDuration('PT1H'))");

        // Assert
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Hour, Is.EqualTo(12));
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(60));
    }

    [Test]
    public void FnAdjustDateTimeToTimezone_EmptyTz_StripsTimezone()
    {
        // Arrange / Act
        // 2-arg with empty → strip TZ
        XdmAtomicValue result = SingleAtomic(
            "fn:adjust-dateTime-to-timezone(xs:dateTime('2024-07-15T12:00:00+01:00'), ())");

        // Assert
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.HasTimezone, Is.False);
        Assert.That(dt.Hour, Is.EqualTo(12)); // no shift
    }

    [Test]
    public void FnAdjustDateTimeToTimezone_Empty_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:adjust-dateTime-to-timezone((), xs:dayTimeDuration('PT1H'))"), Is.True);
    }

    [Test]
    public void FnAdjustDateTimeToTimezone_OutOfRangeTz_ThrowsFODT0003()
    {
        // Arrange / Act / Assert
        // +15 hours exceeds ±14h limit → FODT0003
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr(
                "fn:adjust-dateTime-to-timezone(xs:dateTime('2024-07-15T12:00:00Z'), xs:dayTimeDuration('PT15H'))"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FODT0003));
    }

    // =========================================================================
    // fn:adjust-date-to-timezone — F&O §10.7.7
    // =========================================================================

    [Test]
    public void FnAdjustDateToTimezone_NoTzToPlus1_AttachesTz()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic(
            "fn:adjust-date-to-timezone(xs:date('2024-07-15'), xs:dayTimeDuration('PT1H'))");

        // Assert
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.Date));
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(60));
        Assert.That(dt.Day, Is.EqualTo(15)); // no shift since no existing TZ
    }

    // =========================================================================
    // fn:adjust-time-to-timezone — F&O §10.7.8
    // =========================================================================

    [Test]
    public void FnAdjustTimeToTimezone_UTCToPlus1_ShiftsHour()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic(
            "fn:adjust-time-to-timezone(xs:time('12:00:00Z'), xs:dayTimeDuration('PT1H'))");

        // Assert
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.Time));
        Assert.That(dt.Hour, Is.EqualTo(13));
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(60));
    }

    // =========================================================================
    // fn:dateTime — F&O §10.8.1
    // =========================================================================

    [Test]
    public void FnDateTime_DateAndTime_NoTz_CombinesCorrectly()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic(
            "fn:dateTime(xs:date('2024-07-15'), xs:time('12:00:00'))");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DateTime));
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Year, Is.EqualTo(2024L));
        Assert.That(dt.Month, Is.EqualTo(7));
        Assert.That(dt.Day, Is.EqualTo(15));
        Assert.That(dt.Hour, Is.EqualTo(12));
        Assert.That(dt.Minute, Is.EqualTo(0));
        Assert.That(dt.HasTimezone, Is.False);
    }

    [Test]
    public void FnDateTime_DateWithTzAndTimeWithSameTz_UsesSharedTz()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic(
            "fn:dateTime(xs:date('2024-07-15Z'), xs:time('12:00:00Z'))");

        // Assert
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(0)); // UTC
    }

    [Test]
    public void FnDateTime_DateWithTzTimeWithDifferentTz_ThrowsFORG0008()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr(
                "fn:dateTime(xs:date('2024-07-15Z'), xs:time('12:00:00+01:00'))"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0008));
    }

    [Test]
    public void FnDateTime_EmptyDate_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:dateTime((), xs:time('12:00:00'))"), Is.True);
    }

    [Test]
    public void FnDateTime_EmptyTime_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:dateTime(xs:date('2024-07-15'), ())"), Is.True);
    }

    // =========================================================================
    // Timezone extraction from xs:date and xs:time — F&O §10.5.11, §10.5.15
    // =========================================================================

    [Test]
    public void FnTimezoneFromDate_WithTz_ReturnsDuration()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:timezone-from-date(xs:date('2024-07-15+05:30'))");

        // Assert — +05:30 = 330 minutes = 19800 seconds
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
        Assert.That(result.AsDuration().Seconds.Value, Is.EqualTo(19800m));
    }

    [Test]
    public void FnTimezoneFromTime_NoTz_ReturnsEmpty()
    {
        // Arrange / Act / Assert
        Assert.That(IsEmpty("fn:timezone-from-time(xs:time('12:00:00'))"), Is.True);
    }
}
