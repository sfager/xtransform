using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 3 of P4-23: attribute values of the wrong lexical type (XSLT 2.0 §3.1; XTSE0020).</summary>
[TestFixture]
public sealed class StaticErrorValueTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:decimal-format name=\"{concat('f', 'f')}\"/>", TestName = "DecimalFormatNameIsNotAnAvt")]
    [TestCase("<xsl:attribute-set name='12foo'/>", TestName = "NameStartsWithDigit")]
    [TestCase("<xsl:key name='foo::bar' match='*' use='.'/>", TestName = "NameWithTwoColons")]
    [TestCase("<xsl:template name='name/1223'/>", TestName = "TemplateNameWithSlash")]
    [TestCase("<xsl:template name='x'/><xsl:template match='/'><xsl:call-template name='x/y'/></xsl:template>", TestName = "CallTemplateNameWithSlash")]
    [TestCase("<xsl:variable name='x/y'/>", TestName = "VariableNameWithSlash")]
    [TestCase("<xsl:function name='f:test?' xmlns:f='urn:f'/>", TestName = "FunctionNameWithQuestionMark")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates mode='{$x}'/></xsl:template>", TestName = "ModeIsNotAnAvt")]
    [TestCase("<xsl:param name='p' tunnel='yes'/>", TestName = "TunnelOnGlobalParam")]
    [TestCase("<xsl:function name='f:x' xmlns:f='urn:f'><xsl:param name='p' tunnel='yes'/><xsl:sequence select='2'/></xsl:function>", TestName = "TunnelOnFunctionParam")]
    [TestCase("<xsl:template match='/'><xsl:copy-of select='.' copy-namespaces='maybe'/></xsl:template>", TestName = "CopyNamespacesMaybe")]
    [TestCase("<xsl:template match='/'><xsl:message select=\"'m'\" terminate='NO'/></xsl:template>", TestName = "TerminateUppercase")]
    [TestCase("<xsl:output method='xml' byte-order-mark='true'/>", TestName = "OutputByteOrderMarkTrue")]
    [TestCase("<xsl:output method='xml' indent='true'/>", TestName = "OutputIndentTrue")]
    [TestCase("<xsl:output method='xml' standalone='maybe'/>", TestName = "OutputStandalone")]
    [TestCase("<xsl:template match='/'><xsl:result-document method='html' include-content-type='Yes'><x/></xsl:result-document></xsl:template>", TestName = "ResultDocumentIncludeContentType")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates><xsl:sort select='.' order='up'/></xsl:apply-templates></xsl:template>", TestName = "SortOrder")]
    [TestCase("<xsl:template match='/'><xsl:number level='some'/></xsl:template>", TestName = "NumberLevel")]
    public void Compile_InvalidAttributeValue_RaisesXtse0020(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0020));
    }

    [TestCase("<xsl:variable name='p:v' xmlns:p='urn:p'/><xsl:template name='a.b-c'/>", TestName = "QualifiedAndPunctuatedNames")]
    [TestCase("<xsl:template match='x' mode='#all'/><xsl:template match='y' mode='m #default'/>", TestName = "TemplateModes")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates mode='#current'/><xsl:apply-templates mode='#default'/></xsl:template>", TestName = "ApplyTemplatesModes")]
    [TestCase("<xsl:template match='/'><xsl:param name='p' tunnel='yes' required='no'/><out/></xsl:template>", TestName = "TunnelOnTemplateParam")]
    [TestCase("<xsl:output method='xml' indent='yes' byte-order-mark='no' standalone='omit' omit-xml-declaration='yes'/>", TestName = "OutputFlags")]
    [TestCase("<xsl:template match='/'><xsl:message select=\"'m'\" terminate='{1 = 1}'/></xsl:template>", TestName = "AvtIsNotCheckedStatically")]
    [TestCase("<xsl:template match='/'><xsl:apply-templates><xsl:sort select='.' order='descending' case-order='upper-first' stable='yes'/></xsl:apply-templates></xsl:template>", TestName = "SortAttributes")]
    [TestCase("<xsl:template match='/' version='3.0'><xsl:param name='p' tunnel='true'/><xsl:copy-of select='.' copy-namespaces='1'/></xsl:template>", TestName = "ForwardsCompatibleSpellings")]
    public void Compile_ValidAttributeValue_Compiles(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    [Test]
    public void Compile_UnknownTopLevelElementUnderForwardsCompatibility_IsIgnoredWithItsContent()
    {
        // Arrange (XSLT 2.0 §3.9: the element and everything inside it is ignored, including a nested version="2.0")
        string xsl =
            $"<xsl:stylesheet version='25.0' xmlns:xsl='{XslNs}'>" +
            "<xsl:template match='/'><boo/></xsl:template>" +
            "<xsl:subroutine match='*'><xsl:variable name='A nice cup of tea' version='2.0' extra='x'/><xsl:choose><xsl:always/></xsl:choose></xsl:subroutine>" +
            "</xsl:stylesheet>";

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));
}
