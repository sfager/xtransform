#pragma warning disable CA1861

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

/// <summary>P3-11: xsl:function / xsl:key + fn:key / fn:document / fn:doc / fn:doc-available.</summary>
[TestFixture]
internal sealed class XsltFunctionsAndKeysTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── xsl:function ───────────────────────────────────────────────────────

    [Test]
    public void UserFunction_Callable_ReturnsComputedValue()
    {
        string xsl = Wrap(
            "<xsl:function name='my:double' xmlns:my='urn:my'>" +
            "<xsl:param name='x'/>" +
            "<xsl:sequence select='$x * 2'/>" +
            "</xsl:function>" +
            "<xsl:template match='/' xmlns:my='urn:my'>" +
            "<r><xsl:value-of select='my:double(21)'/></r>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc/>", "<r>42</r>");
    }

    [Test]
    public void UserFunction_CanReferenceAnotherUserFunction()
    {
        string xsl = Wrap(
            "<xsl:function name='my:quad' xmlns:my='urn:my'>" +
            "<xsl:param name='x'/>" +
            "<xsl:sequence select='my:double(my:double($x))'/>" +
            "</xsl:function>" +
            "<xsl:function name='my:double' xmlns:my='urn:my'>" +
            "<xsl:param name='x'/>" +
            "<xsl:sequence select='$x * 2'/>" +
            "</xsl:function>" +
            "<xsl:template match='/' xmlns:my='urn:my'>" +
            "<r><xsl:value-of select='my:quad(5)'/></r>" +
            "</xsl:template>");
        AssertOutput(xsl, "<doc/>", "<r>20</r>");
    }

    // ── xsl:key / fn:key ───────────────────────────────────────────────────

    [Test]
    public void Key_ResolvesToMatchingNode()
    {
        string xsl = Wrap(
            "<xsl:key name='by-id' match='item' use='@id'/>" +
            "<xsl:template match='/'>" +
            "<r><xsl:value-of select='key(&quot;by-id&quot;, &quot;b&quot;)/text()'/></r>" +
            "</xsl:template>");
        AssertOutput(xsl,
            "<doc><item id='a'>A</item><item id='b'>B</item><item id='c'>C</item></doc>",
            "<r>B</r>");
    }

    [Test]
    public void Key_WithUnknownKey_ReturnsEmpty()
    {
        string xsl = Wrap(
            "<xsl:key name='by-id' match='item' use='@id'/>" +
            "<xsl:template match='/'>" +
            "<r><xsl:value-of select='count(key(&quot;by-id&quot;, &quot;nope&quot;))'/></r>" +
            "</xsl:template>");
        AssertOutput(xsl,
            "<doc><item id='a'/></doc>",
            "<r>0</r>");
    }

    // ── fn:document / fn:doc / fn:doc-available ─────────────────────────────

    [Test]
    public void Document_LoadsViaResolver()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<r><xsl:value-of select='document(&quot;file:///suite/other.xml&quot;)/root/text()'/></r>" +
            "</xsl:template>");

        TransformResult result = TransformWithResolver(
            xsl,
            "<doc/>",
            new Dictionary<string, string> { ["other.xml"] = "<root>HELLO</root>" });

        Assert.That(result.Output, Is.EqualTo("<r>HELLO</r>"));
    }

    [Test]
    public void DocAvailable_ReturnsTrueForLoadable()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<r><xsl:value-of select='doc-available(&quot;file:///suite/other.xml&quot;)'/></r>" +
            "</xsl:template>");

        TransformResult result = TransformWithResolver(
            xsl,
            "<doc/>",
            new Dictionary<string, string> { ["other.xml"] = "<root/>" });

        Assert.That(result.Output, Is.EqualTo("<r>true</r>"));
    }

    [Test]
    public void DocAvailable_ReturnsFalseForMissing_NeverThrows()
    {
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<r><xsl:value-of select='doc-available(&quot;file:///suite/missing.xml&quot;)'/></r>" +
            "</xsl:template>");

        TransformResult result = TransformWithResolver(xsl, "<doc/>", new Dictionary<string, string>());
        Assert.That(result.Output, Is.EqualTo("<r>false</r>"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private sealed class TransformResult { public string Output { get; set; } = string.Empty; }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static TransformResult TransformWithResolver(string xsl, string input, IReadOnlyDictionary<string, string> docs)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        foreach (KeyValuePair<string, string> kv in docs)
        {
            resolver.Add(kv.Key, kv.Value);
        }

        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        CompiledStylesheet compiled = XsltCompiler.Compile(asm);

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

        XsltRunner.Transform(compiled, inputDoc, sink, pool, parameters: null, resolver: resolver);
        writer.Flush();
        return new TransformResult { Output = sb.ToString() };
    }

    private static void AssertOutput(string xsl, string input, string expected)
    {
        Assert.That(TransformWithResolver(xsl, input, new Dictionary<string, string>()).Output, Is.EqualTo(expected));
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
