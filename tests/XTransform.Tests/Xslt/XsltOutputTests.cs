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

/// <summary>P3-14: xsl:output collection / merging and output-method selection.</summary>
[TestFixture]
internal sealed class XsltOutputTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";
    private static readonly byte[] s_utf8Bom = { 0xEF, 0xBB, 0xBF };
    private static readonly string[] s_expectedCdata = { "a", "b", "c" };

    // ── Collection and merging ──────────────────────────────────────────────

    [Test]
    public void NoXslOutput_UsesDefaults()
    {
        XsltOutputSettings s = CompileSingle(Wrap("<xsl:template match='/'/>")).PrincipalOutput;

        Assert.Multiple(() =>
        {
            Assert.That(s.Method, Is.EqualTo("xml"));
            Assert.That(s.Encoding, Is.EqualTo("utf-8"));
            Assert.That(s.Indent, Is.False);
            Assert.That(s.OmitXmlDeclaration, Is.False);
        });
    }

    [Test]
    public void XslOutput_AttributesAreParsed()
    {
        XsltOutputSettings s = CompileSingle(Wrap(
            "<xsl:output method='text' encoding='utf-16' indent='yes' omit-xml-declaration='yes' " +
            "standalone='yes' doctype-system='a.dtd' byte-order-mark='no'/>" +
            "<xsl:template match='/'/>")).PrincipalOutput;

        Assert.Multiple(() =>
        {
            Assert.That(s.Method, Is.EqualTo("text"));
            Assert.That(s.Encoding, Is.EqualTo("utf-16"));
            Assert.That(s.Indent, Is.True);
            Assert.That(s.OmitXmlDeclaration, Is.True);
            Assert.That(s.Standalone, Is.True);
            Assert.That(s.DoctypeSystem, Is.EqualTo("a.dtd"));
            Assert.That(s.ByteOrderMark, Is.False);
        });
    }

    [Test]
    public void SameNameSamePrecedence_DifferentValues_RaisesXtse1560()
    {
        string xsl = Wrap(
            "<xsl:output method='xml'/><xsl:output method='text'/>" +
            "<xsl:template match='/'/>");

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => CompileSingle(xsl))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE1560), Is.True);
    }

    [Test]
    public void SameNameSamePrecedence_SameValues_Merges()
    {
        XsltOutputSettings s = CompileSingle(Wrap(
            "<xsl:output method='text'/><xsl:output method='text' encoding='utf-16'/>" +
            "<xsl:template match='/'/>")).PrincipalOutput;

        Assert.Multiple(() =>
        {
            Assert.That(s.Method, Is.EqualTo("text"));
            Assert.That(s.Encoding, Is.EqualTo("utf-16"));
        });
    }

    [Test]
    public void HigherPrecedence_OverridesImportedValue()
    {
        // Arrange — b.xsl is imported (lower precedence) and says method=text; a.xsl says xml.
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("a.xsl", Wrap("<xsl:import href='b.xsl'/><xsl:output method='xml'/><xsl:template match='/'/>"));
        resolver.Add("b.xsl", Wrap("<xsl:output method='text' indent='yes'/>"));

        // Act
        XsltOutputSettings s = Compile(resolver, "a.xsl").PrincipalOutput;

        // Assert — method comes from the importing module; indent (unset there) survives from the import.
        Assert.Multiple(() =>
        {
            Assert.That(s.Method, Is.EqualTo("xml"));
            Assert.That(s.Indent, Is.True);
        });
    }

    [Test]
    public void CdataSectionElements_AccumulateAcrossDeclarations()
    {
        XsltOutputSettings s = CompileSingle(Wrap(
            "<xsl:output cdata-section-elements='a'/><xsl:output cdata-section-elements='b c'/>" +
            "<xsl:template match='/'/>")).PrincipalOutput;

        Assert.That(s.CdataSectionElements.Select(q => q.LocalName), Is.EquivalentTo(s_expectedCdata));
    }

    [Test]
    public void NamedOutput_IsKeptSeparatelyFromPrincipal()
    {
        CompiledStylesheet c = CompileSingle(Wrap(
            "<xsl:output method='xml'/><xsl:output name='alt' method='text'/>" +
            "<xsl:template match='/'/>"));

        QName alt = new QName(string.Empty, string.Empty, "alt");
        Assert.Multiple(() =>
        {
            Assert.That(c.PrincipalOutput.Method, Is.EqualTo("xml"));
            Assert.That(c.Outputs[alt].Method, Is.EqualTo("text"));
        });
    }

    [Test]
    public void InvalidYesNoValue_RaisesXtse0020()
    {
        string xsl = Wrap("<xsl:output indent='maybe'/><xsl:template match='/'/>");

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => CompileSingle(xsl))!;
        Assert.That(ex.Errors.Any(d => d.Code == XsltErrors.XTSE0020), Is.True);
    }

    // ── Method selection (end to end) ───────────────────────────────────────

    [Test]
    public void MethodText_EmitsOnlyText_AndNoBomForUtf8()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:output method='text' encoding='utf-8'/>" +
            "<xsl:template match='/'><out id='x'><xsl:comment>c</xsl:comment>hello</out></xsl:template>");

        // Act
        byte[] bytes = Run(xsl, "<doc/>");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("hello"));
            Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(s_utf8Bom));
        });
    }

    [Test]
    public void MethodText_WithByteOrderMarkYes_WritesBom()
    {
        string xsl = Wrap(
            "<xsl:output method='text' byte-order-mark='yes'/>" +
            "<xsl:template match='/'>hi</xsl:template>");

        byte[] bytes = Run(xsl, "<doc/>");

        Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(s_utf8Bom));
    }

    [Test]
    public void MethodXml_UsesXmlEmitter()
    {
        string xsl = Wrap(
            "<xsl:output method='xml' omit-xml-declaration='yes'/>" +
            "<xsl:template match='/'><a>t</a></xsl:template>");

        Assert.That(Encoding.UTF8.GetString(Run(xsl, "<doc/>")), Is.EqualTo("<a>t</a>"));
    }

    [Test]
    public void MethodHtml_IsNotSupportedYet()
    {
        string xsl = Wrap("<xsl:output method='html'/><xsl:template match='/'/>");

        Assert.That(() => Run(xsl, "<doc/>"), Throws.InstanceOf<NotSupportedException>());
    }

    [Test]
    public void UnsupportedEncoding_RaisesSesu0007()
    {
        string xsl = Wrap("<xsl:output encoding='no-such-encoding'/><xsl:template match='/'/>");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(xsl, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.SESU0007));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static CompiledStylesheet CompileSingle(string xsl)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        return Compile(resolver, "s.xsl");
    }

    private static CompiledStylesheet Compile(InMemoryResolver resolver, string primary)
    {
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/" + primary), resolver);
        return StylesheetCompiler.Compile(asm);
    }

    private static byte[] Run(string xsl, string input)
    {
        CompiledStylesheet compiled = CompileSingle(xsl);

        NamePool pool = new NamePool();
        using StringReader sr = new StringReader(input);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);
        XdmNode inputDoc = new XdmNode(NodeHandle.ForNode(tree, 0));

        using MemoryStream ms = new MemoryStream();
        IResultReceiver emitter = OutputEmitterFactory.Create(compiled.PrincipalOutput, ms, pool);
        using ((IDisposable)emitter)
        {
            XsltRunner.Transform(compiled, inputDoc, emitter, pool);
        }

        return ms.ToArray();
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
