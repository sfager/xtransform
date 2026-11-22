// P2-06: FilterNode evaluation tests.
// XPath 2.0 §3.2.4 — filter expressions.

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <c>FilterNode.Iterate</c> (filter expressions with predicates).
/// XPath 2.0 §3.2.4.
/// </summary>
[TestFixture]
internal sealed class FilterEvaluationTests
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

    // ── Positional predicate ──────────────────────────────────────────────────

    [Test]
    public void Filter_IntegerLiteralPredicate_ReturnsNthItem()
    {
        // Arrange: (10, 20, 30)[2] → (20)
        CompiledExpression ce = Compile("(10, 20, 30)[2]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(20L));
    }

    [Test]
    public void Filter_FirstItemPredicate_ReturnsFirstItem()
    {
        // Arrange: (10, 20, 30)[1] → (10)
        CompiledExpression ce = Compile("(10, 20, 30)[1]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(10L));
    }

    [Test]
    public void Filter_LastPositionPredicate_ReturnsLastItem()
    {
        // Arrange: (10, 20, 30)[3] → (30)
        CompiledExpression ce = Compile("(10, 20, 30)[3]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(30L));
    }

    [Test]
    public void Filter_OutOfRangePredicate_ReturnsEmpty()
    {
        // Arrange: (10, 20, 30)[6] → ()
        CompiledExpression ce = Compile("(10, 20, 30)[6]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Is.Empty);
    }

    // ── EBV predicate ─────────────────────────────────────────────────────────

    [Test]
    public void Filter_EbvPredicate_NonZeroValuesPass()
    {
        // Arrange: (1, 2)[.] — the predicate yields numeric values; treated as position predicates.
        // item 1 at pos 1: 1==1 → true (pass)
        // item 2 at pos 2: 2==2 → true (pass)
        // Result: (1, 2)
        CompiledExpression ce = Compile("(1, 2)[.]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(Int(items[0]), Is.EqualTo(1L));
        Assert.That(Int(items[1]), Is.EqualTo(2L));
    }

    [Test]
    public void Filter_EbvPredicate_ZeroValueFiltered()
    {
        // Arrange: (1, 0, 2)[.] — the predicate yields a numeric value, so XPath 2.0 §2.5.4
        // treats it as a position predicate: item passes only if numeric value == context position.
        // item 1 at pos 1: 1==1 → true (pass)
        // item 0 at pos 2: 0==2 → false (filter out)
        // item 2 at pos 3: 2==3 → false (filter out)
        // Result: (1)
        CompiledExpression ce = Compile("(1, 0, 2)[.]");

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(Int(items[0]), Is.EqualTo(1L));
    }
}
