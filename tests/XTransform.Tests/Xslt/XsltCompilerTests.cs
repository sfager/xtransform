using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for <see cref="StylesheetCompiler"/> — P3-04 declaration collection and name binding.
/// </summary>
[TestFixture]
internal sealed class XsltCompilerTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── Duplicate detection ──────────────────────────────────────────────────

    [Test]
    public void Compile_DuplicateGlobalVariableSamePrecedence_RaisesXtse0630()
    {
        // Arrange
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:variable name='x' select='1'/>" +
            "<xsl:variable name='x' select='2'/>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0630), Is.True);
    }

    [Test]
    public void Compile_DuplicateGlobalVariableDifferentPrecedence_HigherWinsSilently()
    {
        // Arrange — b imports a (a has lower precedence). Both declare 'x'.
        // Primary b has higher precedence, so its 'x' wins silently.
        StylesheetAssembly asm = Link(
            ("b.xsl", Wrap("<xsl:import href='a.xsl'/><xsl:variable name='x' select='2'/>")),
            ("a.xsl", Wrap("<xsl:variable name='x' select='1'/>")));

        // Act
        CompiledStylesheet compiled = StylesheetCompiler.Compile(asm);

        // Assert — only one 'x', from the higher-precedence module.
        QName xName = new QName(string.Empty, string.Empty, "x");
        Assert.Multiple(() =>
        {
            Assert.That(compiled.Declarations.GlobalVariables.ContainsKey(xName), Is.True);
            Assert.That(compiled.Declarations.GlobalVariables[xName].SelectExpression, Is.EqualTo("2"));
        });
    }

    [Test]
    public void Compile_DuplicateNamedTemplateSamePrecedence_RaisesXtse0660()
    {
        // Arrange
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:template name='t'/>" +
            "<xsl:template name='t'/>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0660), Is.True);
    }

    [Test]
    public void Compile_FunctionWithoutPrefix_RaisesXtse0740()
    {
        // Arrange
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:function name='bad-name'/>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0740), Is.True);
    }

    // ── Reference checks ─────────────────────────────────────────────────────

    [Test]
    public void Compile_ApplyTemplatesReferencesUndeclaredMode_RaisesXtse0680()
    {
        // Arrange — the only xsl:template has no @mode, so mode 'summary' is undeclared.
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:template match='/'>" +
            "<xsl:apply-templates mode='summary'/>" +
            "</xsl:template>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0680), Is.True);
    }

    [Test]
    public void Compile_ApplyTemplatesUsesDeclaredMode_NoError()
    {
        // Arrange
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:template match='x' mode='summary'/>" +
            "<xsl:template match='/'>" +
            "<xsl:apply-templates mode='summary'/>" +
            "</xsl:template>")));

        // Act + Assert — no exception.
        Assert.That(() => StylesheetCompiler.Compile(asm), Throws.Nothing);
    }

    [Test]
    public void Compile_GlobalVariableReferencesUndeclaredVariable_RaisesXpst0008()
    {
        // Arrange — $missing has no declaration anywhere.
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:variable name='v' select='$missing'/>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.That(ex.Errors.Any(d => d.Code.LocalName == "XPST0008"), Is.True);
    }

    [Test]
    public void Compile_GlobalReferencesAnotherGlobal_NoError()
    {
        // Arrange — forward reference between globals compiles (runtime cycle check is P3-08).
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:variable name='a' select='$b'/>" +
            "<xsl:variable name='b' select='1'/>")));

        // Act + Assert
        Assert.That(() => StylesheetCompiler.Compile(asm), Throws.Nothing);
    }

    // ── Diagnostic aggregation ───────────────────────────────────────────────

    [Test]
    public void Compile_MultipleStaticErrors_CollectedIntoOneException()
    {
        // Arrange — duplicate variable + duplicate template + unprefixed function = 3 errors.
        StylesheetAssembly asm = Link(("a.xsl", Wrap(
            "<xsl:variable name='v' select='1'/>" +
            "<xsl:variable name='v' select='2'/>" +
            "<xsl:template name='t'/>" +
            "<xsl:template name='t'/>" +
            "<xsl:function name='noprefix'/>")));

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetCompiler.Compile(asm))!;
        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0630), Is.True);
            Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0660), Is.True);
            Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0740), Is.True);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static StylesheetAssembly Link(params (string Name, string Xml)[] modules)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        foreach ((string name, string xml) in modules)
        {
            resolver.Add(name, xml);
        }

        return StylesheetLinker.Link(new Uri("file:///suite/" + modules[0].Name), resolver);
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
