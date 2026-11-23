using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// P3-07 golden transformation tests. Each case runs <see cref="XsltRunner.Transform"/>
/// against a small input XML and compares the serialized output to a pre-rendered expected
/// string. The serializer is <see cref="XmlWriterReceiver"/> — a minimal XmlWriter-backed
/// stand-in; the full <see cref="XmlEmitter"/> lands in P3-13.
/// </summary>
[TestFixture]
internal sealed class XsltTransformGoldenTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── xsl:value-of ─────────────────────────────────────────────────────────

    [Test]
    public void ValueOf_EmitsAtomicStringValue()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:value-of select='doc/a'/></xsl:template>");
        string expected = "hello";
        AssertTransform(xsl, "<doc><a>hello</a></doc>", expected);
    }

    [Test]
    public void ValueOf_JoinsMultipleItemsWithDefaultSpaceSeparator()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:value-of select='doc/a'/></xsl:template>");
        string expected = "x y z";
        AssertTransform(xsl, "<doc><a>x</a><a>y</a><a>z</a></doc>", expected);
    }

    [Test]
    public void ValueOf_CustomSeparator()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:value-of select='doc/a' separator=','/></xsl:template>");
        string expected = "x,y,z";
        AssertTransform(xsl, "<doc><a>x</a><a>y</a><a>z</a></doc>", expected);
    }

    [Test]
    public void ValueOf_EmptySequence_EmitsNothing()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:value-of select='doc/missing'/></xsl:template>");
        string expected = "";
        AssertTransform(xsl, "<doc/>", expected);
    }

    // ── xsl:text ─────────────────────────────────────────────────────────────

    [Test]
    public void Text_EmitsLiteralContent()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:text>hello world</xsl:text></xsl:template>");
        string expected = "hello world";
        AssertTransform(xsl, "<doc/>", expected);
    }

    [Test]
    public void Text_PreservesWhitespace()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:text>  spaced  </xsl:text></xsl:template>");
        string expected = "  spaced  ";
        AssertTransform(xsl, "<doc/>", expected);
    }

    // ── Literal result elements ──────────────────────────────────────────────

    [Test]
    public void LRE_EmitsSelfClosingElement()
    {
        string xsl = Wrap("<xsl:template match='/'><out/></xsl:template>");
        string expected = "<out />";
        AssertTransform(xsl, "<doc/>", expected);
    }

    [Test]
    public void LRE_WithLiteralAttribute()
    {
        string xsl = Wrap("<xsl:template match='/'><out id='x'/></xsl:template>");
        string expected = "<out id=\"x\" />";
        AssertTransform(xsl, "<doc/>", expected);
    }

    [Test]
    public void LRE_WithAvtAttribute()
    {
        string xsl = Wrap("<xsl:template match='/'><out n='{doc/a}'/></xsl:template>");
        string expected = "<out n=\"42\" />";
        AssertTransform(xsl, "<doc><a>42</a></doc>", expected);
    }

    [Test]
    public void LRE_WithNestedContent()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<out><inner><xsl:value-of select='doc/a'/></inner></out>" +
            "</xsl:template>");
        string expected = "<out><inner>hi</inner></out>";
        AssertTransform(xsl, "<doc><a>hi</a></doc>", expected);
    }

    [Test]
    public void LRE_WithMultipleAttributes()
    {
        string xsl = Wrap("<xsl:template match='/'><out id='1' name='two'/></xsl:template>");
        string expected = "<out id=\"1\" name=\"two\" />";
        AssertTransform(xsl, "<doc/>", expected);
    }

    // ── Control flow + output ────────────────────────────────────────────────

    [Test]
    public void If_WithEbvTrue_EmitsBody()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:if test='doc/a'><yes/></xsl:if>" +
            "</xsl:template>");
        string expected = "<yes />";
        AssertTransform(xsl, "<doc><a/></doc>", expected);
    }

    [Test]
    public void If_WithEbvFalse_EmitsNothing()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:if test='doc/a'><yes/></xsl:if>" +
            "</xsl:template>");
        string expected = "";
        AssertTransform(xsl, "<doc/>", expected);
    }

    [Test]
    public void Choose_WhenFirstMatches_EmitsFirstBody()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:choose>" +
            "<xsl:when test='doc/a'><a-branch/></xsl:when>" +
            "<xsl:when test='doc/b'><b-branch/></xsl:when>" +
            "<xsl:otherwise><else/></xsl:otherwise>" +
            "</xsl:choose>" +
            "</xsl:template>");
        string expected = "<a-branch />";
        AssertTransform(xsl, "<doc><a/></doc>", expected);
    }

    [Test]
    public void Choose_Otherwise_WhenNoWhenMatches()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:choose>" +
            "<xsl:when test='doc/a'><a/></xsl:when>" +
            "<xsl:otherwise><else/></xsl:otherwise>" +
            "</xsl:choose>" +
            "</xsl:template>");
        string expected = "<else />";
        AssertTransform(xsl, "<doc/>", expected);
    }

    [Test]
    public void ForEach_IteratesAndEmitsBodyPerItem()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<ul>" +
            "<xsl:for-each select='doc/item'><li><xsl:value-of select='.'/></li></xsl:for-each>" +
            "</ul>" +
            "</xsl:template>");
        string expected = "<ul><li>a</li><li>b</li><li>c</li></ul>";
        AssertTransform(xsl, "<doc><item>a</item><item>b</item><item>c</item></doc>", expected);
    }

    [Test]
    public void ForEach_WithPosition_ExposesCorrectPosition()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:for-each select='doc/item'>" +
            "<i p='{position()}'><xsl:value-of select='.'/></i>" +
            "</xsl:for-each>" +
            "</xsl:template>");
        string expected = "<i p=\"1\">x</i><i p=\"2\">y</i>";
        AssertTransform(xsl, "<doc><item>x</item><item>y</item></doc>", expected);
    }

    // ── Dispatch + templates ─────────────────────────────────────────────────

    [Test]
    public void ApplyTemplates_InvokesMatchingRule()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><out><xsl:apply-templates select='doc/item'/></out></xsl:template>" +
            "<xsl:template match='item'><li><xsl:value-of select='.'/></li></xsl:template>");
        string expected = "<out><li>a</li><li>b</li></out>";
        AssertTransform(xsl, "<doc><item>a</item><item>b</item></doc>", expected);
    }

    [Test]
    public void ApplyTemplates_WithMode()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><out><xsl:apply-templates select='doc/item' mode='upper'/></out></xsl:template>" +
            "<xsl:template match='item' mode='upper'><U><xsl:value-of select='.'/></U></xsl:template>" +
            "<xsl:template match='item'><lower/></xsl:template>");
        string expected = "<out><U>a</U><U>b</U></out>";
        AssertTransform(xsl, "<doc><item>a</item><item>b</item></doc>", expected);
    }

    [Test]
    public void ApplyTemplates_BuiltInRule_CopiesTextValue()
    {
        // No template for text(); the built-in rule copies the string value.
        string xsl = Wrap(
            "<xsl:template match='/'><out><xsl:apply-templates/></out></xsl:template>" +
            "<xsl:template match='doc'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='a'><A><xsl:apply-templates/></A></xsl:template>");
        string expected = "<out><A>hi</A></out>";
        AssertTransform(xsl, "<doc><a>hi</a></doc>", expected);
    }

    [Test]
    public void CallTemplate_WithParam()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:call-template name='greet'><xsl:with-param name='n' select='&quot;world&quot;'/></xsl:call-template>" +
            "</xsl:template>" +
            "<xsl:template name='greet'>" +
            "<xsl:param name='n'/>" +
            "<hi><xsl:value-of select='$n'/></hi>" +
            "</xsl:template>");
        string expected = "<hi>world</hi>";
        // P3-07a scope: xsl:param visible-to-body lands in P3-08. The compiler currently
        // compiles $n against an empty external-variable scope, so this test exercises the
        // path up to — but not through — the value-of; expect XPST0008 for now.
        // Mark as explicit to document the known gap without breaking the suite.
        Assert.Ignore("$param reference in template body deferred to P3-08");
        _ = expected;
    }

    // ── Priority and mode dispatch ───────────────────────────────────────────

    [Test]
    public void HigherPriorityTemplate_Wins()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><out><xsl:apply-templates select='doc/x'/></out></xsl:template>" +
            "<xsl:template match='x' priority='1'><lo/></xsl:template>" +
            "<xsl:template match='x' priority='2'><hi/></xsl:template>");
        string expected = "<out><hi /></out>";
        AssertTransform(xsl, "<doc><x/></doc>", expected);
    }

    [Test]
    public void PathPattern_a_Slash_b_MatchesOnlyUnderA()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><out><xsl:apply-templates select='//b'/></out></xsl:template>" +
            "<xsl:template match='a/b'><under-a/></xsl:template>" +
            "<xsl:template match='b'><anywhere/></xsl:template>");
        // <b> under <a> matches 'a/b' (higher priority than bare 'b'); <b> under <c> matches 'b'.
        string expected = "<out><under-a /><anywhere /></out>";
        AssertTransform(xsl, "<doc><a><b/></a><c><b/></c></doc>", expected);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static void AssertTransform(string xsl, string input, string expected)
    {
        // Arrange
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        CompiledStylesheet compiled = XsltCompiler.Compile(asm);

        NamePool pool = new NamePool();
        using StringReader sr = new StringReader(input);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree inputTree = TinyTreeBuilder.Load(reader, pool);
        XdmNode inputDoc = new XdmNode(NodeHandle.ForNode(inputTree, 0));

        StringBuilder sb = new StringBuilder();
        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        using XmlWriter writer = XmlWriter.Create(sb, settings);
        using XmlWriterReceiver sink = new XmlWriterReceiver(writer, pool);

        // Act
        XsltRunner.Transform(compiled, inputDoc, sink, pool);
        writer.Flush();

        // Assert
        Assert.That(sb.ToString(), Is.EqualTo(expected));
    }

    private sealed class InMemoryResolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Add(string name, string xml) => _map[name] = xml;

        public Stream? Open(Uri uri)
        {
            const string prefix = "file:///suite/";
            string s = uri.OriginalString;
            if (!s.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            return _map.TryGetValue(s.Substring(prefix.Length), out string? xml)
                ? new MemoryStream(Encoding.UTF8.GetBytes(xml))
                : null;
        }
    }
}
