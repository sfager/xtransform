using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 5 of P4-23: the mode list of xsl:template (XSLT 2.0 §6.5; XTSE0550).</summary>
[TestFixture]
public sealed class StaticErrorModeTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("name!1223", TestName = "InvalidToken")]
    [TestCase("", TestName = "Empty")]
    [TestCase("   ", TestName = "OnlyWhitespace")]
    [TestCase("a b c a", TestName = "RepeatedToken")]
    [TestCase("#default b #default", TestName = "RepeatedDefault")]
    [TestCase("a b c# a", TestName = "InvalidTokenInList")]
    [TestCase("a b c# #all", TestName = "InvalidTokenAndAll")]
    [TestCase("#all a", TestName = "AllWithAnotherToken")]
    [TestCase("#all #all", TestName = "AllTwice")]
    [TestCase("#current", TestName = "CurrentIsNotAllowedOnATemplate")]
    public void Compile_InvalidTemplateModeList_RaisesXtse0550(string mode)
    {
        // Arrange
        string xsl = Wrap($"<xsl:template match='doc' mode='{mode}'/>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0550));
    }

    [TestCase("#all", TestName = "All")]
    [TestCase("#default", TestName = "Default")]
    [TestCase("a", TestName = "OneMode")]
    [TestCase("a b #default", TestName = "SeveralModes")]
    [TestCase("  a   b  ", TestName = "ExtraWhitespace")]
    public void Compile_ValidTemplateModeList_Compiles(string mode)
    {
        // Arrange
        string xsl = Wrap($"<xsl:template match='doc' mode='{mode}'/>");

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
