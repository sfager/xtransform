using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §16.5 — format-dateTime, format-date and format-time.</summary>
[TestFixture]
internal sealed class XsltFormatDateTimeTests
{
    private const string Date = "xs:date('2003-12-07')";                       // a Sunday
    private const string DateTime = "xs:dateTime('2008-03-05T14:07:09.25+02:00')"; // a Wednesday
    private const string Time = "xs:time('09:05:03.5-05:30')";

    // ── Date components ──────────────────────────────────────────────────────

    [TestCase("[Y]-[M]-[D]", "2003-12-7")]
    [TestCase("[Y0001]-[M01]-[D01]", "2003-12-07")]
    [TestCase("[Y01]", "03")]
    [TestCase("[Y,2-2]", "03")]
    [TestCase("[Y,6-6]", "002003")]
    [TestCase("[D1o]", "7th")]
    [TestCase("[Y1o]", "2003rd")]
    [TestCase("[MNn]", "December")]
    [TestCase("[MN]", "DECEMBER")]
    [TestCase("[Mn]", "december")]
    [TestCase("[MNn,3-3]", "Dec")]
    [TestCase("[MNn,*-3]", "Dec")]
    [TestCase("[MNn,12-12]", "December    ")]
    [TestCase("[M,3]", "012")]
    [TestCase("[FNn]", "Sunday")]
    [TestCase("[FN,3-3]", "SUN")]
    [TestCase("[F1]", "7")]
    [TestCase("[d]", "341")]
    [TestCase("[d001]", "341")]
    [TestCase("[W]", "49")]
    [TestCase("[w]", "1")]
    [TestCase("[MI]", "XII")]
    [TestCase("[Dw]", "seven")]
    [TestCase("[DW]", "SEVEN")]
    [TestCase("[DWw]", "Seven")]
    [TestCase("[Dwo]", "seventh")]
    [TestCase("[YWw]", "Two Thousand and Three")]
    [TestCase("[EN]", "AD")]
    [TestCase("[[[Y0001]-[M01]-[D01]]]", "[2003-12-07]")]
    [TestCase("Date: [D] [MNn] [Y]", "Date: 7 December 2003")]
    [TestCase("[ Y ]", "2003")]
    public void FormatDate_Pictures(string picture, string expected)
    {
        Assert.That(Eval($"format-date({Date}, '{picture}')"), Is.EqualTo(expected));
    }

    // 2008-03-05 is a Wednesday; ISO week 10; the week holds Thursday 6 March, so it is week 1 of March.
    [TestCase("2003-12-07", "[FNn]", "Sunday")]
    [TestCase("2003-12-08", "[FNn]", "Monday")]
    [TestCase("2000-02-29", "[FNn]", "Tuesday")]
    [TestCase("1970-01-01", "[FNn]", "Thursday")]
    [TestCase("2008-03-05", "[FNn] [W] [w] [d]", "Wednesday 10 1 65")]
    [TestCase("2005-12-04", "[W] [w]", "48 1")]
    [TestCase("2005-12-31", "[W] [w]", "52 5")]
    [TestCase("2006-01-01", "[W] [w]", "52 5")]
    [TestCase("2006-01-02", "[W] [w]", "1 1")]
    [TestCase("2004-12-31", "[W]", "53")]
    [TestCase("2009-01-01", "[W]", "1")]
    [TestCase("2006-01-30", "[w]", "5")]
    [TestCase("2006-02-02", "[w]", "1")]
    [TestCase("2006-02-26", "[w]", "4")]
    [TestCase("2006-03-01", "[w]", "1")]
    [TestCase("2003-12-08", "[FNn,3-4]", "Mon")]
    [TestCase("2003-12-09", "[FNn,3-4]", "Tues")]
    [TestCase("2003-12-10", "[FNn,3-4]", "Weds")]
    [TestCase("2003-12-11", "[FNn,3-4]", "Thur")]
    [TestCase("2003-12-11", "[FNn,3-5]", "Thurs")]
    [TestCase("2003-12-09", "[FNn,3-5]", "Tues")]
    [TestCase("2003-09-01", "[MNn,3-4]", "Sept")]
    public void FormatDate_CalendarArithmetic(string date, string picture, string expected)
    {
        Assert.That(Eval($"format-date(xs:date('{date}'), '{picture}')"), Is.EqualTo(expected));
    }

    [Test]
    public void FormatDate_AddingADayAdvancesTheDayOfWeek()
    {
        Assert.That(
            Eval("format-date(xs:date('2003-12-07') + xs:dayTimeDuration('P1D'), '[FNn]')"),
            Is.EqualTo("Monday"));
    }

    [Test]
    public void FormatDate_NegativeYear_UsesTheAbsoluteYearAndTheBcEra()
    {
        Assert.That(Eval("format-date(xs:date('-0044-03-15'), '[Y] [EN]')"), Is.EqualTo("44 BC"));
    }

    // ── Time components ──────────────────────────────────────────────────────

    [TestCase("[H01]:[m01]:[s01]", "14:07:09")]
    [TestCase("[H]:[m]:[s]", "14:07:09")]
    [TestCase("[h]:[m01] [PN]", "2:07 PM")]
    [TestCase("[h]:[m01] [Pn]", "2:07 pm")]
    [TestCase("[h01]", "02")]
    [TestCase("[s01].[f001]", "09.250")]
    [TestCase("[f1]", "25")]
    [TestCase("[f01]", "25")]
    [TestCase("[f,1-1]", "2")]
    [TestCase("[f,4]", "2500")]
    [TestCase("[Z]", "+02:00")]
    [TestCase("[z]", "GMT+02:00")]
    [TestCase("[z,2-2]", "GMT+02")]
    [TestCase("[z,6-6]", "GMT+02:00")]
    [TestCase("[z0]", "GMT+2")]
    [TestCase("[Z0]", "+2")]
    public void FormatDateTime_TimeComponents(string picture, string expected)
    {
        Assert.That(Eval($"format-dateTime({DateTime}, '{picture}')"), Is.EqualTo(expected));
    }

    [TestCase("[h]:[m01] [PN]", "12:00 AM", "00:00:00")]
    [TestCase("[h]:[m01] [PN]", "12:30 PM", "12:30:00")]
    [TestCase("[h]:[m01] [PN]", "11:59 PM", "23:59:00")]
    [TestCase("[H01]", "00", "00:15:00")]
    public void FormatTime_TwelveAndTwentyFourHourClocks(string picture, string expected, string time)
    {
        Assert.That(Eval($"format-time(xs:time('{time}'), '{picture}')"), Is.EqualTo(expected));
    }

    [Test]
    public void FormatTime_ZoneOffsetWithMinutes_ShowsThem()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval($"format-time({Time}, '[Z]')"), Is.EqualTo("-05:30"));
            Assert.That(Eval($"format-time({Time}, '[z]')"), Is.EqualTo("GMT-05:30"));
            Assert.That(Eval($"format-time({Time}, '[z,2-2]')"), Is.EqualTo("GMT-05:30"));
            Assert.That(Eval($"format-time({Time}, '[z0]')"), Is.EqualTo("GMT-5:30"));
        });
    }

    [Test]
    public void FormatDateTime_NoTimezone_IgnoresTheZoneComponents()
    {
        Assert.That(Eval("format-dateTime(xs:dateTime('2008-03-05T14:07:09'), '[H01]:[m01][Z][z]')"), Is.EqualTo("14:07"));
    }

    [Test]
    public void FormatDateTime_BothDateAndTimeComponents()
    {
        Assert.That(
            Eval($"format-dateTime({DateTime}, '[FNn,3-3], [D1o] [MNn,3-3] [Y] at [H01]:[m01]')"),
            Is.EqualTo("Wed, 5th Mar 2008 at 14:07"));
    }

    // ── Empty value, extra arguments ─────────────────────────────────────────

    [Test]
    public void EmptyValue_YieldsTheEmptySequence()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("empty(format-date((), '[Y]'))"), Is.EqualTo("true"));
            Assert.That(Eval("empty(format-time((), '[H]', 'en', (), ()))"), Is.EqualTo("true"));
            Assert.That(Eval("empty(format-dateTime((), '[Y]'))"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void FiveArgumentForm_WithEnglishAndTheGregorianCalendar()
    {
        Assert.That(Eval($"format-date({Date}, '[MNn] [Y]', 'en', 'AD', 'US')"), Is.EqualTo("December 2003"));
    }

    [Test]
    public void UnsupportedLanguage_FallsBackToEnglishAndSaysSo()
    {
        Assert.That(Eval($"format-date({Date}, '[MNn]', 'xib', (), ())"), Is.EqualTo("[Language: en]December"));
    }

    [Test]
    public void UnsupportedButValidCalendar_FallsBackToTheGregorianCalendarAndSaysSo()
    {
        Assert.That(Eval($"format-date({Date}, '[D01]', 'en', 'CB', ())"), Is.EqualTo("[Calendar: AD]07"));
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    [TestCase("[bla]")]
    [TestCase("[Y")]
    [TestCase("Y]")]
    [TestCase("[]")]
    [TestCase("[Y,0]")]
    [TestCase("[Y,3-2]")]
    [TestCase("[Y,x]")]
    public void InvalidPicture_RaisesXtde1340(string picture)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval($"format-date({Date}, '{picture}')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1340));
    }

    [Test]
    public void ComponentThatTheTypeLacks_RaisesXtde1350()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<XsltDynamicException>(() => Eval($"format-time({Time}, '[Y]')"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTDE1350));
            Assert.That(
                Assert.Throws<XsltDynamicException>(() => Eval($"format-date({Date}, '[H]')"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTDE1350));
            Assert.That(
                Assert.Throws<XsltDynamicException>(() => Eval($"format-time({Time}, '[FNn]')"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTDE1350));
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Eval(string xpath)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
            "<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"" + xpath + "\"/></xsl:template></xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
