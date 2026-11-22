// P2-05: Comparison operator evaluation tests.
// XPath 2.0 §3.5 — value and general comparisons.

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

[TestFixture]
internal sealed class ComparisonEvaluationTests
{
    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static bool EvalBool(string expr)
    {
        CompiledExpression ce = Compile(expr);
        XdmItem? item = ce.Root.EvaluateItem(NewRoot());
        Assert.That(item, Is.Not.Null, $"Expression '{expr}' returned empty sequence");
        return ((XdmAtomicValue)item!).AsBoolean();
    }

    // ── Value comparisons ─────────────────────────────────────────────────────

    private static readonly object[][] ValueComparisonCases =
    {
        // Integer eq
        new object[] { "1 eq 1",     true  },
        new object[] { "1 eq 2",     false },

        // Numeric promotion: integer eq decimal
        new object[] { "1 eq 1.0",   true  },
        new object[] { "1 eq 1.5",   false },

        // String eq
        new object[] { "'a' eq 'a'", true  },
        new object[] { "'a' eq 'b'", false },

        // String ordering
        new object[] { "'a' lt 'b'", true  },
        new object[] { "'b' lt 'a'", false },
        new object[] { "'a' le 'a'", true  },

        // Integer ordering
        new object[] { "2 gt 1",     true  },
        new object[] { "1 gt 2",     false },
        new object[] { "1 ge 1",     true  },
        new object[] { "1 ne 2",     true  },
        new object[] { "1 ne 1",     false },

        // Double comparison
        new object[] { "1E0 eq 1",   true  },
        new object[] { "1E0 lt 2",   true  },
    };

    [TestCaseSource(nameof(ValueComparisonCases))]
    public void ValueComparison_Table_ProducesExpectedResult(string expr, bool expected)
    {
        // Act
        bool result = EvalBool(expr);

        // Assert
        Assert.That(result, Is.EqualTo(expected), $"Wrong result for '{expr}'");
    }

    // ── General comparisons ───────────────────────────────────────────────────

    private static readonly object[][] GeneralComparisonCases =
    {
        // Existential: single values
        new object[] { "1 = 1",             true  },
        new object[] { "1 = 2",             false },
        new object[] { "1 = 1.0",           true  },

        // Sequences
        new object[] { "(1, 2, 3) = 3",     true  },
        new object[] { "(1, 2, 3) = 4",     false },
        new object[] { "(1, 2, 3) = (3, 4, 5)", true  },
        new object[] { "(1, 2, 3) = (4, 5, 6)", false },

        // != is true if any pair is not equal
        new object[] { "(1, 2, 3) != (3, 4, 5)", true  },
        new object[] { "(1, 1, 1) != (1, 1, 1)", false },

        // Empty sequence
        new object[] { "() = (1, 2, 3)",    false },
        new object[] { "(1, 2, 3) = ()",    false },

        // General ordering
        new object[] { "(1, 2, 3) < 5",     true  },
        new object[] { "(5, 6) < 3",        false },
        new object[] { "(1, 2) > 1",        true  },
    };

    [TestCaseSource(nameof(GeneralComparisonCases))]
    public void GeneralComparison_Table_ProducesExpectedResult(string expr, bool expected)
    {
        // Act
        bool result = EvalBool(expr);

        // Assert
        Assert.That(result, Is.EqualTo(expected), $"Wrong result for '{expr}'");
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Test]
    public void ValueComparison_IncompatibleTypes_ThrowsXPTY0004()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => EvalBool("'a' eq 1"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // ── Empty result for value comparison with empty operand ──────────────────

    [Test]
    public void ValueComparison_EmptyOperand_ReturnsEmptyNotFalse()
    {
        // Arrange
        CompiledExpression ce = Compile("1 eq ()");

        // Act
        XdmItem? result = ce.Root.EvaluateItem(NewRoot());

        // Assert: value comparison with empty operand yields empty sequence
        Assert.That(result, Is.Null, "1 eq () should yield empty sequence, not false");
    }

    // ── Logical operators ──────────────────────────────────────────────────────

    [TestCase("1 lt 2 and 3 lt 4", true)]
    [TestCase("1 lt 2 and 4 lt 3", false)]
    [TestCase("1 lt 2 or 4 lt 3",  true)]
    [TestCase("2 lt 1 or 4 lt 3",  false)]
    public void Logical_AndOr_ProducesExpectedResult(string expr, bool expected)
    {
        // Act
        bool result = EvalBool(expr);

        // Assert
        Assert.That(result, Is.EqualTo(expected), $"Wrong result for '{expr}'");
    }

    // ── Short-circuit ─────────────────────────────────────────────────────────

    [Test]
    public void And_ShortCircuit_FalseLeftSkipsRight()
    {
        // Arrange: `0 and .` — left EBV is false (0); right reads context item (would throw XPDY0002
        // if evaluated). With short-circuit, the right side is never evaluated.
        CompiledExpression ce = Compile("0 and .");

        // Act: no context item — if right is evaluated, XPDY0002 would be thrown
        bool result = ce.Root.EffectiveBooleanValue(NewRoot());

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Or_ShortCircuit_TrueLeftSkipsRight()
    {
        // Arrange: `1 or .` — left EBV is true (1); right reads context item (would throw XPDY0002).
        CompiledExpression ce = Compile("1 or .");

        // Act: no context item — if right is evaluated, XPDY0002 would be thrown
        bool result = ce.Root.EffectiveBooleanValue(NewRoot());

        // Assert
        Assert.That(result, Is.True);
    }
}
