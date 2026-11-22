using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>
/// Tests for <see cref="SequenceType"/> matches, subtype relationships, and ToString.
/// XPath 2.0 §2.5.4–§2.5.5.
/// </summary>
[TestFixture]
internal sealed class SequenceTypeTests
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

    private static XdmNode RootElement(string xml)
    {
        TinyTree tree = LoadXml(xml);
        return new XdmNode(NodeHandle.ForNode(tree, 1));
    }

    private static XdmSequence Seq() => XdmSequence.Empty;
    private static XdmSequence Seq(XdmItem item) => XdmSequence.Of(item);
    private static XdmSequence Seq(params XdmItem[] items) => XdmSequence.Of(items);

    // ── EmptySequence singleton ────────────────────────────────────────────────

    [Test]
    public void EmptySequence_Matches_EmptyXdmSequence_ReturnsTrue()
    {
        // Arrange / Act
        bool result = SequenceType.EmptySequence.Matches(XdmSequence.Empty);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void EmptySequence_Matches_SingletonSequence_ReturnsFalse()
    {
        // Arrange
        XdmSequence single = Seq(XdmAtomicValue.Integer(1));

        // Act
        bool result = SequenceType.EmptySequence.Matches(single);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void EmptySequence_ToString_ReturnsEmptySequenceParens()
    {
        Assert.That(SequenceType.EmptySequence.ToString(), Is.EqualTo("empty-sequence()"));
    }

    // ── AnySequence singleton ──────────────────────────────────────────────────

    [Test]
    public void AnySequence_Matches_Empty_ReturnsTrue()
    {
        Assert.That(SequenceType.AnySequence.Matches(XdmSequence.Empty), Is.True);
    }

    [Test]
    public void AnySequence_Matches_MultipleItems_ReturnsTrue()
    {
        // Arrange
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2));

        // Act / Assert
        Assert.That(SequenceType.AnySequence.Matches(seq), Is.True);
    }

    [Test]
    public void AnySequence_ToString_ReturnsItemStar()
    {
        Assert.That(SequenceType.AnySequence.ToString(), Is.EqualTo("item()*"));
    }

    // ── Matches — ExactlyOne ───────────────────────────────────────────────────

    [Test]
    public void IntegerExactlyOne_Matches_SingleInteger_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

        // Act
        bool result = st.Matches(Seq(XdmAtomicValue.Integer(5)));

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void IntegerExactlyOne_Matches_Empty_ReturnsFalse()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

        // Act
        bool result = st.Matches(XdmSequence.Empty);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void IntegerExactlyOne_Matches_TwoIntegers_ReturnsFalse()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2));

        // Act
        bool result = st.Matches(seq);

        // Assert
        Assert.That(result, Is.False);
    }

    // ── Matches — ZeroOrOne ────────────────────────────────────────────────────

    [Test]
    public void IntegerZeroOrOne_Matches_Empty_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);

        // Act / Assert
        Assert.That(st.Matches(XdmSequence.Empty), Is.True);
    }

    [Test]
    public void IntegerZeroOrOne_Matches_SingleInteger_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);

        // Act / Assert
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(7))), Is.True);
    }

    [Test]
    public void IntegerZeroOrOne_Matches_TwoIntegers_ReturnsFalse()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2));

        // Act / Assert
        Assert.That(st.Matches(seq), Is.False);
    }

    // ── Matches — OneOrMore ────────────────────────────────────────────────────

    [Test]
    public void IntegerOneOrMore_Matches_Empty_ReturnsFalse()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);

        // Act / Assert
        Assert.That(st.Matches(XdmSequence.Empty), Is.False);
    }

    [Test]
    public void IntegerOneOrMore_Matches_SingleInteger_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);

        // Act / Assert
        Assert.That(st.Matches(Seq(XdmAtomicValue.Integer(1))), Is.True);
    }

    [Test]
    public void IntegerOneOrMore_Matches_ManyIntegers_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2), XdmAtomicValue.Integer(3));

        // Act / Assert
        Assert.That(st.Matches(seq), Is.True);
    }

    // ── Matches — ZeroOrMore ───────────────────────────────────────────────────

    [Test]
    public void IntegerZeroOrMore_Matches_Empty_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);

        // Act / Assert
        Assert.That(st.Matches(XdmSequence.Empty), Is.True);
    }

    [Test]
    public void IntegerZeroOrMore_Matches_ManyIntegers_ReturnsTrue()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.Integer(2));

        // Act / Assert
        Assert.That(st.Matches(seq), Is.True);
    }

    // Item type checking across the sequence
    [Test]
    public void IntegerZeroOrMore_Matches_MixedTypes_ReturnsFalse()
    {
        // Arrange — one string in the sequence fails the integer type check
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        XdmSequence seq = Seq(XdmAtomicValue.Integer(1), XdmAtomicValue.String("hello"));

        // Act / Assert
        Assert.That(st.Matches(seq), Is.False);
    }

    // ── IsSubtypeOf ────────────────────────────────────────────────────────────

    [Test]
    public void IntegerExactlyOne_IsSubtypeOf_DecimalZeroOrMore_ReturnsTrue()
    {
        // Arrange — xs:integer is subtype of xs:decimal; ExactlyOne ⊆ ZeroOrMore
        SequenceType st1 = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        SequenceType st2 = new SequenceType(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ZeroOrMore);

        // Act / Assert
        Assert.That(st1.IsSubtypeOf(st2), Is.True);
    }

    [Test]
    public void IntegerZeroOrMore_IsSubtypeOf_DecimalExactlyOne_ReturnsFalse()
    {
        // Arrange — ZeroOrMore ⊄ ExactlyOne
        SequenceType st1 = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        SequenceType st2 = new SequenceType(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ExactlyOne);

        // Act / Assert
        Assert.That(st1.IsSubtypeOf(st2), Is.False);
    }

    [Test]
    public void EmptySequence_IsSubtypeOf_IntegerZeroOrOne_ReturnsTrue()
    {
        // Arrange — empty-sequence() is subtype of anything allowing 0 items
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);

        // Act / Assert
        Assert.That(SequenceType.EmptySequence.IsSubtypeOf(st), Is.True);
    }

    [Test]
    public void EmptySequence_IsSubtypeOf_IntegerExactlyOne_ReturnsFalse()
    {
        // Arrange — empty-sequence() is NOT subtype of xs:integer (which requires exactly 1)
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

        // Act / Assert
        Assert.That(SequenceType.EmptySequence.IsSubtypeOf(st), Is.False);
    }

    [Test]
    public void AnySequence_IsSubtypeOf_IntegerExactlyOne_ReturnsFalse()
    {
        // Arrange
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

        // Act / Assert
        Assert.That(SequenceType.AnySequence.IsSubtypeOf(st), Is.False);
    }

    [Test]
    public void IntegerExactlyOne_IsSubtypeOf_AnySequence_ReturnsTrue()
    {
        Assert.That(
            new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne)
                .IsSubtypeOf(SequenceType.AnySequence),
            Is.True);
    }

    // ── ToString ───────────────────────────────────────────────────────────────

    [Test]
    public void ToString_ExactlyOne_ReturnsItemTypeOnly()
    {
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
        Assert.That(st.ToString(), Is.EqualTo("xs:integer"));
    }

    [Test]
    public void ToString_ZeroOrOne_AppendsQuestionMark()
    {
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
        Assert.That(st.ToString(), Is.EqualTo("xs:integer?"));
    }

    [Test]
    public void ToString_ZeroOrMore_AppendsStar()
    {
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
        Assert.That(st.ToString(), Is.EqualTo("xs:integer*"));
    }

    [Test]
    public void ToString_OneOrMore_AppendsPlus()
    {
        SequenceType st = new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.OneOrMore);
        Assert.That(st.ToString(), Is.EqualTo("xs:integer+"));
    }

    [Test]
    public void ToString_ElementZeroOrOne_ReturnsElementQuestionMark()
    {
        SequenceType st = new SequenceType(
            ItemType.Kind(XdmNodeKind.Element, new QName("x")),
            Occurrence.ZeroOrOne);
        Assert.That(st.ToString(), Is.EqualTo("element(x)?"));
    }

    [Test]
    public void ToString_Empty_ReturnsEmptySequenceParens()
    {
        Assert.That(SequenceType.EmptySequence.ToString(), Is.EqualTo("empty-sequence()"));
    }
}
