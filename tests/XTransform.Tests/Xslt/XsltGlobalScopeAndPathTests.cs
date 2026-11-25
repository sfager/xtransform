using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Global declarations see the stylesheet's function library (doc, key, user functions);
/// circular keys are an error; paths may end in atomic values (XPath 2.0 §3.2); regexes work
/// on code points, not UTF-16 units (F&amp;O §7.6).
/// </summary>
[TestFixture]
internal sealed class XsltGlobalScopeAndPathTests
{
    private const string Doc = "<doc><a n='1' id='x'/><a n='2' id='y'/></doc>";

    [Test]
    public void GlobalVariable_CallingUserFunction_IsResolved()
    {
        // Arrange
        const string xsl =
            "<xsl:function name='f:two' xmlns:f='urn:f'><xsl:sequence select='2'/></xsl:function>" +
            "<xsl:variable name='v' select='f:two() + 1' xmlns:f='urn:f'/>" +
            "<xsl:template match='/'><xsl:value-of select='$v'/></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("3"));
    }

    [Test]
    public void GlobalVariable_CallingKey_IsResolved()
    {
        // Arrange
        const string xsl =
            "<xsl:key name='k' match='a' use='@id'/>" +
            "<xsl:variable name='v' select=\"key('k', 'y')/@n\"/>" +
            "<xsl:template match='/'><xsl:value-of select='$v'/></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("2"));
    }

    [Test]
    public void KeyDefinition_DependingOnItself_RaisesXtde0640()
    {
        // Arrange
        const string xsl =
            "<xsl:key name='k' match='a' use=\"key('k', 'x')\"/>" +
            "<xsl:template match='/'><xsl:value-of select=\"count(key('k', 'x'))\"/></xsl:template>";

        // Act
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, Doc))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0640));
    }

    [Test]
    public void Path_LastStepYieldsAtomics_ReturnsThemInOrder()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:value-of select=\"string-join(/doc/a/string(@n), ',')\"/></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo("1,2"));
    }

    [TestCase(11)]
    [TestCase(16)]
    [TestCase(40)]
    public void Concat_MoreThanTenArguments_ConcatenatesThemAll(int count)
    {
        // Arrange — fn:concat accepts any number of arguments (F&O §7.4.1).
        StringBuilder args = new StringBuilder();
        StringBuilder expected = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            args.Append(i == 0 ? string.Empty : ", ").Append('\'').Append((char)('a' + (i % 26))).Append('\'');
            expected.Append((char)('a' + (i % 26)));
        }

        string xsl = $"<xsl:template match='/'><xsl:value-of select=\"concat({args})\"/></xsl:template>";

        // Act / Assert
        Assert.That(Run(xsl, Doc), Is.EqualTo(expected.ToString()));
    }

    [Test]
    public void Path_FirstStepIsAVariable_WorksWhenTheContextItemIsAtomic()
    {
        // Arrange — inside the for-each the context item is the string 'x'; $d/a does not read it.
        const string xsl =
            "<xsl:variable name='d'><r><a/><a/></r></xsl:variable>" +
            "<xsl:template match='/'><xsl:for-each select=\"'x'\"><xsl:value-of select='count($d//a)'/></xsl:for-each></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert — this raised XPTY0004 ("a path step is being applied to an atomic value").
        Assert.That(output, Is.EqualTo("2"));
    }

    [Test]
    public void Path_AxisStepWithAnAtomicContextItem_RaisesXpty0020()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:for-each select=\"'x'\"><xsl:value-of select='child::a'/></xsl:for-each></xsl:template>";

        XTransformException ex = Assert.Throws<XPathDynamicException>(() => Run(xsl, Doc))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0020));
    }

    [Test]
    public void Path_StepOtherThanTheLastYieldsAtomics_RaisesXpty0019()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:value-of select=\"count(/doc/a/string(@n)/x)\"/></xsl:template>";

        XTransformException ex = Assert.Throws<XPathDynamicException>(() => Run(xsl, Doc))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0019));
    }

    [Test]
    public void Path_LastStepMixesNodesAndAtomics_RaisesXpty0018()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='/'><xsl:value-of select=\"count(/doc/a/(if (@n = '1') then . else 'x'))\"/></xsl:template>";

        // Act
        XTransformException ex = Assert.Throws<XPathDynamicException>(() => Run(xsl, Doc))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0018));
    }

    [TestCase("matches(codepoints-to-string(66600), '\\p{Ll}')", "true")]
    [TestCase("matches(codepoints-to-string(66600), '\\P{Ll}')", "false")]
    [TestCase("matches(codepoints-to-string(66600), '^[\\p{Ll}]$')", "true")]
    [TestCase("matches(codepoints-to-string(66600), '^[^a]$')", "true")]
    [TestCase("matches(codepoints-to-string(66600), '^.$')", "true")]
    [TestCase("string-length(replace(codepoints-to-string((97, 66600, 98)), '.', 'x'))", "3")]
    public void Regex_SupplementaryCharacter_IsOneCodePoint(string expression, string expected)
    {
        // Arrange
        string xsl = "<xsl:template match='/'><xsl:value-of select=\"" + expression + "\"/></xsl:template>";

        // Act
        string output = Run(xsl, Doc);

        // Assert
        Assert.That(output, Is.EqualTo(expected));
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
