using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>F&amp;O §15.3.1 — fn:deep-equal compares atomic values with eq, where xs:untypedAtomic counts as xs:string.</summary>
[TestFixture]
internal sealed class XsltDeepEqualTests
{
    [TestCase("deep-equal(xs:untypedAtomic('a'), 'a')", "true")]
    [TestCase("deep-equal('a', xs:untypedAtomic('a'))", "true")]
    [TestCase("deep-equal(xs:untypedAtomic('a'), xs:untypedAtomic('a'))", "true")]
    [TestCase("deep-equal(xs:untypedAtomic('a'), 'b')", "false")]
    [TestCase("deep-equal(('a', 'b'), (xs:untypedAtomic('a'), xs:untypedAtomic('b')))", "true")]
    [TestCase("deep-equal(data(/d/c/@n), ('x', 'y'))", "true")]
    [TestCase("deep-equal(data(/d/c/@n), ('x', 'z'))", "false")]
    [TestCase("deep-equal(1, xs:untypedAtomic('1'))", "false")]
    [TestCase("deep-equal(1, 1.0)", "true")]
    [TestCase("deep-equal(xs:double('NaN'), xs:double('NaN'))", "true")]
    [TestCase("deep-equal((1, 2), (1, 2, 3))", "false")]
    public void DeepEqual_Atomics(string expression, string expected)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
            "<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"" + expression + "\"/></xsl:template></xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d><c n='x'/><c n='y'/></d>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);

        Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo(expected));
    }
}
