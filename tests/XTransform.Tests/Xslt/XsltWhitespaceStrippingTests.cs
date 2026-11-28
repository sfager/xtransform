using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §4.3 / §4.4 — xsl:strip-space and xsl:preserve-space on source documents.</summary>
[TestFixture]
internal sealed class XsltWhitespaceStrippingTests
{
    private const string Input =
        "<doc>\n <a> </a>\n <b>  x  </b>\n <c xml:space='preserve'> <d> </d> <e xml:space='default'> </e> </c>\n</doc>";

    private static string Count(string xpath) =>
        $"<xsl:template match='/'><xsl:value-of select=\"count({xpath})\"/></xsl:template>";

    [Test]
    public void NoDeclaration_WhitespaceIsPreserved()
    {
        // doc has 4 whitespace children, a 1, b 1, c 3, d 1 and e 1.
        Assert.That(Run(Count("//text()"), Input), Is.EqualTo("11"));
    }

    [Test]
    public void StripAll_RemovesWhitespaceOnlyTextNodes()
    {
        // Survivors: "  x  " in b, the 3 whitespace children of c (xml:space="preserve") and the one in d (inherited).
        Assert.That(Run("<xsl:strip-space elements='*'/>" + Count("//text()"), Input), Is.EqualTo("5"));
    }

    [Test]
    public void StripAll_KeepsTextThatIsNotWhitespaceOnly()
    {
        Assert.That(Run("<xsl:strip-space elements='*'/>" + Count("//b/text()"), Input), Is.EqualTo("1"));
    }

    [Test]
    public void XmlSpacePreserve_ProtectsTheElementAndItsDescendants()
    {
        // c (preserve) keeps its own whitespace children (3) and d inherits preserve (1 text node);
        // e resets to default so its text is stripped.
        const string xsl = "<xsl:strip-space elements='*'/>";

        Assert.Multiple(() =>
        {
            Assert.That(Run(xsl + Count("//c/text()"), Input), Is.EqualTo("3"));
            Assert.That(Run(xsl + Count("//d/text()"), Input), Is.EqualTo("1"));
            Assert.That(Run(xsl + Count("//e/text()"), Input), Is.EqualTo("0"));
        });
    }

    [Test]
    public void PreserveSpaceForANamedElement_BeatsStripAllWhateverTheOrder()
    {
        // A QName has priority 0, '*' has -0.5.
        Assert.Multiple(() =>
        {
            Assert.That(Run("<xsl:strip-space elements='*'/><xsl:preserve-space elements='a'/>" + Count("//a/text()"), Input), Is.EqualTo("1"));
            Assert.That(Run("<xsl:preserve-space elements='a'/><xsl:strip-space elements='*'/>" + Count("//a/text()"), Input), Is.EqualTo("1"));
        });
    }

    [Test]
    public void SeveralNameTestsInOneDeclaration_AreAllApplied()
    {
        Assert.That(Run("<xsl:strip-space elements='a  e'/>" + Count("//a/text() | //e/text() | //d/text()"), Input), Is.EqualTo("1"));
    }

    [Test]
    public void NameTestForms_NamespaceWildcardLocalWildcardAndEqName()
    {
        // Arrange
        const string doc = "<r xmlns:p='urn:p'><p:x> </p:x><p:y> </p:y><z> </z><w xmlns='urn:q'> </w></r>";
        string ns = "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:p='urn:p'><xsl:output method='text'/>";

        // Act / Assert
        Assert.Multiple(() =>
        {
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='p:*'/>", doc, "count(//text())"), Is.EqualTo("2"));
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='*:y'/>", doc, "count(//text())"), Is.EqualTo("3"));
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='p:x'/>", doc, "count(//text())"), Is.EqualTo("3"));
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='z'/>", doc, "count(//text())"), Is.EqualTo("3"));
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='Q{urn:q}w'/>", doc, "count(//text())"), Is.EqualTo("3"));
            Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='Q{urn:p}*'/>", doc, "count(//text())"), Is.EqualTo("2"));
        });
    }

    [Test]
    public void UnprefixedName_IsInNoNamespace()
    {
        // The default namespace does not apply to the name test: 'w' means no-namespace w, not urn:q's w.
        const string doc = "<r><w> </w><w xmlns='urn:q'> </w></r>";
        string ns = "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns='urn:q'><xsl:output method='text'/>";

        Assert.That(RunWithNamespaces(ns, "<xsl:strip-space elements='w'/>", doc, "count(//text())"), Is.EqualTo("1"));
    }

    [Test]
    public void Text_WrittenAsCdataOrCharacterReference_IsStrippedToo()
    {
        const string doc = "<r><a><![CDATA[ ]]></a><b>&#32;</b><c>&#9;x</c></r>";

        Assert.That(Run("<xsl:strip-space elements='*'/>" + Count("//text()"), doc), Is.EqualTo("1"));
    }

    [Test]
    public void StrippingDoesNotApplyToTemporaryTrees()
    {
        // The variable's content is a temporary tree: its whitespace-only text node stays.
        const string xsl =
            "<xsl:strip-space elements='*'/>" +
            "<xsl:template match='/'><xsl:variable name='v'><t><xsl:text> </xsl:text></t></xsl:variable>" +
            "<xsl:value-of select='count($v//text())'/></xsl:template>";

        Assert.That(Run(xsl, "<d/>"), Is.EqualTo("1"));
    }

    [Test]
    public void UndeclaredPrefixInANameTest_RaisesXtse0280()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run("<xsl:strip-space elements='nope:x'/>" + Count("//a"), Input))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0280));
    }

    [Test]
    public void MissingElementsAttribute_RaisesXtse0010()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run("<xsl:strip-space/>" + Count("//a"), Input))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [Test]
    public void ConflictingDeclarationsForTheSameName_RecoverByTakingTheLastAndWarn()
    {
        // Arrange — XTRE0270 is recoverable.
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:output method='text'/>" +
            "<xsl:strip-space elements='a'/><xsl:preserve-space elements='a'/>" + Count("//a/text()") + "</xsl:stylesheet>";
        XsltCompiler compiler = new XsltCompiler();

        // Act
        XsltExecutable exe = compiler.Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Input));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);

        // Assert — preserve-space came last, so a's whitespace is kept.
        Assert.Multiple(() =>
        {
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo("1"));
            Assert.That(compiler.Warnings.Select(w => w.Code), Does.Contain(XsltErrors.XTRE0270));
        });
    }

    private static string Run(string body, string input) =>
        RunWithNamespaces(
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'><xsl:output method='text'/>",
            body,
            input,
            null);

    // Runs a stylesheet whose opening tag is supplied (to declare namespaces); count is an XPath to count, or null
    // when the body already contains the template.
    private static string RunWithNamespaces(string open, string declarations, string input, string? countXPath)
    {
        string template = countXPath is null
            ? string.Empty
            : $"<xsl:template match='/'><xsl:value-of select=\"{countXPath}\"/></xsl:template>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(open + declarations + template + "</xsl:stylesheet>"));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
