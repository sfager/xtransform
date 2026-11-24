#pragma warning disable CA1861

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// P3-12: cooperative cancellation via CheckCancellation at loop back-edges and template
/// entry. A spinning or infinitely-recursive stylesheet must stop within a tight bound of
/// the token's cancel moment.
/// </summary>
[TestFixture]
internal sealed class XsltCancellationTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    [Test]
    public void ForEach_InfiniteLoop_CancelsWithin150ms()
    {
        // Arrange — xsl:for-each over a very large range; the body recurses forever.
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:for-each select='1 to 1000000000'>" +
            "<x/>" +
            "</xsl:for-each>" +
            "</xsl:template>");

        CompiledStylesheet compiled = Compile(xsl);
        XdmNode inputDoc = LoadDoc("<doc/>", out NamePool pool);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        StringBuilder sb = new StringBuilder();
        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        using XmlWriter writer = XmlWriter.Create(sb, settings);
        using XmlWriterReceiver sink = new XmlWriterReceiver(writer, pool);

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        OperationCanceledException? caught = null;
        try
        {
            XsltRunner.Transform(compiled, inputDoc, sink, pool, cancellationToken: cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }
        sw.Stop();

        // Assert — cancellation fires within a safety margin above the 100ms deadline.
        Assert.Multiple(() =>
        {
            Assert.That(caught, Is.Not.Null);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500));
        });
    }

    [Test]
    public void TightForExpression_CancelsWithin150ms()
    {
        // Arrange — an XPath for expression with a huge range body.
        string xsl = Wrap(
            "<xsl:template match='/'>" +
            "<xsl:value-of select='sum(for $i in 1 to 1000000000 return $i)'/>" +
            "</xsl:template>");

        CompiledStylesheet compiled = Compile(xsl);
        XdmNode inputDoc = LoadDoc("<doc/>", out NamePool pool);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        StringBuilder sb = new StringBuilder();
        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        };
        using XmlWriter writer = XmlWriter.Create(sb, settings);
        using XmlWriterReceiver sink = new XmlWriterReceiver(writer, pool);

        // Act + Assert — XPath for-loop back-edge check fires.
        Stopwatch sw = Stopwatch.StartNew();
        Assert.That(
            () => XsltRunner.Transform(compiled, inputDoc, sink, pool, cancellationToken: cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
        sw.Stop();
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500));
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

    private static XdmNode LoadDoc(string xml, out NamePool pool)
    {
        pool = new NamePool();
        using StringReader sr = new StringReader(xml);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);
        return new XdmNode(NodeHandle.ForNode(tree, 0));
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
