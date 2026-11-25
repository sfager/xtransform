// Test file: inline string[] tag lists are intentionally per-case
#pragma warning disable CA1861 // Avoid constant arrays as arguments

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for P3-08: xsl:variable (local), xsl:param binding, global variables with cycle
/// detection, and external parameter overrides.
/// </summary>
[TestFixture]
internal sealed class XsltVariablesTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── Local xsl:variable ──────────────────────────────────────────────────

    [Test]
    public void LocalVariable_SelectExpression_ReadableInSiblings()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:variable name='x' select='1 + 2'/>" +
            "<r><xsl:value-of select='$x'/></r>" +
            "</xsl:template>");

        // Act + Assert
        AssertOutput(xsl, "<doc/>", "<r>3</r>");
    }

    [Test]
    public void LocalVariable_ShadowedInInnerBlock_InnerWins()
    {
        // Arrange — inner xsl:if redeclares $x; the outer value is restored after the if.
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:variable name='x' select='1'/>" +
            "<xsl:if test='true()'>" +
            "<xsl:variable name='x' select='2'/>" +
            "<inner><xsl:value-of select='$x'/></inner>" +
            "</xsl:if>" +
            "<outer><xsl:value-of select='$x'/></outer>" +
            "</xsl:template>");

        // Act + Assert
        AssertOutput(xsl, "<doc/>", "<inner>2</inner><outer>1</outer>");
    }

    [Test]
    public void LocalVariable_UndeclaredReference_RaisesXpst0008AtCompileTime()
    {
        // Arrange — $unknown isn't declared anywhere.
        string xsl = Wrap(
            "<xsl:template match='/'><r><xsl:value-of select='$unknown'/></r></xsl:template>");

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;
        Assert.That(ex.Errors.Any(d => d.Code.LocalName == "XPST0008"), Is.True);
    }

    // ── Global xsl:variable ─────────────────────────────────────────────────

    [Test]
    public void GlobalVariable_UsableInTemplates()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:variable name='g' select='&quot;hello&quot;'/>" +
            "<xsl:template match='/'><r><xsl:value-of select='$g'/></r></xsl:template>");

        // Act + Assert
        AssertOutput(xsl, "<doc/>", "<r>hello</r>");
    }

    [Test]
    public void GlobalVariable_ReferencingAnotherGlobal_InAnyOrder()
    {
        // Arrange — $a is declared before $b; $a's value depends on $b via a forward reference.
        string xsl = Wrap(
            "<xsl:variable name='a' select='$b * 2'/>" +
            "<xsl:variable name='b' select='21'/>" +
            "<xsl:template match='/'><r><xsl:value-of select='$a'/></r></xsl:template>");

        // Act + Assert
        AssertOutput(xsl, "<doc/>", "<r>42</r>");
    }

    [Test]
    public void GlobalVariable_Cycle_RaisesXtde0640()
    {
        // Arrange — $a → $b → $a.
        string xsl = Wrap(
            "<xsl:variable name='a' select='$b'/>" +
            "<xsl:variable name='b' select='$a'/>" +
            "<xsl:template match='/'><r><xsl:value-of select='$a'/></r></xsl:template>");

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Transform(xsl, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0640));
    }

    // ── xsl:param: external override ────────────────────────────────────────

    [Test]
    public void ExternalParameter_OverridesDefaultSelect()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:param name='p' select='&quot;default&quot;'/>" +
            "<xsl:template match='/'><r><xsl:value-of select='$p'/></r></xsl:template>");

        QName pName = new QName(string.Empty, string.Empty, "p");
        Dictionary<QName, XdmSequence> overrides = new Dictionary<QName, XdmSequence>
        {
            [pName] = XdmSequence.Of(XdmAtomicValue.String("external")),
        };

        // Act + Assert
        AssertOutput(xsl, "<doc/>", "<r>external</r>", overrides);
    }

    [Test]
    public void RequiredParam_WithoutExternalValue_RaisesXtde0050()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:param name='p' required='yes'/>" +
            "<xsl:template match='/'><r><xsl:value-of select='$p'/></r></xsl:template>");

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Transform(xsl, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0050));
    }

    [Test]
    public void ExternalParameter_OnUnknownName_RaisesArgumentException()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:template match='/'><r/></xsl:template>");

        QName unknown = new QName(string.Empty, string.Empty, "nope");
        Dictionary<QName, XdmSequence> overrides = new Dictionary<QName, XdmSequence>
        {
            [unknown] = XdmSequence.Of(XdmAtomicValue.String("x")),
        };

        // Act + Assert
        Assert.That(
            () => Transform(xsl, "<doc/>", overrides),
            Throws.ArgumentException);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static CompiledStylesheet Compile(string xsl)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        return StylesheetCompiler.Compile(asm);
    }

    private static void Transform(string xsl, string input, IReadOnlyDictionary<QName, XdmSequence>? parameters = null)
    {
        CompiledStylesheet compiled = Compile(xsl);

        NamePool pool = new NamePool();
        using StringReader sr = new StringReader(input);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree inputTree = TinyTreeBuilder.Load(reader, pool);
        XdmNode inputDoc = new XdmNode(NodeHandle.ForNode(inputTree, 0));

        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        StringBuilder sb = new StringBuilder();
        using XmlWriter writer = XmlWriter.Create(sb, settings);
        using XmlWriterReceiver sink = new XmlWriterReceiver(writer, pool);

        XsltRunner.Transform(compiled, inputDoc, sink, pool, parameters);
    }

    private static void AssertOutput(string xsl, string input, string expected, IReadOnlyDictionary<QName, XdmSequence>? parameters = null)
    {
        // Arrange
        CompiledStylesheet compiled = Compile(xsl);

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
        XsltRunner.Transform(compiled, inputDoc, sink, pool, parameters);
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
