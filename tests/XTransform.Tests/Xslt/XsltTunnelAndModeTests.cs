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
