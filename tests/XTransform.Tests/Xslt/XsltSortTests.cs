using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §13 — xsl:sort in xsl:for-each and xsl:apply-templates, and xsl:perform-sort.</summary>
[TestFixture]
internal sealed class XsltSortTests
{
    private const string Words = "<d><w>pear</w><w>Apple</w><w>fig</w><w>banana</w><w>apple</w></d>";
    private const string Numbers = "<d><n>10</n><n>9</n><n>100</n><n>-5</n><n>2.5</n></d>";

    private static string ForEach(string sort, string select = "/d/*", string body = "<xsl:value-of select='.'/><xsl:text> </xsl:text>") =>
        $"<xsl:template match='/'><xsl:for-each select='{select}'>{sort}{body}</xsl:for-each></xsl:template>";

    // ── Order, data type, keys ───────────────────────────────────────────────

    [Test]
    public void Sort_DefaultIsAscendingTextInCodepointOrder()
    {
        // Uppercase letters come before lowercase ones in codepoint order.
        Assert.That(Run(ForEach("<xsl:sort/>"), Words), Is.EqualTo("Apple apple banana fig pear "));
    }

    [Test]
    public void Sort_Descending()
    {
        Assert.That(Run(ForEach("<xsl:sort order='descending'/>"), Words), Is.EqualTo("pear fig banana apple Apple "));
    }

    [Test]
    public void Sort_NumberDataType_ComparesNumerically()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Run(ForEach("<xsl:sort data-type='number'/>"), Numbers), Is.EqualTo("-5 2.5 9 10 100 "));
            Assert.That(Run(ForEach("<xsl:sort data-type='text'/>"), Numbers), Is.EqualTo("-5 10 100 2.5 9 "));
        });
    }

    [Test]
    public void Sort_TypedKeys_CompareAsTheirType()
    {
        // The key is xs:integer, so 9 sorts before 10 with no data-type.
        Assert.That(Run(ForEach("<xsl:sort select='xs:integer(.)'/>", "/d/n[number(.) != 2.5]"), Numbers), Is.EqualTo("-5 9 10 100 "));
        Assert.That(Run(ForEach("<xsl:sort select='number(.)'/>"), Numbers), Is.EqualTo("-5 2.5 9 10 100 "));
    }

    [Test]
    public void Sort_SelectDefaultsToTheContextItem_AndCanBeAnyExpression()
    {
        Assert.That(Run(ForEach("<xsl:sort select='string-length(.)'/><xsl:sort select='.'/>"), Words), Is.EqualTo("fig pear Apple apple banana "));
    }

    [Test]
    public void Sort_KeyFromContent_UsesTheSequenceConstructor()
    {
        Assert.That(Run(ForEach("<xsl:sort><xsl:value-of select='string-length(.)'/></xsl:sort><xsl:sort select='.'/>"), Words), Is.EqualTo("fig pear Apple apple banana "));
    }

    [Test]
    public void Sort_SeveralKeys_LaterKeysBreakTies()
    {
        const string xml = "<d><p a='2' b='x'/><p a='1' b='z'/><p a='2' b='a'/><p a='1' b='b'/></d>";

        Assert.That(
            Run(ForEach("<xsl:sort select='@a'/><xsl:sort select='@b' order='descending'/>", body: "<xsl:value-of select='concat(@a, @b)'/><xsl:text> </xsl:text>"), xml),
            Is.EqualTo("1z 1b 2x 2a "));
    }

    // ── Stability, empty keys, NaN ───────────────────────────────────────────

    [Test]
    public void Sort_IsStable_EqualKeysKeepTheirInputOrder()
    {
        // 10,000 items with only three distinct keys: within a key the ids must stay ascending.
        StringBuilder xml = new StringBuilder("<d>");
        for (int i = 0; i < 10000; i++)
        {
            xml.Append("<i k='").Append(i % 3).Append("' id='").Append(i).Append("'/>");
        }

        xml.Append("</d>");

        string output = Run(
            ForEach("<xsl:sort select='@k' data-type='number'/>", body: "<xsl:value-of select='@id'/><xsl:text> </xsl:text>"),
            xml.ToString());
        string[] ids = output.Trim().Split(' ');

        Assert.That(ids, Has.Length.EqualTo(10000));
        int lastKey = -1;
        int lastId = -1;
        foreach (string id in ids)
        {
            int n = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture);
            int key = n % 3;
            if (key == lastKey)
            {
                Assert.That(n, Is.GreaterThan(lastId), "items with an equal key must keep their input order");
            }
            else
            {
                Assert.That(key, Is.GreaterThan(lastKey));
            }

            lastKey = key;
            lastId = n;
        }
    }

    [Test]
    public void Sort_DescendingWithEqualKeys_StillKeepsInputOrder()
    {
        const string xml = "<d><p k='1' id='a'/><p k='2' id='b'/><p k='1' id='c'/><p k='2' id='d'/></d>";

        Assert.That(
            Run(ForEach("<xsl:sort select='@k' order='descending'/>", body: "<xsl:value-of select='@id'/>"), xml),
            Is.EqualTo("bdac"));
    }

    [Test]
    public void Sort_EmptyKeysAndNaN_SortFirstInAscendingOrder()
    {
        const string xml = "<d><p v='3'/><p/><p v='abc'/><p v='1'/></d>";

        Assert.That(
            Run(ForEach("<xsl:sort select='@v' data-type='number'/>", body: "<xsl:value-of select='concat(\"[\", @v, \"]\")'/>"), xml),
            Is.EqualTo("[][abc][1][3]"));
    }

    [Test]
    public void Sort_EmptyKeysSortLastInDescendingOrder()
    {
        const string xml = "<d><p v='3'/><p/><p v='1'/></d>";

        Assert.That(
            Run(ForEach("<xsl:sort select='@v' data-type='number' order='descending'/>", body: "<xsl:value-of select='concat(\"[\", @v, \"]\")'/>"), xml),
            Is.EqualTo("[3][1][]"));
    }

    // ── Focus and positions ──────────────────────────────────────────────────

    [Test]
    public void Sort_PositionAndLastInTheBody_FollowTheSortedOrder()
    {
        Assert.That(
            Run(ForEach("<xsl:sort select='.' order='descending'/>", select: "/d/n[. > 0]", body: "<xsl:value-of select='concat(position(), \"/\", last(), \":\", .)'/><xsl:text> </xsl:text>"), Numbers),
            Is.EqualTo("1/4:9 2/4:2.5 3/4:100 4/4:10 "));
    }

    [Test]
    public void Sort_KeyIsEvaluatedWithTheItemAsContext_AndPositionIsItsInputPosition()
    {
        // Sorting by minus the position reverses the sequence.
        Assert.That(
            Run(ForEach("<xsl:sort select='-position()' data-type='number'/>"), "<d><a>1</a><a>2</a><a>3</a></d>"),
            Is.EqualTo("3 2 1 "));
    }

    [Test]
    public void Sort_InApplyTemplates_OrdersTheMatchedNodes()
    {
        string xsl =
            "<xsl:template match='/'><xsl:apply-templates select='/d/w'><xsl:sort select='.'/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='w'><xsl:value-of select='concat(position(), \"=\", .)'/><xsl:text> </xsl:text></xsl:template>";

        Assert.That(Run(xsl, Words), Is.EqualTo("1=Apple 2=apple 3=banana 4=fig 5=pear "));
    }

    [Test]
    public void Sort_OfAtomicValuesFromAnExpression()
    {
        Assert.That(
            Run(ForEach("<xsl:sort select='.' data-type='number'/>", select: "(5, 3, 11, 1)"), "<d/>"),
            Is.EqualTo("1 3 5 11 "));
    }

    // ── Collation, language, case order ──────────────────────────────────────

    [Test]
    public void Sort_CodepointCollation_PutsSupplementaryCharactersAfterTheBmp()
    {
        // U+E000 is a single UTF-16 unit above the surrogates; as code points it is below U+10000.
        const string xml = "<d><w>\U00010000</w><w></w></d>";
        string xsl = ForEach(
            "<xsl:sort select='.' collation='http://www.w3.org/2005/xpath-functions/collation/codepoint'/>",
            body: "<xsl:value-of select='string-to-codepoints(.)'/><xsl:text> </xsl:text>");

        Assert.That(Run(xsl, xml), Is.EqualTo("57344 65536 "));
    }

    [Test]
    public void Sort_UnknownCollation_RaisesXtde1035()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(ForEach("<xsl:sort collation='http://unknown.example/collation'/>"), Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1035));
    }

    [Test]
    public void Sort_CaseOrder_UpperFirstAndLowerFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Run(ForEach("<xsl:sort lang='en' case-order='upper-first'/>"), Words), Is.EqualTo("Apple apple banana fig pear "));
            Assert.That(Run(ForEach("<xsl:sort lang='en' case-order='lower-first'/>"), Words), Is.EqualTo("apple Apple banana fig pear "));
        });
    }

    [Test]
    public void Sort_Language_ChangesTheOrderOfAccentedLetters()
    {
        // German sorts ä with a; Swedish sorts it after z.
        const string xml = "<d><w>z</w><w>ä</w><w>a</w></d>";

        Assert.Multiple(() =>
        {
            Assert.That(Run(ForEach("<xsl:sort lang='de'/>"), xml), Is.EqualTo("a ä z "));
            Assert.That(Run(ForEach("<xsl:sort lang='sv'/>"), xml), Is.EqualTo("a z ä "));
        });
    }

    // ── perform-sort ─────────────────────────────────────────────────────────

    [Test]
    public void PerformSort_WithSelect_ReturnsTheSortedSequence()
    {
        string xsl = "<xsl:template match='/'><xsl:value-of select=\"string-join(for $x in (xs:string('b'), 'a', 'c') return $x, '')\"/>" +
                     "<xsl:text>|</xsl:text><xsl:variable name='s'><xsl:perform-sort select='/d/w'><xsl:sort select='.'/></xsl:perform-sort></xsl:variable>" +
                     "<xsl:value-of select='$s/*' separator=','/></xsl:template>";

        Assert.That(Run(xsl, "<d><w>c</w><w>a</w><w>b</w></d>"), Does.StartWith("bac|"));
    }

    [Test]
    public void PerformSort_OfNodes_KeepsTheNodesInSortedOrder()
    {
        string xsl =
            "<xsl:template match='/' exclude-result-prefixes='xs'><out><xsl:perform-sort select='/d/w'><xsl:sort select='.' order='descending'/></xsl:perform-sort></out></xsl:template>";

        Assert.That(Run(xsl, "<d><w>b</w><w>a</w><w>c</w></d>"), Is.EqualTo("<out><w>c</w><w>b</w><w>a</w></out>"));
    }

    [Test]
    public void PerformSort_WithoutASortKey_RaisesXtse0010()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run("<xsl:template match='/'><xsl:perform-sort select='/d/w'/></xsl:template>", Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    [Test]
    public void Sort_KeyWithMoreThanOneItem_RaisesXtte1020()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(ForEach("<xsl:sort select='(1, 2)'/>"), Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE1020));
    }

    [Test]
    public void Sort_KeysThatCannotBeCompared_RaiseXtde1030()
    {
        const string xml = "<d><p/><p/></d>";
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Run(ForEach("<xsl:sort select='if (position() = 1) then 1 else \"x\"'/>", select: "/d/p"), xml))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1030));
    }

    [Test]
    public void Sort_SelectAndContent_RaisesXtse1015()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run(ForEach("<xsl:sort select='.'>x</xsl:sort>"), Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE1015));
    }

    [TestCase("order='sideways'")]
    [TestCase("data-type='other'")]
    [TestCase("case-order='middle'")]
    [TestCase("stable='maybe'")]
    [TestCase("lang='not a language'")]
    public void Sort_LiteralAttributeWithAnInvalidValue_RaisesXtse0020(string attribute)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run(ForEach($"<xsl:sort {attribute}/>"), Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0020));
    }

    [TestCase("order", "sideways")]
    [TestCase("data-type", "other")]
    [TestCase("case-order", "middle")]
    [TestCase("stable", "maybe")]
    [TestCase("lang", "'de'")]
    public void Sort_ComputedAttributeWithAnInvalidValue_RaisesXtde0030(string attribute, string value)
    {
        string xsl = $"<xsl:param name='v'>{value}</xsl:param>" + ForEach($"<xsl:sort {attribute}='{{$v}}'/>");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, Words))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0030));
    }

    [Test]
    public void Sort_ComputedAttributeWithAValidValue_IsAccepted()
    {
        string xsl = "<xsl:param name='o'>descending</xsl:param>" + ForEach("<xsl:sort order='{$o}'/>");

        Assert.That(Run(xsl, Words), Is.EqualTo("pear fig banana apple Apple "));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Run(string body, string input)
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
            "<xsl:output method='xml' omit-xml-declaration='yes'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
