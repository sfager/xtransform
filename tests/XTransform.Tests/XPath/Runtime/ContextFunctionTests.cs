// P2-08: Context function tests (F&O §15.2 / §16).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for fn:position, fn:last, fn:current-dateTime, fn:current-date,
/// fn:current-time, fn:implicit-timezone, fn:default-collation.
/// F&amp;O §15.2 / §16.
/// </summary>
[TestFixture]
internal sealed class ContextFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static List<XdmItem> Drain(IXdmIterator iter)
    {
        List<XdmItem> items = new List<XdmItem>();
        XdmItem? item;
        while ((item = iter.Next()) is not null) { items.Add(item); }
        return items;
    }

    private static XdmAtomicValue AtomicResult(string expr, DynamicContext? ctx = null)
    {
        List<XdmItem> items = Drain(Compile(expr).Root.Iterate(ctx ?? NewRoot()));
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton for: {expr}");
        return (XdmAtomicValue)items[0];
    }

    // ── fn:position() ─────────────────────────────────────────────────────────

    [Test]
    public void FnPosition_InsidePredicate_ReturnsCorrectPosition()
    {
        // (10, 20, 30)[position() = 2] → (20)
        List<XdmItem> result = Drain(Compile("(10, 20, 30)[position() = 2]").Root.Iterate(NewRoot()));

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsInt64(), Is.EqualTo(20L));
    }

    [Test]
    public void FnLast_InsidePredicate_ReturnsLastItem()
    {
        // (10, 20, 30)[last()] → (30)
        List<XdmItem> result = Drain(Compile("(10, 20, 30)[last()]").Root.Iterate(NewRoot()));

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsInt64(), Is.EqualTo(30L));
    }

    [Test]
    public void FnPosition_OutsideFocus_ThrowsXPDY0002()
    {
        // position() with no focus → XPDY0002
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:position()").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }

    [Test]
    public void FnLast_OutsideFocus_ThrowsXPDY0002()
    {
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:last()").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }

    // ── fn:current-dateTime ───────────────────────────────────────────────────

    [Test]
    public void FnCurrentDateTime_ReturnsMatchingRunStateValue()
    {
        // Arrange
        XsdDateTime pinned = new XsdDateTime(
            XsdDateTimeKind.DateTime, 2026, 1, 15, 10, 30, default, timezoneMinutes: 0);
        RunState run = new RunState(currentDateTime: pinned);
        DynamicContext ctx = DynamicContext.Root(run);

        // Act
        XdmAtomicValue result = AtomicResult("fn:current-dateTime()", ctx);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DateTime));
        Assert.That(result.AsDateTime(), Is.EqualTo(pinned));
    }

    // ── fn:current-date ───────────────────────────────────────────────────────

    [Test]
    public void FnCurrentDate_ReturnsDateKind()
    {
        // Arrange
        XsdDateTime pinned = new XsdDateTime(
            XsdDateTimeKind.DateTime, 2026, 11, 21, 8, 0, default, timezoneMinutes: 0);
        RunState run = new RunState(currentDateTime: pinned);
        DynamicContext ctx = DynamicContext.Root(run);

        // Act
        XdmAtomicValue result = AtomicResult("fn:current-date()", ctx);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Date));
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.Date));
        Assert.That(dt.Year, Is.EqualTo(2026));
        Assert.That(dt.Month, Is.EqualTo(11));
        Assert.That(dt.Day, Is.EqualTo(21));
    }

    // ── fn:current-time ───────────────────────────────────────────────────────

    [Test]
    public void FnCurrentTime_ReturnsTimeKind()
    {
        // Arrange
        XsdDateTime pinned = new XsdDateTime(
            XsdDateTimeKind.DateTime, 2026, 11, 21, 14, 30, new XsdDecimal(45m), timezoneMinutes: 0);
        RunState run = new RunState(currentDateTime: pinned);
        DynamicContext ctx = DynamicContext.Root(run);

        // Act
        XdmAtomicValue result = AtomicResult("fn:current-time()", ctx);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Time));
        XsdDateTime dt = result.AsDateTime();
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.Time));
        Assert.That(dt.Hour, Is.EqualTo(14));
        Assert.That(dt.Minute, Is.EqualTo(30));
    }

    // ── fn:implicit-timezone ──────────────────────────────────────────────────

    [Test]
    public void FnImplicitTimezone_ReturnsDayTimeDuration()
    {
        // Arrange — 60 minutes = +01:00
        RunState run = new RunState(implicitTimezoneMinutes: 60);
        DynamicContext ctx = DynamicContext.Root(run);

        // Act
        XdmAtomicValue result = AtomicResult("fn:implicit-timezone()", ctx);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
        XsdDuration dur = result.AsDuration();
        Assert.That(dur.Months, Is.EqualTo(0));
        Assert.That((decimal)dur.Seconds.Value, Is.EqualTo(3600m));  // 60 * 60
    }

    // ── fn:default-collation ──────────────────────────────────────────────────

    [Test]
    public void FnDefaultCollation_ReturnsCodepointUri()
    {
        XdmAtomicValue result = AtomicResult("fn:default-collation()");
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.String));
        Assert.That(result.GetStringValue(),
            Is.EqualTo("http://www.w3.org/2005/xpath-functions/collation/codepoint"));
    }
}
