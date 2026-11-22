// P2-05: Range expression evaluation tests.
// XPath 2.0 §3.3 — range expressions.

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

[TestFixture]
internal sealed class RangeEvaluationTests
{
    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static long[] Drain(IXdmIterator iter)
    {
        List<long> result = new List<long>();
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            result.Add(((XdmAtomicValue)item).AsInt64());
        }
        return result.ToArray();
    }

    // ── Basic range ───────────────────────────────────────────────────────────

    [Test]
    public void Range_OneToFive_ProducesFiveIntegers()
    {
        // Arrange
        CompiledExpression ce = Compile("1 to 5");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.EqualTo(new long[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void Range_FiveToOne_ProducesEmpty()
    {
        // Arrange
        CompiledExpression ce = Compile("5 to 1");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Range_FiveToFive_ProducesSingleItem()
    {
        // Arrange
        CompiledExpression ce = Compile("5 to 5");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.EqualTo(new long[] { 5 }));
    }

    [Test]
    public void Range_NegativeToPositive_ProducesSequence()
    {
        // Arrange
        CompiledExpression ce = Compile("-2 to 2");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.EqualTo(new long[] { -2, -1, 0, 1, 2 }));
    }

    // ── Empty-sequence operands ───────────────────────────────────────────────

    [Test]
    public void Range_EmptyLeft_ProducesEmpty()
    {
        // Arrange
        CompiledExpression ce = Compile("() to 3");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Range_EmptyRight_ProducesEmpty()
    {
        // Arrange
        CompiledExpression ce = Compile("1 to ()");

        // Act
        long[] result = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── Position tracking ─────────────────────────────────────────────────────

    [Test]
    public void Range_Position_IncrementsPerItem()
    {
        // Arrange
        CompiledExpression ce = Compile("1 to 3");
        IXdmIterator iter = ce.Root.Iterate(NewRoot());

        // Act
        iter.Next();
        iter.Next();

        // Assert
        Assert.That(iter.Position, Is.EqualTo(2));
    }

    // ── Clone ─────────────────────────────────────────────────────────────────

    [Test]
    public void Range_Clone_RestartsFromBeginning()
    {
        // Arrange
        CompiledExpression ce = Compile("1 to 5");
        IXdmIterator iter = ce.Root.Iterate(NewRoot());
        iter.Next();
        iter.Next();
        iter.Next();

        // Act
        IXdmIterator clone = iter.Clone();
        long[] cloneItems = Drain(clone);

        // Assert: clone starts from 1, not from where the original was
        Assert.That(cloneItems, Is.EqualTo(new long[] { 1, 2, 3, 4, 5 }));
    }

    // ── All items are xs:integer ──────────────────────────────────────────────

    [Test]
    public void Range_AllItemsAreXsInteger()
    {
        // Arrange
        CompiledExpression ce = Compile("1 to 3");
        IXdmIterator iter = ce.Root.Iterate(NewRoot());

        // Act / Assert
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            Assert.That(item, Is.TypeOf<XdmAtomicValue>());
            Assert.That(((XdmAtomicValue)item).TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        }
    }
}
