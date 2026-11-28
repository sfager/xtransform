using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §10.1 tunnel parameters and call checks, and §6.5 mode="#all".</summary>
[TestFixture]
internal sealed class XsltTunnelAndModeTests
{
    // ── Tunnel parameters ───────────────────────────────────────────────────

    [Test]
    public void Tunnel_ValuePassesThroughAnIntermediateTemplate()
    {
        // Arrange
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='d'><xsl:with-param name='p' select=\"'deep'\" tunnel='yes'/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='d'><xsl:apply-templates select='e'/></xsl:template>" +
            "<xsl:template match='e'><xsl:param name='p' tunnel='yes'/><xsl:value-of select='$p'/></xsl:template>";

        // Act + Assert
        Assert.That(Eval(body, "<d><e/></d>"), Is.EqualTo("deep"));
    }

    [Test]
    public void Tunnel_DoesNotBindANonTunnelParameter()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='d'><xsl:with-param name='p' select=\"'x'\" tunnel='yes'/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='d'><xsl:param name='p' select=\"'default'\"/><xsl:value-of select='$p'/></xsl:template>";

        Assert.That(Eval(body, "<d/>"), Is.EqualTo("default"));
    }

    [Test]
    public void Tunnel_IsEmptyInsideAStylesheetFunction()
    {
        const string body =
            "<xsl:function name='f:get'><xsl:param name='n'/><xsl:apply-templates select='$n' mode='m'/></xsl:function>" +
            "<xsl:template match='/'><xsl:apply-templates select='d'><xsl:with-param name='p' select=\"'x'\" tunnel='yes'/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='d'><xsl:value-of select='f:get(.)'/></xsl:template>" +
            "<xsl:template match='d' mode='m'><xsl:param name='p' tunnel='yes' select=\"'none'\"/><xsl:value-of select='$p'/></xsl:template>";

        Assert.That(Eval(body, "<d/>"), Is.EqualTo("none"));
    }

    [Test]
    public void Tunnel_InvalidValue_RaisesXtse0020()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:param name='p' tunnel='maybe'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0020));
    }

    // ── Call checks ─────────────────────────────────────────────────────────

    [Test]
    public void CallTemplate_MissingRequiredParameter_RaisesXtse0690()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:call-template name='n'/></xsl:template>" +
            "<xsl:template name='n'><xsl:param name='p' required='yes'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0690));
    }

    [Test]
    public void CallTemplate_WithParamThatMatchesNoParameter_RaisesXtse0680()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:call-template name='n'><xsl:with-param name='q' select='1'/></xsl:call-template></xsl:template>" +
            "<xsl:template name='n'><xsl:param name='p'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0680));
    }

    [Test]
    public void CallTemplate_UnknownTemplate_RaisesXtse0650()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:call-template name='nope'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0650));
    }

    [Test]
    public void WithParam_RequiredAttribute_RaisesXtse0090()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:call-template name='n'><xsl:with-param name='p' select='1' required='yes'/></xsl:call-template></xsl:template>" +
            "<xsl:template name='n'><xsl:param name='p'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0090));
    }

    [Test]
    public void Param_RequiredWithSelect_RaisesXtse0010()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template match='/'><xsl:param name='p' select='1' required='yes'/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [Test]
    public void Template_NameInTheXslNamespace_RaisesXtse0080()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Eval(
            "<xsl:template name='xsl:t'/>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0080));
    }

    // ── Modes ───────────────────────────────────────────────────────────────

    [Test]
    public void Mode_All_AppliesInEveryMode()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='d' mode='m1'/>|<xsl:apply-templates select='d'/>|<xsl:apply-templates select='d' mode='other'/></xsl:template>" +
            "<xsl:template match='d' mode='m1' priority='2'>a</xsl:template>" +
            "<xsl:template match='d' mode='#all'>b</xsl:template>";

        Assert.That(Eval(body, "<d/>"), Is.EqualTo("a|b|b"));
    }

    [Test]
    public void Mode_UndeclaredInApplyTemplates_UsesTheBuiltInRule()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='d' mode='none'/></xsl:template>";

        Assert.That(Eval(body, "<d>text</d>"), Is.EqualTo("text"));
    }

    // ── Patterns: key(), id($var), current(), priorities, xpath-default-namespace ──

    [Test]
    public void Pattern_KeyWithVariableValue_MatchesTheKeyedNodes()
    {
        const string body =
            "<xsl:variable name='v' select=\"'b'\"/>" +
            "<xsl:key name='k' match='e' use='@id'/>" +
            "<xsl:template match='/'><xsl:apply-templates select='d/e'/></xsl:template>" +
            "<xsl:template match='e'>-</xsl:template>" +
            "<xsl:template match=\"key('k', $v)\">[<xsl:value-of select='@id'/>]</xsl:template>";

        Assert.That(Eval(body, "<d><e id='a'/><e id='b'/><e id='c'/></d>"), Is.EqualTo("-[b]-"));
    }

    [Test]
    public void Pattern_IdWithVariableValue_MatchesElementsWithThatId()
    {
        const string body =
            "<xsl:variable name='v' select=\"'x'\"/>" +
            "<xsl:template match='/'><xsl:apply-templates select='d/e'/></xsl:template>" +
            "<xsl:template match='e'>-</xsl:template>" +
            "<xsl:template match='id($v)'>[<xsl:value-of select='@id'/>]</xsl:template>";

        Assert.That(Eval(body, "<d><e id='w'/><e id='x'/></d>"), Is.EqualTo("-[x]"));
    }

    [Test]
    public void Pattern_CurrentInAnEarlierStep_IsTheNodeBeingMatched()
    {
        const string body =
            "<xsl:template match='*'><xsl:apply-templates/></xsl:template>" +
            "<xsl:template match='*[name() = name(current())]/*'>Y</xsl:template>";

        // The child <a> of <a> matches; the child <b> of <a> does not.
        Assert.That(Eval(body, "<a><a/><b>text</b></a>"), Is.EqualTo("Ytext"));
    }

    [Test]
    public void Pattern_SlashAlone_HasPriorityOfNodeTests()
    {
        const string body =
            "<xsl:template match='/' priority='-0.4'>high</xsl:template>" +
            "<xsl:template match='/'>low</xsl:template>";

        Assert.That(Eval(body), Is.EqualTo("high"));
    }

    [Test]
    public void XPathDefaultNamespace_AppliesToSelectAndPatterns()
    {
        const string body =
            "<xsl:template match='/' xpath-default-namespace='urn:p'><xsl:apply-templates select='d/e'/></xsl:template>" +
            "<xsl:template match='p:e' xmlns:p='urn:p'>found</xsl:template>";

        Assert.That(Eval(body, "<d xmlns='urn:p'><e/></d>"), Is.EqualTo("found"));
    }

    [Test]
    public void XPathDefaultNamespace_IsInheritedAndOverridden()
    {
        const string body =
            "<xsl:template match='/' xpath-default-namespace='urn:p'>" +
            "<xsl:value-of select='count(d)'/>" +
            "<xsl:for-each select='.' xpath-default-namespace=''><xsl:value-of select='count(d)'/></xsl:for-each>" +
            "</xsl:template>";

        Assert.That(Eval(body, "<d xmlns='urn:p'/>"), Is.EqualTo("10"));
    }

    [Test]
    public void ApplyTemplates_WithoutSelectOnAnAtomicContextItem_RaisesXtte0510()
    {
        const string body =
            "<xsl:template match='/'><xsl:for-each select='1 to 2'><xsl:apply-templates/></xsl:for-each></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTTE0510));
    }

    [TestCase("child::document-node()", "[other]")]
    [TestCase("document-node()", "[doc]")]
    public void Pattern_DocumentNodeTest_MatchesOnlyWithoutAnExplicitChildAxis(string pattern, string expected)
    {
        // Arrange (XSLT 2.0 §5.5.2; the document node is nobody's child)
        string body =
            $"<xsl:template match='{pattern}'>[doc]</xsl:template><xsl:template match='*'>[other]</xsl:template>";

        // Act
        string result = Eval(body);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Pattern_ChildAxisWithAttributeTest_NeverMatchesAnAttribute()
    {
        // Arrange (XPath 2.0 §3.2.1.1: only the abbreviated attribute() is on the attribute axis)
        const string body =
            "<xsl:template match='child::attribute()'>[wrong]</xsl:template>" +
            "<xsl:template match='attribute()'>[attr]</xsl:template>" +
            "<xsl:template match='/'><xsl:apply-templates select='d/@a'/></xsl:template>";

        // Act
        string result = Eval(body, "<d a='1'/>");

        // Assert
        Assert.That(result, Is.EqualTo("[attr]"));
    }

    [TestCase("<xsl:param name='x'><xsl:value-of select='$z'/></xsl:param><xsl:param name='z'>foo</xsl:param>")]
    [TestCase("<xsl:param name='x'><xsl:value-of select='f:a()'/></xsl:param><xsl:param name='z'>foo</xsl:param>")]
    [TestCase("<xsl:param name='y'><xsl:value-of select='f:a()'/></xsl:param><xsl:param name='x'><xsl:value-of select='$y'/></xsl:param><xsl:param name='z'>foo</xsl:param>")]
    [TestCase("<xsl:variable name='x'><xsl:value-of select='f:a()'/></xsl:variable><xsl:variable name='z'>foo</xsl:variable>")]
    public void GlobalParam_ContentBodyReferencingALaterDeclaration_IsEvaluated(string globals)
    {
        // Arrange (XSLT 2.0 §9.5: global declarations may be referenced before they are declared)
        string body =
            globals + "<xsl:function name='f:a'><xsl:value-of select='$z'/></xsl:function>" +
            "<xsl:template match='/'>[<xsl:value-of select='$x'/>]</xsl:template>";

        // Act
        string result = Eval(body);

        // Assert
        Assert.That(result, Is.EqualTo("[foo]"));
    }

    [Test]
    public void GlobalVariable_FunctionMentioningItWithoutEvaluatingIt_IsNotCircular()
    {
        // Arrange (a function's unused local variable that names the global being evaluated)
        const string body =
            "<xsl:variable name='x' select='f:a(1)'/>" +
            "<xsl:function name='f:a'><xsl:param name='p'/><xsl:variable name='b' select='$x'/><xsl:sequence select='$p + 2'/></xsl:function>" +
            "<xsl:template match='/'>[<xsl:value-of select='$x'/>]</xsl:template>";

        // Act
        string result = Eval(body);

        // Assert
        Assert.That(result, Is.EqualTo("[3]"));
    }

    [TestCase("count(//c/@*/descendant::node())", "0")]
    [TestCase("count(//c/@*/descendant-or-self::node())", "2")]
    [TestCase("count(//c/@a/descendant-or-self::attribute(b))", "0")]
    public void Axis_DescendantFromAnAttribute_HasNoDescendants(string expression, string expected)
    {
        // Arrange (XPath 2.0 §3.2.1.1: an attribute has no children; descendant-or-self is the node itself)
        string body = $"<xsl:template match='/'><xsl:value-of select='{expression}'/></xsl:template>";

        // Act
        string result = Eval(body, "<d><c a='1' b='2'/></d>");

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Mode_Current_InAStylesheetFunctionIsTheDefaultMode()
    {
        // Arrange (XSLT 2.0 §6.5, erratum XT.E19)
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='d' mode='m'/></xsl:template>" +
            "<xsl:template match='d' mode='m'><xsl:value-of select='f:go(e)'/>|<xsl:apply-templates select='e' mode='#current'/></xsl:template>" +
            "<xsl:template match='e' mode='m'>[m]</xsl:template>" +
            "<xsl:template match='e'>[default]</xsl:template>" +
            "<xsl:function name='f:go'><xsl:param name='n'/><xsl:apply-templates select='$n' mode='#current'/></xsl:function>";

        // Act
        string result = Eval(body, "<d><e/></d>");

        // Assert: the function sees the default mode; the template, outside the call, is still in mode m.
        Assert.That(result, Is.EqualTo("[default]|[m]"));
    }

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
