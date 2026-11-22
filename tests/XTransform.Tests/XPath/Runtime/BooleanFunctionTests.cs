// P2-08: Boolean function tests (F&O §9 / §15.1.1).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for fn:true, fn:false, fn:boolean, and fn:not.
/// F&amp;O §9.1.1, §9.1.2, §9.3.3, §15.1.1.
/// </summary>
[TestFixture]
internal sealed class BooleanFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static bool BoolResult(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = ce.Root.Iterate(NewRoot());
        XdmItem? item;
        while ((item = iter.Next()) is not null) { items.Add(item); }

        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton boolean for: {expr}");
        return ((XdmAtomicValue)items[0]).AsBoolean();
    }

    // ── fn:true / fn:false ────────────────────────────────────────────────────

    [Test]
    public void FnTrue_ReturnsTrue()
    {
        Assert.That(BoolResult("fn:true()"), Is.True);
    }

    [Test]
    public void FnFalse_ReturnsFalse()
    {
        Assert.That(BoolResult("fn:false()"), Is.False);
    }

    // ── fn:boolean ────────────────────────────────────────────────────────────

    [TestCase("fn:boolean(0)",     false)]
    [TestCase("fn:boolean(1)",     true)]
    [TestCase("fn:boolean('')",    false)]
    [TestCase("fn:boolean('x')",   true)]
    [TestCase("fn:boolean('false')", true)]  // non-empty string → true
    [TestCase("fn:boolean(())",    false)]
    [TestCase("fn:boolean(0.0e0)", false)]
    [TestCase("fn:boolean(1.5e0)", true)]
    public void FnBoolean_Table(string expr, bool expected)
    {
        Assert.That(BoolResult(expr), Is.EqualTo(expected));
    }

    // ── fn:not ────────────────────────────────────────────────────────────────

    [TestCase("fn:not(1)",   false)]
    [TestCase("fn:not(0)",   true)]
    [TestCase("fn:not(())",  true)]
    [TestCase("fn:not('')",  true)]
    [TestCase("fn:not('x')", false)]
    public void FnNot_Table(string expr, bool expected)
    {
        Assert.That(BoolResult(expr), Is.EqualTo(expected));
    }
}
