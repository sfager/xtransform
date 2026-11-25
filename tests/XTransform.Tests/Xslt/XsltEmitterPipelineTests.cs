using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>P3-13: end-to-end XsltRunner → SequenceNormalizer → XmlEmitter.</summary>
[TestFixture]
internal sealed class XsltEmitterPipelineTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    [Test]
    public void XslSequence_OfAtomics_SerializesJoinedBySpaces()
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/'><xsl:sequence select=\"(1, 2, 'x')\"/></xsl:template>");

        // Act
        string output = Transform(xsl, "<doc/>", new XmlEmitterOptions { OmitXmlDeclaration = true });

        // Assert
        Assert.That(output, Is.EqualTo("1 2 x"));
    }

    [Test]
    public void CdataSectionElements_AppliedToLiteralResultElement()
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/'><code>a&lt;b</code></xsl:template>");
        XmlEmitterOptions options = new XmlEmitterOptions
        {
            OmitXmlDeclaration = true,
            CdataSectionElements = new[] { new QName(string.Empty, string.Empty, "code") },
        };

        // Act
        string output = Transform(xsl, "<doc/>", options);

        // Assert
        Assert.That(output, Is.EqualTo("<code><![CDATA[a<b]]></code>"));
    }

    [Test]
    public void Indent_AppliedToConstructedResult()
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/'><a><b/><c><d/></c></a></xsl:template>");

        // Act
        string output = Transform(xsl, "<doc/>", new XmlEmitterOptions { Indent = true, OmitXmlDeclaration = true });

        // Assert
        Assert.That(output, Is.EqualTo("<a>\n  <b />\n  <c>\n    <d />\n  </c>\n</a>"));
    }

    [Test]
    public void TopLevelAttribute_RaisesXtde0420()
    {
        // Arrange — xsl:attribute with no enclosing element targets the document node.
        string xsl = Wrap("<xsl:template match='/'><xsl:attribute name='a' select=\"'v'\"/></xsl:template>");

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => Transform(xsl, "<doc/>", new XmlEmitterOptions { OmitXmlDeclaration = true }))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0420));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static string Transform(string xsl, string input, XmlEmitterOptions options)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("s.xsl", xsl);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/s.xsl"), resolver);
        CompiledStylesheet compiled = XsltCompiler.Compile(asm);

        NamePool pool = new NamePool();
        using StringReader sr = new StringReader(input);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);
        XdmNode inputDoc = new XdmNode(NodeHandle.ForNode(tree, 0));

        using StringWriter sw = new StringWriter();
        using (XmlEmitter emitter = new XmlEmitter(sw, pool, options))
        {
            XsltRunner.Transform(compiled, inputDoc, emitter, pool);
        }

        return sw.ToString();
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
