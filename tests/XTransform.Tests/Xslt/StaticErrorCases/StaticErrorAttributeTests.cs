using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 1 of P4-23: attributes an XSLT element does not define (XSLT 2.0 §3.1, XTSE0090).</summary>
[TestFixture]
public sealed class StaticErrorAttributeTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template match='/' extra='1'/>", TestName = "Template")]
    [TestCase("<xsl:template match='/'><xsl:apply-imports select='x'/></xsl:template>", TestName = "ApplyImports")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates name='n'/></xsl:template>", TestName = "ApplyTemplatesName")]
    [TestCase("<xsl:template name='t'/><xsl:template match='/'><xsl:call-template name='t' extra='x'/></xsl:template>", TestName = "CallTemplate")]
    [TestCase("<xsl:attribute-set name='a' extra='x'/>", TestName = "AttributeSet")]
    [TestCase("<xsl:key name='k' match='*' use='.' undefined='3'/>", TestName = "Key")]
    [TestCase("<xsl:variable name='v' department='IT'/>", TestName = "Variable")]
    [TestCase("<xsl:variable name='v' xsl:department='IT'/>", TestName = "AttributeInTheXsltNamespace")]
    [TestCase("<xsl:template match='/' xsl:name='banana'/>", TestName = "NameInTheXsltNamespace")]
    [TestCase("<xsl:template match='/'><xsl:value-of select='1' test='x'/></xsl:template>", TestName = "ValueOf")]
    [TestCase("<xsl:template match='/'><xsl:if test='true()' select='1'/></xsl:template>", TestName = "If")]
    [TestCase("<xsl:template match='/'><xsl:for-each select='.' mode='m'/></xsl:template>", TestName = "ForEach")]
    public void Compile_AttributeNotDefinedForTheElement_RaisesXtse0090(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0090));
    }

    [Test]
    public void Compile_AttributeOnTheStylesheetElement_RaisesXtse0090()
    {
        // Arrange
        string xsl = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' extra-attribute='2'><xsl:template match='/'/></xsl:stylesheet>";

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Is.EqualTo(new[] { XsltErrors.XTSE0090 }));
    }

    [Test]
    public void Compile_SeveralInvalidAttributes_ReportsAllOfThemTogether()
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/' a='1'/><xsl:template match='x' b='2'/><xsl:variable name='v' c='3'/>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Count(e => e.Code.Equals(XsltErrors.XTSE0090)), Is.EqualTo(3));
    }

    [TestCase("<xsl:template match='/' xmlns:e='urn:e' e:flavour='x'/>", TestName = "ExtensionAttributeInAnotherNamespace")]
    [TestCase("<xsl:template match='/' xml:space='preserve' default-collation='http://www.w3.org/2005/xpath-functions/collation/codepoint' use-when='true()'/>", TestName = "StandardAttributes")]
    [TestCase("<xsl:template match='/'><out xsl:use-attribute-sets='' extra='x'/></xsl:template>", TestName = "LiteralResultElementAttributes")]
    [TestCase("<xsl:template match='/' version='3.0' brand-new='1'/>", TestName = "ForwardsCompatibleVersion")]
    [TestCase("<xsl:template match='/'><xsl:result-document href='o.xml' format='f' output-version='1.0' method='xml'/></xsl:template>", TestName = "ResultDocumentOutputVersion")]
    [TestCase("<xsl:output method='xml' indent='yes' cdata-section-elements='a' undeclare-prefixes='no'/><xsl:template match='/'/>", TestName = "OutputProperties")]
    [TestCase("<xsl:decimal-format name='d' zero-digit='0'/><xsl:template match='/'><xsl:number value='1' grouping-size='3'/><xsl:apply-templates select='x'><xsl:sort select='.' order='descending'/></xsl:apply-templates></xsl:template>", TestName = "DecimalFormatNumberAndSort")]
    public void Compile_ValidAttributes_Compiles(string body)
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
