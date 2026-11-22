using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// P2-04 evaluation coverage for VariableRef, ForExpr, QuantifiedExpr, IfExpr.
/// XPath 2.0 §3.8 / §3.9 / §3.10.
/// </summary>
[TestFixture]
internal sealed class FlowControlEvaluationTests
{
    private static readonly long[] s_one_two_three = { 1L, 2L, 3L };
    private static readonly long[] s_1_1_2_2_3_3 = { 1L, 1L, 2L, 2L, 3L, 3L };
    private static readonly string[] s_cross_ij = { "1-a", "1-b", "2-a", "2-b" };
    private static readonly long[] s_nested_for_cross = { 1L, 10L, 1L, 20L, 2L, 10L, 2L, 20L };
    private static readonly long[] s_inner_outer_i = { 1L, 1L, 2L, 2L };

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext Context(CompiledExpression ce) =>
        DynamicContext.Root(new RunState()).WithFrame(ce.SlotCount);

    private static List<XdmItem> Drain(IXdmIterator iter)
    {
        List<XdmItem> items = new();
        while (true)
        {
            XdmItem? n = iter.Next();
            if (n is null) { break; }
            items.Add(n);
        }
        return items;
    }

    private static long[] Longs(List<XdmItem> items)
    {
        long[] result = new long[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            result[i] = ((XdmAtomicValue)items[i]).AsInt64();
        }
        return result;
    }

    // ── VariableRef via an outer for ─────────────────────────────────────────

    [Test]
    public void For_ReturnsBoundVariableInOrder()
    {
        CompiledExpression ce = Compile("for $i in (1, 2, 3) return $i");
        List<XdmItem> result = Drain(ce.Root.Iterate(Context(ce)));
        Assert.That(Longs(result), Is.EqualTo(s_one_two_three));
    }

    [Test]
    public void For_WithDuplicatingReturn_EmitsEachItemTwice()
    {
        CompiledExpression ce = Compile("for $i in (1, 2, 3) return ($i, $i)");
        List<XdmItem> result = Drain(ce.Root.Iterate(Context(ce)));
        Assert.That(Longs(result), Is.EqualTo(s_1_1_2_2_3_3));
    }

    [Test]
    public void For_MultipleClauses_IteratesAsCrossProduct()
    {
        // Need something that yields distinguishable strings; use string concat via sequence.
        CompiledExpression ce = Compile("for $i in ('1', '2'), $j in ('a', 'b') return ($i, '-', $j)");
        List<XdmItem> items = Drain(ce.Root.Iterate(Context(ce)));
        List<string> flat = new();
        for (int i = 0; i < items.Count; i += 3)
        {
            flat.Add(((XdmAtomicValue)items[i]).GetStringValue()
                + ((XdmAtomicValue)items[i + 1]).GetStringValue()
                + ((XdmAtomicValue)items[i + 2]).GetStringValue());
        }
        Assert.That(flat, Is.EqualTo(s_cross_ij));
    }

    [Test]
    public void For_LazyIteration_StopsAfterFirstItem()
    {
        // Pull one item from a for over a large input; verify we didn't consume the rest.
        // We can't directly observe this with pure XPath 2.0 syntax in P2-04 (no predicates
        // yet), but we can assert via the iterator contract that Position is 1 after one Next.
        CompiledExpression ce = Compile("for $i in (10, 20, 30, 40, 50) return $i");
        IXdmIterator iter = ce.Root.Iterate(Context(ce));
        XdmItem? first = iter.Next();
        Assert.That(((XdmAtomicValue)first!).AsInt64(), Is.EqualTo(10L));
        Assert.That(iter.Position, Is.EqualTo(1));
    }

    [Test]
    public void For_EmptySource_YieldsEmpty()
    {
        CompiledExpression ce = Compile("for $i in () return $i");
        Assert.That(Drain(ce.Root.Iterate(Context(ce))), Is.Empty);
    }

    [Test]
    public void For_Clone_RestartsFromFirstBinding()
    {
        CompiledExpression ce = Compile("for $i in (1, 2, 3) return $i");
        IXdmIterator iter = ce.Root.Iterate(Context(ce));
        iter.Next();
        iter.Next();
        IXdmIterator fresh = iter.Clone();
        Assert.That(Longs(Drain(fresh)), Is.EqualTo(s_one_two_three));
    }

    [Test]
    public void For_NestedClauses_InnerSeesOuterBinding()
    {
        // for $i in (1,2), $j in (10, 20) return ($i, $j)
        CompiledExpression ce = Compile("for $i in (1, 2), $j in (10, 20) return ($i, $j)");
        List<XdmItem> items = Drain(ce.Root.Iterate(Context(ce)));
        Assert.That(Longs(items), Is.EqualTo(s_nested_for_cross));
    }

    // ── IfExpr ──────────────────────────────────────────────────────────────

    [Test]
    public void If_TrueBranch_ReturnsThen()
    {
        CompiledExpression ce = Compile("if ('x') then 'y' else 'n'");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("y"));
    }

    [Test]
    public void If_EmptyTest_SelectsElse()
    {
        CompiledExpression ce = Compile("if (()) then 'y' else 'n'");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("n"));
    }

    [Test]
    public void If_ZeroInteger_SelectsElse()
    {
        CompiledExpression ce = Compile("if (0) then 'y' else 'n'");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("n"));
    }

    [Test]
    public void If_EmptyStringTest_SelectsElse()
    {
        CompiledExpression ce = Compile("if ('') then 'y' else 'n'");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("n"));
    }

    [Test]
    public void If_UntakenBranch_IsNotEvaluated()
    {
        // The else-branch references an undeclared context item — it should not blow up
        // because the if-test is truthy and the else-branch is skipped.
        CompiledExpression ce = Compile("if (1) then 'y' else .");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("y"));
    }

    // ── QuantifiedExpr ──────────────────────────────────────────────────────

    [Test]
    public void Some_EmptySource_IsFalse()
    {
        CompiledExpression ce = Compile("some $x in () satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.False);
    }

    [Test]
    public void Every_EmptySource_IsTrue()
    {
        CompiledExpression ce = Compile("every $x in () satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.True);
    }

    [Test]
    public void Some_AnyMatch_IsTrue()
    {
        // (0, 1, 0) — EBV(0)=false, EBV(1)=true, EBV(0)=false. 'some' true as soon as
        // any match found.
        CompiledExpression ce = Compile("some $x in (0, 1, 0) satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.True);
    }

    [Test]
    public void Some_NoneMatch_IsFalse()
    {
        CompiledExpression ce = Compile("some $x in (0, 0, 0) satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.False);
    }

    [Test]
    public void Every_AllMatch_IsTrue()
    {
        CompiledExpression ce = Compile("every $x in (1, 2, 3) satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.True);
    }

    [Test]
    public void Every_OneMismatch_IsFalse()
    {
        CompiledExpression ce = Compile("every $x in (1, 0, 3) satisfies $x");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.False);
    }

    [Test]
    public void Some_NestedClauses_CrossProduct()
    {
        // At least one (i, j) where j > 0: all (1,10),(1,20),(2,10),(2,20) — all j > 0 → some = true
        CompiledExpression ce = Compile("some $i in (1, 2), $j in (10, 20) satisfies $j");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.True);
    }

    [Test]
    public void Every_NestedClauses_RequiresAllTuples()
    {
        // (i, j) pairs: (1,1), (1,0), (2,1), (2,0). One $j is 0 → every = false
        CompiledExpression ce = Compile("every $i in (1, 2), $j in (1, 0) satisfies $j");
        Assert.That(ce.Root.EffectiveBooleanValue(Context(ce)), Is.False);
    }

    [Test]
    public void Quantified_EvaluateItem_ReturnsBooleanXsAtomic()
    {
        CompiledExpression ce = Compile("some $x in (1) satisfies $x");
        XdmItem? item = ce.Root.EvaluateItem(Context(ce));
        Assert.That(item, Is.TypeOf<XdmAtomicValue>());
        Assert.That(((XdmAtomicValue)item!).TypeCode, Is.EqualTo(XdmTypeCode.Boolean));
        Assert.That(((XdmAtomicValue)item!).AsBoolean(), Is.True);
    }

    // ── VariableRef reads the current slot value ────────────────────────────

    [Test]
    public void VariableRef_InnerFor_OverwritesInnerSlot()
    {
        // Inner $j rebinding doesn't disturb outer $i reads.
        CompiledExpression ce = Compile("for $i in (1, 2) return (for $j in (10, 20) return $i)");
        // For each outer i, the inner for yields $i twice.
        List<XdmItem> items = Drain(ce.Root.Iterate(Context(ce)));
        Assert.That(Longs(items), Is.EqualTo(s_inner_outer_i));
    }
}
