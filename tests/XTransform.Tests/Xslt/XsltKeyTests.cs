using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §16.3 / §16.4 — xsl:key declarations and the key() function.</summary>
[TestFixture]
internal sealed class XsltKeyTests
{
    private const string Doc = "<d><a id='1' n='a1'/><b id='1' n='b1'/><a id='2' n='a2'/><b id='3' n='b3'/></d>";

    private static string Value(string xpath) =>
        $"<xsl:template match='/'><xsl:value-of select=\"{xpath}\"/></xsl:template>";

    [Test]
    public void SeveralKeysWithTheSameName_AreCombined()
    {
        // Arrange — one declaration indexes the a elements, the other the b elements.
        const string xsl =
            "<xsl:key name='k' match='a' use='@id'/><xsl:key name='k' match='b' use='@id'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"string-join(key('k', '1')/@n, ',')\"/></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert — only the first declaration used to count.
        Assert.That(output, Is.EqualTo("a1,b1"));
    }

    [Test]
    public void KeyResult_IsInDocumentOrderWithoutDuplicates()
    {
        // Arrange — the values are listed out of order and one repeats.
        const string xsl =
            "<xsl:key name='k' match='*[@id]' use='@id'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"string-join(key('k', ('3', '1', '3', '2'))/@n, ',')\"/></xsl:template>";

        // Act / Assert
        Assert.That(Run(xsl, Doc), Is.EqualTo("a1,b1,a2,b3"));
    }

    [Test]
    public void KeyWithThreeArguments_SearchesTheTreeOfTheThirdArgument()
    {
        // Arrange — the same key value exists in the source document and in a temporary tree.
        const string xsl =
            "<xsl:key name='k' match='a' use='@id'/>" +
            "<xsl:variable name='t'><r><a id='1' n='tree'/></r></xsl:variable>" +
            "<xsl:template match='/'><xsl:value-of select=\"concat(key('k', '1')/@n, ';', key('k', '1', $t)/@n)\"/></xsl:template>";

        // Act / Assert
        Assert.That(Run(xsl, Doc), Is.EqualTo("a1;tree"));
    }

    [Test]
    public void KeyWithAPrefixedName_IsFound()
    {
        const string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:p='urn:p'><xsl:output method='text'/>" +
            "<xsl:key name='p:k' match='a' use='@id'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"key('p:k', '2')/@n\"/></xsl:template></xsl:stylesheet>";

        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Doc));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);

        Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo("a2"));
    }

    [Test]
    public void KeyThatIsNotDeclared_YieldsNoNodes()
    {
        Assert.That(Run(Value("count(key('nope', '1'))"), Doc), Is.EqualTo("0"));
    }

    private static string Run(string body, string input)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>"));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
