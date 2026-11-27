using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.DynamicErrorCases;

/// <summary>Batch 2 of P4-24: the key() function (XSLT 2.0 §16.3, §16.4; XTDE1260, XTDE1270).</summary>
[TestFixture]
public sealed class DynamicErrorKeyTests
{
    [TestCase("key('nonkey', '12')", TestName = "UndeclaredName")]
    [TestCase("key('123456', '12')", TestName = "NameIsNotAQName")]
    [TestCase("key('bad:name', '12')", TestName = "PrefixIsNotBound")]
    [TestCase("key('my:nokey', 'abc')", TestName = "NameInADeclaredNamespaceButNotDeclared")]
    [TestCase("key('k', 'abc', /)[1] | key('your:k', 'abc')", TestName = "UnboundPrefixOnADeclaredLocalName")]
    [TestCase("key('p:k', 'abc')", TestName = "DeclaredLocalNameInAnotherNamespace")]
    public void Transform_KeyNameThatIsNotDeclared_RaisesXtde1260(string call)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Transform(call))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1260));
    }

    [Test]
    public void Transform_KeyCalledWithoutAContextItem_RaisesXtde1270()
    {
        // A stylesheet function has no context item.
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Transform("my:f()", "<xsl:function name='my:f'><xsl:sequence select=\"key('k', 'abc')\"/></xsl:function>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1270));
    }

    [TestCase("count(key('k', 'x'))", "1", TestName = "DeclaredKey")]
    [TestCase("count(key('my:q', 'y'))", "1", TestName = "DeclaredKeyInANamespace")]
    [TestCase("count(key(' k ', 'x'))", "1", TestName = "WhitespaceAroundTheName")]
    [TestCase("count(key('k', 'x', /))", "1", TestName = "ThirdArgumentIsADocument")]
    [TestCase("count(key('k', 'absent'))", "0", TestName = "NoMatchIsNotAnError")]
    public void Transform_DeclaredKey_Succeeds(string call, string expected)
    {
        Assert.That(Transform($"string({call})"), Is.EqualTo(expected));
    }

    private static string Transform(string expression, string extraDeclarations = "")
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:my='urn:my' xmlns:p='urn:p'>" +
            "<xsl:output method='text'/>" +
            "<xsl:key name='k' match='e' use='@id'/>" +
            "<xsl:key name='my:q' match='e' use='@id2'/>" +
            extraDeclarations +
            $"<xsl:template match='/'><xsl:value-of select=\"{expression.Replace("\"", "'")}\"/></xsl:template>" +
            "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d><e id='x' id2='y'/></d>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
