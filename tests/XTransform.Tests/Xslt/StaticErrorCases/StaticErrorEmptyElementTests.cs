using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 9 of P4-23: content inside an XSLT element that must be empty (XSLT 2.0 §3.1; XTSE0260).</summary>
[TestFixture]
public sealed class StaticErrorEmptyElementTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:include href='x.xsl'>text</xsl:include>", TestName = "IncludeWithText")]
    [TestCase("<xsl:import href='x.xsl'>text</xsl:import>", TestName = "ImportWithText")]
    [TestCase("<xsl:include href='x.xsl'><a/></xsl:include>", TestName = "IncludeWithElement")]
    [TestCase("<xsl:strip-space elements='*'>*</xsl:strip-space>", TestName = "StripSpaceWithText")]
    [TestCase("<xsl:strip-space elements='*' xml:space='preserve'> </xsl:strip-space>", TestName = "StripSpaceWithPreservedWhitespace")]
    [TestCase("<xsl:preserve-space elements='*'><x/></xsl:preserve-space>", TestName = "PreserveSpaceWithElement")]
    [TestCase("<xsl:decimal-format name='d'>x</xsl:decimal-format>", TestName = "DecimalFormat")]
    [TestCase("<xsl:output method='xml'><xsl:text/></xsl:output>", TestName = "Output")]
    [TestCase("<xsl:namespace-alias stylesheet-prefix='a' result-prefix='b'>x</xsl:namespace-alias>", TestName = "NamespaceAlias")]
    [TestCase("<xsl:template match='/'><xsl:copy-of select='.'><in/></xsl:copy-of></xsl:template>", TestName = "CopyOf")]
    [TestCase("<xsl:template match='/'><xsl:number value='1'>1</xsl:number></xsl:template>", TestName = "Number")]
    public void Compile_ContentInAnEmptyElement_RaisesXtse0260(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0260));
    }

    [TestCase("<xsl:strip-space elements='*'>  <!-- a comment --><?pi data?>  </xsl:strip-space>", TestName = "CommentsProcessingInstructionsAndStrippedWhitespace")]
    [TestCase("<xsl:output method='xml'>\n</xsl:output>", TestName = "OutputWithStrippedWhitespace")]
    [TestCase("<xsl:template match='/'><xsl:copy-of select='.'/><xsl:number value='1'/></xsl:template>", TestName = "EmptyInstructions")]
    public void Compile_EmptyElementWithoutContent_Compiles(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
