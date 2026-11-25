using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §16.1 — fn:current().</summary>
[TestFixture]
internal sealed class XsltCurrentFunctionTests
{
    private const string Doc =
        "<doc><a ref='2'/><a ref='1'/><b id='1'>one</b><b id='2'>two</b></doc>";

    [Test]
    public void Current_InPredicate_RefersToForEachItemNotPredicateFocus()
    {
        // Arrange
        const string xsl =
            "<xsl:for-each select='doc/a'><xsl:value-of select=\"/doc/b[@id = current()/@ref]\"/>;</xsl:for-each>";

        // Act
        string output = Run(Wrap(xsl), Doc);

        // Assert
        Assert.That(output, Is.EqualTo("two;one;"));
    }

    [Test]
    public void Current_InApplyTemplates_IsTheMatchedNode()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:apply-templates select='doc/a'/></xsl:template>" +
            "<xsl:template match='a'>[<xsl:value-of select=\"count(current()/../b[@id = current()/@ref])\"/>]</xsl:template>";

        // Act
        string output = RunTemplates(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("[1][1]"));
    }

    [Test]
    public void Current_AfterNestedForEach_IsRestoredToOuterItem()
    {
        // Arrange
        const string xsl =
            "<xsl:for-each select='doc/a'>" +
            "<xsl:for-each select='../b'><xsl:value-of select='name(current())'/></xsl:for-each>" +
            "<xsl:value-of select='name(current())'/>" +
            "</xsl:for-each>";

        // Act
        string output = Run(Wrap(xsl), Doc);

        // Assert
        Assert.That(output, Is.EqualTo("bbabba"));
    }

    [Test]
    public void Current_InGlobalContext_IsTheSourceDocumentNode()
    {
        // Arrange
        const string xsl = "<xsl:value-of select='count(current()/doc/a)'/>";

        // Act
        string output = Run(Wrap(xsl), Doc);

        // Assert
        Assert.That(output, Is.EqualTo("2"));
    }

    [Test]
    public void Current_WithNoContextItem_RaisesXtde1360()
    {
        // Arrange
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:template name='t'><xsl:value-of select='current()'/></xsl:template>" +
            "</xsl:stylesheet>"));
        XsltTransformer transformer = exe.CreateTransformer();
        transformer.InitialTemplate = new QName("t");

        // Act
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => transformer.TransformToSequence(null))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1360));
    }

    private static string Wrap(string rootTemplateBody) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
        "<xsl:output method='text'/>" +
        "<xsl:template match='/'>" + rootTemplateBody + "</xsl:template>" +
        "</xsl:stylesheet>";

    private static string RunTemplates(string templates, string input) =>
        Run(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/>" + templates + "</xsl:stylesheet>",
            input);

    private static string Run(string stylesheet, string input)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(stylesheet));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
