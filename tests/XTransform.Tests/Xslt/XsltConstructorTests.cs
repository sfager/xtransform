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

/// <summary>P3-10: xsl:element / attribute / comment / processing-instruction / document.</summary>
[TestFixture]
internal sealed class XsltConstructorTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── xsl:element ─────────────────────────────────────────────────────────

    [Test]
    public void Element_StaticName_EmitsElement()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:element name='out'/></xsl:template>");
        AssertOutput(xsl, "<doc/>", "<out />");
    }

    [Test]
    public void Element_AvtName_UsesRuntimeValue()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:element name='{doc/@tag}'/></xsl:template>");
        AssertOutput(xsl, "<doc tag='dynamic'/>", "<dynamic />");
    }

    [Test]
    public void Element_WithExplicitNamespaceAvt_ProducesNamespacedElement()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:element name='n:x' namespace='urn:runtime' xmlns:n='urn:static'/>" +
            "</xsl:template>");
        // XmlWriter picks the shortest-serialisation prefix form; the element's name and
        // namespace are what matter, not the chosen prefix.
        AssertOutput(xsl, "<doc/>", "<x xmlns=\"urn:runtime\" />");
    }

    // ── xsl:attribute ───────────────────────────────────────────────────────

    [Test]
    public void Attribute_WithSelect_EmitsAttributeOnParent()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<out><xsl:attribute name='id' select='&quot;x&quot;'/></out>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc/>", "<out id=\"x\" />");
    }

    [Test]
    public void Attribute_WithBody_ConcatenatesText()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<out><xsl:attribute name='label'>pre-<xsl:value-of select='doc/a'/></xsl:attribute></out>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc><a>x</a></doc>", "<out label=\"pre-x\" />");
    }

    [Test]
    public void Attribute_AfterContent_IsDroppedWithXtde0410()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<out><child/><xsl:attribute name='late' select='&quot;val&quot;'/></out>" +
            "</xsl:template>");

        string output = Transform(xsl, "<doc/>", out IReadOnlyList<Diagnostic> diagnostics);
        Assert.Multiple(() =>
        {
            Assert.That(output, Is.EqualTo("<out><child /></out>"));
            Assert.That(diagnostics.Any(d => d.Code == XsltErrors.XTDE0410), Is.True);
        });
    }

    // ── xsl:comment ─────────────────────────────────────────────────────────

    [Test]
    public void Comment_WithBody_EmitsComment()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:comment>hello</xsl:comment></xsl:template>");
        AssertOutput(xsl, "<doc/>", "<!--hello-->");
    }

    [Test]
    public void Comment_ContainingDoubleHyphen_RaisesXtde0450()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:comment>bad -- content</xsl:comment></xsl:template>");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Transform(xsl, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0450));
    }

    [Test]
    public void Comment_EndingInHyphen_RaisesXtde0450()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:comment>oops-</xsl:comment></xsl:template>");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Transform(xsl, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0450));
    }

    // ── xsl:processing-instruction ──────────────────────────────────────────

    [Test]
    public void ProcessingInstruction_WithBody_EmitsPi()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:processing-instruction name='xml-stylesheet'>href=\"s.css\"</xsl:processing-instruction>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc/>", "<?xml-stylesheet href=\"s.css\"?>");
    }

    // ── xsl:document ────────────────────────────────────────────────────────

    [Test]
    public void Document_RunsBody()
    {
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:document><root/></xsl:document></xsl:template>");
        AssertOutput(xsl, "<doc/>", "<root />");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static CompiledStylesheet Compile(string xsl)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        return StylesheetCompiler.Compile(asm);
    }

    private static string Transform(string xsl, string input) => Transform(xsl, input, out _);

    private static string Transform(string xsl, string input, out IReadOnlyList<Diagnostic> diagnostics)
    {
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

        diagnostics = XsltRunner.Transform(compiled, inputDoc, sink, pool);
        writer.Flush();
        return sb.ToString();
    }

    private static void AssertOutput(string xsl, string input, string expected)
    {
        Assert.That(Transform(xsl, input), Is.EqualTo(expected));
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
