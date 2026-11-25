using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>XPath 2.0 §2.1.2 — the context size (last()) of a focus set by xsl:for-each and xsl:apply-templates.</summary>
[TestFixture]
internal sealed class XsltFocusSizeTests
{
    [Test]
    public void ForEach_Last_IsTheNumberOfItems()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:for-each select='1 to 3'>" +
            "<xsl:value-of select='position()'/>/<xsl:value-of select='last()'/>;</xsl:for-each></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert — last() was 0 whenever the expression was evaluated with its own variable frame.
        Assert.That(output, Is.EqualTo("1/3;2/3;3/3;"));
    }

    [Test]
    public void ApplyTemplates_Last_IsTheNumberOfSelectedNodes()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='doc'><xsl:apply-templates select='i'/></xsl:template>" +
            "<xsl:template match='i'><xsl:value-of select='position()'/>/<xsl:value-of select='last()'/>;</xsl:template>";

        // Act
        string output = Run(xsl, "<doc><i/><i/></doc>");

        // Assert
        Assert.That(output, Is.EqualTo("1/2;2/2;"));
    }

    [Test]
    public void ForEach_LastInsideAVariableScope_IsStillTheNumberOfItems()
    {
        // Arrange — a local variable gives the expression a non-empty frame.
        const string xsl =
            "<xsl:template match='/'><xsl:for-each select=\"'a', 'b'\">" +
            "<xsl:variable name='v' select='last()'/><xsl:value-of select='$v + position()'/>;</xsl:for-each></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("3;4;"));
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
