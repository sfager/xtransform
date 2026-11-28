using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>P4-21: xsl:key (use content, typed values, third argument, patterns), id() / idref() from a DTD or xml:id, DTD whitespace, and stylesheet text around comments.</summary>
[TestFixture]
public sealed class XsltKeysIdsAndWhitespaceTests
{
    private const string Xsl = "http://www.w3.org/1999/XSL/Transform";

    // ── keys ────────────────────────────────────────────────────────────────

    [Test]
    public void Key_UseExpression_SeesGlobalVariablesAndParameters()
    {
        string result = Run(
            "<xsl:param name='p' select=\"'2'\"/><xsl:key name='k' match='a' use='@c[. = $p]'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"count(key('k', '2')), count(key('k', '1'))\"/></xsl:template>",
            input: "<d><a c='1'/><a c='2'/><a c='2'/></d>");

        Assert.That(result, Is.EqualTo("2 0"));
    }

    [Test]
    public void Key_ValueFromTheContentOfTheDeclaration()
    {
        string result = Run(
            "<xsl:key name='k' match='a'><xsl:sequence select=\"concat(@c, '!')\"/></xsl:key>" +
            "<xsl:template match='/'><xsl:value-of select=\"key('k', '2!')/@id\"/></xsl:template>",
            input: "<d><a id='x' c='1'/><a id='y' c='2'/></d>");

        Assert.That(result, Is.EqualTo("y"));
    }

    [TestCase("xs:dateTime(.)", "xs:dateTime('2002-10-05T22:00:00-05:00')", "<d><i id='a'>2002-10-05T22:00:00-05:00</i><i id='b'>2002-10-05T22:00:00-04:00</i><i id='c'>2002-10-06T03:00:00Z</i></d>", "a,c", TestName = "DateTimesCompareAsInstants")]
    [TestCase("xs:double(.)", "xs:double('NaN')", "<d><i id='a'>NaN</i><i id='b'>2</i></d>", "", TestName = "NaNMatchesNothing")]
    [TestCase("xs:integer(.)", "1.0", "<d><i id='a'>1</i><i id='b'>2</i></d>", "a", TestName = "IntegerKeyMatchesADecimalValue")]
    public void Key_Values_AreComparedByTheirTypes(string use, string lookup, string input, string expected)
    {
        string result = Run(
            "<xsl:key name='k' match='i' use='" + use + "'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"key('k', " + lookup + ")/@id\" separator=','/></xsl:template>",
            input: input);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Key_ThirdArgument_LimitsTheSearchToThatSubtree()
    {
        string result = Run(
            "<xsl:key name='k' match='t' use='@n'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"key('k', 'x', /d/s[1])/@id\" separator=','/></xsl:template>",
            input: "<d><s><t n='x' id='1'/></s><s><t n='x' id='2'/></s></d>");

        Assert.That(result, Is.EqualTo("1"));
    }

    [Test]
    public void Pattern_KeyFollowedByAPath_MatchesTheDescendantsOfTheKeyResult()
    {
        string result = Run(
            "<xsl:key name='k' match='div' use='@t'/>" +
            "<xsl:template match='/'><xsl:apply-templates select='//p'/></xsl:template>" +
            "<xsl:template match=\"key('k', 'a')//p\">[<xsl:value-of select='.'/>]</xsl:template>" +
            "<xsl:template match='p'/>",
            input: "<d><div t='a'><p>one</p><x><p>two</p></x></div><div t='b'><p>three</p></div></d>");

        Assert.That(result, Is.EqualTo("[one][two]"));
    }

    [Test]
    public void Pattern_KeyWithANumericValue_Matches()
    {
        string result = Run(
            "<xsl:key name='k' match='b' use='string-length(.)'/>" +
            "<xsl:template match='/'><xsl:apply-templates select='//b'/></xsl:template>" +
            "<xsl:template match='key(\"k\", 3)'>three </xsl:template><xsl:template match='b'>other </xsl:template>",
            input: "<d><b>abc</b><b>ab</b></d>");

        Assert.That(result, Is.EqualTo("three other "));
    }

    // ── id() and idref() ────────────────────────────────────────────────────

    [Test]
    public void Id_AttributeDeclaredIdInTheDtd_IsFound()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select=\"id('b')/@n\"/></xsl:template>",
            input: "<!DOCTYPE d [<!ELEMENT d (e*)><!ELEMENT e EMPTY><!ATTLIST e code ID #REQUIRED n CDATA #IMPLIED>]><d><e code='a' n='1'/><e code='b' n='2'/></d>");

        Assert.That(result, Is.EqualTo("2"));
    }

    [Test]
    public void Id_AttributeNamedIdButNotDeclaredInTheDtd_IsNotAnId()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select=\"count(id('a'))\"/></xsl:template>",
            input: "<!DOCTYPE d [<!ELEMENT d (e*)><!ELEMENT e EMPTY><!ATTLIST e id CDATA #IMPLIED code ID #IMPLIED>]><d><e id='a'/></d>");

        Assert.That(result, Is.EqualTo("0"));
    }

    [Test]
    public void Id_XmlId_IsFoundAndSeveralTokensGiveTheElementsInDocumentOrder()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select=\"id('z y')/@n\" separator=','/></xsl:template>",
            input: "<d><e xml:id='y' n='1'/><e xml:id='z' n='2'/></d>");

        Assert.That(result, Is.EqualTo("1,2"));
    }

    [Test]
    public void IdPattern_FollowedByAPath_Matches()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:apply-templates select='//t'/></xsl:template>" +
            "<xsl:template match=\"id('a')//t\">[<xsl:value-of select='.'/>]</xsl:template><xsl:template match='t'/>",
            input: "<d><e xml:id='a'><t>one</t></e><e xml:id='b'><t>two</t></e></d>");

        Assert.That(result, Is.EqualTo("[one]"));
    }

    [Test]
    public void Idref_AttributesDeclaredIdrefsInTheDtd_AreFound()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select=\"idref('a')/../@code\" separator=','/></xsl:template>",
            input: "<!DOCTYPE d [<!ELEMENT d (e*)><!ELEMENT e EMPTY><!ATTLIST e code ID #REQUIRED refs IDREFS #IMPLIED>]><d><e code='a'/><e code='b' refs='a c'/><e code='c' refs='b'/></d>");

        Assert.That(result, Is.EqualTo("b"));
    }

    // ── whitespace ──────────────────────────────────────────────────────────

    [Test]
    public void Whitespace_InElementOnlyContentDeclaredByTheDtd_IsNotInTheTree()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select='count(/d/node())'/></xsl:template>",
            input: "<!DOCTYPE d [<!ELEMENT d (e*)><!ELEMENT e (#PCDATA)>]><d>\n <e> x </e>\n <e/>\n</d>");

        Assert.That(result, Is.EqualTo("2"));
    }

    [Test]
    public void Whitespace_InMixedContentDeclaredByTheDtd_IsKept()
    {
        string result = Run(
            "<xsl:template match='/'><xsl:value-of select='count(/d/node())'/></xsl:template>",
            input: "<!DOCTYPE d [<!ELEMENT d (#PCDATA|e)*><!ELEMENT e EMPTY>]><d> <e/> </d>");

        Assert.That(result, Is.EqualTo("3"));
    }

    [Test]
    public void StylesheetText_AroundACommentOrAProcessingInstruction_IsOneTextNode()
    {
        string result = Run(
            "<xsl:template match='/'><e>   h<!--c-->   </e><e>   <?pi?>h</e></xsl:template>",
            text: false);

        Assert.That(result, Is.EqualTo("<e>   h   </e><e>   h</e>"));
    }

    private static string Run(string body, string input = "<d/>", bool text = true)
    {
        string xsl = "<xsl:stylesheet version='2.0' xmlns:xsl='" + Xsl + "' xmlns:xs='http://www.w3.org/2001/XMLSchema' exclude-result-prefixes='xs'><xsl:output method='" + (text ? "text" : "xml") + "' omit-xml-declaration='yes'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(inputStream, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
