using System;
using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>
/// Tests for the P3-06 receiver chain: TreeBuilder round-trip, MultiplexReceiver fan-out,
/// SequenceNormalizer pass-through.
/// </summary>
[TestFixture]
internal sealed class ReceiverChainTests
{
    private static readonly string[] s_expectedMultiplexEvents = new[]
    {
        "StartDocument",
        "StartElement(1)",
        "Text(x)",
        "EndElement",
        "EndDocument",
    };

    private static readonly string[] s_expectedNormalizerEvents = new[]
    {
        "StartDocument",
        "Text(hello)",
        "EndDocument",
    };

    // ── TreeBuilder round-trip ───────────────────────────────────────────────

    [Test]
    public void TreeBuilder_RoundTrip_ProducesDeepEqualTree()
    {
        // Arrange — build a small tree via events.
        NamePool pool = new NamePool();
        TreeBuilder a = new TreeBuilder(pool);
        BuildSample(a, pool);
        TinyTree original = ((XdmNode)a.Build()).Handle.Tree;

        // Act — emit the first tree into a second builder.
        TreeBuilder b = new TreeBuilder(pool);
        TinyTreeEmitter.Emit(original, b);
        TinyTree copy = ((XdmNode)b.Build()).Handle.Tree;

        // Assert — same node count, same string value at the document node, same
        // attribute / namespace counts. That is our deep-equal proxy for P3-06.
        Assert.Multiple(() =>
        {
            Assert.That(copy.NodeCount, Is.EqualTo(original.NodeCount));
            Assert.That(copy.AttributeCount, Is.EqualTo(original.AttributeCount));
            Assert.That(copy.NamespaceCount, Is.EqualTo(original.NamespaceCount));
            Assert.That(copy.GetStringValue(0), Is.EqualTo(original.GetStringValue(0)));
        });
    }

    [Test]
    public void TreeBuilder_WithoutExplicitStartDocument_StillBuildsDocumentNode()
    {
        // Arrange
        NamePool pool = new NamePool();
        TreeBuilder tb = new TreeBuilder(pool);

        // Act — start with a Text event (no preceding StartDocument).
        tb.Text("abc");
        tb.EndDocument();
        XdmNode root = tb.Build();

        // Assert
        Assert.That(root.NodeKind, Is.EqualTo(XdmNodeKind.Document));
        Assert.That(root.Handle.Tree.NodeCount, Is.GreaterThanOrEqualTo(2));
    }

    // ── MultiplexReceiver ────────────────────────────────────────────────────

    [Test]
    public void Multiplex_ForwardsEveryEventToEverySinkInOrder()
    {
        // Arrange
        RecordingReceiver a = new RecordingReceiver();
        RecordingReceiver b = new RecordingReceiver();
        MultiplexReceiver mux = new MultiplexReceiver(a, b);

        // Act
        mux.StartDocument();
        mux.StartElement(nameCode: 1, SourceSpan.None);
        mux.Text("x");
        mux.EndElement();
        mux.EndDocument();

        // Assert — both sinks saw identical event streams.
        Assert.Multiple(() =>
        {
            Assert.That(a.Events, Is.EqualTo(b.Events));
            Assert.That(a.Events, Is.EqualTo(s_expectedMultiplexEvents));
        });
    }

    [Test]
    public void Multiplex_CloseIsIdempotent_DisposesEachSinkOnce()
    {
        // Arrange
        DisposableSink a = new DisposableSink();
        DisposableSink b = new DisposableSink();
        MultiplexReceiver mux = new MultiplexReceiver(a, b);

        // Act — close twice.
        mux.Close();
        mux.Close();

        // Assert — each sink disposed exactly once.
        Assert.Multiple(() =>
        {
            Assert.That(a.DisposeCount, Is.EqualTo(1));
            Assert.That(b.DisposeCount, Is.EqualTo(1));
        });
    }

    // ── SequenceNormalizer scaffold ──────────────────────────────────────────

    [Test]
    public void SequenceNormalizer_PassThrough_ForwardsEveryEvent()
    {
        // Arrange
        RecordingReceiver sink = new RecordingReceiver();
        SequenceNormalizer n = new SequenceNormalizer(sink);

        // Act
        n.StartDocument();
        n.Text("hello");
        n.EndDocument();

        // Assert
        Assert.That(sink.Events, Is.EqualTo(s_expectedNormalizerEvents));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void BuildSample(TreeBuilder sink, NamePool pool)
    {
        int rootName = pool.Allocate(string.Empty, "root");
        int childName = pool.Allocate(string.Empty, "child");
        int idAttr = pool.Allocate(string.Empty, "id");

        sink.StartDocument();
        sink.StartElement(rootName, SourceSpan.None);
        sink.Namespace("ns", "urn:example");
        sink.Attribute(idAttr, "42", SourceSpan.None);
        sink.StartElement(childName, SourceSpan.None);
        sink.Text("payload");
        sink.EndElement();
        sink.Comment(" a comment ");
        sink.ProcessingInstruction("target", "data");
        sink.EndElement();
        sink.EndDocument();
    }

    private sealed class RecordingReceiver : IResultReceiver
    {
        public List<string> Events { get; } = new List<string>();

        public void StartDocument() => Events.Add("StartDocument");
        public void EndDocument() => Events.Add("EndDocument");
        public void StartElement(int nameCode, SourceSpan location) => Events.Add($"StartElement({nameCode})");
        public void Namespace(string prefix, string uri) => Events.Add($"Namespace({prefix},{uri})");
        public void Attribute(int nameCode, string value, SourceSpan location) => Events.Add($"Attribute({nameCode},{value})");
        public void EndElement() => Events.Add("EndElement");
        public void Text(string text) => Events.Add($"Text({text})");
        public void Comment(string text) => Events.Add($"Comment({text})");
        public void ProcessingInstruction(string target, string data) => Events.Add($"PI({target},{data})");
        public void Append(XdmItem item) => Events.Add("Append");
    }

    private sealed class DisposableSink : IResultReceiver, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;

        public void StartDocument() { }
        public void EndDocument() { }
        public void StartElement(int nameCode, SourceSpan location) { }
        public void Namespace(string prefix, string uri) { }
        public void Attribute(int nameCode, string value, SourceSpan location) { }
        public void EndElement() { }
        public void Text(string text) { }
        public void Comment(string text) { }
        public void ProcessingInstruction(string target, string data) { }
        public void Append(XdmItem item) { }
    }
}
