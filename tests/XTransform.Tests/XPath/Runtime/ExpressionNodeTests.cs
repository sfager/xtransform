using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <see cref="ExpressionNode"/> base behaviour: Iterate, EvaluateItem,
/// EffectiveBooleanValue and the XPTY0004 error paths.
/// </summary>
[TestFixture]
internal sealed class ExpressionNodeTests
{
    // ── Toy subclass ──────────────────────────────────────────────────────────

    /// <summary>
    /// A trivial concrete expression node that iterates over a fixed list of items.
    /// Used to exercise the base-class default implementations without depending on the compiler.
    /// </summary>
    private sealed class FixedExpressionNode : ExpressionNode
    {
        private readonly XdmItem[] _items;

        /// <summary>Constructs a node that iterates the given items.</summary>
        public FixedExpressionNode(params XdmItem[] items)
            : base(SourceSpan.None, SequenceType.AnySequence, ExpressionDependencies.None)
        {
            _items = items;
        }

        /// <inheritdoc/>
        public override IXdmIterator Iterate(DynamicContext context)
        {
            return new ArrayIterator(_items);
        }
    }

    private static DynamicContext MakeContext() => DynamicContext.Root(new RunState());

    // ── Iterate ───────────────────────────────────────────────────────────────

    [Test]
    public void Iterate_EmptyNode_ReturnsEmptyIterator()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode();
        DynamicContext ctx = MakeContext();

        // Act
        IXdmIterator iter = node.Iterate(ctx);

        // Assert
        Assert.That(iter.Next(), Is.Null);
    }

    // ── EvaluateItem ──────────────────────────────────────────────────────────

    [Test]
    public void EvaluateItem_EmptySequence_ReturnsNull()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode();
        DynamicContext ctx = MakeContext();

        // Act
        XdmItem? result = node.EvaluateItem(ctx);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void EvaluateItem_SingleItem_ReturnsIt()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(42L);
        FixedExpressionNode node = new FixedExpressionNode(item);
        DynamicContext ctx = MakeContext();

        // Act
        XdmItem? result = node.EvaluateItem(ctx);

        // Assert
        Assert.That(result, Is.SameAs(item));
    }

    [Test]
    public void EvaluateItem_TwoItems_ThrowsXPTY0004()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(
            XdmAtomicValue.Integer(1L),
            XdmAtomicValue.Integer(2L));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => node.EvaluateItem(ctx))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // ── EffectiveBooleanValue — empty / node ──────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_EmptySequence_False()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode();
        DynamicContext ctx = MakeContext();

        // Act
        bool ebv = node.EffectiveBooleanValue(ctx);

        // Assert
        Assert.That(ebv, Is.False);
    }

    [Test]
    public void EffectiveBooleanValue_SingletonNode_True()
    {
        // Arrange — build a 1-element TinyTree and wrap the document node as XdmNode
        NamePool pool = new NamePool();
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);
        builder.StartDocument();
        builder.StartElement(pool.Allocate(string.Empty, "root"), SourceSpan.None);
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();
        XdmNode docNode = new XdmNode(NodeHandle.ForNode(tree, 0));

        FixedExpressionNode node = new FixedExpressionNode(docNode);
        DynamicContext ctx = MakeContext();

        // Act
        bool ebv = node.EffectiveBooleanValue(ctx);

        // Assert
        Assert.That(ebv, Is.True);
    }

    // ── EffectiveBooleanValue — boolean ───────────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_TrueBoolean_True()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Boolean(true));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.True);
    }

    [Test]
    public void EffectiveBooleanValue_FalseBoolean_False()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Boolean(false));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.False);
    }

    // ── EffectiveBooleanValue — string ────────────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_EmptyString_False()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.String(string.Empty));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.False);
    }

    [Test]
    public void EffectiveBooleanValue_NonEmptyString_True()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.String("hello"));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.True);
    }

    // ── EffectiveBooleanValue — integer ───────────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_ZeroInteger_False()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Integer(0L));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.False);
    }

    [Test]
    public void EffectiveBooleanValue_NonZeroInteger_True()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Integer(1L));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.True);
    }

    // ── EffectiveBooleanValue — double ────────────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_NaN_False()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Double(double.NaN));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.False);
    }

    [Test]
    public void EffectiveBooleanValue_NonZeroDouble_True()
    {
        // Arrange
        FixedExpressionNode node = new FixedExpressionNode(XdmAtomicValue.Double(3.14));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        Assert.That(node.EffectiveBooleanValue(ctx), Is.True);
    }

    // ── EffectiveBooleanValue — error cases ───────────────────────────────────

    [Test]
    public void EffectiveBooleanValue_TwoAtomics_ThrowsXPTY0004()
    {
        // Arrange — two integers: EBV on a sequence with more than one atomic is undefined
        FixedExpressionNode node = new FixedExpressionNode(
            XdmAtomicValue.Integer(1L),
            XdmAtomicValue.Integer(2L));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => node.EffectiveBooleanValue(ctx))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void EffectiveBooleanValue_QNameAtomic_ThrowsXPTY0004()
    {
        // Arrange — xs:QName has no defined EBV (F&O §15.1.1)
        FixedExpressionNode node = new FixedExpressionNode(
            XdmAtomicValue.QNameValue(new QName(string.Empty, string.Empty, "foo")));
        DynamicContext ctx = MakeContext();

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => node.EffectiveBooleanValue(ctx))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }
}
