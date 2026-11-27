using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>F&amp;O §10.7 (date and time arithmetic) and §15.4 (sum, avg, min and max over durations and dates).</summary>
[TestFixture]
internal sealed class XsltDateArithmeticTests
{
    // ── Adding durations to dates and times (F&O Appendix E) ─────────────────

    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('P1D')", "2003-12-08")]
    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('P30D')", "2004-01-06")]
    [TestCase("xs:date('2003-12-07') - xs:dayTimeDuration('P7D')", "2003-11-30")]
    [TestCase("xs:date('2000-03-01') - xs:dayTimeDuration('P1D')", "2000-02-29")]
    [TestCase("xs:date('1999-03-01') - xs:dayTimeDuration('P1D')", "1999-02-28")]
    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('-P1D')", "2003-12-06")]
    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('P1DT23H')", "2003-12-08")]
    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('PT24H')", "2003-12-08")]
    [TestCase("xs:date('2003-12-07') + xs:dayTimeDuration('PT23H')", "2003-12-07")]
    [TestCase("xs:date('2003-12-07') + xs:yearMonthDuration('P1M')", "2004-01-07")]
    [TestCase("xs:date('2008-01-31') + xs:yearMonthDuration('P1M')", "2008-02-29")]
    [TestCase("xs:date('2007-01-31') + xs:yearMonthDuration('P1M')", "2007-02-28")]
    [TestCase("xs:date('2003-03-15') - xs:yearMonthDuration('P3M')", "2002-12-15")]
    [TestCase("xs:date('2003-12-07') + xs:yearMonthDuration('P2Y')", "2005-12-07")]
    [TestCase("xs:date('2008-02-29') + xs:yearMonthDuration('P1Y')", "2009-02-28")]
    [TestCase("xs:dateTime('2003-12-31T23:30:00') + xs:dayTimeDuration('PT1H')", "2004-01-01T00:30:00")]
    [TestCase("xs:dateTime('2003-12-31T23:30:00') + xs:dayTimeDuration('PT25H')", "2004-01-02T00:30:00")]
    [TestCase("xs:dateTime('2003-01-01T00:30:00') - xs:dayTimeDuration('PT1H')", "2002-12-31T23:30:00")]
    [TestCase("xs:dateTime('2003-12-07T10:00:00.25') + xs:dayTimeDuration('PT0.5S')", "2003-12-07T10:00:00.75")]
    [TestCase("xs:dateTime('2003-12-07T10:00:00+02:00') + xs:dayTimeDuration('P1D')", "2003-12-08T10:00:00+02:00")]
    [TestCase("xs:dateTime('2003-01-31T10:00:00') + xs:yearMonthDuration('P1M')", "2003-02-28T10:00:00")]
    [TestCase("xs:dateTime('2003-01-31T10:00:00') + xs:duration('P1M1DT2H')", "2003-03-01T12:00:00")]
    [TestCase("xs:time('10:00:00') + xs:dayTimeDuration('PT2H')", "12:00:00")]
    [TestCase("xs:time('23:00:00') + xs:dayTimeDuration('PT2H')", "01:00:00")]
    [TestCase("xs:time('01:00:00') - xs:dayTimeDuration('PT2H')", "23:00:00")]
    [TestCase("xs:time('10:00:00') + xs:dayTimeDuration('P3D')", "10:00:00")]
    [TestCase("xs:dayTimeDuration('P1D') + xs:date('2003-12-07')", "2003-12-08")]
    [TestCase("xs:date('-0001-12-31') + xs:dayTimeDuration('P1D')", "0001-01-01")]
    public void AddingADuration_FollowsAppendixE(string expression, string expected)
    {
        Assert.That(Eval($"string({expression})"), Is.EqualTo(expected));
    }

    [Test]
    public void AddingADurationToAGregorianType_RaisesXpty0004()
    {
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Eval("string(xs:gYear('2003') + xs:yearMonthDuration('P1Y'))"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // ── The canonical zero yearMonthDuration ─────────────────────────────────

    [Test]
    public void ZeroYearMonthDuration_IsWrittenP0M()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("string(xs:yearMonthDuration('P0M'))"), Is.EqualTo("P0M"));
            Assert.That(Eval("string(xs:yearMonthDuration('P1Y') - xs:yearMonthDuration('P12M'))"), Is.EqualTo("P0M"));
            Assert.That(Eval("string(xs:dayTimeDuration('PT0S'))"), Is.EqualTo("PT0S"));
            Assert.That(Eval("string(xs:duration('P0Y'))"), Is.EqualTo("PT0S"));
        });
    }

    // ── sum, avg, min and max ────────────────────────────────────────────────

    [TestCase("sum((xs:dayTimeDuration('P1D'), xs:dayTimeDuration('PT12H')))", "P1DT12H")]
    [TestCase("sum((xs:yearMonthDuration('P1Y'), xs:yearMonthDuration('P6M')))", "P1Y6M")]
    [TestCase("sum(xs:dayTimeDuration('P1D'))", "P1D")]
    [TestCase("avg((xs:dayTimeDuration('P1D'), xs:dayTimeDuration('P3D')))", "P2D")]
    [TestCase("avg((xs:yearMonthDuration('P1Y'), xs:yearMonthDuration('P3Y')))", "P2Y")]
    [TestCase("min((xs:dayTimeDuration('P2D'), xs:dayTimeDuration('PT12H'), xs:dayTimeDuration('P1D')))", "PT12H")]
    [TestCase("max((xs:yearMonthDuration('P1Y'), xs:yearMonthDuration('P18M'), xs:yearMonthDuration('P2M')))", "P1Y6M")]
    [TestCase("min((xs:date('2003-12-07'), xs:date('2001-01-01'), xs:date('2002-06-01')))", "2001-01-01")]
    [TestCase("max((xs:date('2003-12-07'), xs:date('2001-01-01')))", "2003-12-07")]
    [TestCase("min((xs:dateTime('2003-12-07T10:00:00Z'), xs:dateTime('2003-12-07T09:00:00Z')))", "2003-12-07T09:00:00Z")]
    [TestCase("max((xs:time('10:00:00Z'), xs:time('23:30:00Z'), xs:time('01:00:00Z')))", "23:30:00Z")]
    [TestCase("min(('banana', 'apple', 'cherry'))", "apple")]
    [TestCase("max(('banana', 'apple', 'cherry'))", "cherry")]
    [TestCase("min((3, 1.5, 2e0))", "1.5")]
    [TestCase("sum((1, 2.5))", "3.5")]
    [TestCase("string(sum(()))", "0")]
    public void Aggregates_AcceptDurationsDatesAndStrings(string expression, string expected)
    {
        Assert.That(Eval($"string({expression})"), Is.EqualTo(expected));
    }

    [TestCase("sum((xs:dayTimeDuration('P1D'), xs:yearMonthDuration('P1M')))")]
    [TestCase("sum((xs:dayTimeDuration('P1D'), 1))")]
    [TestCase("sum(('a', 'b'))")]
    [TestCase("avg((xs:date('2003-12-07'), xs:date('2003-12-08')))")]
    [TestCase("min(('a', 1))")]
    [TestCase("max((xs:date('2003-12-07'), xs:time('10:00:00')))")]
    public void Aggregates_OverIncompatibleValues_RaiseForg0006(string expression)
    {
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() => Eval($"string({expression})"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0006));
    }

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
