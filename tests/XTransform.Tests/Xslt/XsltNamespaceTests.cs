using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>P4-14: prefixes of constructed names, namespace nodes on literal result elements and the namespaces left out of the result (XSLT 2.0 §5.7.1, §11.1.2, §11.1.3).</summary>
[TestFixture]
public sealed class XsltNamespaceTests
{
    private const string Xsl = "http://www.w3.org/1999/XSL/Transform";

    [Test]
    public void LiteralResultElement_CopiesTheNamespacesInScope()
    {
        string result = Run("<xsl:template match='/'><out/></xsl:template>", "xmlns:a='urn:a' xmlns:b='urn:b'");

        Assert.That(result, Is.EqualTo("<out xmlns:a=\"urn:a\" xmlns:b=\"urn:b\" />"));
    }

    [Test]
    public void LiteralResultElement_NeverCopiesTheXsltNamespace()
    {
        string result = Run("<xsl:template match='/'><out/></xsl:template>", string.Empty);

        Assert.That(result, Is.EqualTo("<out />"));
    }

    [TestCase("a", "<out xmlns:b=\"urn:b\" />", TestName = "ExcludeOnePrefix")]
    [TestCase("a b", "<out />", TestName = "ExcludeTwoPrefixes")]
    [TestCase("#all", "<out />", TestName = "ExcludeAll")]
    [TestCase("b a", "<out />", TestName = "ExcludeInAnyOrder")]
    public void ExcludeResultPrefixes_LeavesTheNamespacesOutOfTheResult(string exclude, string expected)
    {
        string result = Run(
            "<xsl:template match='/'><out/></xsl:template>",
            $"xmlns:a='urn:a' xmlns:b='urn:b' exclude-result-prefixes='{exclude}'");

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ExcludeResultPrefixes_OnAnElement_AppliesToItsDescendantsOnly()
    {
        string result = Run(
            "<xsl:template match='/'><out><in xsl:exclude-result-prefixes='a'/></out></xsl:template>",
            "xmlns:a='urn:a'");

        Assert.That(result, Does.StartWith("<out xmlns:a=\"urn:a\">"));
    }

    [Test]
    public void ExcludeResultPrefixes_Default_LeavesTheDefaultNamespaceOut()
    {
        string result = Run(
            "<xsl:template match='/'><out/></xsl:template>",
            "xmlns='urn:d' xmlns:e='urn:e' exclude-result-prefixes='#default e'");

        // The element is in urn:d, so its own name needs the declaration; xmlns:e is not copied.
        Assert.That(result, Is.EqualTo("<out xmlns=\"urn:d\" />"));
    }

    [Test]
    public void ExtensionElementPrefixes_AreLeftOutOfTheResult()
    {
        string result = Run(
            "<xsl:template match='/'><out/></xsl:template>",
            "xmlns:ext='urn:ext' xmlns:a='urn:a' extension-element-prefixes='ext'");

        Assert.That(result, Is.EqualTo("<out xmlns:a=\"urn:a\" />"));
    }

    [TestCase("exclude-result-prefixes='nope'", "XTSE0808", TestName = "ExcludeAPrefixThatIsNotBound")]
    [TestCase("exclude-result-prefixes='#default'", "XTSE0809", TestName = "ExcludeDefaultWithNoDefaultNamespace")]
    [TestCase("extension-element-prefixes='nope'", "XTSE1430", TestName = "ExtensionPrefixThatIsNotBound")]
    [TestCase("extension-element-prefixes='#default'", "XTSE0809", TestName = "ExtensionDefaultWithNoDefaultNamespace")]
    public void NamespaceDesignation_WithAnUnboundPrefix_RaisesTheStaticError(string attribute, string code)
    {
        string xsl = $"<xsl:stylesheet version='2.0' xmlns:xsl='{Xsl}' {attribute}><xsl:template match='/'/></xsl:stylesheet>";

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => new XsltCompiler().Compile(new StringReader(xsl)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain(code));
    }

    [TestCase("<p:out xmlns:p='urn:p'/>", "name(/*)", "p:out", TestName = "LiteralResultElementKeepsItsPrefix")]
    [TestCase("<xsl:element name='q:e' namespace='urn:q'/>", "name(/*)", "q:e", TestName = "ElementKeepsThePrefixOfItsName")]
    [TestCase("<out><xsl:attribute name='q:a' namespace='urn:q'>1</xsl:attribute></out>", "name(/*/@*)", "q:a", TestName = "AttributeKeepsThePrefixOfItsName")]
    [TestCase("<xsl:element name='p:e' namespace=''/>", "name(/*)", "e", TestName = "NoNamespaceMeansNoPrefix")]
    [TestCase("<xsl:element name='e' namespace='urn:x'/>", "namespace-uri(/*)", "urn:x", TestName = "NamespaceFromTheAttribute")]
    public void NameOfAConstructedNode_IncludesThePrefix(string constructor, string expression, string expected)
    {
        string body =
            "<xsl:template match='/'><xsl:variable name='t'>" + constructor + "</xsl:variable>" +
            "<xsl:for-each select='$t'><xsl:value-of select=\"" + expression + "\"/></xsl:for-each></xsl:template>";

        Assert.That(Run(body, string.Empty, text: true), Is.EqualTo(expected));
    }

    [Test]
    public void Attribute_WithAPrefixBoundToAnotherNamespace_GetsAGeneratedPrefix()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:variable name='v'><p:e><xsl:attribute name='p:a' namespace='urn:other'>1</xsl:attribute></p:e></xsl:variable><xsl:value-of select='name($v/*/@*)'/></xsl:template>",
            "xmlns:p='urn:p'",
            text: true);

        Assert.That(result, Is.EqualTo("ns0:a"));
    }

    [Test]
    public void Attribute_InANamespaceWithNoPrefix_GetsAGeneratedPrefix()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:variable name='v'><e><xsl:attribute name='a' namespace='urn:x'>1</xsl:attribute></e></xsl:variable><xsl:value-of select='name($v/*/@*)'/></xsl:template>",
            string.Empty,
            text: true);

        Assert.That(result, Is.EqualTo("ns0:a"));
    }

    [Test]
    public void NameOfASourceNode_IncludesThePrefixItWasWrittenWith()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select='name(/*)'/>,<xsl:value-of select='name(/*/@*)'/></xsl:template>",
            string.Empty,
            text: true,
            input: "<s:r xmlns:s='urn:s' xmlns:t='urn:t' t:a='1'/>");

        Assert.That(result, Is.EqualTo("s:r,t:a"));
    }

    [Test]
    public void NameTest_MatchesAnElementWhateverPrefixItWasWrittenWith()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select='count(/a:r/a:c)'/>,<xsl:value-of select='count(/*/b:c)'/></xsl:template>",
            "xmlns:a='urn:s' xmlns:b='urn:s'",
            text: true,
            input: "<s:r xmlns:s='urn:s'><x:c xmlns:x='urn:s'/><s:c/></s:r>");

        Assert.That(result, Is.EqualTo("2,2"));
    }

    private static string Run(string body, string stylesheetAttributes, bool text = false, string input = "<d/>")
    {
        string method = text ? "text" : "xml";
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='" + Xsl + "' " + stylesheetAttributes + ">" +
            "<xsl:output method='" + method + "' omit-xml-declaration='yes'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(inputStream, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
