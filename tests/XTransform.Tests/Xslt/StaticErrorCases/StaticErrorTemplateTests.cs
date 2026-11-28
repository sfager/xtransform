using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 12 of P4-23: the attributes an xsl:template needs (XSLT 2.0 §6; XTSE0500).</summary>
[TestFixture]
public sealed class StaticErrorTemplateTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template/>", TestName = "NeitherMatchNorName")]
    [TestCase("<xsl:template mode='x'/>", TestName = "ModeOnly")]
    [TestCase("<xsl:template priority='2'/>", TestName = "PriorityOnly")]
    [TestCase("<xsl:template name='sub' priority='2'/>", TestName = "NameAndPriority")]
    [TestCase("<xsl:template name='sub' mode='m2'/>", TestName = "NameAndMode")]
    [TestCase("<xsl:template name='sub' mode='#all' priority='1'/>", TestName = "NameModeAndPriority")]
    public void Compile_TemplateWithoutAMatchOrWithOnlyModeOrPriority_RaisesXtse0500(string template)
    {
        // Arrange
        string xsl = Wrap(template);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0500));
    }

    [TestCase("<xsl:template match='a'/>", TestName = "Match")]
    [TestCase("<xsl:template name='t'/>", TestName = "Name")]
    [TestCase("<xsl:template match='a' name='t'/>", TestName = "MatchAndName")]
    [TestCase("<xsl:template match='a' mode='m' priority='2'/>", TestName = "MatchModeAndPriority")]
    [TestCase("<xsl:template name='t' match='a' mode='#all' priority='-1'/>", TestName = "AllFour")]
    public void Compile_TemplateWithAMatch_Compiles(string template)
    {
        // Arrange
        string xsl = Wrap(template);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
