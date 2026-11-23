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
/// P3-09: xsl:sequence / xsl:copy / xsl:copy-of + xsl:text DOE warning.
/// </summary>
[TestFixture]
internal sealed class XsltCopyTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── xsl:sequence ────────────────────────────────────────────────────────

    [Test]
    public void Sequence_Atomic_EmitsStringValue()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:sequence select='42'/></xsl:template>");
        AssertOutput(xsl, "<doc/>", "42");
    }

    [Test]
    public void Sequence_Node_EmitsSubtree()
    {
        // xsl:sequence of a node forwards it to the receiver; the XmlWriter sink walks the
        // subtree so the emitted output is the full element.
        string xsl = Wrap("<xsl:template match='/'><xsl:sequence select='doc/a'/></xsl:template>");
        AssertOutput(xsl, "<doc><a><b/></a></doc>", "<a><b /></a>");
    }

    // ── xsl:copy (shallow) ─────────────────────────────────────────────────

    [Test]
    public void Copy_Element_ShallowWithBody()
    {
        // xsl:copy on an element emits a start/end element pair with the same name;
        // the body determines the content.
        string xsl = Wrap(
            "<xsl:template match='/doc'><xsl:copy>INSIDE</xsl:copy></xsl:template>");
        AssertOutput(xsl, "<doc/>", "<doc>INSIDE</doc>");
    }

    [Test]
    public void Copy_Element_WithoutChildren_DropsOriginalContent()
    {
        // The original <a><b/></a> content is NOT copied — xsl:copy is shallow. An empty
        // body produces an empty element.
        string xsl = Wrap(
            "<xsl:template match='doc'><xsl:copy/></xsl:template>");
        AssertOutput(xsl, "<doc><a/></doc>", "<doc />");
    }

    [Test]
    public void Copy_Attribute_EmitsAttributeEvent()
    {
        string xsl = Wrap(
            "<xsl:template match='/doc'>" +
            "<out><xsl:for-each select='@id'><xsl:copy/></xsl:for-each></out>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc id='x'/>", "<out id=\"x\" />");
    }

    // ── xsl:copy-of (deep) ─────────────────────────────────────────────────

    [Test]
    public void CopyOf_Element_EmitsStartEndPair()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:copy-of select='doc/a'/></xsl:template>");
        AssertOutput(xsl, "<doc><a/></doc>", "<a />");
    }

    [Test]
    public void CopyOf_Element_DeepCopiesSubtree()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:copy-of select='doc'/></xsl:template>");
        AssertOutput(xsl, "<doc><a><b>t</b></a></doc>", "<doc><a><b>t</b></a></doc>");
    }

    [Test]
    public void CopyOf_Atomic_EmitsStringValue()
    {
        string xsl = Wrap("<xsl:template match='/'><xsl:copy-of select='17'/></xsl:template>");
        AssertOutput(xsl, "<doc/>", "17");
    }

    // ── xsl:text @disable-output-escaping warning ──────────────────────────

    [Test]
    public void Text_DisableOutputEscaping_EmitsWarning()
    {
        // Arrange
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:text disable-output-escaping='yes'>hi</xsl:text></xsl:template>");

        // Act
        CompiledStylesheet compiled = Compile(xsl);

        // Assert — one warning carrying the DOE text.
        Assert.That(compiled.Warnings.Any(w => w.Message.Contains("disable-output-escaping")), Is.True);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static CompiledStylesheet Compile(string xsl)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        return XsltCompiler.Compile(asm);
    }

    private static void AssertOutput(string xsl, string input, string expected)
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

        XsltRunner.Transform(compiled, inputDoc, sink, pool);
        writer.Flush();

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
