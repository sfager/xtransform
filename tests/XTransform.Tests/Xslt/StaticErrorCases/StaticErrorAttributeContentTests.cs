using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>P4-24 batch 2: attribute values and attribute / content combinations (XSLT 2.0 §3.6, §5.6.1, §6.4, §10.3, §11, §13.1.1, §16.3).</summary>
[TestFixture]
public sealed class StaticErrorAttributeContentTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template match='/'><xsl:attribute name='a' select='2'>five</xsl:attribute></xsl:template>", "XTSE0840", TestName = "AttributeWithSelectAndContent")]
    [TestCase("<xsl:template match='/'><xsl:processing-instruction name='pi' select='3'>four</xsl:processing-instruction></xsl:template>", "XTSE0880", TestName = "ProcessingInstructionWithSelectAndContent")]
    [TestCase("<xsl:template match='/'><xsl:comment select=\"'c'\">no comment</xsl:comment></xsl:template>", "XTSE0940", TestName = "CommentWithSelectAndContent")]
    [TestCase("<xsl:template match='/'><xsl:attribute name='a' select='2'><b/></xsl:attribute></xsl:template>", "XTSE0840", TestName = "AttributeWithSelectAndAnElement")]
    [TestCase("<xsl:template match='/'><xsl:perform-sort select='1 to 5'><xsl:sort select='.'/><xsl:value-of select='.'/></xsl:perform-sort></xsl:template>", "XTSE1040", TestName = "PerformSortWithSelectAndContent")]
    [TestCase("<xsl:key name='k' match='*'/>", "XTSE1205", TestName = "KeyWithNeitherUseNorContent")]
    [TestCase("<xsl:key name='k' match='*' use='17'>twelve</xsl:key>", "XTSE1205", TestName = "KeyWithUseAndContent")]
    [TestCase("<xsl:template match='/'><a b='x{3}y{4'/></xsl:template>", "XTSE0350", TestName = "UnmatchedLeftBrace")]
    [TestCase("<xsl:template match='/'><a b='x}y'/></xsl:template>", "XTSE0370", TestName = "UnmatchedRightBrace")]
    [TestCase("<xsl:template match='doc' priority='2.0e2'/>", "XTSE0530", TestName = "PriorityWithAnExponent")]
    [TestCase("<xsl:template match='doc' priority='high'/>", "XTSE0530", TestName = "PriorityThatIsNotANumber")]
    [TestCase("<xsl:template match='doc' priority=''/>", "XTSE0530", TestName = "EmptyPriority")]
    [TestCase("<xsl:function name='my:f' xmlns:my='urn:my'/><xsl:function name='my:f' xmlns:my='urn:my'/>", "XTSE0770", TestName = "DuplicateFunction")]
    public void Compile_InvalidDeclaration_RaisesTheErrorCode(string body, string code)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain(code));
    }

    [TestCase("2.x", TestName = "VersionWithALetter")]
    [TestCase("two", TestName = "VersionThatIsAWord")]
    [TestCase("2.0e0", TestName = "VersionWithAnExponent")]
    [TestCase("", TestName = "EmptyVersion")]
    [TestCase("2.0.1", TestName = "VersionWithTwoPoints")]
    public void Compile_VersionThatIsNotADecimal_RaisesXtse0110(string version)
    {
        string xsl = $"<xsl:stylesheet version='{version}' xmlns:xsl='{XslNs}'><xsl:template match='/'/></xsl:stylesheet>";

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0110));
    }

    [TestCase("<xsl:template match='/'><xsl:attribute name='a' select='2'/></xsl:template>", TestName = "AttributeWithSelectOnly")]
    [TestCase("<xsl:template match='/'><xsl:attribute name='a'>five</xsl:attribute></xsl:template>", TestName = "AttributeWithContentOnly")]
    [TestCase("<xsl:template match='/'><xsl:comment select=\"'c'\"/><xsl:processing-instruction name='p' select=\"'d'\"/></xsl:template>", TestName = "CommentAndProcessingInstructionWithSelectOnly")]
    [TestCase("<xsl:template match='/'><xsl:perform-sort select='1 to 3'><xsl:sort select='.'/><xsl:fallback>f</xsl:fallback></xsl:perform-sort></xsl:template>", TestName = "PerformSortWithSelectAndSorts")]
    [TestCase("<xsl:key name='k' match='*' use='@id'/>", TestName = "KeyWithUse")]
    [TestCase("<xsl:template match='/'><a b='x{{3}}y{3}'/></xsl:template>", TestName = "EscapedBraces")]
    [TestCase("<xsl:template match='doc' priority='-2'/><xsl:template match='x' priority='+.5'/><xsl:template match='y' priority='3.'/>", TestName = "DecimalPriorities")]
    [TestCase("<xsl:function name='my:f' xmlns:my='urn:my'/><xsl:function name='my:f' xmlns:my='urn:my'><xsl:param name='a'/></xsl:function>", TestName = "SameFunctionNameWithDifferentArities")]
    public void Compile_ValidDeclaration_Compiles(string body)
    {
        Assert.DoesNotThrow(() => Compile(Wrap(body)));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
