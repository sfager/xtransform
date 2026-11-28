using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §14 — xsl:for-each-group, current-group() and current-grouping-key().</summary>
[TestFixture]
internal sealed class XsltForEachGroupTests
{
    private const string Items =
        "<d><i k='a' v='1'/><i k='b' v='2'/><i k='a' v='3'/><i k='c' v='4'/><i k='b' v='5'/><i k='a' v='6'/></d>";

    private static string Group(string attributes, string body = "<xsl:value-of select='concat(current-grouping-key(), \":\", string-join(current-group()/@v, \",\"))'/><xsl:text> </xsl:text>", string select = "/d/i") =>
        $"<xsl:template match='/'><xsl:for-each-group select='{select}' {attributes}>{body}</xsl:for-each-group></xsl:template>";

    // ── group-by ─────────────────────────────────────────────────────────────

    [Test]
    public void GroupBy_FormsGroupsInOrderOfFirstAppearance()
    {
        Assert.That(Run(Group("group-by='@k'"), Items), Is.EqualTo("a:1,3,6 b:2,5 c:4 "));
    }

    [Test]
    public void GroupBy_PositionAndLast_CountTheGroups()
    {
        string body = "<xsl:value-of select='concat(position(), \"/\", last(), \"=\", @k, \" \")'/>";

        Assert.That(Run(Group("group-by='@k'", body), Items), Is.EqualTo("1/3=a 2/3=b 3/3=c "));
    }

    [Test]
    public void GroupBy_ContextItemIsTheInitialItemOfTheGroup()
    {
        Assert.That(Run(Group("group-by='@k'", "<xsl:value-of select='@v'/><xsl:text> </xsl:text>"), Items), Is.EqualTo("1 2 4 "));
    }

    [Test]
    public void GroupBy_AnItemWithSeveralKeysJoinsSeveralGroups_ButNeverTheSameGroupTwice()
    {
        const string xml = "<d><i t='x y x' v='1'/><i t='y' v='2'/><i t='z' v='3'/><i v='4'/></d>";

        // The last item has no key and so belongs to no group; item 1 lists x twice but is in x once.
        Assert.That(
            Run(Group("group-by='tokenize(@t, &quot; &quot;)'"), xml),
            Is.EqualTo("x:1 y:1,2 z:3 "));
    }

    [Test]
    public void GroupBy_NumbersOfDifferentTypesThatAreEqualShareAGroup()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:for-each-group select='(1, 1.0, xs:double(1), 2, xs:float(2), 3)' group-by='.'>" +
            "<xsl:value-of select='count(current-group())'/><xsl:text> </xsl:text></xsl:for-each-group></xsl:template>";

        Assert.That(Run(xsl, "<d/>"), Is.EqualTo("3 2 1 "));
    }

    [Test]
    public void GroupBy_NaNIsEqualToItself_AndValuesOfIncomparableTypesAreDifferentGroups()
    {
        const string xsl =
            "<xsl:template match='/'><xsl:for-each-group select=\"(xs:double('NaN'), 1, xs:double('NaN'), '1', true())\" group-by='.'>" +
            "<xsl:value-of select='count(current-group())'/><xsl:text> </xsl:text></xsl:for-each-group></xsl:template>";

        Assert.That(Run(xsl, "<d/>"), Is.EqualTo("2 1 1 1 "));
    }

    [Test]
    public void GroupBy_UntypedKeysAreComparedAsStrings_AndCurrentGroupingKeyIsAString()
    {
        const string xml = "<d><i k='01'/><i k='1'/><i k='01'/></d>";
        string body = "<xsl:value-of select='concat(current-grouping-key(), \"=\", count(current-group()), \" \")'/>";

        Assert.That(Run(Group("group-by='@k'", body), xml), Is.EqualTo("01=2 1=1 "));
    }

    [Test]
    public void GroupBy_DateTimesInDifferentTimeZones_AreEqualWhenTheyAreTheSameInstant()
    {
        const string xml = "<d><i t='2008-01-01T10:00:00+01:00'/><i t='2008-01-01T09:00:00Z'/><i t='2008-01-01T10:00:00Z'/></d>";
        string body = "<xsl:value-of select='count(current-group())'/><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-by='xs:dateTime(@t)'", body), xml), Is.EqualTo("2 1 "));
    }

    [Test]
    public void GroupBy_ThatSelectsNothing_RunsTheBodyZeroTimes()
    {
        Assert.That(Run(Group("group-by='@k'", select: "/d/none"), Items), Is.EqualTo(string.Empty));
    }

    [Test]
    public void GroupBy_AHundredThousandItems_CompletesQuickly()
    {
        // The grouping is hash based: 100,000 items in 1,000 groups.
        StringBuilder xml = new StringBuilder("<d>");
        for (int i = 0; i < 100000; i++)
        {
            xml.Append("<i k='").Append(i % 1000).Append("'/>");
        }

        xml.Append("</d>");
        string xsl = Group("group-by='@k'", "<xsl:value-of select='count(current-group())'/><xsl:text> </xsl:text>");

        Stopwatch watch = Stopwatch.StartNew();
        string output = Run(xsl, xml.ToString());
        watch.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(output.Trim().Split(' '), Has.Length.EqualTo(1000));
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(10), $"took {watch.Elapsed.TotalSeconds:F2} s");
        });
    }

    // ── group-adjacent ───────────────────────────────────────────────────────

    [Test]
    public void GroupAdjacent_AdjacentItemsWithEqualKeysFormAGroup_AndAKeyCanRecurInASeparateGroup()
    {
        const string xml = "<d><i k='a' v='1'/><i k='a' v='2'/><i k='b' v='3'/><i k='a' v='4'/><i k='a' v='5'/></d>";

        Assert.That(Run(Group("group-adjacent='@k'"), xml), Is.EqualTo("a:1,2 b:3 a:4,5 "));
    }

    [TestCase("()")]
    [TestCase("(1, 2)")]
    public void GroupAdjacent_KeyThatIsNotExactlyOneValue_RaisesXttE1100(string key)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(Group($"group-adjacent='{key}'"), Items))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE1100));
    }

    // ── group-starting-with / group-ending-with ──────────────────────────────

    private const string Doc =
        "<d><h>H1</h><p>a</p><p>b</p><h>H2</h><p>c</p></d>";

    [Test]
    public void GroupStartingWith_AMatchingNodeStartsAGroup()
    {
        string body = "<xsl:value-of select='string-join(current-group(), \"|\")'/><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-starting-with='h'", body, "/d/*"), Doc), Is.EqualTo("H1|a|b H2|c "));
    }

    [Test]
    public void GroupStartingWith_TheFirstNodeStartsAGroupEvenWhenItDoesNotMatch()
    {
        const string doc = "<d><p>x</p><h>H1</h><p>a</p></d>";
        string body = "<xsl:value-of select='string-join(current-group(), \"|\")'/><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-starting-with='h'", body, "/d/*"), doc), Is.EqualTo("x H1|a "));
    }

    [Test]
    public void GroupEndingWith_AMatchingNodeEndsTheGroup()
    {
        string body = "<xsl:value-of select='string-join(current-group(), \"|\")'/><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-ending-with='p[. = &quot;b&quot;]'", body, "/d/*"), Doc), Is.EqualTo("H1|a|b H2|c "));
    }

    [Test]
    public void GroupStartingWith_CurrentGroupingKeyIsEmpty()
    {
        string body = "<xsl:value-of select='empty(current-grouping-key())'/><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-starting-with='h'", body, "/d/*"), Doc), Is.EqualTo("true true "));
    }

    [Test]
    public void GroupStartingWith_APopulationItemThatIsNotANode_RaisesXtte1120()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(Group("group-starting-with='h'", select: "(1, 2)"), Doc))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE1120));
    }

    [Test]
    public void GroupStartingWith_PatternMayUseALocalVariable()
    {
        string xsl =
            "<xsl:template match='/'><xsl:variable name='tag' select=\"'h'\"/><xsl:for-each-group select='/d/*' group-starting-with='*[name() = $tag]'>" +
            "<xsl:value-of select='count(current-group())'/><xsl:text> </xsl:text></xsl:for-each-group></xsl:template>";

        Assert.That(Run(xsl, Doc), Is.EqualTo("3 2 "));
    }

    // ── Sorting the groups ───────────────────────────────────────────────────

    [Test]
    public void Sort_OrdersTheGroups_ByKeyOrBySize()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Run(Group("group-by='@k'", "<xsl:sort select='current-grouping-key()' order='descending'/><xsl:value-of select='current-grouping-key()'/>"), Items),
                Is.EqualTo("cba"));
            Assert.That(
                Run(Group("group-by='@k'", "<xsl:sort select='count(current-group())'/><xsl:value-of select='current-grouping-key()'/>"), Items),
                Is.EqualTo("cba"));
            Assert.That(
                Run(Group("group-by='@k'", "<xsl:sort select='count(current-group())' order='descending'/><xsl:value-of select='current-grouping-key()'/>"), Items),
                Is.EqualTo("abc"));
        });
    }

    [Test]
    public void Sort_PositionAndLast_FollowTheSortedGroups()
    {
        string body = "<xsl:sort select='current-grouping-key()' order='descending'/><xsl:value-of select='concat(position(), current-grouping-key())'/>";

        Assert.That(Run(Group("group-by='@k'", body), Items), Is.EqualTo("1c2b3a"));
    }

    [Test]
    public void Sort_EqualKeysKeepTheOrderOfFirstAppearance()
    {
        // b and c both have one or two members: sorting by 1 for all keeps a, b, c.
        string body = "<xsl:sort select='1'/><xsl:value-of select='current-grouping-key()'/>";

        Assert.That(Run(Group("group-by='@k'", body), Items), Is.EqualTo("abc"));
    }

    // ── current-group() and current-grouping-key() ───────────────────────────

    [Test]
    public void OutsideAGroup_CurrentGroupAndKeyAreEmpty()
    {
        string xsl = "<xsl:template match='/'><xsl:value-of select='count(current-group()), empty(current-grouping-key())'/></xsl:template>";

        Assert.That(Run(xsl, Items), Is.EqualTo("0 true"));
    }

    [Test]
    public void NestedGroups_EachLevelHasItsOwnGroup_AndTheOuterOneIsRestored()
    {
        string body =
            "<xsl:value-of select='concat(current-grouping-key(), \"[\")'/>" +
            "<xsl:for-each-group select='current-group()' group-by='@v mod 2'><xsl:value-of select='concat(current-grouping-key(), \":\", count(current-group()), \" \")'/></xsl:for-each-group>" +
            "<xsl:value-of select='concat(\"]\", current-grouping-key(), count(current-group()), \" \")'/>";

        Assert.That(Run(Group("group-by='@k'", body), Items), Is.EqualTo("a[1:2 0:1 ]a3 b[0:1 1:1 ]b2 c[0:1 ]c1 "));
    }

    [Test]
    public void InsideAnXslForEach_TheCurrentGroupIsStillInEffect()
    {
        string body = "<xsl:for-each select='1 to 2'><xsl:value-of select='count(current-group())'/></xsl:for-each><xsl:text> </xsl:text>";

        Assert.That(Run(Group("group-by='@k'", body), Items), Is.EqualTo("33 22 11 "));
    }

    [Test]
    public void InsideAStylesheetFunction_TheCurrentGroupIsEmpty()
    {
        string xsl =
            "<xsl:function name='f:n' xmlns:f='urn:f'><xsl:sequence select='count(current-group())'/></xsl:function>" +
            "<xsl:template match='/' xmlns:f='urn:f'><xsl:for-each-group select='/d/i' group-by='@k'><xsl:value-of select='concat(count(current-group()), f:n(), \" \")'/></xsl:for-each-group></xsl:template>";

        Assert.That(Run(xsl, Items), Is.EqualTo("30 20 10 "));
    }

    [Test]
    public void AStylesheetFunction_CanBuildElementsAndGroupThem()
    {
        // Elements constructed inside a function body are returned as nodes.
        string xsl =
            "<xsl:function name='f:g' xmlns:f='urn:f'><xsl:param name='n'/>" +
            "<xsl:for-each-group select='$n' group-by='@k'><g k='{current-grouping-key()}' n='{count(current-group())}'/></xsl:for-each-group></xsl:function>" +
            "<xsl:template match='/' xmlns:f='urn:f'><xsl:value-of select=\"string-join(for $e in f:g(/d/i) return concat($e/@k, '=', $e/@n), ' ')\"/></xsl:template>";

        Assert.That(Run(xsl, Items), Is.EqualTo("a=3 b=2 c=1"));
    }

    [Test]
    public void AStylesheetFunction_CanCopyNodesAndSeeItsParametersInAPattern()
    {
        string xsl =
            "<xsl:function name='f:h' xmlns:f='urn:f'><xsl:param name='n'/><xsl:param name='tag'/>" +
            "<xsl:for-each-group select='$n' group-starting-with='*[name() = $tag]'><s n='{count(current-group())}'/></xsl:for-each-group></xsl:function>" +
            "<xsl:template match='/' xmlns:f='urn:f'><xsl:value-of select=\"string-join(f:h(/d/*, 'h')/@n, ',')\"/></xsl:template>";

        Assert.That(Run(xsl, Doc), Is.EqualTo("3,2"));
    }

    // ── Collation ────────────────────────────────────────────────────────────

    [Test]
    public void Collation_Codepoint_IsAccepted()
    {
        Assert.That(
            Run(Group("group-by='@k' collation='http://www.w3.org/2005/xpath-functions/collation/codepoint'"), Items),
            Is.EqualTo("a:1,3,6 b:2,5 c:4 "));
    }

    [Test]
    public void Collation_ThatIsNotSupported_RaisesXtde1110()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(Group("group-by='@k' collation='http://example.com/unknown'"), Items))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1110));
    }

    // ── Static errors ────────────────────────────────────────────────────────

    [TestCase("")]
    [TestCase("group-by='@k' group-adjacent='@k'")]
    [TestCase("group-starting-with='h' group-ending-with='h'")]
    [TestCase("group-by='@k' group-starting-with='h'")]
    public void NoneOrSeveralGroupingAttributes_RaiseXtse1080(string attributes)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Run(Group(attributes), Items))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE1080));
    }

    [Test]
    public void CollationWithAPatternMethod_RaisesXtse1090()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run(Group("group-starting-with='h' collation='http://www.w3.org/2005/xpath-functions/collation/codepoint'"), Items))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE1090));
    }

    [Test]
    public void CurrentGroupInAPattern_RaisesXtse1060_AndCurrentGroupingKeyXtse1070()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<XsltStaticException>(() => Run(Group("group-starting-with='i[exists(current-group())]'"), Items))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTSE1060));
            Assert.That(
                Assert.Throws<XsltStaticException>(() => Run(Group("group-ending-with='i[current-grouping-key() = 1]'"), Items))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTSE1070));
        });
    }

    [Test]
    public void StableOnASecondXslSort_RaisesXtse1017()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run(Group("group-by='@k'", "<xsl:sort select='.'/><xsl:sort select='.' stable='yes'/>"), Items))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE1017));
    }

    // ── Simplified stylesheets (§3.7) ────────────────────────────────────────

    [Test]
    public void SimplifiedStylesheet_ALiteralResultElementWithXslVersion_ActsAsTheTemplateForTheRoot()
    {
        const string xsl =
            "<out xsl:version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:value-of select='count(/d/i)'/></out>";

        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Items));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);

        Assert.That(Encoding.UTF8.GetString(output.ToArray()), Does.Contain(">6</out>"));
    }

    [Test]
    public void SimplifiedStylesheet_WithoutXslVersion_RaisesXtse0150()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => new XsltCompiler().Compile(new StringReader("<out xmlns:xsl='http://www.w3.org/1999/XSL/Transform'/>")))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0150));
    }

    // ── Time-zone aware comparison (F&O §10.4) ───────────────────────────────

    [TestCase("xs:dateTime('2008-01-01T10:00:00+01:00') eq xs:dateTime('2008-01-01T09:00:00Z')", "true")]
    [TestCase("xs:dateTime('2008-01-01T10:00:00+01:00') lt xs:dateTime('2008-01-01T10:00:00Z')", "true")]
    [TestCase("xs:dateTime('2008-01-01T23:30:00-05:00') gt xs:dateTime('2008-01-02T00:00:00Z')", "true")]
    [TestCase("xs:time('10:00:00+02:00') eq xs:time('08:00:00Z')", "true")]
    [TestCase("xs:time('23:00:00-05:00') gt xs:time('12:00:00Z')", "true")]
    [TestCase("xs:date('2008-01-01+02:00') lt xs:date('2008-01-01Z')", "true")]
    [TestCase("xs:dateTime('2008-01-01T10:00:00') eq xs:dateTime('2008-01-01T10:00:00Z')", "true")]
    [TestCase("max((xs:dateTime('2008-01-01T10:00:00+05:00'), xs:dateTime('2008-01-01T10:00:00Z')))", "2008-01-01T10:00:00Z")]
    public void DateTimeComparison_UsesTheInstant(string expression, string expected)
    {
        Assert.That(Run($"<xsl:template match='/'><xsl:value-of select=\"{expression}\"/></xsl:template>", "<d/>"), Is.EqualTo(expected));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Run(string body, string input)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
