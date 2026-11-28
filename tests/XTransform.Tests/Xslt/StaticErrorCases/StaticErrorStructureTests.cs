using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 2 of P4-23: content-model violations (XSLT 2.0 §3.6, §5.7, §6, §11; XTSE0010 and XTSE0120).</summary>
[TestFixture]
public sealed class StaticErrorStructureTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:text>not allowed</xsl:text>", TestName = "TopLevelText")]
    [TestCase("<xsl:apply-templates select='x'/>", TestName = "TopLevelInstruction")]
    [TestCase("<xsl:stylesheet version='2.0'><xsl:template match='/'/></xsl:stylesheet>", TestName = "NestedStylesheet")]
    [TestCase("<xsl:unknown-declaration/>", TestName = "UnknownDeclaration")]
    [TestCase("<xsl:template match='/'><out/><xsl:param name='p'/></xsl:template>", TestName = "ParamAfterContent")]
    [TestCase("<xsl:template match='/'><out><xsl:param name='p'/></out></xsl:template>", TestName = "ParamInsideLiteralElement")]
    [TestCase("<xsl:template match='/'><xsl:template match='x'/></xsl:template>", TestName = "TemplateInsideTemplate")]
    [TestCase("<xsl:template match='/'><xsl:sort select='.'/></xsl:template>", TestName = "SortInsideTemplate")]
    [TestCase("<xsl:template match='/'><xsl:with-param name='p'/></xsl:template>", TestName = "WithParamInsideTemplate")]
    [TestCase("<xsl:template match='/'><xsl:decimal-format name='d'/></xsl:template>", TestName = "DeclarationInsideTemplate")]
    [TestCase("<xsl:template match='/'><xsl:apply-imports><xsl:fallback/></xsl:apply-imports></xsl:template>", TestName = "FallbackInApplyImports")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates><xsl:text>x</xsl:text></xsl:apply-templates></xsl:template>", TestName = "TextInstructionInApplyTemplates")]
    [TestCase("<xsl:template name='t'/><xsl:template match='/'><xsl:call-template name='t'><xsl:sort select='.'/></xsl:call-template></xsl:template>", TestName = "SortInCallTemplate")]
    [TestCase("<xsl:template match='/'><xsl:choose/></xsl:template>", TestName = "ChooseWithoutWhen")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:otherwise/></xsl:choose></xsl:template>", TestName = "ChooseOnlyOtherwise")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:otherwise/><xsl:when test='1'/></xsl:choose></xsl:template>", TestName = "OtherwiseBeforeWhen")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:when test='1'/><xsl:otherwise/><xsl:otherwise/></xsl:choose></xsl:template>", TestName = "TwoOtherwise")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:when test='1'/>text</xsl:choose></xsl:template>", TestName = "TextInChoose")]
    [TestCase("<xsl:template match='/'><xsl:for-each select='.'><xsl:value-of select='.'/><xsl:sort select='.'/></xsl:for-each></xsl:template>", TestName = "SortAfterContent")]
    [TestCase("<xsl:attribute-set name='a'><bad/></xsl:attribute-set>", TestName = "AttributeSetWithLiteralElement")]
    [TestCase("<xsl:attribute-set name='a'><xsl:attribute-set name='b'/></xsl:attribute-set>", TestName = "AttributeSetInAttributeSet")]
    [TestCase("<xsl:attribute-set name='a'>text</xsl:attribute-set>", TestName = "TextInAttributeSet")]
    [TestCase("<xsl:variable name='v'><xsl:key name='k' match='*' use='.'/></xsl:variable>", TestName = "KeyInsideVariable")]
    [TestCase("<xsl:attribute-set><xsl:attribute name='x'>1</xsl:attribute></xsl:attribute-set>", TestName = "AttributeSetWithoutName")]
    [TestCase("<xsl:template match='/'><xsl:text><xsl:text>x</xsl:text></xsl:text></xsl:template>", TestName = "ElementInText")]
    public void Compile_ContentModelViolation_RaisesXtse0010Or0120(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(
            ex.Errors.Select(e => e.Code),
            Does.Contain(XsltErrors.XTSE0010).Or.Contain(XsltErrors.XTSE0120));
    }

    [Test]
    public void Compile_TextAtTopLevel_RaisesXtse0120()
    {
        // Arrange
        string xsl = Wrap("loose text<xsl:template match='/'/>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0120));
    }

    [Test]
    public void Compile_StylesheetWithoutVersion_RaisesXtse0010()
    {
        // Arrange
        string xsl = $"<xsl:stylesheet xmlns:xsl='{XslNs}'><xsl:template match='/'/></xsl:stylesheet>";

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0010));
    }

    [Test]
    public void Compile_SeveralViolations_ReportsAllOfThemTogether()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:text>a</xsl:text><xsl:template match='/'><xsl:choose/><xsl:sort select='.'/></xsl:template>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Count(e => e.Code.Equals(XsltErrors.XTSE0010)), Is.GreaterThanOrEqualTo(3));
    }

    [TestCase("<xsl:template match='/'><xsl:param name='a'/><xsl:param name='b'/><out/></xsl:template>", TestName = "LeadingParams")]
    [TestCase("<xsl:template match='/'><xsl:for-each select='.'><xsl:sort select='.'/><xsl:sort select='.'/><xsl:value-of select='.'/></xsl:for-each></xsl:template>", TestName = "LeadingSorts")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates><xsl:sort select='.'/><xsl:with-param name='p' select='1'/></xsl:apply-templates></xsl:template>", TestName = "ApplyTemplatesChildren")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:when test='1'>a</xsl:when><xsl:when test='2'>b</xsl:when><xsl:otherwise>c</xsl:otherwise></xsl:choose></xsl:template>", TestName = "ChooseWithOtherwise")]
    [TestCase("<xsl:template match='/'><xsl:analyze-string select=\"'a'\" regex='a'><xsl:matching-substring>m</xsl:matching-substring><xsl:non-matching-substring>n</xsl:non-matching-substring></xsl:analyze-string></xsl:template>", TestName = "AnalyzeString")]
    [TestCase("<xsl:template match='/'><xsl:variable name='v'><xsl:fallback/>text<out/></xsl:variable></xsl:template>", TestName = "VariableContent")]
    [TestCase("<xsl:attribute-set name='a'><xsl:attribute name='x'>1</xsl:attribute></xsl:attribute-set><data xmlns='urn:d'>top-level data element</data>", TestName = "AttributeSetAndDataElement")]
    [TestCase("<xsl:template match='/'><out><xsl:text>  </xsl:text><xsl:copy-of select='.'/></out></xsl:template>", TestName = "TextAndCopyOf")]
    [TestCase("<xsl:function name='f:a' xmlns:f='urn:f'><xsl:param name='x'/><xsl:sequence select='$x'/></xsl:function>", TestName = "Function")]
    public void Compile_ValidStructure_Compiles(string body)
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
