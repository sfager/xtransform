using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.DynamicErrorCases;

/// <summary>Batch 5 of P4-24: an attribute node must come before any other content of its element (XSLT 2.0 §5.7.1; XTDE0410).</summary>
[TestFixture]
public sealed class DynamicErrorAttributePlacementTests
{
    [TestCase("<out>fred<xsl:attribute name='ginger'/></out>", TestName = "AttributeAfterText")]
    [TestCase("<out><child/><xsl:attribute name='late'>v</xsl:attribute></out>", TestName = "AttributeAfterAnElement")]
    [TestCase("<out><xsl:comment>c</xsl:comment><xsl:attribute name='late'>v</xsl:attribute></out>", TestName = "AttributeAfterAComment")]
    [TestCase("<out><xsl:value-of select='1'/><xsl:attribute name='late'>v</xsl:attribute></out>", TestName = "AttributeAfterAValue")]
    [TestCase("<out><xsl:copy-of select='/d/c'/><xsl:attribute name='late'>v</xsl:attribute></out>", TestName = "AttributeAfterACopiedElement")]
    [TestCase("<out><xsl:for-each select='1 to 2'><xsl:attribute name='a{.}'>v</xsl:attribute><b/></xsl:for-each></out>", TestName = "AttributeInTheSecondIterationAfterAnElement")]
    public void Transform_AttributeAfterContent_RaisesXtde0410(string body)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Transform(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0410));
    }

    [TestCase("<out><xsl:attribute name='a'>1</xsl:attribute><xsl:attribute name='b'>2</xsl:attribute><child/></out>", "<out a=\"1\" b=\"2\"><child /></out>", TestName = "AttributesFirst")]
    [TestCase("<out><xsl:attribute name='a'>1</xsl:attribute>text</out>", "<out a=\"1\">text</out>", TestName = "AttributeThenText")]
    [TestCase("<out><xsl:attribute name='a'/><xsl:text></xsl:text><xsl:attribute name='b'/></out>", "<out a=\"\" b=\"\" />", TestName = "EmptyTextDoesNotCountAsContent")]
    [TestCase("<out><xsl:copy-of select='/d/@x'/><xsl:attribute name='a'>1</xsl:attribute></out>", "<out x=\"1\" a=\"1\" />", TestName = "CopiedAttributeThenAttribute")]
    [TestCase("<out><xsl:for-each select='1 to 2'><b><xsl:attribute name='a'>1</xsl:attribute></b></xsl:for-each></out>", "<out><b a=\"1\" /><b a=\"1\" /></out>", TestName = "AttributeInsideEachChild")]
    public void Transform_AttributeBeforeContent_Succeeds(string body, string expected)
    {
        Assert.That(Transform(body), Is.EqualTo(expected));
    }

    private static string Transform(string body)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='xml' omit-xml-declaration='yes'/>" +
            $"<xsl:template match='/'>{body}</xsl:template></xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d x='1'><c/></d>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
