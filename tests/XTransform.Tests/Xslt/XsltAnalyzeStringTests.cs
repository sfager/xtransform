using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §15 — xsl:analyze-string and regex-group().</summary>
[TestFixture]
internal sealed class XsltAnalyzeStringTests
{
    private static string Wrap(string select, string regex, string body, string attributes = "") =>
        $"<xsl:template match='/'><xsl:analyze-string select=\"{select}\" regex=\"{regex}\" {attributes}>{body}</xsl:analyze-string></xsl:template>";

    private const string BothBodies =
        "<xsl:matching-substring>[<xsl:value-of select='.'/>]</xsl:matching-substring>" +
        "<xsl:non-matching-substring><xsl:value-of select='.'/></xsl:non-matching-substring>";

    // ── Splitting ───────────────────────────────────────────────────────────────

    [TestCase("'banana'", "an", "b[an][an]a")]
    [TestCase("'banana'", "a", "b[a]n[a]n[a]")]
    [TestCase("'abc'", "x", "abc")]
    [TestCase("'abc'", "abc", "[abc]")]
    [TestCase("'aaa'", "a+", "[aaa]")]
    [TestCase("'aaa'", "a+?", "[a][a][a]")]
    [TestCase("'one two'", "\\s", "one[ ]two")]
    [TestCase("''", "a", "")]
    [TestCase("'a\U0001F600b'", ".", "[a][\U0001F600][b]")]             // a supplementary character is one match
    public void Analyze_Splits_IntoMatchingAndNonMatchingSubstrings(string select, string regex, string expected)
    {
        Assert.That(Run(Wrap(select, regex, BothBodies), "<doc/>"), Is.EqualTo(expected));
    }

    [Test]
    public void Analyze_OnlyMatchingSubstringPresent_NonMatchingTextIsDiscarded()
    {
        const string body = "<xsl:matching-substring>[<xsl:value-of select='.'/>]</xsl:matching-substring>";

        Assert.That(Run(Wrap("'a1b22c'", "[0-9]+", body), "<doc/>"), Is.EqualTo("[1][22]"));
    }

    [Test]
    public void Analyze_OnlyNonMatchingSubstringPresent_MatchesAreDiscarded()
    {
        const string body = "<xsl:non-matching-substring><xsl:value-of select='.'/>;</xsl:non-matching-substring>";

        Assert.That(Run(Wrap("'a1b22c'", "[0-9]+", body), "<doc/>"), Is.EqualTo("a;b;c;"));
    }

    // ── Focus ───────────────────────────────────────────────────────────────────

    [Test]
    public void Analyze_Focus_ContextItemIsTheSubstringAndPositionCountsAllSubstrings()
    {
        // Arrange
        const string body =
            "<xsl:matching-substring>M<xsl:value-of select='position()'/>/<xsl:value-of select='last()'/><xsl:text> </xsl:text></xsl:matching-substring>" +
            "<xsl:non-matching-substring>N<xsl:value-of select='position()'/>/<xsl:value-of select='last()'/><xsl:text> </xsl:text></xsl:non-matching-substring>";

        // Act
        string output = Run(Wrap("'a1b2'", "[0-9]", body), "<doc/>");

        // Assert — substrings are a, 1, b, 2.
        Assert.That(output, Is.EqualTo("N1/4 M2/4 N3/4 M4/4 "));
    }

    [Test]
    public void Analyze_Focus_CurrentIsTheSubstring()
    {
        const string body = "<xsl:matching-substring><xsl:value-of select='string-length(current())'/></xsl:matching-substring>";

        Assert.That(Run(Wrap("'xaay'", "a+", body), "<doc/>"), Is.EqualTo("2"));
    }

    // ── regex-group ──────────────────────────────────────────────────────────────

    [Test]
    public void RegexGroup_ReturnsTheCapturedGroups()
    {
        // Arrange
        const string body =
            "<xsl:matching-substring><xsl:value-of select=\"regex-group(1)\"/>=<xsl:value-of select=\"regex-group(2)\"/>;</xsl:matching-substring>";

        // Act
        string output = Run(Wrap("'a=1,b=2'", "([a-z])=([0-9])", body), "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("a=1;b=2;"));
    }

    [TestCase("0", "ab")]                 // the whole match
    [TestCase("1", "a")]
    [TestCase("2", "")]                    // group 2 did not take part
    [TestCase("3", "")]                    // no such group
    [TestCase("-1", "")]
    public void RegexGroup_NumberOutOfRangeOrUnmatched_IsTheEmptyString(string n, string expected)
    {
        // Arrange
        string body = $"<xsl:matching-substring><xsl:value-of select=\"regex-group({n})\"/></xsl:matching-substring>";

        // Act
        string output = Run(Wrap("'ab'", "(a)(x)?b", body), "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void RegexGroup_InsideNonMatchingSubstringAndOutsideAnalyzeString_IsTheEmptyString()
    {
        // Arrange
        const string body =
            "<xsl:non-matching-substring>[<xsl:value-of select=\"regex-group(0)\"/>]</xsl:non-matching-substring>";
        string xsl = "<xsl:template match='/'>" +
                     "<xsl:analyze-string select=\"'ab'\" regex='b'>" + body + "</xsl:analyze-string>" +
                     "<xsl:value-of select=\"regex-group(0)\"/>|</xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("[]|"));
    }

    [Test]
    public void RegexGroup_NestedAnalyzeString_InnerMatchWinsAndOuterIsRestored()
    {
        // Arrange
        const string inner =
            "<xsl:analyze-string select='.' regex='([a-z])'>" +
            "<xsl:matching-substring>(<xsl:value-of select='regex-group(1)'/>)</xsl:matching-substring>" +
            "</xsl:analyze-string>";
        string body =
            "<xsl:matching-substring>" + inner + "{<xsl:value-of select='regex-group(1)'/>}</xsl:matching-substring>";

        // Act
        string output = Run(Wrap("'ab-cd'", "([a-z]+)", body), "<doc/>");

        // Assert — after the inner analysis the outer group (the word) is visible again.
        Assert.That(output, Is.EqualTo("(a)(b){ab}(c)(d){cd}"));
    }

    // ── Flags and attribute value templates ──────────────────────────────────────

    [TestCase("i", "'aBc'", "b", "a[B]c")]
    [TestCase("", "'aBc'", "b", "aBc")]
    [TestCase("s", "codepoints-to-string((97, 10, 98))", "a.b", "[a\nb]")]
    [TestCase("m", "codepoints-to-string((97, 10, 98))", "^b", "a\n[b]")]
    [TestCase("x", "'abc'", "a b", "[ab]c")]
    public void Analyze_Flags_ChangeMatching(string flags, string select, string regex, string expected)
    {
        Assert.That(Run(Wrap(select, regex, BothBodies, $"flags='{flags}'"), "<doc/>"), Is.EqualTo(expected));
    }

    [Test]
    public void Analyze_RegexAndFlagsAreAttributeValueTemplates()
    {
        // Arrange
        const string xsl =
            "<xsl:param name='r' select=\"'B'\"/><xsl:param name='f' select=\"'i'\"/>" +
            "<xsl:template match='/'><xsl:analyze-string select=\"'abc'\" regex='{$r}' flags='{$f}'>" +
            "<xsl:matching-substring>[<xsl:value-of select='.'/>]</xsl:matching-substring>" +
            "<xsl:non-matching-substring><xsl:value-of select='.'/></xsl:non-matching-substring>" +
            "</xsl:analyze-string></xsl:template>";

        // Act
        string output = Run(xsl, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("a[b]c"));
    }

    [Test]
    public void Analyze_SelectIsANode_ItsStringValueIsAnalysed()
    {
        Assert.That(Run(Wrap("/doc/t", "-", BothBodies), "<doc><t>a-b</t></doc>"), Is.EqualTo("a[-]b"));
    }

    // ── Errors ───────────────────────────────────────────────────────────────────

    [Test]
    public void Analyze_NeitherSubstringBody_RaisesXtse1130()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Run(Wrap("'a'", "a", string.Empty), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE1130));
    }

    [TestCase("<xsl:non-matching-substring/><xsl:matching-substring/>")]   // wrong order
    [TestCase("<xsl:matching-substring/><xsl:matching-substring/>")]
    [TestCase("<xsl:matching-substring/><xsl:value-of select='1'/>")]
    [TestCase("<xsl:matching-substring/>text")]
    public void Analyze_ContentModelViolation_RaisesXtse0010(string body)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Run(Wrap("'a'", "a", body), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [Test]
    public void Analyze_MissingRegexAttribute_RaisesXtse0010()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:analyze-string select=\"'a'\"><xsl:matching-substring/></xsl:analyze-string></xsl:template>";

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Run(xsl, "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [TestCase("(?:a)")]                 // non-capturing groups are XPath 3.0
    [TestCase("a{{")]
    [TestCase("[")]
    public void Analyze_InvalidLiteralRegex_RaisesXtde1140(string regex)
    {
        XTransformException ex = Assert.Throws<XsltStaticException>(() => Run(Wrap("'a'", regex, BothBodies), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1140));
    }

    [Test]
    public void Analyze_InvalidRegexComputedAtRunTime_RaisesXtde1140()
    {
        const string xsl =
            "<xsl:param name='r' select=\"'('\"/>" +
            "<xsl:template match='/'><xsl:analyze-string select=\"'a'\" regex='{$r}'><xsl:matching-substring/></xsl:analyze-string></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1140));
    }

    [TestCase("q")]
    [TestCase("g")]
    public void Analyze_InvalidFlags_RaisesXtde1145(string flags)
    {
        XTransformException ex = Assert.Throws<XsltStaticException>(
            () => Run(Wrap("'a'", "a", BothBodies, $"flags='{flags}'"), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1145));
    }

    [TestCase("a*")]
    [TestCase("^")]
    [TestCase("(b)?")]
    public void Analyze_RegexMatchingTheEmptyString_RaisesXtde1150(string regex)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(Wrap("'abc'", regex, BothBodies), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1150));
    }

    [TestCase("22")]                    // xs:integer is not an xs:string
    [TestCase("()")]                    // an empty sequence is not allowed in XSLT 2.0
    [TestCase("('a', 'b')")]
    [TestCase("true()")]
    public void Analyze_SelectNotASingleString_RaisesXpty0004(string select)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(Wrap(select, "a", BothBodies), "<doc/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
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
