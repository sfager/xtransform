using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Tests.Serialization;

/// <summary>P3-13: XSLT 2.0 §5.7.1 sequence-normalization rules.</summary>
[TestFixture]
internal sealed class SequenceNormalizerTests
{
    [Test]
    public void Append_AdjacentAtomics_JoinedBySingleSpace()
    {
        // Arrange
        Recorder sink = new Recorder();
        SequenceNormalizer n = new SequenceNormalizer(sink);

        // Act
        n.Append(XdmAtomicValue.Integer(1));
        n.Append(XdmAtomicValue.Integer(2));
        n.Append(XdmAtomicValue.String("x"));

        // Assert
        Assert.That(sink.Joined, Is.EqualTo("1 2 x"));
    }

    [Test]
    public void Append_AtomicTextAtomic_NoSpaceAcrossTextNode()
    {
        // Arrange
        Recorder sink = new Recorder();
        SequenceNormalizer n = new SequenceNormalizer(sink);

        // Act
        n.Append(XdmAtomicValue.String("a"));
        n.Text("-");
        n.Append(XdmAtomicValue.String("b"));

        // Assert
        Assert.That(sink.Joined, Is.EqualTo("a-b"));
    }

    [Test]
    public void Text_Empty_DroppedAndDoesNotCountAsContent()
    {
        // Arrange
        Recorder sink = new Recorder();
        SequenceNormalizer n = new SequenceNormalizer(sink);
        n.StartElement(1, SourceSpan.None);

        // Act — an attribute after an empty text node is still legal.
        n.Text(string.Empty);
        n.Attribute(2, "v", SourceSpan.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events, Does.Not.Contain("Text()"));
            Assert.That(sink.Events, Does.Contain("Attribute(2=v)"));
            Assert.That(n.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void Attribute_WithNoOpenElement_RaisesXtde0420()
    {
        // Arrange
        SequenceNormalizer n = new SequenceNormalizer(new Recorder());

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => n.Attribute(1, "v", SourceSpan.None))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0420));
    }

    [Test]
    public void Namespace_WithNoOpenElement_RaisesXtde0420()
    {
        // Arrange
        SequenceNormalizer n = new SequenceNormalizer(new Recorder());

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => n.Namespace("p", "urn:p"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0420));
    }

    [Test]
    public void Attribute_AfterChildElement_DroppedWithXtde0410Warning()
    {
        // Arrange
        Recorder sink = new Recorder();
        SequenceNormalizer n = new SequenceNormalizer(sink);
        n.StartElement(1, SourceSpan.None);
        n.StartElement(2, SourceSpan.None);
        n.EndElement();

        // Act
        n.Attribute(3, "late", SourceSpan.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sink.Events, Does.Not.Contain("Attribute(3=late)"));
            Assert.That(n.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(n.Diagnostics[0].Code, Is.EqualTo(XsltErrors.XTDE0410));
        });
    }

    [Test]
    public void Append_AttributeNodeItem_RoutedThroughPlacementRules()
    {
        // Arrange — an attribute node item, appended inside an open element.
        NamePool pool = new NamePool();
        TreeBuilder tb = new TreeBuilder(pool);
        tb.StartElement(pool.Allocate(string.Empty, "e"), SourceSpan.None);
        tb.Attribute(pool.Allocate(string.Empty, "id"), "7", SourceSpan.None);
        tb.EndElement();
        TinyTree tree = tb.Build().Handle.Tree;
        XdmNode attrNode = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        Recorder sink = new Recorder();
        SequenceNormalizer n = new SequenceNormalizer(sink, pool);
        n.StartElement(pool.Allocate(string.Empty, "out"), SourceSpan.None);

        // Act
        n.Append(attrNode);

        // Assert
        Assert.That(sink.Events, Has.Some.StartsWith("Attribute(").And.EndsWith("=7)"));
    }

    private sealed class Recorder : IResultReceiver
    {
        private readonly List<string> _events = new List<string>();
        private readonly System.Text.StringBuilder _text = new System.Text.StringBuilder();

        public IReadOnlyList<string> Events => _events;
        public string Joined => _text.ToString();

        public void StartDocument() => _events.Add("StartDocument");
        public void EndDocument() => _events.Add("EndDocument");
        public void StartElement(int nameCode, SourceSpan location) => _events.Add($"Start({nameCode})");
        public void Namespace(string prefix, string uri) => _events.Add($"Namespace({prefix}={uri})");
        public void Attribute(int nameCode, string value, SourceSpan location) => _events.Add($"Attribute({nameCode}={value})");
        public void EndElement() => _events.Add("End");

        public void Text(string text)
        {
            _events.Add($"Text({text})");
            _text.Append(text);
        }

        public void Comment(string text) => _events.Add($"Comment({text})");
        public void ProcessingInstruction(string target, string data) => _events.Add($"PI({target})");
        public void Append(XdmItem item) => _events.Add("Append");
    }
}
