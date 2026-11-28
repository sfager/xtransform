using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>P4-14: xsl:namespace, xsl:namespace-alias, the fix-up of clashing prefixes and the xml namespace node (XSLT 2.0 §5.7.1, §11.1.4, §11.7).</summary>
[TestFixture]
public sealed class XsltNamespaceInstructionTests
{
    private const string Xsl = "http://www.w3.org/1999/XSL/Transform";

    // ── xsl:namespace ───────────────────────────────────────────────────────

    [TestCase("<out><xsl:namespace name='p'>urn:p</xsl:namespace></out>", "<out xmlns:p=\"urn:p\" />", TestName = "NamespaceFromContent")]
    [TestCase("<out><xsl:namespace name='p' select=\"'urn:p'\"/></out>", "<out xmlns:p=\"urn:p\" />", TestName = "NamespaceFromSelect")]
    [TestCase("<xsl:element name='e' namespace='urn:d'><xsl:namespace name=''>urn:d</xsl:namespace></xsl:element>", "<e xmlns=\"urn:d\" />", TestName = "DefaultNamespace")]
    [TestCase("<out><xsl:namespace name='p'>urn:p</xsl:namespace><xsl:namespace name='p'>urn:p</xsl:namespace></out>", "<out xmlns:p=\"urn:p\" />", TestName = "TheSameNodeTwiceIsKeptOnce")]
    [TestCase("<out><xsl:namespace name='p'><xsl:attribute name='a'>urn:p</xsl:attribute></xsl:namespace></out>", "<out xmlns:p=\"urn:p\" />", TestName = "AttributeNodeInTheContentGivesItsValue")]
    public void Namespace_Instruction_PutsANamespaceNodeOnTheElement(string body, string expected)
    {
        Assert.That(Run($"<xsl:template match='/'>{body}</xsl:template>"), Is.EqualTo(expected));
    }

    [TestCase("<xsl:namespace name='a b'>urn:p</xsl:namespace>", "XTDE0920", TestName = "NameIsNotAnNcName")]
    [TestCase("<xsl:namespace name='xmlns'>urn:p</xsl:namespace>", "XTDE0920", TestName = "NameIsXmlns")]
    [TestCase("<xsl:namespace name='p'>####</xsl:namespace>", "XTDE0905", TestName = "ValueIsNotAUri")]
    [TestCase("<xsl:namespace name='p'>http://www.w3.org/2000/xmlns/</xsl:namespace>", "XTDE0905", TestName = "ValueIsTheXmlnsNamespace")]
    [TestCase("<xsl:namespace name='xml'>urn:p</xsl:namespace>", "XTDE0925", TestName = "XmlPrefixBoundToAnotherNamespace")]
    [TestCase("<xsl:namespace name='p'>http://www.w3.org/XML/1998/namespace</xsl:namespace>", "XTDE0925", TestName = "XmlNamespaceBoundToAnotherPrefix")]
    [TestCase("<xsl:namespace name='p' select=\"''\"/>", "XTDE0930", TestName = "EmptyValue")]
    [TestCase("<xsl:namespace name='n'>one</xsl:namespace><xsl:namespace name='n'>two</xsl:namespace>", "XTDE0430", TestName = "SamePrefixTwoValues")]
    [TestCase("<xsl:namespace name=''>urn:d</xsl:namespace>", "XTDE0440", TestName = "DefaultNamespaceOnAnElementInNoNamespace")]
    public void Namespace_Instruction_WithAnInvalidNameOrValue_RaisesTheDynamicError(string instruction, string code)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run($"<xsl:template match='/'><out>{instruction}</out></xsl:template>"))!;

        Assert.That(ex.ErrorCode.LocalName, Is.EqualTo(code));
    }

    [TestCase("<xsl:namespace name='p' select=\"'urn:p'\">urn:q</xsl:namespace>", TestName = "SelectAndContent")]
    [TestCase("<xsl:namespace name='p'/>", TestName = "NeitherSelectNorContent")]
    public void Namespace_Instruction_WithSelectAndContentOrNeither_RaisesXtse0910(string instruction)
    {
        string xsl = Wrap($"<xsl:template match='/'><out>{instruction}</out></xsl:template>", string.Empty);

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => new XsltCompiler().Compile(new StringReader(xsl)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain("XTSE0910"));
    }

    // ── xsl:namespace-alias ─────────────────────────────────────────────────

    [Test]
    public void NamespaceAlias_ReplacesTheNamespaceAndPrefixOfLiteralResultNames()
    {
        string result = Run(
            "<xsl:namespace-alias stylesheet-prefix='a' result-prefix='b'/><xsl:template match='/'><a:e a:att='1'><a:f/></a:e></xsl:template>",
            "xmlns:a='urn:a' xmlns:b='urn:b'");

        Assert.That(result, Is.EqualTo("<b:e xmlns:b=\"urn:b\" b:att=\"1\"><b:f /></b:e>"));
    }

    [Test]
    public void NamespaceAlias_DoesNotApplyToXslElementOrXslAttribute()
    {
        string result = Run(
            "<xsl:namespace-alias stylesheet-prefix='a' result-prefix='b'/><xsl:template match='/'><xsl:element name='a:e' namespace='urn:a'><xsl:attribute name='a:x' namespace='urn:a'>1</xsl:attribute></xsl:element></xsl:template>",
            "xmlns:a='urn:a' xmlns:b='urn:b' exclude-result-prefixes='b'");

        Assert.That(result, Is.EqualTo("<a:e xmlns:a=\"urn:a\" a:x=\"1\" />"));
    }

    [Test]
    public void NamespaceAlias_ToTheXsltNamespace_BuildsAStylesheet()
    {
        string result = Run(
            "<xsl:namespace-alias stylesheet-prefix='axsl' result-prefix='xsl'/><xsl:template match='/'><axsl:stylesheet version='2.0'><axsl:template match='/'/></axsl:stylesheet></xsl:template>",
            "xmlns:axsl='urn:alias'");

        Assert.That(result, Is.EqualTo("<xsl:stylesheet xmlns:xsl=\"" + Xsl + "\" version=\"2.0\"><xsl:template match=\"/\" /></xsl:stylesheet>"));
    }

    [Test]
    public void NamespaceAlias_ResultPrefixDefaultWithNoDefaultNamespace_MeansNoNamespace()
    {
        string result = Run(
            "<xsl:namespace-alias stylesheet-prefix='a' result-prefix='#default'/><xsl:template match='/'><a:e/></xsl:template>",
            "xmlns:a='urn:a'");

        Assert.That(result, Is.EqualTo("<e />"));
    }

    [TestCase("<xsl:namespace-alias stylesheet-prefix='a' result-prefix='b'/><xsl:namespace-alias stylesheet-prefix='a' result-prefix='c'/>", "XTSE0810", TestName = "OneNamespaceAliasedTwice")]
    [TestCase("<xsl:namespace-alias stylesheet-prefix='a' result-prefix='nope'/>", "XTSE0812", TestName = "ResultPrefixNotBound")]
    [TestCase("<xsl:namespace-alias stylesheet-prefix='nope' result-prefix='b'/>", "XTSE0812", TestName = "StylesheetPrefixNotBound")]
    public void NamespaceAlias_Invalid_RaisesTheStaticError(string declarations, string code)
    {
        string xsl = Wrap(declarations + "<xsl:template match='/'/>", "xmlns:a='urn:a' xmlns:b='urn:b' xmlns:c='urn:c'");

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => new XsltCompiler().Compile(new StringReader(xsl)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain(code));
    }

    // ── fix-up and serialization ────────────────────────────────────────────

    [Test]
    public void FixUp_ElementPrefixThatClashesWithANamespaceNodeOfTheElement_GetsAFreePrefix()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:element name='ns:e' xmlns:ns='urn:one'><xsl:namespace name='ns'>urn:two</xsl:namespace></xsl:element></xsl:template>",
            string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("xmlns:ns=\"urn:two\""));
            Assert.That(result, Does.Contain("<ns0:e").And.Contain("xmlns:ns0=\"urn:one\""));
        });
    }

    [Test]
    public void Serialization_RepeatedNamespaceNodesOnNestedElements_AreWrittenOnce()
    {
        string result = Run(
            "<xsl:template match='/'><a><b><c/></b></a></xsl:template>",
            "xmlns:p='urn:p'");

        Assert.That(result, Is.EqualTo("<a xmlns:p=\"urn:p\"><b><c /></b></a>"));
    }

    // ── the xml namespace node ──────────────────────────────────────────────

    [Test]
    public void NamespaceAxis_OfAnElement_IncludesTheXmlNamespaceNode()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select='count(/*/namespace::*), namespace-uri-for-prefix(\"xml\", /*), /*/namespace::xml' separator=','/></xsl:template>",
            string.Empty,
            text: true);

        Assert.That(result, Is.EqualTo("1,http://www.w3.org/XML/1998/namespace,http://www.w3.org/XML/1998/namespace"));
    }

    [Test]
    public void Serialization_NeverDeclaresTheXmlPrefix()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:copy-of select='/*'/></xsl:template>",
            string.Empty,
            input: "<d xml:space='preserve'><e/></d>");

        Assert.That(result, Is.EqualTo("<d xml:space=\"preserve\"><e /></d>"));
    }

    private static string Wrap(string body, string stylesheetAttributes) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='" + Xsl + "' " + stylesheetAttributes + ">" + body + "</xsl:stylesheet>";

    private static string Run(string body, string stylesheetAttributes = "", bool text = false, string input = "<d/>")
    {
        string method = text ? "text" : "xml";
        string xsl = Wrap("<xsl:output method='" + method + "' omit-xml-declaration='yes'/>" + body, stylesheetAttributes);
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(inputStream, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
