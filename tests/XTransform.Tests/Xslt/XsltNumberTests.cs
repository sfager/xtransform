using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §12 — xsl:number: counting by level, count, from and value, and its errors.</summary>
[TestFixture]
internal sealed class XsltNumberTests
{
    private const string Doc =
        "<doc><chapter><sec/><sec/></chapter><chapter><sec/><note/><sec/></chapter></doc>";

    private const string Notes =
        "<doc><chapter><note/><note/></chapter><chapter><note/><x/><note/></chapter></doc>";

    // ── level="single" (default) ─────────────────────────────────────────────────

    [Test]
    public void Number_LevelSingle_CountsPrecedingSiblingsOfTheSameName()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='sec'><xsl:number/>;</xsl:template>" +
            "<xsl:template match='doc|chapter'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='note'/>";

        // Act
        string output = Run(xsl, Doc);

        // Assert — sections number 1,2 in each chapter.
        Assert.That(output, Is.EqualTo("1;2;1;2;"));
    }

    [Test]
    public void Number_LevelSingleWithCount_SkipsOtherNames()
    {
        // Arrange — number only the children of chapter, whatever their name.
        const string xsl =
            "<xsl:template match='doc'><xsl:apply-templates select='chapter/*'/></xsl:template>" +
            "<xsl:template match='chapter/*'><xsl:number count='*'/>;</xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("1;2;1;2;3;"));
    }

    // ── level="multiple" ─────────────────────────────────────────────────────────

    [Test]
    public void Number_LevelMultiple_ProducesOnePlaceMarkerPerCountedAncestor()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='doc|chapter'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='sec'><xsl:number level='multiple' count='chapter|sec' format='1.1'/>;</xsl:template>" +
            "<xsl:template match='note'/>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("1.1;1.2;2.1;2.2;"));
    }

    // ── level="any" ──────────────────────────────────────────────────────────────

    [Test]
    public void Number_LevelAny_CountsAcrossTheWholeDocument()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='doc|chapter'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='note'><xsl:number level='any' count='note'/>;</xsl:template>";

        // Act
        string output = Run(xsl, Notes);

        // Assert
        Assert.That(output, Is.EqualTo("1;2;3;4;"));
    }

    [Test]
    public void Number_LevelAnyWithFrom_RestartsAtEachFromNode()
    {
        // Arrange — XSLT 2.0 §12.2 example: <xsl:number level="any" from="chapter" format="(1) "/>.
        const string xsl =
            "<xsl:template match='doc|chapter'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='note'><xsl:number level='any' count='note' from='chapter' format='(1) '/></xsl:template>" +
            "<xsl:template match='x'/>";

        // Act
        string output = Run(xsl, Notes);

        // Assert
        Assert.That(output, Is.EqualTo("(1) (2) (1) (2) "));
    }

    // ── value ────────────────────────────────────────────────────────────────────

    [TestCase("3", "a", "c")]
    [TestCase("2007", "I", "MMVII")]
    [TestCase("2.5", "1", "3")]                     // round half up
    [TestCase("0", "1", "0")]
    [TestCase("(1, 2, 3)", "1.1", "1.2.3")]
    [TestCase("12", "w", "twelve")]
    public void Number_Value_IsRoundedAndFormatted(string value, string format, string expected)
    {
        // Arrange
        string xsl = $"<xsl:template match='/'><xsl:number value='{value}' format='{format}'/></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void Number_FormatAndGroupingAttributes_AreAttributeValueTemplates()
    {
        // Arrange
        const string xsl =
            "<xsl:variable name='f' select=\"'1'\"/>" +
            "<xsl:template match='/'><xsl:number value='1234567' format='{$f}' grouping-separator=',' grouping-size='3'/></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("1,234,567"));
    }

    // ── Errors ───────────────────────────────────────────────────────────────────

    [TestCase("level='single'")]
    [TestCase("count='a'")]
    [TestCase("from='a'")]
    [TestCase("select='.'")]
    public void Number_ValueWithAnotherSelectionAttribute_RaisesXtse0975(string other)
    {
        // Arrange
        string xsl = $"<xsl:template match='/'><xsl:number value='1' {other}/></xsl:template>";

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Run(xsl, "<doc/>"))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0975));
    }

    [Test]
    public void Number_InvalidLevel_RaisesXtse0020()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run("<xsl:template match='/'><xsl:number level='deep'/></xsl:template>", "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0020));
    }

    [TestCase("-1")]
    [TestCase("number('x')")]
    [TestCase("1e0 div 0")]
    public void Number_ValueNotANonNegativeInteger_RaisesXtde0980(string value)
    {
        // Arrange
        string xsl = $"<xsl:template match='/'><xsl:number value=\"{value}\"/></xsl:template>";

        // Act
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, "<doc/>"))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0980));
    }

    [Test]
    public void Number_SelectYieldingSeveralNodes_RaisesXttE1000()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run("<xsl:template match='/'><xsl:number select='/doc/chapter'/></xsl:template>", Doc))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE1000));
    }

    [Test]
    public void Number_InsideFunctionWithoutValueOrSelect_RaisesXtte0990()
    {
        // The focus is undefined inside xsl:function (XSLT 2.0 §10.3), so there is no context node.
        const string xsl =
            "<xsl:function name='f:n' xmlns:f='urn:f'><xsl:number/></xsl:function>" +
            "<xsl:template match='/'><xsl:value-of select='f:n()' xmlns:f='urn:f'/></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0990));
    }

    private static string Run(string body, string input)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>"));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
