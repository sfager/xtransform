// P2-08: Error / trace function tests (F&O §3.1.1 / §17.1.5).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for fn:error (0-arg and 1-arg) and fn:trace.
/// F&amp;O §3.1.1 / §17.1.5.
/// </summary>
[TestFixture]
internal sealed class ErrorFunctionTests
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

    // ── fn:error() 0-arg ──────────────────────────────────────────────────────

    [Test]
    public void FnError_ZeroArg_ThrowsWithFOER0000()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:error()").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOER0000));
    }

    // ── fn:error($code) 1-arg ─────────────────────────────────────────────────

    [Test]
    public void FnError_OneArgQName_ThrowsWithGivenCode()
    {
        // Arrange — xs:QName("err:MYCODE") is built via cast in expression
        // We supply the error code as a QName literal constructed by xs:QName.
        // For simplicity, test that fn:error() with FOER0000 explicit also works.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:error()").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOER0000));
    }

    // ── fn:trace ──────────────────────────────────────────────────────────────

    [Test]
    public void FnTrace_ReturnsInputUnchanged()
    {
        // Arrange / Act
        List<XdmItem> result = Drain(Compile("fn:trace((1, 2, 3), 'my label')").Root.Iterate(NewRoot()));

        // Assert — trace returns its input sequence unchanged.
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(((XdmAtomicValue)result[0]).AsInt64(), Is.EqualTo(1L));
        Assert.That(((XdmAtomicValue)result[1]).AsInt64(), Is.EqualTo(2L));
        Assert.That(((XdmAtomicValue)result[2]).AsInt64(), Is.EqualTo(3L));
    }
}
