using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>Serialization §6 — unrepresentable characters become numeric character references in XML output.</summary>
[TestFixture]
internal sealed class XsltOutputEncodingTests
{
    [Test]
    public void XmlOutputInLatin1_WritesUnrepresentableCharactersAsCharacterReferences()
    {
        // Arrange — U+2030 and U+0660 are outside ISO-8859-1; U+00E9 is inside it.
        const string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='xml' encoding='ISO-8859-1' omit-xml-declaration='yes'/>" +
            "<xsl:template match='/'><out a='&#x2030;'>&#xE9;&#x2030;&#x660;</out></xsl:template></xsl:stylesheet>";

        // Act
        byte[] bytes = Transform(xsl);

        // Assert
        Assert.That(Encoding.GetEncoding("ISO-8859-1").GetString(bytes), Is.EqualTo("<out a=\"&#8240;\">é&#8240;&#1632;</out>"));
    }

    [Test]
    public void XmlOutputInLatin1_WritesACharacterOutsideTheBmpAsOneReference()
    {
        const string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='xml' encoding='ISO-8859-1' omit-xml-declaration='yes'/>" +
            "<xsl:template match='/'><out>&#x10400;</out></xsl:template></xsl:stylesheet>";

        Assert.That(Encoding.GetEncoding("ISO-8859-1").GetString(Transform(xsl)), Is.EqualTo("<out>&#66560;</out>"));
    }

    [Test]
    public void XmlOutputInUtf8_IsNotAffected()
    {
        const string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='xml' omit-xml-declaration='yes'/>" +
            "<xsl:template match='/'><out>&#x2030;</out></xsl:template></xsl:stylesheet>";

        Assert.That(Encoding.UTF8.GetString(Transform(xsl)), Is.EqualTo("<out>‰</out>"));
    }

    private static byte[] Transform(string xsl)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return output.ToArray();
    }
}
