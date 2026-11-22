// P2-10: Numeric and aggregate function tests (F&O §6.4 and §15.4).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §6.4 single-item numeric functions and §15.4 aggregate functions:
/// fn:abs, fn:ceiling, fn:floor, fn:round, fn:round-half-to-even,
/// fn:sum, fn:avg, fn:min, fn:max, fn:count, fn:number.
/// </summary>
[TestFixture]
internal sealed class NumericFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static List<XdmItem> DrainExpr(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = ce.Root.Iterate(NewRoot());
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }

        return items;
    }

    private static XdmAtomicValue SingleAtomic(string expr)
    {
        List<XdmItem> items = DrainExpr(expr);
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton result for: {expr}");
        return (XdmAtomicValue)items[0];
    }

    private static double DoubleResult(string expr) => SingleAtomic(expr).AsDouble();

    private static long IntegerResult(string expr) => SingleAtomic(expr).AsInt64();

    private static decimal DecimalResult(string expr) => SingleAtomic(expr).AsDecimal().Value;

    // =========================================================================
    // fn:abs — F&O §6.4.1
    // =========================================================================

    [Test]
    public void FnAbs_NegativeDecimal_ReturnsPositiveDecimal()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:abs(-3.5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(3.5m));
    }

    [Test]
    public void FnAbs_NegativeInteger_ReturnsPositiveInteger()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:abs(-5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(result.AsInt64(), Is.EqualTo(5L));
    }

    [Test]
    public void FnAbs_NegativeDouble_ReturnsPositiveDouble()
    {
        // Arrange / Act — -1.5e2 = -150.0 as xs:double
        XdmAtomicValue result = SingleAtomic("fn:abs(-1.5e2)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(result.AsDouble(), Is.EqualTo(150.0));
    }

    [Test]
    public void FnAbs_Empty_ReturnsEmpty()
    {
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:abs(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FnAbs_NegativeZeroDouble_ReturnsPositiveZero()
    {
        // F&O §6.4.1: abs(-0.0e0) → 0.0e0
        // Arrange / Act
        double result = DoubleResult("fn:abs(-0.0e0)");

        // Assert
        Assert.That(result, Is.EqualTo(0.0));
        // Confirm it is positive zero (not negative zero)
        Assert.That(1.0 / result, Is.Positive.Or.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void FnAbs_NonNumeric_RaisesXPTY0004()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:abs('x')"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // =========================================================================
    // fn:ceiling — F&O §6.4.2
    // =========================================================================

    [Test]
    public void FnCeiling_PositiveDecimal_ReturnsCeiling()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:ceiling(1.5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(2m));
    }

    [Test]
    public void FnCeiling_NegativeDecimal_ReturnsCeiling()
    {
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:ceiling(-1.5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(-1m));
    }

    [Test]
    public void FnCeiling_Integer_ReturnsIdentity()
    {
        // F&O §6.4.2: xs:integer ceiling is identity.
        // Arrange / Act
        long result = IntegerResult("fn:ceiling(5)");

        // Assert
        Assert.That(result, Is.EqualTo(5L));
    }

    [Test]
    public void FnCeiling_Empty_ReturnsEmpty()
    {
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:ceiling(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // fn:floor — F&O §6.4.3
    // =========================================================================

    [Test]
    public void FnFloor_NegativeDecimal_ReturnsFloor()
    {
        // Arrange / Act — acceptance test from spec: floor(-1.5) → -2
        XdmAtomicValue result = SingleAtomic("fn:floor(-1.5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(-2m));
    }

    [Test]
    public void FnFloor_PositiveDecimal_ReturnsFloor()
    {
        // Arrange / Act
        decimal result = DecimalResult("fn:floor(1.5)");

        // Assert
        Assert.That(result, Is.EqualTo(1m));
    }

    [Test]
    public void FnFloor_Empty_ReturnsEmpty()
    {
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:floor(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // fn:round — F&O §6.4.4 (half towards +∞, NOT banker's)
    // =========================================================================

    [TestCase("fn:round(2.5)", 3)]
    [TestCase("fn:round(-2.5)", -2)]   // half towards +∞: -2.5 → -2 (not -3)
    [TestCase("fn:round(2.4)", 2)]
    [TestCase("fn:round(-2.4)", -2)]
    [TestCase("fn:round(1.5)", 2)]
    [TestCase("fn:round(-1.5)", -1)]  // half towards +∞
    public void FnRound_Decimal_HalfToPositiveInfinity(string expr, int expected)
    {
        // F&O §6.4.4: round half towards +∞
        // Arrange / Act
        decimal result = DecimalResult(expr);

        // Assert
        Assert.That(result, Is.EqualTo((decimal)expected));
    }

    [Test]
    public void FnRound_Integer_ReturnsIdentity()
    {
        // F&O §6.4.4: xs:integer round is identity.
        // Arrange / Act
        long result = IntegerResult("fn:round(42)");

        // Assert
        Assert.That(result, Is.EqualTo(42L));
    }

    [Test]
    public void FnRound_Double_NaN_ReturnsNaN()
    {
        // F&O §6.4.4: NaN → NaN
        // Arrange / Act
        double result = DoubleResult("fn:round(0e0 div 0e0)");

        // Assert
        Assert.That(double.IsNaN(result), Is.True);
    }

    [Test]
    public void FnRound_Double_PositiveInfinity_ReturnsPositiveInfinity()
    {
        // F&O §6.4.4: +INF → +INF
        // Arrange / Act
        double result = DoubleResult("fn:round(1e0 div 0e0)");

        // Assert
        Assert.That(double.IsPositiveInfinity(result), Is.True);
    }

    [Test]
    public void FnRound_Empty_ReturnsEmpty()
    {
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:round(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // fn:round-half-to-even — F&O §6.4.5 (banker's rounding)
    // =========================================================================

    [TestCase("fn:round-half-to-even(2.5)", 2)]   // half-to-even: 2.5 → 2 (nearest even)
    [TestCase("fn:round-half-to-even(3.5)", 4)]   // half-to-even: 3.5 → 4 (nearest even)
    [TestCase("fn:round-half-to-even(1.5)", 2)]   // half-to-even: 1.5 → 2 (nearest even)
    [TestCase("fn:round-half-to-even(0.5)", 0)]   // half-to-even: 0.5 → 0 (nearest even)
    public void FnRoundHalfToEven_1Arg_BankersRounding(string expr, int expected)
    {
        // F&O §6.4.5: banker's rounding (MidpointRounding.ToEven)
        // Arrange / Act
        decimal result = DecimalResult(expr);

        // Assert
        Assert.That(result, Is.EqualTo((decimal)expected));
    }

    [Test]
    public void FnRoundHalfToEven_2Arg_RoundsToDecimalPlaces()
    {
        // F&O §6.4.5: round-half-to-even(2.345, 2) → 2.34 (banker's at precision 2)
        // Arrange / Act
        decimal result = DecimalResult("fn:round-half-to-even(2.345, 2)");

        // Assert
        Assert.That(result, Is.EqualTo(2.34m));
    }

    [Test]
    public void FnRoundHalfToEven_Integer_ReturnsIdentity()
    {
        // F&O §6.4.5: xs:integer with non-negative precision → identity
        // Arrange / Act
        long result = IntegerResult("fn:round-half-to-even(7)");

        // Assert
        Assert.That(result, Is.EqualTo(7L));
    }

    [Test]
    public void FnRoundHalfToEven_Empty_ReturnsEmpty()
    {
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:round-half-to-even(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // fn:sum — F&O §15.4.9
    // =========================================================================

    [Test]
    public void FnSum_Empty_ReturnsIntegerZero()
    {
        // F&O §15.4.9: sum(()) → xs:integer(0)
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:sum(())");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(result.AsInt64(), Is.EqualTo(0L));
    }

    [Test]
    public void FnSum_Empty_WithSecondArg_ReturnsSecondArg()
    {
        // F&O §15.4.9: sum((), 42) → 42
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:sum((), 42)");

        // Assert
        Assert.That(result.AsInt64(), Is.EqualTo(42L));
    }

    [Test]
    public void FnSum_Empty_WithEmptySecondArg_ReturnsEmpty()
    {
        // F&O §15.4.9: sum((), ()) → empty sequence
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:sum((), ())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FnSum_Integers_ReturnsSumInteger()
    {
        // F&O §15.4.9: sum((1, 2, 3)) → 6
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:sum((1, 2, 3))");

        // Assert
        Assert.That(result.AsInt64(), Is.EqualTo(6L));
    }

    [Test]
    public void FnSum_Decimals_ReturnsSumDecimal()
    {
        // F&O §15.4.9: sum((1.5, 2.5)) → 4.0 xs:decimal
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:sum((1.5, 2.5))");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(4.0m));
    }

    [Test]
    public void FnSum_MixedPromotion_ReturnsDouble()
    {
        // F&O §15.4.9: sum((1, 1.5, 1e0)) → 3.5 xs:double (promotion to double)
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:sum((1, 1.5, 1e0))");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(result.AsDouble(), Is.EqualTo(3.5));
    }

    [Test]
    public void FnSum_NonNumeric_RaisesXPTY0004()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:sum((1, 'x'))"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // =========================================================================
    // fn:avg — F&O §15.4.4
    // =========================================================================

    [Test]
    public void FnAvg_Empty_ReturnsEmpty()
    {
        // F&O §15.4.4: avg(()) → empty sequence
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:avg(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FnAvg_OddCount_ReturnsExactAvg()
    {
        // F&O §15.4.4: avg((1, 2, 3)) → 2
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:avg((1, 2, 3))");

        // Assert — 6/3 = 2 (xs:decimal because integer / integer = decimal)
        Assert.That(result.AsDecimal().Value, Is.EqualTo(2m));
    }

    [Test]
    public void FnAvg_EvenCount_ReturnsDecimal()
    {
        // F&O §15.4.4: avg((1, 2, 3, 4)) → 2.5
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:avg((1, 2, 3, 4))");

        // Assert — 10/4 = 2.5 (xs:decimal because integer / integer = decimal)
        Assert.That(result.AsDecimal().Value, Is.EqualTo(2.5m));
    }

    // =========================================================================
    // fn:min — F&O §15.4.5
    // =========================================================================

    [Test]
    public void FnMin_Integers_ReturnsMin()
    {
        // F&O §15.4.5: min((3, 1, 2)) → 1
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:min((3, 1, 2))");

        // Assert
        Assert.That(result.AsInt64(), Is.EqualTo(1L));
    }

    [Test]
    public void FnMin_Empty_ReturnsEmpty()
    {
        // F&O §15.4.5: min(()) → empty sequence
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:min(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FnMin_Mixed_PromotesToDecimal()
    {
        // min((1.5, 2, 3)) → 1.5 (xs:decimal after promotion from integer)
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:min((1.5, 2, 3))");

        // Assert
        Assert.That(result.AsDecimal().Value, Is.EqualTo(1.5m));
    }

    // =========================================================================
    // fn:max — F&O §15.4.6
    // =========================================================================

    [Test]
    public void FnMax_Integers_ReturnsMax()
    {
        // F&O §15.4.6: max((3, 1, 2)) → 3
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:max((3, 1, 2))");

        // Assert
        Assert.That(result.AsInt64(), Is.EqualTo(3L));
    }

    [Test]
    public void FnMax_Empty_ReturnsEmpty()
    {
        // F&O §15.4.6: max(()) → empty sequence
        // Arrange / Act
        List<XdmItem> items = DrainExpr("fn:max(())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    [Test]
    public void FnMax_MixedDecimalsAndIntegers_ReturnsMaxDecimal()
    {
        // F&O §15.4.6: max((1.5, 2, 1.8)) → 2 (type xs:decimal after promotion)
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:max((1.5, 2, 1.8))");

        // Assert — 2 is the maximum, promoted to xs:decimal
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
        Assert.That(result.AsDecimal().Value, Is.EqualTo(2m));
    }

    // =========================================================================
    // fn:count — F&O §15.4.3
    // =========================================================================

    [Test]
    public void FnCount_Empty_ReturnsZero()
    {
        // F&O §15.4.3: count(()) → 0
        // Arrange / Act
        long result = IntegerResult("fn:count(())");

        // Assert
        Assert.That(result, Is.EqualTo(0L));
    }

    [Test]
    public void FnCount_ThreeItems_ReturnsThree()
    {
        // F&O §15.4.3: count((1, 2, 3)) → 3
        // Arrange / Act
        long result = IntegerResult("fn:count((1, 2, 3))");

        // Assert
        Assert.That(result, Is.EqualTo(3L));
    }

    // =========================================================================
    // fn:number — F&O §6.4.6
    // =========================================================================

    [Test]
    public void FnNumber_ValidDoubleString_ReturnsDouble()
    {
        // F&O §6.4.6: number("3.14") → 3.14 xs:double
        // Arrange / Act
        double result = DoubleResult("fn:number('3.14')");

        // Assert
        Assert.That(result, Is.EqualTo(3.14));
    }

    [Test]
    public void FnNumber_InvalidString_ReturnsNaN()
    {
        // F&O §6.4.6: number("foo") → NaN
        // Arrange / Act
        double result = DoubleResult("fn:number('foo')");

        // Assert
        Assert.That(double.IsNaN(result), Is.True);
    }

    [Test]
    public void FnNumber_EmptySequence_ReturnsNaN()
    {
        // F&O §6.4.6: number(()) → NaN
        // Arrange / Act
        double result = DoubleResult("fn:number(())");

        // Assert
        Assert.That(double.IsNaN(result), Is.True);
    }

    [Test]
    public void FnNumber_BooleanTrue_ReturnsOne()
    {
        // F&O §6.4.6: boolean true → 1.0
        // Arrange / Act
        double result = DoubleResult("fn:number(fn:true())");

        // Assert
        Assert.That(result, Is.EqualTo(1.0));
    }

    [Test]
    public void FnNumber_BooleanFalse_ReturnsZero()
    {
        // F&O §6.4.6: boolean false → 0.0
        // Arrange / Act
        double result = DoubleResult("fn:number(fn:false())");

        // Assert
        Assert.That(result, Is.EqualTo(0.0));
    }

    [Test]
    public void FnNumber_Integer_ReturnsDouble()
    {
        // F&O §6.4.6: number(5) → 5.0 xs:double
        // Arrange / Act
        XdmAtomicValue result = SingleAtomic("fn:number(5)");

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Double));
        Assert.That(result.AsDouble(), Is.EqualTo(5.0));
    }

    [Test]
    public void FnNumber_InfString_ReturnsPositiveInfinity()
    {
        // F&O §6.4.6: number("INF") → +Infinity
        // Arrange / Act
        double result = DoubleResult("fn:number('INF')");

        // Assert
        Assert.That(double.IsPositiveInfinity(result), Is.True);
    }
}
