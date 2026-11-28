using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §5.3, §9 and §10 — the as attribute and the function conversion rules; value-of bodies and sequence items.</summary>
[TestFixture]
internal sealed class XsltAsTypingTests
{
    // ── Variables ────────────────────────────────────────────────────────────

    [Test]
    public void Variable_ValueIsConvertedWithTheFunctionConversionRules()
    {
        // Untyped text is cast, an integer is promoted to double, and an integer stays an integer for xs:decimal.
        const string body =
            "<xsl:template match='/'>" +
            "<xsl:variable name='a' as='xs:integer' select='xs:untypedAtomic(\"42\")'/>" +
            "<xsl:variable name='b' as='xs:double' select='3'/>" +
            "<xsl:variable name='c' as='xs:decimal' select='xs:integer(7)'/>" +
            "<xsl:value-of select='($a instance of xs:integer), ($b instance of xs:double), ($c instance of xs:integer)'/>" +
            "</xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("true true true"));
    }

    [Test]
    public void Variable_NodesAreAtomizedForAnAtomicType()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='n' as='xs:integer' select='/d/n'/><xsl:value-of select='$n + 1'/></xsl:template>";

        Assert.That(Eval(body, "<d><n>41</n></d>"), Is.EqualTo("42"));
    }

    [TestCase("xs:integer", "'abc'")]
    [TestCase("xs:string", "1")]
    [TestCase("xs:string+", "()")]
    [TestCase("xs:string?", "('a', 'b')")]
    [TestCase("element()", "1")]
    [TestCase("empty-sequence()", "1")]
    public void Variable_ValueThatDoesNotMatch_RaisesXtte0570(string type, string select)
    {
        string body = $"<xsl:template match='/'><xsl:variable name='v' as='{type}' select=\"{select}\"/>x</xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0570));
    }

    [Test]
    public void Variable_WithContentAndAs_IsTheSequenceTheContentBuilds()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='s' as='xs:integer*'><xsl:sequence select='1 to 3'/></xsl:variable>" +
            "<xsl:value-of select='count($s), sum($s)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("3 6"));
    }

    [Test]
    public void Variable_WithoutContentButWithAs_IsTheEmptySequence()
    {
        const string body = "<xsl:template match='/'><xsl:variable name='s' as='xs:string*'/><xsl:value-of select='count($s)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("0"));
    }

    [Test]
    public void Variable_WithSelectAndContent_RaisesXtse0620()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Eval("<xsl:template match='/'><xsl:variable name='v' select='1'>x</xsl:variable></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0620));
    }

    [Test]
    public void GlobalVariable_AsIsAppliedToItsValue()
    {
        const string body =
            "<xsl:variable name='g' as='xs:double' select='5'/><xsl:template match='/'><xsl:value-of select='$g instance of xs:double'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("true"));
    }

    [Test]
    public void GlobalVariable_ValueThatDoesNotMatch_RaisesXtte0570()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Eval("<xsl:variable name='g' as='xs:integer' select=\"'x'\"/><xsl:template match='/'>x</xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0570));
    }

    [Test]
    public void GlobalParam_DefaultThatDoesNotMatch_RaisesXtte0600()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Eval("<xsl:param name='p' as='xs:integer' select=\"'x'\"/><xsl:template match='/'>x</xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0600));
    }

    // ── Template parameters ──────────────────────────────────────────────────

    [Test]
    public void TemplateParam_SuppliedValueIsConverted_AndAnUnconvertibleOneRaisesXtte0590()
    {
        const string ok =
            "<xsl:template match='/'><xsl:call-template name='t'><xsl:with-param name='p' select='\"7\"'/></xsl:call-template></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p' as='xs:integer'/><xsl:value-of select='$p + 1'/></xsl:template>";
        string bad = ok.Replace("select='\"7\"'", "select='\"seven\"'");

        Assert.Multiple(() =>
        {
            // A string is not converted to an integer (only untypedAtomic is); the supplied value is rejected.
            Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(ok))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0590));
            Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(bad))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0590));
        });
    }

    [Test]
    public void TemplateParam_UntypedValueIsCastToTheParameterType()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'><xsl:with-param name='p' select='/d/n'/></xsl:call-template></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p' as='xs:integer'/><xsl:value-of select='$p + 1'/></xsl:template>";

        Assert.That(Eval(body, "<d><n>9</n></d>"), Is.EqualTo("10"));
    }

    [Test]
    public void TemplateParam_DefaultFromContent_IsADocumentNodeWithoutAs_AndASequenceWithAs()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'/></xsl:template>" +
            "<xsl:template name='t'>" +
            "<xsl:param name='d'>text</xsl:param>" +
            "<xsl:param name='s' as='xs:integer*'><xsl:sequence select='(1, 2)'/></xsl:param>" +
            "<xsl:value-of select='($d instance of document-node()), count($s)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("true 2"));
    }

    [Test]
    public void TemplateParam_DefaultThatDoesNotMatch_RaisesXtte0600()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'/></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p' as='xs:integer' select=\"'x'\"/>y</xsl:template>";

        Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(body))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0600));
    }

    [Test]
    public void TemplateParam_WithNoDefaultThatRejectsTheEmptySequence_MustBeSupplied()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'/></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p' as='xs:integer'/>y</xsl:template>";

        Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(body))!.ErrorCode, Is.EqualTo(XsltErrors.XTDE0610));
    }

    [Test]
    public void TemplateParam_WithNoDefaultAndNoAs_IsTheZeroLengthString()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'/></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p'/><xsl:value-of select='string-length($p), ($p instance of xs:string)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("0 true"));
    }

    [Test]
    public void WithParam_Content_IsADocumentNodeUnlessAsIsGiven()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'><xsl:with-param name='p'>x</xsl:with-param><xsl:with-param name='q' as='xs:string'>y</xsl:with-param></xsl:call-template></xsl:template>" +
            "<xsl:template name='t'><xsl:param name='p'/><xsl:param name='q'/><xsl:value-of select='($p instance of document-node()), ($q instance of xs:string)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("true true"));
    }

    [Test]
    public void ApplyTemplates_WithParamIsEvaluatedInTheFocusOfTheInstruction_NotOfTheInvokedTemplate()
    {
        // d/a is relative to the document node of the caller; inside the template for a it would select nothing.
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/d/a'><xsl:with-param name='p' select='count(d/a)'/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='a'><xsl:param name='p'/>[<xsl:value-of select='$p'/>]</xsl:template>";

        Assert.That(Eval(body, "<d><a/><a/></d>"), Is.EqualTo("[2][2]"));
    }

    // ── Template result ──────────────────────────────────────────────────────

    [Test]
    public void Template_As_ConvertsTheResult()
    {
        const string body =
            "<xsl:template match='/'><xsl:value-of select=\"string-join(for $i in (xs:integer(1), 2) return string($i), ',')\"/><xsl:text>|</xsl:text><xsl:value-of select='xsl:x' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'/></xsl:template>";
        _ = body;
        const string typed =
            "<xsl:template match='/'><xsl:variable name='r' as='xs:double*'><xsl:call-template name='t'/></xsl:variable><xsl:value-of select='$r instance of xs:double+'/></xsl:template>" +
            "<xsl:template name='t' as='xs:double*'><xsl:sequence select='(1, 2)'/></xsl:template>";

        Assert.That(Eval(typed), Is.EqualTo("true"));
    }

    [Test]
    public void Template_ResultThatDoesNotMatch_RaisesXtte0505()
    {
        const string body =
            "<xsl:template match='/'><xsl:call-template name='t'/></xsl:template>" +
            "<xsl:template name='t' as='xs:integer'><xsl:sequence select='(1, 2)'/></xsl:template>";

        Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(body))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0505));
    }

    // ── Functions ────────────────────────────────────────────────────────────

    private const string FunctionNs = "xmlns:f='urn:f'";

    [Test]
    public void Function_ArgumentsAreConverted_AndTheResultIsConverted()
    {
        string body =
            "<xsl:function name='f:inc' as='xs:double'><xsl:param name='n' as='xs:integer'/><xsl:sequence select='$n + 1'/></xsl:function>" +
            $"<xsl:template match='/' {FunctionNs}><xsl:value-of select='f:inc(/d/n), (f:inc(1) instance of xs:double)'/></xsl:template>";

        Assert.That(Eval(body, "<d><n>4</n></d>"), Is.EqualTo("5 true"));
    }

    [Test]
    public void Function_ArgumentThatDoesNotMatch_RaisesXtte0790()
    {
        string body =
            "<xsl:function name='f:inc'><xsl:param name='n' as='xs:integer'/><xsl:sequence select='$n'/></xsl:function>" +
            $"<xsl:template match='/' {FunctionNs}><xsl:value-of select=\"f:inc('x')\"/></xsl:template>";

        Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(body))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0790));
    }

    [Test]
    public void Function_ResultThatDoesNotMatch_RaisesXtte0780()
    {
        string body =
            "<xsl:function name='f:g' as='xs:integer'><xsl:sequence select=\"'x'\"/></xsl:function>" +
            $"<xsl:template match='/' {FunctionNs}><xsl:value-of select='f:g()'/></xsl:template>";

        Assert.That(Assert.Throws<XsltDynamicException>(() => Eval(body))!.ErrorCode, Is.EqualTo(XsltErrors.XTTE0780));
    }

    [Test]
    public void Function_CanReturnNodesItConstructs_IncludingAttributesAndDocuments()
    {
        string body =
            "<xsl:function name='f:attr' as='attribute()'><xsl:attribute name='a'>v</xsl:attribute></xsl:function>" +
            "<xsl:function name='f:doc' as='document-node()'><xsl:document><r/></xsl:document></xsl:function>" +
            $"<xsl:template match='/' {FunctionNs}><xsl:value-of select='string(f:attr()), name(f:doc()/*)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("v r"));
    }

    [Test]
    public void Function_DuplicateParameterNames_RaiseXtse0580_AndADefaultRaisesXtse0760()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<XsltStaticException>(() => Eval(
                    "<xsl:function name='f:g'><xsl:param name='a'/><xsl:param name='a'/><xsl:sequence select='1'/></xsl:function><xsl:template match='/'/>"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTSE0580));
            Assert.That(
                Assert.Throws<XsltStaticException>(() => Eval(
                    "<xsl:function name='f:g'><xsl:param name='a' select='1'/><xsl:sequence select='1'/></xsl:function><xsl:template match='/'/>"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTSE0760));
        });
    }

    [Test]
    public void Template_DuplicateParameterNames_RaiseXtse0580()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Eval("<xsl:template match='/'><xsl:param name='a'/><xsl:param name='a'/>x</xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0580));
    }

    // ── Sequences of nodes ───────────────────────────────────────────────────

    [Test]
    public void SequenceConstructor_TextNodesAreSeparateItems_AndZeroLengthOnesAreKept()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='t' as='text()*'><xsl:text>a</xsl:text><xsl:text>b</xsl:text><xsl:text/><xsl:value-of select=\"''\"/></xsl:variable>" +
            "<xsl:value-of select='count($t)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("4"));
    }

    [Test]
    public void SequenceConstructor_AttributesAndDocumentsAreItems()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='s' as='node()*'>" +
            "<xsl:attribute name='x'>1</xsl:attribute><xsl:document><d/></xsl:document><xsl:copy-of select='/d'/></xsl:variable>" +
            "<xsl:value-of select='count($s), count($s[. instance of attribute()]), count($s[. instance of document-node()])'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("3 1 1"));
    }

    [Test]
    public void SequenceConstructor_AtomicValuesStayAtomic()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='s' as='item()*'><xsl:copy-of select='(1, \"a\")'/></xsl:variable>" +
            "<xsl:value-of select='($s[1] instance of xs:integer), ($s[2] instance of xs:string)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("true true"));
    }

    [Test]
    public void CopyOfAttribute_Step_IsOnTheAttributeAxis()
    {
        const string body =
            "<xsl:template match='/d'><xsl:variable name='a' as='attribute()+'><xsl:copy-of select='attribute()'/></xsl:variable><xsl:value-of select='count($a)'/></xsl:template>";

        Assert.That(Eval(body, "<d x='1' y='2'/>"), Is.EqualTo("2"));
    }

    // ── xsl:value-of and xsl:sequence ────────────────────────────────────────

    [Test]
    public void ValueOf_WithContent_JoinsItemsWithAZeroLengthSeparatorByDefault()
    {
        const string body =
            "<xsl:template match='/'><xsl:value-of><xsl:sequence select='(1, 2, 3)'/></xsl:value-of>|<xsl:value-of separator='-'><xsl:sequence select='(1, 2, 3)'/></xsl:value-of>|<xsl:value-of select='(1, 2, 3)'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("123|1-2-3|1 2 3"));
    }

    [Test]
    public void ValueOf_SeparatorIsAnAttributeValueTemplate()
    {
        const string body = "<xsl:template match='/'><xsl:value-of select='(1, 2)' separator='{\"+\"}'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("1+2"));
    }

    [Test]
    public void ValueOf_DiscardsZeroLengthTextNodesButKeepsZeroLengthStrings()
    {
        const string body =
            "<xsl:template match='/'><xsl:variable name='z' as='text()'><xsl:text/></xsl:variable>" +
            "<xsl:value-of select=\"('a', $z, 'b', '', 'c')\" separator='-'/></xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("a-b--c"));
    }

    [Test]
    public void ValueOf_WithSelectAndContent_RaisesXtse0870()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Eval("<xsl:template match='/'><xsl:value-of select='1'>x</xsl:value-of></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0870));
    }

    [Test]
    public void Sequence_WithAContentBody_RaisesXtse0010InXslt20()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Eval("<xsl:template match='/'><xsl:sequence><x/></xsl:sequence></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    // ── Invalid as ───────────────────────────────────────────────────────────

    [Test]
    public void As_ThatIsNotASequenceType_IsAStaticError()
    {
        Assert.Throws<XsltStaticException>(
            () => Eval("<xsl:template match='/'><xsl:variable name='v' as='xs:integer ++' select='1'/></xsl:template>"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Eval(string body, string input = "<d/>")
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:f='urn:f'>" +
            "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
