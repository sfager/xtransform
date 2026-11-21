using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>
/// Tests for <see cref="ItemType"/> matches and subtype relationships.
/// XPath 2.0 §2.5.4 — matching; §2.5.5 — subtype rules.
/// </summary>
[TestFixture]
internal sealed class ItemTypeTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

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

    /// <summary>Returns the document node of a parsed XML string.</summary>
    private static XdmNode DocNode(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForNode(tree, 0));
    }

    /// <summary>Returns the root element (index 1) of a parsed XML string.</summary>
    private static XdmNode RootElement(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForNode(tree, 1));
    }

    /// <summary>Returns the first attribute (index 0) of the root element.</summary>
    private static XdmNode RootAttribute(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForAttribute(tree, 0));
    }

    // ── AnyItem.Matches ────────────────────────────────────────────────────────

    [Test]
    public void AnyItem_Matches_AtomicValue_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue atomic = XdmAtomicValue.Integer(5);

        // Act
        bool result = ItemType.AnyItem.Matches(atomic);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void AnyItem_Matches_Node_ReturnsTrue()
    {
        // Arrange
        XdmNode node = RootElement("<x/>");

        // Act
        bool result = ItemType.AnyItem.Matches(node);

        // Assert
        Assert.That(result, Is.True);
    }

    // ── AnyNode.Matches ────────────────────────────────────────────────────────

    [Test]
    public void AnyNode_Matches_Node_ReturnsTrue()
    {
        // Arrange
        XdmNode node = RootElement("<x/>");

        // Act
        bool result = ItemType.AnyNode.Matches(node);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void AnyNode_Matches_AtomicValue_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue atomic = XdmAtomicValue.Integer(42);

        // Act
        bool result = ItemType.AnyNode.Matches(atomic);

        // Assert
        Assert.That(result, Is.False);
    }

    // ── Atomic.Matches ─────────────────────────────────────────────────────────

    [Test]
    public void Atomic_Integer_Matches_IntegerValue_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue val = XdmAtomicValue.Integer(5);

        // Act
        bool result = ItemType.Atomic(XdmTypeCode.Integer).Matches(val);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Atomic_Decimal_Matches_IntegerValue_ReturnsTrue()
    {
        // Arrange — xs:integer is a subtype of xs:decimal
        XdmAtomicValue val = XdmAtomicValue.Integer(5);

        // Act
        bool result = ItemType.Atomic(XdmTypeCode.Decimal).Matches(val);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Atomic_String_Matches_IntegerValue_ReturnsFalse()
    {
        // Arrange — xs:integer is not a subtype of xs:string
        XdmAtomicValue val = XdmAtomicValue.Integer(5);

        // Act
        bool result = ItemType.Atomic(XdmTypeCode.String).Matches(val);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Atomic_Integer_Matches_Node_ReturnsFalse()
    {
        // Arrange
        XdmNode node = RootElement("<x/>");

        // Act
        bool result = ItemType.Atomic(XdmTypeCode.Integer).Matches(node);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Atomic_AnyAtomicType_Matches_AnyAtomicValue_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue val = XdmAtomicValue.Boolean(true);

        // Act
        bool result = ItemType.Atomic(XdmTypeCode.AnyAtomicType).Matches(val);

        // Assert
        Assert.That(result, Is.True);
    }

    // ── Kind.Matches ───────────────────────────────────────────────────────────

    [Test]
    public void KindElement_Matches_ElementNode_ReturnsTrue()
    {
        // Arrange
        XdmNode elem = RootElement("<root/>");

        // Act
        bool result = ItemType.Kind(XdmNodeKind.Element).Matches(elem);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void KindElement_Matches_AttributeNode_ReturnsFalse()
    {
        // Arrange
        XdmNode attr = RootAttribute("<root id=\"1\"/>");

        // Act
        bool result = ItemType.Kind(XdmNodeKind.Element).Matches(attr);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void KindElement_WithName_Matches_MatchingElement_ReturnsTrue()
    {
        // Arrange
        XdmNode elem = RootElement("<root/>");
        ItemType it = ItemType.Kind(XdmNodeKind.Element, new QName("root"));

        // Act
        bool result = it.Matches(elem);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void KindElement_WithName_Matches_DifferentName_ReturnsFalse()
    {
        // Arrange
        XdmNode elem = RootElement("<root/>");
        ItemType it = ItemType.Kind(XdmNodeKind.Element, new QName("other"));

        // Act
        bool result = it.Matches(elem);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void KindAttribute_Matches_AttributeNode_ReturnsTrue()
    {
        // Arrange
        XdmNode attr = RootAttribute("<root id=\"1\"/>");

        // Act
        bool result = ItemType.Kind(XdmNodeKind.Attribute).Matches(attr);

        // Assert
        Assert.That(result, Is.True);
    }

    // ── DocumentNode.Matches ───────────────────────────────────────────────────

    [Test]
    public void DocumentNode_NoConstraint_Matches_DocumentNode_ReturnsTrue()
    {
        // Arrange
        XdmNode doc = DocNode("<root/>");

        // Act
        bool result = ItemType.DocumentNode().Matches(doc);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void DocumentNode_NoConstraint_Matches_ElementNode_ReturnsFalse()
    {
        // Arrange
        XdmNode elem = RootElement("<root/>");

        // Act
        bool result = ItemType.DocumentNode().Matches(elem);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void DocumentNode_WithMatchingElementContent_Matches_DocumentWithMatchingChild_ReturnsTrue()
    {
        // Arrange
        XdmNode doc = DocNode("<x/>");
        ItemType it = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));

        // Act
        bool result = it.Matches(doc);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void DocumentNode_WithElementContent_Matches_DocumentWithWrongChild_ReturnsFalse()
    {
        // Arrange
        XdmNode doc = DocNode("<y/>");
        ItemType it = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));

        // Act
        bool result = it.Matches(doc);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void DocumentNode_WithAnyElement_Matches_DocumentWithElement_ReturnsTrue()
    {
        // Arrange
        XdmNode doc = DocNode("<anything/>");
        ItemType it = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element));

        // Act
        bool result = it.Matches(doc);

        // Assert
        Assert.That(result, Is.True);
    }

    // ── IsSubtypeOf ────────────────────────────────────────────────────────────

    // Reflexivity
    [Test]
    public void AnyItem_IsSubtypeOf_AnyItem_ReturnsTrue()
    {
        // Arrange / Act / Assert
        Assert.That(ItemType.AnyItem.IsSubtypeOf(ItemType.AnyItem), Is.True);
    }

    [Test]
    public void AnyNode_IsSubtypeOf_AnyNode_ReturnsTrue()
    {
        Assert.That(ItemType.AnyNode.IsSubtypeOf(ItemType.AnyNode), Is.True);
    }

    // Every type is subtype of AnyItem
    [Test]
    public void AnyNode_IsSubtypeOf_AnyItem_ReturnsTrue()
    {
        Assert.That(ItemType.AnyNode.IsSubtypeOf(ItemType.AnyItem), Is.True);
    }

    [Test]
    public void Atomic_Integer_IsSubtypeOf_AnyItem_ReturnsTrue()
    {
        Assert.That(ItemType.Atomic(XdmTypeCode.Integer).IsSubtypeOf(ItemType.AnyItem), Is.True);
    }

    [Test]
    public void KindElement_IsSubtypeOf_AnyItem_ReturnsTrue()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Element).IsSubtypeOf(ItemType.AnyItem), Is.True);
    }

    // Node kinds are subtypes of AnyNode
    [Test]
    public void KindElement_IsSubtypeOf_AnyNode_ReturnsTrue()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Element).IsSubtypeOf(ItemType.AnyNode), Is.True);
    }

    [Test]
    public void KindAttribute_IsSubtypeOf_AnyNode_ReturnsTrue()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Attribute).IsSubtypeOf(ItemType.AnyNode), Is.True);
    }

    // AnyItem is NOT subtype of AnyNode (direction matters)
    [Test]
    public void AnyItem_IsSubtypeOf_AnyNode_ReturnsFalse()
    {
        Assert.That(ItemType.AnyItem.IsSubtypeOf(ItemType.AnyNode), Is.False);
    }

    // Named kind subtype of unnamed kind (same kind)
    [Test]
    public void KindElement_Named_IsSubtypeOf_KindElement_Unnamed_ReturnsTrue()
    {
        // Arrange
        ItemType named = ItemType.Kind(XdmNodeKind.Element, new QName("x"));
        ItemType unnamed = ItemType.Kind(XdmNodeKind.Element);

        // Act / Assert
        Assert.That(named.IsSubtypeOf(unnamed), Is.True);
    }

    // Unnamed kind is NOT subtype of named kind
    [Test]
    public void KindElement_Unnamed_IsSubtypeOf_KindElement_Named_ReturnsFalse()
    {
        // Arrange
        ItemType named = ItemType.Kind(XdmNodeKind.Element, new QName("x"));
        ItemType unnamed = ItemType.Kind(XdmNodeKind.Element);

        // Act / Assert
        Assert.That(unnamed.IsSubtypeOf(named), Is.False);
    }

    // Cross-kind: element is NOT subtype of attribute
    [Test]
    public void KindElement_IsSubtypeOf_KindAttribute_ReturnsFalse()
    {
        Assert.That(
            ItemType.Kind(XdmNodeKind.Element).IsSubtypeOf(ItemType.Kind(XdmNodeKind.Attribute)),
            Is.False);
    }

    // Atomic type subtyping via CastTable
    [Test]
    public void Atomic_Integer_IsSubtypeOf_Decimal_ReturnsTrue()
    {
        Assert.That(
            ItemType.Atomic(XdmTypeCode.Integer).IsSubtypeOf(ItemType.Atomic(XdmTypeCode.Decimal)),
            Is.True);
    }

    [Test]
    public void Atomic_Integer_IsSubtypeOf_String_ReturnsFalse()
    {
        Assert.That(
            ItemType.Atomic(XdmTypeCode.Integer).IsSubtypeOf(ItemType.Atomic(XdmTypeCode.String)),
            Is.False);
    }

    // Atomic is NOT subtype of node
    [Test]
    public void Atomic_Integer_IsSubtypeOf_AnyNode_ReturnsFalse()
    {
        Assert.That(ItemType.Atomic(XdmTypeCode.Integer).IsSubtypeOf(ItemType.AnyNode), Is.False);
    }

    // DocumentElementItemType subtyping
    [Test]
    public void DocumentNode_WithElement_IsSubtypeOf_DocumentNode_NoConstraint_ReturnsTrue()
    {
        // Arrange
        ItemType constrained = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));
        ItemType unconstrained = ItemType.DocumentNode();

        // Act / Assert
        Assert.That(constrained.IsSubtypeOf(unconstrained), Is.True);
    }

    [Test]
    public void DocumentNode_NoConstraint_IsSubtypeOf_DocumentNode_WithElement_ReturnsFalse()
    {
        // Arrange
        ItemType constrained = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));
        ItemType unconstrained = ItemType.DocumentNode();

        // Act / Assert
        Assert.That(unconstrained.IsSubtypeOf(constrained), Is.False);
    }

    [Test]
    public void DocumentNode_IsSubtypeOf_KindDocument_ReturnsTrue()
    {
        // Arrange — document-node(element(x)) ⊆ document-node() (KindItemType)
        ItemType docElem = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));
        ItemType kindDoc = ItemType.Kind(XdmNodeKind.Document);

        // Act / Assert
        Assert.That(docElem.IsSubtypeOf(kindDoc), Is.True);
    }

    [Test]
    public void KindDocument_IsSubtypeOf_DocumentNode_WithElement_ReturnsFalse()
    {
        // Arrange — KindItemType(Document) is NOT subtype of DocumentElementItemType(element(x))
        ItemType docElem = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));
        ItemType kindDoc = ItemType.Kind(XdmNodeKind.Document);

        // Act / Assert
        Assert.That(kindDoc.IsSubtypeOf(docElem), Is.False);
    }

    // ── ToString ───────────────────────────────────────────────────────────────

    [Test]
    public void AnyItem_ToString_ReturnsItemParens()
    {
        Assert.That(ItemType.AnyItem.ToString(), Is.EqualTo("item()"));
    }

    [Test]
    public void AnyNode_ToString_ReturnsNodeParens()
    {
        Assert.That(ItemType.AnyNode.ToString(), Is.EqualTo("node()"));
    }

    [Test]
    public void Atomic_Integer_ToString_ReturnsXsInteger()
    {
        Assert.That(ItemType.Atomic(XdmTypeCode.Integer).ToString(), Is.EqualTo("xs:integer"));
    }

    [Test]
    public void KindElement_ToString_ReturnsElementParens()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Element).ToString(), Is.EqualTo("element()"));
    }

    [Test]
    public void KindElement_WithLocalName_ToString_ReturnsElementWithName()
    {
        Assert.That(
            ItemType.Kind(XdmNodeKind.Element, new QName("x")).ToString(),
            Is.EqualTo("element(x)"));
    }

    [Test]
    public void KindElement_WithPrefixedName_ToString_ReturnsElementWithPrefix()
    {
        Assert.That(
            ItemType.Kind(XdmNodeKind.Element, new QName("ns", "http://example.com", "x")).ToString(),
            Is.EqualTo("element(ns:x)"));
    }

    [Test]
    public void KindText_ToString_ReturnsTextParens()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Text).ToString(), Is.EqualTo("text()"));
    }

    [Test]
    public void KindComment_ToString_ReturnsCommentParens()
    {
        Assert.That(ItemType.Kind(XdmNodeKind.Comment).ToString(), Is.EqualTo("comment()"));
    }

    [Test]
    public void DocumentNode_NoConstraint_ToString_ReturnsDocumentNodeParens()
    {
        Assert.That(ItemType.DocumentNode().ToString(), Is.EqualTo("document-node()"));
    }

    [Test]
    public void DocumentNode_WithElement_ToString_ReturnsDocumentNodeElementParens()
    {
        ItemType it = ItemType.DocumentNode(ItemType.Kind(XdmNodeKind.Element, new QName("x")));
        Assert.That(it.ToString(), Is.EqualTo("document-node(element(x))"));
    }
}
