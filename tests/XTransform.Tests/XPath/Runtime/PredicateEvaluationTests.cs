// P2-06: Predicate evaluation tests.
// XPath 2.0 §2.5.4 — predicates.

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <c>PredicateApplicator</c> and predicate semantics in filter and axis step expressions.
/// XPath 2.0 §2.5.4.
/// </summary>
[TestFixture]
internal sealed class PredicateEvaluationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static List<XdmItem> Drain(IXdmIterator iter)
    {
        List<XdmItem> items = new List<XdmItem>();
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }
        return items;
    }

    private static long Int(XdmItem item) => ((XdmAtomicValue)item).AsInt64();

    // ── Basic positional predicates ───────────────────────────────────────────

    [TestCase("(1, 2, 3, 4, 5)[1]", 1L)]
    [TestCase("(1, 2, 3, 4, 5)[2]", 2L)]
    [TestCase("(1, 2, 3, 4, 5)[5]", 5L)]
    public void Predicate_PositionalLiteral_ReturnsNthItem(string expr, long expected)
    {
        // Arrange / Act
        List<XdmItem> items = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(expected));
    }

    [Test]
    public void Predicate_OutOfRange_ReturnsEmpty()
    {
        // Arrange: (1, 2, 3, 4, 5)[6] → ()
        List<XdmItem> items = Drain(Compile("(1, 2, 3, 4, 5)[6]").Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Is.Empty);
    }

    // ── ACCEPTANCE: (1 to 100)[. mod 2 = 0][3] → 6 ──────────────────────────

    [Test]
    public void Predicate_ChainedPredicates_EvenNumbersThird()
    {
        // Arrange
        // (1 to 100)[. mod 2 = 0] → (2, 4, 6, 8, …, 100)
        // Then [3] → 6 (the 3rd even number)
        CompiledExpression ce = Compile("(1 to 100)[. mod 2 = 0][3]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(6L));
    }

    // ── ACCEPTANCE: (1 to 1000000)[1] must consume exactly one item ──────────

    [Test]
    public void Predicate_LargeRangeFirstItem_IsLazy()
    {
        // Arrange: (1 to 1000000)[1] should yield 1 and NOT consume all 1 million items.
        // We verify this by measuring that it completes in well under 1 second; a non-lazy
        // implementation that consumed all items would be observably slow.
        CompiledExpression ce = Compile("(1 to 1000000)[1]");

        // Act
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));
        sw.Stop();

        // Assert: correct result
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(1L));

        // Assert: laziness — should complete in well under 100ms (non-lazy would take >1s).
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100),
            "Predicate [1] on a large range should be lazy and consume only the first item.");
    }

    // ── position() / last() inside predicate ─────────────────────────────────

    [Test]
    public void Predicate_PositionFunction_ReturnsCorrectItem()
    {
        // (10, 20, 30)[position() = 2] → (20)
        // Requires BuiltInFunctionLibrary so position() can be resolved and evaluated.
        CompiledExpression ce = XPathCompiler.ParseAndCompile(
            "(10, 20, 30)[position() = 2]",
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(20L));
    }

    [Test]
    public void Predicate_LastFunction_ReturnsLastItem()
    {
        // (10, 20, 30)[last()] → (30)
        CompiledExpression ce = XPathCompiler.ParseAndCompile(
            "(10, 20, 30)[last()]",
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(30L));
    }
}
