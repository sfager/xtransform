using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>XPath 2.0 §3.2: the result of a path expression is in document order, whatever the axis direction.</summary>
[TestFixture]
internal sealed class XsltAxisOrderTests
{
    private const string Doc = "<a id='1'><b id='2'><c id='3'/><d id='4'/></b><e id='5'/></a>";

    [TestCase("string-join(//c/ancestor::*/@id, ',')", "1,2")]
    [TestCase("string-join(//c/ancestor-or-self::*/@id, ',')", "1,2,3")]
    [TestCase("string-join(//d/preceding-sibling::*/@id, ',')", "3")]
    [TestCase("string-join(//e/preceding::*/@id, ',')", "2,3,4")]
    [TestCase("string-join(//c/ancestor::*[1]/@id, ',')", "2")]
    [TestCase("string-join((//c/ancestor::*)[1]/@id, ',')", "1")]
    [TestCase("string-join(for $n in //c/ancestor::* return $n/@id, ',')", "1,2")]
    [TestCase("string-join(//c/ancestor::*[position() = 1]/@id, ',')", "2")]
    [TestCase("string-join(//e/preceding::*/name(), ',')", "b,c,d")]
    [TestCase("string-join(//d/preceding::*/name(), ',')", "c")]
    [TestCase("string-join(//e/preceding::*[position() &lt;= 2]/name(), ',')", "c,d")]
    [TestCase("string-join(//c/ancestor-or-self::*[position() &gt; 1]/@id, ',')", "1,2")]
    public void PathResults_AreInDocumentOrder(string expression, string expected)
    {
        Assert.That(Eval(expression), Is.EqualTo(expected));
    }

    [Test]
    public void ForEach_OverAReverseAxis_VisitsNodesInDocumentOrder()
    {
        const string body =
            "<xsl:template match='/'><xsl:for-each select='//c/ancestor::*'><xsl:value-of select='concat(@id, \":\", position(), \" \")'/></xsl:for-each></xsl:template>";

        Assert.That(Run(body), Is.EqualTo("1:1 2:2 "));
    }

    private static string Eval(string xpath) =>
        Run("<xsl:template match='/'><xsl:value-of select=\"" + xpath + "\"/></xsl:template>");

    private static string Run(string body)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:output method='text'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Doc));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
