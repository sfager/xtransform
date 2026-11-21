using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
internal sealed class XdmNodeTests
{
    private static TinyTree LoadXml(string xml)
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
        return TinyTreeBuilder.Load(reader, pool);
    }

    // ── IsNode / IsAtomic ─────────────────────────────────────────────────────

    [Test]
    public void IsNode_Always_ReturnsTrue()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode node = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.That(node.IsNode, Is.True);
        Assert.That(node.IsAtomic, Is.False);
    }

    // ── NodeKind ──────────────────────────────────────────────────────────────

    [Test]
    public void NodeKind_DocumentNode_IsDocument()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");

        // Act
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));

        // Assert
        Assert.That(doc.NodeKind, Is.EqualTo(XdmNodeKind.Document));
    }

    [Test]
    public void NodeKind_ElementNode_IsElement()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");

        // Act
        XdmNode element = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Assert
        Assert.That(element.NodeKind, Is.EqualTo(XdmNodeKind.Element));
    }

    [Test]
    public void NodeKind_AttributeHandle_IsAttribute()
    {
        // Arrange
        TinyTree tree = LoadXml("<root id=\"1\"/>");

        // Act
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Assert
        Assert.That(attr.NodeKind, Is.EqualTo(XdmNodeKind.Attribute));
    }

    [Test]
    public void NodeKind_NamespaceHandle_IsNamespace()
    {
        // Arrange
        TinyTree tree = LoadXml("<root xmlns:x=\"http://example.com\"/>");

        // Act
        XdmNode ns = new XdmNode(NodeHandle.ForNamespace(tree, 0));

        // Assert
        Assert.That(ns.NodeKind, Is.EqualTo(XdmNodeKind.Namespace));
    }

    // ── NodeName ──────────────────────────────────────────────────────────────

    [Test]
    public void NodeName_DocumentNode_IsNull()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));

        // Act / Assert
        Assert.That(doc.NodeName, Is.Null);
    }

    [Test]
    public void NodeName_Element_ReturnsLocalName()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode el = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act
        QName? name = el.NodeName;

        // Assert
        Assert.That(name, Is.Not.Null);
        Assert.That(name!.Value.LocalName, Is.EqualTo("root"));
    }

    [Test]
    public void NodeName_Attribute_ReturnsLocalName()
    {
        // Arrange
        TinyTree tree = LoadXml("<root id=\"1\"/>");
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act
        QName? name = attr.NodeName;

        // Assert
        Assert.That(name, Is.Not.Null);
        Assert.That(name!.Value.LocalName, Is.EqualTo("id"));
    }

    [Test]
    public void NodeName_TextNode_IsNull()
    {
        // Arrange
        TinyTree tree = LoadXml("<root>hello</root>");
        // Index 2 should be a text node
        XdmNode text = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act / Assert
        Assert.That(text.NodeKind, Is.EqualTo(XdmNodeKind.Text));
        Assert.That(text.NodeName, Is.Null);
    }

    [Test]
    public void NodeName_Namespace_ReturnsPrefix()
    {
        // Arrange
        TinyTree tree = LoadXml("<root xmlns:x=\"http://example.com\"/>");
        XdmNode ns = new XdmNode(NodeHandle.ForNamespace(tree, 0));

        // Act
        QName? name = ns.NodeName;

        // Assert
        Assert.That(name, Is.Not.Null);
        Assert.That(name!.Value.LocalName, Is.EqualTo("x"));
    }

    // ── Parent ────────────────────────────────────────────────────────────────

    [Test]
    public void Parent_DocumentNode_IsNull()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));

        // Act / Assert
        Assert.That(doc.Parent, Is.Null);
    }

    [Test]
    public void Parent_ElementUnderDocument_IsDocument()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode el = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act
        XdmNode? parent = el.Parent;

        // Assert
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent!.NodeKind, Is.EqualTo(XdmNodeKind.Document));
    }

    [Test]
    public void Parent_Attribute_IsOwnerElement()
    {
        // Arrange
        TinyTree tree = LoadXml("<root id=\"1\"/>");
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act
        XdmNode? parent = attr.Parent;

        // Assert
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent!.NodeKind, Is.EqualTo(XdmNodeKind.Element));
        Assert.That(parent.NodeName!.Value.LocalName, Is.EqualTo("root"));
    }

    // ── GetStringValue ────────────────────────────────────────────────────────

    [Test]
    public void GetStringValue_TextNode_ReturnsContent()
    {
        // Arrange
        TinyTree tree = LoadXml("<root>hello</root>");
        // index 2 = text node
        XdmNode text = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act / Assert
        Assert.That(text.GetStringValue(), Is.EqualTo("hello"));
    }

    [Test]
    public void GetStringValue_Element_ReturnsConcatOfDescendantText()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><a>foo</a><b>bar</b></root>");
        XdmNode root = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.That(root.GetStringValue(), Is.EqualTo("foobar"));
    }

    [Test]
    public void GetStringValue_Attribute_ReturnsValue()
    {
        // Arrange
        TinyTree tree = LoadXml("<root id=\"42\"/>");
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act / Assert
        Assert.That(attr.GetStringValue(), Is.EqualTo("42"));
    }

    // ── GetTypedValue ─────────────────────────────────────────────────────────

    [Test]
    public void GetTypedValue_Element_ReturnsUntypedAtomic()
    {
        // Arrange
        TinyTree tree = LoadXml("<root>hello</root>");
        XdmNode el = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act
        XdmSequence seq = el.GetTypedValue();

        // Assert
        Assert.That(seq.Count, Is.EqualTo(1));
        XdmAtomicValue val = (XdmAtomicValue)seq[0];
        Assert.That(val.TypeCode, Is.EqualTo(XdmTypeCode.UntypedAtomic));
    }

    [Test]
    public void GetTypedValue_Comment_ReturnsString()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><!-- hello --></root>");
        // index 2 = comment
        XdmNode comment = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act
        Assert.That(comment.NodeKind, Is.EqualTo(XdmNodeKind.Comment));
        XdmSequence seq = comment.GetTypedValue();

        // Assert
        XdmAtomicValue val = (XdmAtomicValue)seq[0];
        Assert.That(val.TypeCode, Is.EqualTo(XdmTypeCode.String));
    }

    [Test]
    public void GetTypedValue_ProcessingInstruction_ReturnsString()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><?pi data?></root>");
        // index 2 = PI
        XdmNode pi = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act
        Assert.That(pi.NodeKind, Is.EqualTo(XdmNodeKind.ProcessingInstruction));
        XdmSequence seq = pi.GetTypedValue();

        // Assert
        XdmAtomicValue val = (XdmAtomicValue)seq[0];
        Assert.That(val.TypeCode, Is.EqualTo(XdmTypeCode.String));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Test]
    public void Equals_SameHandle_ReturnsTrue()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode a = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode b = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
        Assert.That(a != b, Is.False);
    }

    [Test]
    public void Equals_DifferentIndex_ReturnsFalse()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><child/></root>");
        XdmNode a = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode b = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a == b, Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void EqualityOperator_NullOperands_AreNullSafe()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode node = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.That(node == null, Is.False);
        Assert.That(null == node, Is.False);
        Assert.That((XdmNode?)null == (XdmNode?)null, Is.True);
    }
}
