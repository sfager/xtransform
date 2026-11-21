using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>
/// P1-10 acceptance test — table-driven "instance of" semantics covering every
/// occurrence indicator and every kind test (including element(name),
/// attribute(*), and document-node(element(x))). XPath 2.0 §3.5.1.
/// </summary>
[TestFixture]
internal sealed class InstanceOfSemanticsTests
{
    // ── Shared fixtures ──────────────────────────────────────────────────────

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

    /// <summary>Returns the first element child of the document node.</summary>
    private static XdmNode RootElement(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForNode(tree, 1));
    }

    /// <summary>Returns the document node itself.</summary>
    private static XdmNode DocumentNode(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForNode(tree, 0));
    }

    /// <summary>Returns the first attribute of the root element.</summary>
    private static XdmNode AttributeOnRoot(string xml, string attrLocalName)
    {
        TinyTree tree = LoadXml(xml);
        int attrIdx = tree.GetFirstAttribute(1);
        while (attrIdx >= 0)
        {
            int nameCode = tree.GetAttributeNameCode(attrIdx);
            if (tree.NamePool.GetLocalName(nameCode) == attrLocalName)
            {
                return new XdmNode(NodeHandle.ForAttribute(tree, attrIdx));
            }
            attrIdx++;
            if (attrIdx >= tree.AttributeCount || tree.GetAttributeParent(attrIdx) != 1)
            {
                break;
            }
        }
        throw new System.InvalidOperationException($"attribute '{attrLocalName}' not found");
    }

    private static XdmSequence Seq() => XdmSequence.Empty;
    private static XdmSequence Seq(XdmItem item) => XdmSequence.Of(item);
    private static XdmSequence Seq(params XdmItem[] items) => XdmSequence.Of(items);

    private static SequenceType ST(ItemType t, Occurrence o) => new SequenceType(t, o);

    // ── Occurrence × atomic — every indicator ────────────────────────────────

    [Test]
    public void Empty_MatchesEmptySequence_True()
    {
        Assert.That(SequenceType.EmptySequence.Matches(Seq()), Is.True);
    }

    [Test]
    public void Empty_MatchesSingleton_False()
    {
        Assert.That(SequenceType.EmptySequence.Matches(Seq(XdmAtomicValue.Integer(1))), Is.False);
    }

    [Test]
    public void ExactlyOne_EmptySequence_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq()), Is.False);
    }

    [Test]
    public void ExactlyOne_Singleton_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1))), Is.True);
    }

    [Test]
    public void ExactlyOne_Two_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2))), Is.False);
    }

    [Test]
    public void ZeroOrOne_Empty_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
        Assert.That(st.Matches(Seq()), Is.True);
    }

    [Test]
    public void ZeroOrOne_Singleton_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1))), Is.True);
    }

    [Test]
    public void ZeroOrOne_Two_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2))), Is.False);
    }

    [Test]
    public void OneOrMore_Empty_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);
        Assert.That(st.Matches(Seq()), Is.False);
    }

    [Test]
    public void OneOrMore_Singleton_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1))), Is.True);
    }

    [Test]
    public void OneOrMore_Two_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2))), Is.True);
    }

    [Test]
    public void ZeroOrMore_Empty_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        Assert.That(st.Matches(Seq()), Is.True);
    }

    [Test]
    public void ZeroOrMore_Many_True()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2), XdmAtomicValue.Integer(3))), Is.True);
    }

    // ── Atomic subtype matching ──────────────────────────────────────────────

    [Test]
    public void IntegerInstanceOfDecimal_True()
    {
        // Integer ⊏ Decimal via CastTable
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(5))), Is.True);
    }

    [Test]
    public void DecimalInstanceOfInteger_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Decimal(5.5m))), Is.False);
    }

    [Test]
    public void IntegerInstanceOfString_False()
    {
        SequenceType st = ST(ItemType.Atomic(XdmTypeCode.String), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(5))), Is.False);
    }

    [Test]
    public void AtomicValueInstanceOfItem_True()
    {
        SequenceType st = ST(ItemType.AnyItem, Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(5))), Is.True);
    }

    [Test]
    public void AtomicValueInstanceOfNode_False()
    {
        SequenceType st = ST(ItemType.AnyNode, Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(5))), Is.False);
    }

    // ── Every kind test ──────────────────────────────────────────────────────

    [Test]
    public void ElementNode_InstanceOfElement_True()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Element), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.True);
    }

    [Test]
    public void ElementNode_InstanceOfElementWithMatchingName_True()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Element, new QName("x")), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.True);
    }

    [Test]
    public void ElementNode_InstanceOfElementWithDifferentName_False()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Element, new QName("y")), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.False);
    }

    [Test]
    public void ElementNode_InstanceOfAttribute_False()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Attribute), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.False);
    }

    [Test]
    public void AttributeNode_InstanceOfAttribute_True()
    {
        XdmNode attr = AttributeOnRoot("<x a='1'/>", "a");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Attribute), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(attr)), Is.True);
    }

    [Test]
    public void AttributeNode_InstanceOfAttributeWildcard_True()
    {
        // attribute(*) is equivalent to attribute() per XPath 2.0
        XdmNode attr = AttributeOnRoot("<x a='1'/>", "a");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Attribute), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(attr)), Is.True);
    }

    [Test]
    public void AttributeNode_InstanceOfAttributeWithName_True()
    {
        XdmNode attr = AttributeOnRoot("<x a='1'/>", "a");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Attribute, new QName("a")), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(attr)), Is.True);
    }

    [Test]
    public void ElementNode_InstanceOfNode_True()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.AnyNode, Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.True);
    }

    [Test]
    public void ElementNode_InstanceOfItem_True()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.AnyItem, Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.True);
    }

    // ── document-node tests ──────────────────────────────────────────────────

    [Test]
    public void DocumentNode_InstanceOfDocumentNode_True()
    {
        XdmNode doc = DocumentNode("<x/>");
        SequenceType st = ST(ItemType.DocumentNode(), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(doc)), Is.True);
    }

    [Test]
    public void ElementNode_InstanceOfDocumentNode_False()
    {
        XdmNode root = RootElement("<x/>");
        SequenceType st = ST(ItemType.DocumentNode(), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(root)), Is.False);
    }

    [Test]
    public void DocumentWithMatchingElement_InstanceOfDocumentNodeOfElementX_True()
    {
        XdmNode doc = DocumentNode("<x/>");
        ItemType elementX = ItemType.Kind(XdmNodeKind.Element, new QName("x"));
        SequenceType st = ST(ItemType.DocumentNode(elementX), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(doc)), Is.True);
    }

    [Test]
    public void DocumentWithWrongElement_InstanceOfDocumentNodeOfElementX_False()
    {
        XdmNode doc = DocumentNode("<y/>");
        ItemType elementX = ItemType.Kind(XdmNodeKind.Element, new QName("x"));
        SequenceType st = ST(ItemType.DocumentNode(elementX), Occurrence.ExactlyOne);
        Assert.That(st.Matches(Seq(doc)), Is.False);
    }

    // ── Multi-item sequences with kind tests ─────────────────────────────────

    [Test]
    public void TwoElements_InstanceOfElementPlus_True()
    {
        XdmNode a = RootElement("<x/>");
        XdmNode b = RootElement("<y/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Element), Occurrence.OneOrMore);
        Assert.That(st.Matches(Seq(a, b)), Is.True);
    }

    [Test]
    public void ElementAndAtomic_InstanceOfElementStar_False()
    {
        XdmNode a = RootElement("<x/>");
        SequenceType st = ST(ItemType.Kind(XdmNodeKind.Element), Occurrence.ZeroOrMore);
        Assert.That(st.Matches(Seq(a, XdmAtomicValue.Integer(1))), Is.False);
    }

    [Test]
    public void MixedAtomics_InstanceOfItemStar_True()
    {
        XdmNode a = RootElement("<x/>");
        SequenceType st = ST(ItemType.AnyItem, Occurrence.ZeroOrMore);
        Assert.That(st.Matches(Seq(a, XdmAtomicValue.Integer(1), XdmAtomicValue.String("s"))), Is.True);
    }
}
