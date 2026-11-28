using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 6 of P4-23: a prefix in a lexical QName attribute that no namespace declaration binds (XSLT 2.0 §5.1.1; XTSE0280).</summary>
[TestFixture]
public sealed class StaticErrorPrefixTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template name='nasty:name'/>", TestName = "TemplateName")]
    [TestCase("<xsl:template name='x'/><xsl:template match='/'><xsl:call-template name='bad:y'/></xsl:template>", TestName = "CallTemplateName")]
    [TestCase("<xsl:attribute-set name='unknown:name'/>", TestName = "AttributeSetName")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates mode='undeclared:mode'/></xsl:template>", TestName = "ApplyTemplatesMode")]
    [TestCase("<xsl:template match='x' mode='a undeclared:m'/>", TestName = "TemplateModeList")]
    [TestCase("<xsl:variable name='nope:v' select='1'/>", TestName = "VariableName")]
    [TestCase("<xsl:key name='nope:k' match='*' use='.'/>", TestName = "KeyName")]
    [TestCase("<xsl:decimal-format name='nope:d'/>", TestName = "DecimalFormatName")]
    public void Compile_UndeclaredPrefixInAQName_RaisesXtse0280(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0280));
    }

    [TestCase("<xsl:template xmlns:a='urn:a' name='a:t'/>", TestName = "DeclaredOnTheElement")]
    [TestCase("<xsl:variable name='p:v' select='1'/>", TestName = "DeclaredOnTheStylesheet")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates mode='p:m'/></xsl:template>", TestName = "ApplyTemplatesModeDeclared")]
    [TestCase("<xsl:template match='x' mode='p:a #default'/>", TestName = "TemplateModeListDeclared")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates mode='#current'/></xsl:template>", TestName = "HashModes")]
    [TestCase("<xsl:variable name='xml:v' select='1'/>", TestName = "XmlPrefix")]
    public void Compile_DeclaredPrefix_Compiles(string body)
    {
        // Arrange
        string xsl = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' xmlns:p='urn:p'>{body}</xsl:stylesheet>";

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
