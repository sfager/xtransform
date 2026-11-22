using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
internal sealed class DocumentOrderComparisonTests
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

    // ── Same node ─────────────────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_SameHandle_ReturnsZero()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode a = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode b = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.That(a.CompareDocumentOrder(b), Is.EqualTo(0));
    }

    // ── Element vs child ──────────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_ElementBeforeChild_IsNegative()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><child/></root>");
        XdmNode root = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode child = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act
        int result = root.CompareDocumentOrder(child);

        // Assert
        Assert.That(result, Is.LessThan(0), "parent must come before child");
    }

    [Test]
    public void CompareDocumentOrder_ChildAfterElement_IsPositive()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><child/></root>");
        XdmNode root = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode child = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act
        int result = child.CompareDocumentOrder(root);

        // Assert
        Assert.That(result, Is.GreaterThan(0));
    }

    // ── Attribute vs element ──────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_AttributeAfterOwnerElement_IsPositive()
    {
        // Arrange — attribute comes after its owner element
        TinyTree tree = LoadXml("<root id=\"1\"/>");
        XdmNode el = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act
        int cmp = attr.CompareDocumentOrder(el);

        // Assert
        Assert.That(cmp, Is.GreaterThan(0), "attribute must come after its owner element");
    }

    [Test]
    public void CompareDocumentOrder_ElementBeforeOwnAttribute_IsNegative()
    {
        // Arrange
        TinyTree tree = LoadXml("<root id=\"1\"/>");
        XdmNode el = new XdmNode(NodeHandle.ForNode(tree, 1));
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act
        int cmp = el.CompareDocumentOrder(attr);

        // Assert
        Assert.That(cmp, Is.LessThan(0));
    }

    // ── Attribute vs element's child ──────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_AttributeBeforeOwnersFirstChild_IsNegative()
    {
        // Arrange — attribute before the element's first child (but after the element)
        TinyTree tree = LoadXml("<root id=\"1\"><child/></root>");
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));
        XdmNode child = new XdmNode(NodeHandle.ForNode(tree, 2));

        // Act
        int cmp = attr.CompareDocumentOrder(child);

        // Assert
        Assert.That(cmp, Is.LessThan(0), "attribute must come before owner's children");
    }

    // ── Namespace vs attribute ────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_NamespaceBeforeAttribute_IsNegative()
    {
        // Arrange — namespace node comes before attribute node
        TinyTree tree = LoadXml("<root xmlns:x=\"http://example.com\" id=\"1\"/>");
        XdmNode ns = new XdmNode(NodeHandle.ForNamespace(tree, 0));
        XdmNode attr = new XdmNode(NodeHandle.ForAttribute(tree, 0));

        // Act
        int cmp = ns.CompareDocumentOrder(attr);

        // Assert
        Assert.That(cmp, Is.LessThan(0), "namespace node must come before attribute node");
    }

    // ── Two attributes on same element ────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_TwoAttributesOnSameElement_CompareByIndex()
    {
        // Arrange
        TinyTree tree = LoadXml("<root a=\"1\" b=\"2\"/>");
        XdmNode attr0 = new XdmNode(NodeHandle.ForAttribute(tree, 0));
        XdmNode attr1 = new XdmNode(NodeHandle.ForAttribute(tree, 1));

        // Act
        int cmp = attr0.CompareDocumentOrder(attr1);

        // Assert
        Assert.That(cmp, Is.LessThan(0), "first attribute (index 0) comes before second (index 1)");
    }

    // ── Cross-tree ────────────────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_CrossTree_StableByTreeId()
    {
        // Arrange — two separate trees; tree IDs are monotonically increasing
        TinyTree tree1 = LoadXml("<a/>");
        TinyTree tree2 = LoadXml("<b/>");
        XdmNode n1 = new XdmNode(NodeHandle.ForNode(tree1, 1));
        XdmNode n2 = new XdmNode(NodeHandle.ForNode(tree2, 1));

        // Act
        int cmp1 = n1.CompareDocumentOrder(n2);
        int cmp2 = n2.CompareDocumentOrder(n1);

        // Assert — must be consistent and non-zero
        Assert.That(cmp1, Is.Not.EqualTo(0), "cross-tree comparison must be non-zero");
        Assert.That(System.Math.Sign(cmp1), Is.EqualTo(-System.Math.Sign(cmp2)), "must be antisymmetric");
    }

    // ── Null argument ─────────────────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange
        TinyTree tree = LoadXml("<root/>");
        XdmNode node = new XdmNode(NodeHandle.ForNode(tree, 1));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => node.CompareDocumentOrder(null!));
    }
}
