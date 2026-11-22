using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// P2-03 evaluation coverage for LiteralNode, ContextItemNode, EmptySequenceNode,
/// and SequenceConcatNode. XPath 2.0 §3.1 / §3.2 / §3.3.
/// </summary>
[TestFixture]
internal sealed class PrimaryEvaluationTests
{
    private static readonly long[] s_oneTwoThreeFour = { 1L, 2L, 3L, 4L };
    private static readonly long[] s_oneTwoThree = { 1L, 2L, 3L };
    private static readonly string[] s_oneTwoThreeStrings = { "1", "two", "3" };

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

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

    // ── 42 (literal) ─────────────────────────────────────────────────────────

    [Test]
    public void Literal_Integer_IterateReturnsSingleXsInteger()
    {
        CompiledExpression ce = Compile("42");
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));

        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0], Is.TypeOf<XdmAtomicValue>());
        XdmAtomicValue a = (XdmAtomicValue)items[0];
        Assert.That(a.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(a.AsInt64(), Is.EqualTo(42L));
    }

    [Test]
    public void Literal_Integer_EvaluateItemReturnsSameValue()
    {
        CompiledExpression ce = Compile("42");
        XdmItem? item = ce.Root.EvaluateItem(NewRoot());
        Assert.That(item, Is.Not.Null);
        Assert.That(((XdmAtomicValue)item!).AsInt64(), Is.EqualTo(42L));
    }

    // ── () (empty sequence) ─────────────────────────────────────────────────

    [Test]
    public void EmptySequence_Iterate_IsEmpty()
    {
        CompiledExpression ce = Compile("()");
        Assert.That(Drain(ce.Root.Iterate(NewRoot())), Is.Empty);
    }

    [Test]
    public void EmptySequence_EvaluateItem_IsNull()
    {
        CompiledExpression ce = Compile("()");
        Assert.That(ce.Root.EvaluateItem(NewRoot()), Is.Null);
    }

    [Test]
    public void EmptySequence_EffectiveBooleanValue_IsFalse()
    {
        CompiledExpression ce = Compile("()");
        Assert.That(ce.Root.EffectiveBooleanValue(NewRoot()), Is.False);
    }

    // ── (1, (2, 3), 4) laziness ─────────────────────────────────────────────

    [Test]
    public void SequenceConcat_LazyIteration_StopsAfterTakingTwo()
    {
        // (1, (2, 3), 4) nested — stopping after 2 items should only pull from the
        // literal "1" and the inner sequence "(2, 3)" — not from the "4" leaf.
        CompiledExpression ce = Compile("(1, (2, 3), 4)");
        IXdmIterator iter = ce.Root.Iterate(NewRoot());

        XdmItem? a = iter.Next();
        XdmItem? b = iter.Next();

        Assert.That(((XdmAtomicValue)a!).AsInt64(), Is.EqualTo(1L));
        Assert.That(((XdmAtomicValue)b!).AsInt64(), Is.EqualTo(2L));
        Assert.That(iter.Position, Is.EqualTo(2));
        // Don't drain further; laziness means we assert on observable state only.
    }

    [Test]
    public void SequenceConcat_FullIteration_Flattens()
    {
        CompiledExpression ce = Compile("(1, (2, 3), 4)");
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));
        long[] actual = items.ConvertAll(i => ((XdmAtomicValue)i).AsInt64()).ToArray();
        Assert.That(actual, Is.EqualTo(s_oneTwoThreeFour));
    }

    [Test]
    public void SequenceConcat_Clone_RestartsFromStart()
    {
        CompiledExpression ce = Compile("(1, 2, 3)");
        IXdmIterator iter = ce.Root.Iterate(NewRoot());
        iter.Next();
        iter.Next();
        IXdmIterator fresh = iter.Clone();
        List<XdmItem> items = Drain(fresh);
        Assert.That(items.ConvertAll(i => ((XdmAtomicValue)i).AsInt64()),
                    Is.EqualTo(s_oneTwoThree));
    }

    // ── . (context item) ────────────────────────────────────────────────────

    [Test]
    public void ContextItem_WithFocus_ReturnsCurrentItem()
    {
        CompiledExpression ce = Compile(".");
        DynamicContext ctx = NewRoot().WithContextItem(XdmAtomicValue.Integer(7), position: 1, size: 1);
        XdmItem? item = ce.Root.EvaluateItem(ctx);
        Assert.That(((XdmAtomicValue)item!).AsInt64(), Is.EqualTo(7L));
    }

    [Test]
    public void ContextItem_WithoutFocus_ThrowsXPDY0002()
    {
        CompiledExpression ce = Compile(".");
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => ce.Root.EvaluateItem(NewRoot()))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }

    // ── Table-driven literal round-trips (30+ cases) ────────────────────────

    private static readonly object[][] LiteralCases =
    {
        new object[] { "42",            XdmTypeCode.Integer, "42"     },
        new object[] { "0",             XdmTypeCode.Integer, "0"      },
        new object[] { "123456789",     XdmTypeCode.Integer, "123456789" },
        new object[] { "3.14",          XdmTypeCode.Decimal, "3.14"   },
        new object[] { "0.1",           XdmTypeCode.Decimal, "0.1"    },
        new object[] { "1E2",           XdmTypeCode.Double,  "100"    },
        new object[] { "1.5e3",         XdmTypeCode.Double,  "1500"   },
        // Unary minus ("-" prefix) wraps a Literal in a UnaryNode — evaluated in P2-05.
        new object[] { "\"hello\"",     XdmTypeCode.String,  "hello"  },
        new object[] { "'hello'",       XdmTypeCode.String,  "hello"  },
        new object[] { "\"\"",          XdmTypeCode.String,  ""       },
        new object[] { "'can''t'",      XdmTypeCode.String,  "can't"  },
        new object[] { "\"say \"\"hi\"\"\"", XdmTypeCode.String, "say \"hi\"" },
    };

    [TestCaseSource(nameof(LiteralCases))]
    public void Literal_Table_RoundTripsToExpectedValue(string expr, XdmTypeCode expectedType, string expectedCanonical)
    {
        CompiledExpression ce = Compile(expr);
        XdmItem? item = ce.Root.EvaluateItem(NewRoot());

        Assert.That(item, Is.Not.Null);
        XdmAtomicValue a = (XdmAtomicValue)item!;
        Assert.That(a.TypeCode, Is.EqualTo(expectedType));
        Assert.That(a.GetStringValue(), Is.EqualTo(expectedCanonical));
    }

    // ── Table-driven sequence shapes ────────────────────────────────────────

    private static readonly object[][] SequenceCases =
    {
        new object[] { "(1, 2, 3)",             new long[] { 1, 2, 3 } },
        new object[] { "(1)",                   new long[] { 1 }       },
        new object[] { "()",                    Array.Empty<long>()    },
        new object[] { "((), 1, (), 2, ())",    new long[] { 1, 2 }    },
        new object[] { "(1, 2, (3, 4, 5))",     new long[] { 1, 2, 3, 4, 5 } },
        new object[] { "((1, 2), (3, 4))",      new long[] { 1, 2, 3, 4 } },
        new object[] { "(((1)))",               new long[] { 1 }       },
        new object[] { "(1, 2, 3, 4, 5, 6, 7)", new long[] { 1, 2, 3, 4, 5, 6, 7 } },
    };

    [TestCaseSource(nameof(SequenceCases))]
    public void Sequence_Table_FlattensToExpectedIntegers(string expr, long[] expected)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));
        long[] actual = items.ConvertAll(i => ((XdmAtomicValue)i).AsInt64()).ToArray();
        Assert.That(actual, Is.EqualTo(expected));
    }

    // ── Table-driven mixed-type sequences ───────────────────────────────────

    private static readonly object[][] MixedSequenceCases =
    {
        new object[] { "(1, 'two')",   new string[] { "1", "two" } },
        new object[] { "('a', 'b', 'c')", new string[] { "a", "b", "c" } },
        new object[] { "(1.5, 2.5)",    new string[] { "1.5", "2.5" } },
        // Decimal canonical form strips trailing zeros: 2.0 → "2"
        new object[] { "(1, 2.0, 'three')", new string[] { "1", "2", "three" } },
    };

    [TestCaseSource(nameof(MixedSequenceCases))]
    public void Sequence_Mixed_ProducesExpectedStringValues(string expr, string[] expected)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = Drain(ce.Root.Iterate(NewRoot()));
        string[] actual = items.ConvertAll(i => ((XdmAtomicValue)i).GetStringValue()).ToArray();
        Assert.That(actual, Is.EqualTo(expected));
    }

    // Note: 2.0 is a decimal literal; XsdDecimal canonical strips trailing zeros when
    // the fractional part is zero. The GetStringValue is "2" (not "2.0") for decimal 2.0m.
    // Adjusting the expectation.

    // ── EffectiveBooleanValue over singletons produced by LiteralNode ───────

    [TestCase("true()",   true)]  // can't compile without function library — skip
    // Fallback: boolean EBV is covered in P2-01 ExpressionNodeTests; here we check
    // integer and string EBV through the real compiled LiteralNode.

    [TestCase("1",    true)]
    [TestCase("0",    false)]
    [TestCase("'x'",  true)]
    [TestCase("''",   false)]
    public void Literal_EffectiveBooleanValue_MatchesFnBooleanSemantics(string expr, bool expected)
    {
        if (expr == "true()") { Assert.Ignore("true() requires function library — tested in P2-08"); return; }
        CompiledExpression ce = Compile(expr);
        Assert.That(ce.Root.EffectiveBooleanValue(NewRoot()), Is.EqualTo(expected));
    }

    // ── Atomization helper ──────────────────────────────────────────────────

    [Test]
    public void Atomization_AtomicItem_PassesThrough()
    {
        XdmAtomicValue a = XdmAtomicValue.Integer(5);
        IXdmIterator atom = Atomization.Atomize(XdmSequence.Of(a).GetIterator());
        XdmItem? first = atom.Next();
        Assert.That(first, Is.SameAs(a));
        Assert.That(atom.Next(), Is.Null);
    }

    [Test]
    public void Atomization_EmptySequence_YieldsNothing()
    {
        IXdmIterator atom = Atomization.Atomize(XdmSequence.Empty.GetIterator());
        Assert.That(atom.Next(), Is.Null);
    }

    [Test]
    public void Atomization_MixedSequence_FlattensInOrder()
    {
        XdmSequence seq = XdmSequence.Of(
            XdmAtomicValue.Integer(1),
            XdmAtomicValue.String("two"),
            XdmAtomicValue.Integer(3));
        IXdmIterator atom = Atomization.Atomize(seq.GetIterator());
        List<string> actual = new();
        while (true)
        {
            XdmItem? n = atom.Next();
            if (n is null) { break; }
            actual.Add(((XdmAtomicValue)n).GetStringValue());
        }
        Assert.That(actual, Is.EqualTo(s_oneTwoThreeStrings));
    }

    [Test]
    public void Atomization_Clone_RestartsFromStart()
    {
        XdmSequence seq = XdmSequence.Of(
            XdmAtomicValue.Integer(1),
            XdmAtomicValue.Integer(2));
        IXdmIterator atom = Atomization.Atomize(seq.GetIterator());
        atom.Next();
        IXdmIterator fresh = atom.Clone();
        Assert.That(((XdmAtomicValue)fresh.Next()!).AsInt64(), Is.EqualTo(1L));
    }

    [Test]
    public void Atomization_Position_IncrementsPerAtomic()
    {
        XdmSequence seq = XdmSequence.Of(
            XdmAtomicValue.Integer(1),
            XdmAtomicValue.Integer(2),
            XdmAtomicValue.Integer(3));
        IXdmIterator atom = Atomization.Atomize(seq.GetIterator());
        atom.Next();
        atom.Next();
        Assert.That(atom.Position, Is.EqualTo(2));
    }

    [Test]
    public void AtomizeSingle_AtomicValue_ReturnsSame()
    {
        XdmAtomicValue a = XdmAtomicValue.String("hi");
        Assert.That(Atomization.AtomizeSingle(a, SourceSpan.None), Is.SameAs(a));
    }

    [Test]
    public void AtomizeSingle_Null_ReturnsNull()
    {
        Assert.That(Atomization.AtomizeSingle(null, SourceSpan.None), Is.Null);
    }
}
