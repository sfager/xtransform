// P2-05: Arithmetic operator evaluation tests.
// XPath 2.0 §3.4 — arithmetic expressions; F&O §6.2 — numeric type promotion.

using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

[TestFixture]
internal sealed class ArithmeticEvaluationTests
{
    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static XdmAtomicValue Eval(string expr)
    {
        CompiledExpression ce = Compile(expr);
        XdmItem? item = ce.Root.EvaluateItem(NewRoot());
        Assert.That(item, Is.Not.Null, $"Expression '{expr}' returned empty sequence");
        return (XdmAtomicValue)item!;
    }

    // ── Table-driven arithmetic cases ─────────────────────────────────────────

    private static readonly object[][] ArithmeticCases =
    {
        // Arrange: basic integer arithmetic
        new object[] { "1 + 2",          XdmTypeCode.Integer, "3"     },
        new object[] { "1 - 2",          XdmTypeCode.Integer, "-1"    },
        new object[] { "3 * 4",          XdmTypeCode.Integer, "12"    },
        new object[] { "10 idiv 3",      XdmTypeCode.Integer, "3"     },
        new object[] { "10 mod 3",       XdmTypeCode.Integer, "1"     },
        new object[] { "-7 mod 3",       XdmTypeCode.Integer, "-1"    },

        // Precedence
        new object[] { "1 + 2 * 3",      XdmTypeCode.Integer, "7"     },
        new object[] { "(1 + 2) * 3",    XdmTypeCode.Integer, "9"     },

        // Integer div → decimal
        new object[] { "10 div 4",       XdmTypeCode.Decimal, "2.5"   },
        new object[] { "5 div 2",        XdmTypeCode.Decimal, "2.5"   },
        new object[] { "6 div 3",        XdmTypeCode.Decimal, "2"     },

        // Decimal promotion
        new object[] { "1.5 + 1",        XdmTypeCode.Decimal, "2.5"   },
        new object[] { "1.5 - 0.5",      XdmTypeCode.Decimal, "1"     },
        new object[] { "2.5 * 2",        XdmTypeCode.Decimal, "5"     },

        // Double promotion
        new object[] { "1E2 + 1",        XdmTypeCode.Double,  "101"   },
        new object[] { "1 + 1E0",        XdmTypeCode.Double,  "2"     },
        new object[] { "1E0 * 2",        XdmTypeCode.Double,  "2"     },
    };

    [TestCaseSource(nameof(ArithmeticCases))]
    public void Arithmetic_Table_ProducesExpectedResult(string expr, XdmTypeCode expectedType, string expectedCanonical)
    {
        // Act
        XdmAtomicValue result = Eval(expr);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(expectedType), $"Wrong type for '{expr}'");
        Assert.That(result.GetStringValue(), Is.EqualTo(expectedCanonical), $"Wrong value for '{expr}'");
    }

    // ── Unary cases ───────────────────────────────────────────────────────────

    [TestCase("-5",    XdmTypeCode.Integer, "-5")]
    [TestCase("--5",   XdmTypeCode.Integer, "5")]
    [TestCase("-5.5",  XdmTypeCode.Decimal, "-5.5")]
    [TestCase("- -5",  XdmTypeCode.Integer, "5")]
    public void Unary_Table_ProducesExpectedResult(string expr, XdmTypeCode expectedType, string expectedCanonical)
    {
        // Act
        XdmAtomicValue result = Eval(expr);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(expectedType), $"Wrong type for '{expr}'");
        Assert.That(result.GetStringValue(), Is.EqualTo(expectedCanonical), $"Wrong value for '{expr}'");
    }

    // ── IEEE float/double edge cases ──────────────────────────────────────────

    [Test]
    public void Double_ZeroDivZero_IsNaN()
    {
        // Arrange / Act
        XdmAtomicValue result = Eval("0E0 div 0E0");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(double.IsNaN(result.AsDouble()), Is.True);
    }

    [Test]
    public void Double_OneDivZero_IsPositiveInfinity()
    {
        // Arrange / Act
        XdmAtomicValue result = Eval("1E0 div 0E0");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(double.IsPositiveInfinity(result.AsDouble()), Is.True);
    }

    [Test]
    public void Double_NegOneDivZero_IsNegativeInfinity()
    {
        // Arrange / Act
        XdmAtomicValue result = Eval("-1E0 div 0E0");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(double.IsNegativeInfinity(result.AsDouble()), Is.True);
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Test]
    public void IntegerDivByZero_ThrowsFOAR0001()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Eval("1 div 0"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOAR0001));
    }

    [Test]
    public void IntegerIdivByZero_ThrowsFOAR0001()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Eval("5 idiv 0"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOAR0001));
    }

    [Test]
    public void IntegerModByZero_ThrowsFOAR0001()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Eval("5 mod 0"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOAR0001));
    }

    [Test]
    public void NonNumericAdd_ThrowsXPTY0004()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Eval("'a' + 1"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // ── Empty-sequence propagation ────────────────────────────────────────────

    [Test]
    public void Arithmetic_EmptyLeft_ReturnsEmpty()
    {
        // Arrange
        CompiledExpression ce = Compile("() + 1");

        // Act
        XdmItem? result = ce.Root.EvaluateItem(NewRoot());

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Arithmetic_EmptyRight_ReturnsEmpty()
    {
        // Arrange
        CompiledExpression ce = Compile("1 + ()");

        // Act
        XdmItem? result = ce.Root.EvaluateItem(NewRoot());

        // Assert
        Assert.That(result, Is.Null);
    }

    // ── Large integer (BigInteger) ─────────────────────────────────────────────

    [Test]
    public void Integer_LargeMultiply_UsesBigInteger()
    {
        // Arrange: 2^63 - 1 = 9223372036854775807 (long.MaxValue)
        // Multiplying by 2 would overflow long; BigInteger handles it.
        string expr = "9223372036854775807 * 2";
        BigInteger expected = new BigInteger(long.MaxValue) * 2;

        // Act
        XdmAtomicValue result = Eval(expr);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(result.AsBigInteger(), Is.EqualTo(expected));
    }
}
