using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>
/// XSLT 2.0 §5.5.3 — predicates in patterns are evaluated: a node matches a step with predicates only when
/// it is selected by the equivalent expression, with position and last() relative to the step.
/// </summary>
[TestFixture]
internal sealed class XsltPatternPredicateTests
{
    private const string Items =
        "<doc><item x='a'>1</item><item x='b'>2</item><item x='a'>3</item><item>4</item></doc>";

    private const string Rules =
        "<xsl:template match='doc'><xsl:apply-templates select='item'/></xsl:template>";

    [Test]
    public void Match_AttributePredicate_OnlyMatchingNodesUseTheRule()
    {
        // Arrange
        const string xsl = Rules +
            "<xsl:template match=\"item[@x = 'a']\">A</xsl:template>" +
            "<xsl:template match='item'>-</xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert — before the fix every item matched the predicate rule.
        Assert.That(output, Is.EqualTo("A-A-"));
    }

    [TestCase("item[1]", "F---")]
    [TestCase("item[2]", "-F--")]
    [TestCase("item[position() mod 2 = 0]", "-F-F")]
    [TestCase("item[last()]", "---F")]
    [TestCase("item[position() = last() - 1]", "--F-")]
    [TestCase("item[@x][2]", "-F--")]            // second item that has @x: predicates filter in order
    [TestCase("item[2][@x]", "-F--")]
    [TestCase("item[3][not(@x)]", "----")]
    [TestCase("doc/item[1]", "F---")]
    public void Match_PositionalPredicate_UsesPositionAmongTheSiblingsSelectedByTheStep(string pattern, string expected)
    {
        // Arrange
        string xsl = Rules +
            $"<xsl:template match=\"{pattern}\">F</xsl:template>" +
            "<xsl:template match='item'>-</xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert
        Assert.That(output, Is.EqualTo(expected));
    }

    [Test]
    public void Match_PositionCountsOnlySiblingsSatisfyingTheNodeTest()
    {
        // Arrange — the x element does not count towards the position of y.
        const string xsl =
            "<xsl:template match='d'><xsl:apply-templates select='*'/></xsl:template>" +
            "<xsl:template match='y[1]'>first-y;</xsl:template>" +
            "<xsl:template match='*'>other;</xsl:template>";

        // Act
        string output = Run(xsl, "<d><x/><y/><y/></d>");

        // Assert
        Assert.That(output, Is.EqualTo("other;first-y;other;"));
    }

    [Test]
    public void Match_PredicateOnAttributeStep_IsEvaluated()
    {
        // Arrange
        const string xsl =
            "<xsl:template match='doc'><xsl:apply-templates select='item/@x'/></xsl:template>" +
            "<xsl:template match=\"@x[. = 'a']\">[a]</xsl:template>" +
            "<xsl:template match='@x'>[other]</xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert
        Assert.That(output, Is.EqualTo("[a][other][a]"));
    }

    [Test]
    public void Match_PredicateUsingAGlobalVariable_SeesItsValue()
    {
        // Arrange
        const string xsl =
            "<xsl:variable name='v' select=\"'b'\"/>" + Rules +
            "<xsl:template match='item[@x = $v]'>B</xsl:template>" +
            "<xsl:template match='item'>-</xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert
        Assert.That(output, Is.EqualTo("-B--"));
    }

    [Test]
    public void Match_CurrentInAPredicate_IsTheNodeBeingMatched()
    {
        // Arrange
        const string xsl = Rules +
            "<xsl:template match='item[@x = current()/@x]'>X</xsl:template>" +
            "<xsl:template match='item'>-</xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert — true exactly for the items that have an @x.
        Assert.That(output, Is.EqualTo("XXX-"));
    }

    [Test]
    public void Key_MatchPatternWithPredicate_IndexesOnlyMatchingNodes()
    {
        // Arrange — only items whose value exceeds 2 are indexed.
        const string xsl =
            "<xsl:key name='k' match='item[. &gt; 2]' use='@x'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"count(key('k', 'a'))\"/></xsl:template>";

        // Act
        string output = Run(xsl, Items);

        // Assert — without the predicate both 'a' items would be indexed.
        Assert.That(output, Is.EqualTo("1"));
    }

    [Test]
    public void Number_CountPatternWithPositionalPredicate_UsesThePredicate()
    {
        // Arrange — only odd-positioned n elements are counted (XSLT 2.0 §12.2).
        const string xsl =
            "<xsl:template match='d'><xsl:apply-templates select='n'/></xsl:template>" +
            "<xsl:template match='n'><xsl:number count='n[position() mod 2 = 1]'/>;</xsl:template>";

        // Act
        string output = Run(xsl, "<d><n/><n/><n/><n/><n/></d>");

        // Assert — an even-positioned n has no counted ancestor-or-self.
        Assert.That(output, Is.EqualTo("1;;2;;3;"));
    }

    [Test]
    public void Match_PredicateThatIsNotNumericAndNotPositional_DoesNotLookAtSiblings()
    {
        // Arrange — a flat document with many siblings still matches correctly (fast path).
        StringBuilder doc = new StringBuilder("<doc>");
        for (int i = 0; i < 2000; i++)
        {
            doc.Append(i % 2 == 0 ? "<item x='a'/>" : "<item/>");
        }

        doc.Append("</doc>");
        const string xsl = Rules +
            "<xsl:template match='item[@x]'>1</xsl:template>" +
            "<xsl:template match='item'></xsl:template>";

        // Act
        string output = Run(xsl, doc.ToString());

        // Assert
        Assert.That(output.Length, Is.EqualTo(1000));
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
