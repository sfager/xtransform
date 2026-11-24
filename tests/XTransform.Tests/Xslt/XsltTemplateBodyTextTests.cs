using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>Literal text at the start of a template or function body must not be dropped.</summary>
[TestFixture]
internal sealed class XsltTemplateBodyTextTests
{
    [TestCase("<xsl:template match='/'>X<xsl:value-of select='1'/>Y</xsl:template>", "X1Y")]
    [TestCase("<xsl:template match='/'>[<xsl:value-of select='1'/>]</xsl:template>", "[1]")]
    [TestCase("<xsl:template match='/'><xsl:param name='p' select='2'/>A<xsl:value-of select='$p'/></xsl:template>", "A2")]
    [TestCase("<xsl:template match='/'>plain</xsl:template>", "plain")]
    public void TemplateBody_LeadingText_IsKept(string template, string expected)
    {
        // Arrange / Act
        string output = Run(template);

        // Assert
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void FunctionBody_LeadingTextBeforeInstruction_IsKept()
    {
        // Arrange
        const string xsl =
            "<xsl:function name='f:w' xmlns:f='urn:f'><xsl:param name='x'/>(<xsl:value-of select='$x'/>)</xsl:function>" +
            "<xsl:template match='/'><xsl:value-of select=\"string-join(f:w('v'), '')\" xmlns:f='urn:f'/></xsl:template>";

        // Act
        string output = Run(xsl);

        // Assert
        Assert.That(output, Is.EqualTo("(v)"));
    }

    private static string Run(string body)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>"));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<doc/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
