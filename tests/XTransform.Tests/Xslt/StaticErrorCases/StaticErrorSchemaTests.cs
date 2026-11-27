using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 4 of P4-23: schema-aware constructs in a basic processor (XSLT 2.0 §3.12; XTSE1650, XTSE1660).</summary>
[TestFixture]
public sealed class StaticErrorSchemaTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template match='/'><xsl:element name='e' type='xs:double'>2.2</xsl:element></xsl:template>", TestName = "TypeOnElement")]
    [TestCase("<xsl:template match='/'><xsl:attribute name='a' type='xs:integer'>1</xsl:attribute></xsl:template>", TestName = "TypeOnAttribute")]
    [TestCase("<xsl:template match='/'><xsl:copy type='xs:string'/></xsl:template>", TestName = "TypeOnCopy")]
    [TestCase("<xsl:template match='/'><xsl:element name='e' validation='strict'>2.2</xsl:element></xsl:template>", TestName = "ValidationStrict")]
    [TestCase("<xsl:template match='/'><xsl:copy-of select='.' validation='lax'/></xsl:template>", TestName = "ValidationLax")]
    [TestCase("<xsl:template match='/'><xsl:document validation='preserve'/></xsl:template>", TestName = "ValidationPreserve")]
    [TestCase("<xsl:template match='/'><out xsl:validation='strict'/></xsl:template>", TestName = "ValidationOnLiteralResultElement")]
    [TestCase("<xsl:template match='/'><out xsl:type='xs:string'/></xsl:template>", TestName = "TypeOnLiteralResultElement")]
    public void Compile_SchemaAwareAttribute_RaisesXtse1660(string body)
    {
        // Arrange
        string xsl = Wrap(body, string.Empty);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE1660));
    }

    [TestCase("strict")]
    [TestCase("lax")]
    [TestCase("preserve")]
    public void Compile_DefaultValidationOtherThanStrip_RaisesXtse1660(string value)
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/'/>", $" default-validation='{value}'");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE1660));
    }

    [Test]
    public void Compile_ImportSchema_RaisesXtse1650()
    {
        // Arrange
        string xsl = Wrap("<xsl:import-schema namespace='urn:s'/><xsl:template match='/'/>", string.Empty);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE1650));
    }

    [TestCase("<xsl:template match='/'><xsl:element name='e' validation='strip'/><xsl:copy-of select='.' validation='strip'/></xsl:template>", TestName = "ValidationStrip")]
    [TestCase("<xsl:template match='/'><out xsl:validation='strip'/></xsl:template>", TestName = "LiteralResultElementStrip")]
    [TestCase("<xsl:template match='/' version='3.0'><xsl:element name='e' validation='preserve'/></xsl:template>", TestName = "PreserveUnderForwardsCompatibility")]
    public void Compile_NoSchemaAwareConstruct_Compiles(string body)
    {
        // Arrange
        string xsl = Wrap(body, string.Empty);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body, string rootAttributes) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' xmlns:xs='http://www.w3.org/2001/XMLSchema'{rootAttributes}>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
