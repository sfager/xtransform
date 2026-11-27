using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.DynamicErrorCases;

/// <summary>Batch 1 of P4-24: names given to constructed nodes (XSLT 2.0 §11.2, §11.3.1, §11.5).</summary>
[TestFixture]
public sealed class DynamicErrorConstructorNameTests
{
    [TestCase("<xsl:element name='a bad name'/>", "XTDE0820", TestName = "ElementNameWithSpaces")]
    [TestCase("<xsl:element name='1prefix:name' namespace='urn:a'/>", "XTDE0820", TestName = "ElementNameStartsWithDigit")]
    [TestCase("<xsl:element name=\"{concat('my:element', '#')}\"/>", "XTDE0820", TestName = "ElementNameFromAnAvt")]
    [TestCase("<xsl:element name='a:b:c'/>", "XTDE0820", TestName = "ElementNameWithTwoColons")]
    [TestCase("<xsl:element name='prefix:name'/>", "XTDE0830", TestName = "ElementPrefixNotDeclared")]
    [TestCase("<xsl:element name=\"{concat('your:element', '10')}\"/>", "XTDE0830", TestName = "ElementPrefixNotDeclaredFromAnAvt")]
    [TestCase("<xsl:attribute name='a bad name'/>", "XTDE0850", TestName = "AttributeNameWithSpaces")]
    [TestCase("<xsl:attribute name=\"{concat('a', '#')}\"/>", "XTDE0850", TestName = "AttributeNameWithHash")]
    [TestCase("<xsl:attribute name=\"{concat('xml', 'ns')}\">v</xsl:attribute>", "XTDE0855", TestName = "AttributeNamedXmlns")]
    [TestCase("<xsl:attribute name=\"{concat('your:', 'ns')}\">v</xsl:attribute>", "XTDE0860", TestName = "AttributePrefixNotDeclared")]
    [TestCase("<xsl:processing-instruction name='42' select='3'/>", "XTDE0890", TestName = "ProcessingInstructionNameIsNotAnNcName")]
    [TestCase("<xsl:processing-instruction name='xml'>version='1.0'</xsl:processing-instruction>", "XTDE0890", TestName = "ProcessingInstructionNamedXml")]
    [TestCase("<xsl:processing-instruction name='XmL' select='1'/>", "XTDE0890", TestName = "ProcessingInstructionNamedXmlInAnyCase")]
    [TestCase("<xsl:processing-instruction name='a:b' select='1'/>", "XTDE0890", TestName = "ProcessingInstructionNameWithAColon")]
    public void Transform_InvalidConstructedName_RaisesTheDynamicError(string instruction, string code)
    {
        // Arrange
        string body = $"<xsl:template match='/'><out>{instruction}</out></xsl:template>";

        // Act
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Transform(body))!;

        // Assert
        Assert.That(ex.ErrorCode.LocalName, Is.EqualTo(code));
    }

    [TestCase("<xsl:element name='a'/>", "<a", TestName = "PlainElement")]
    [TestCase("<xsl:element name='p:a' namespace='urn:other'/>", "urn:other", TestName = "PrefixWithExplicitNamespace")]
    [TestCase("<xsl:element name=' a '/>", "<a", TestName = "WhitespaceAroundTheNameIsTrimmed")]
    [TestCase("<xsl:element name='q:a'/>", "urn:q", TestName = "PrefixDeclaredInTheStylesheet")]
    [TestCase("<xsl:attribute name='x'>1</xsl:attribute>", "x=\"1\"", TestName = "PlainAttribute")]
    [TestCase("<xsl:processing-instruction name='target'>data</xsl:processing-instruction>", "<?target data?>", TestName = "PlainProcessingInstruction")]
    [TestCase("<xsl:processing-instruction name='xml-stylesheet'>href='a.css'</xsl:processing-instruction>", "<?xml-stylesheet href='a.css'?>", TestName = "NameStartingWithXml")]
    public void Transform_ValidConstructedName_Succeeds(string instruction, string fragment)
    {
        // Arrange
        string body = $"<xsl:template match='/'><out>{instruction}</out></xsl:template>";

        // Act
        string result = Transform(body, method: "xml");

        // Assert
        Assert.That(result, Does.Contain(fragment));
    }
    private static string Transform(string body, string method = "text")
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:q='urn:q'>" +
            $"<xsl:output method='{method}' omit-xml-declaration='yes'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
