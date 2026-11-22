// P2-08: FunctionLibrary infrastructure tests.

using System.Collections.Immutable;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <see cref="FunctionLibrary"/> infrastructure:
/// compile-time resolution, arity checks, and <see cref="BuiltInFunctionLibrary"/> basics.
/// </summary>
[TestFixture]
internal sealed class FunctionLibraryTests
{
    // ── Compile-time resolution errors ────────────────────────────────────────

    [Test]
    public void UnknownFunction_ThrowsXPST0017_AtCompileTime()
    {
        // Arrange
        string expr = "fn:no-such-function()";

        // Act / Assert
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                expr,
                SourceSpan.None,
                new DictionaryNamespaceResolver(
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["fn"] = BuiltInFunctionLibrary.FnNamespace,
                    }),
                BuiltInFunctionLibrary.Instance))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0017));
    }

    [Test]
    public void ArityMismatch_ThrowsXPST0017()
    {
        // Arrange — fn:true() takes 0 args; calling with 1 is an arity error.
        string expr = "fn:true(1)";

        // Act / Assert
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                expr,
                SourceSpan.None,
                new DictionaryNamespaceResolver(
                    new System.Collections.Generic.Dictionary<string, string>
                    {
                        ["fn"] = BuiltInFunctionLibrary.FnNamespace,
                    }),
                BuiltInFunctionLibrary.Instance))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0017));
    }

    // ── BuiltInFunctionLibrary.TryResolve ─────────────────────────────────────

    [Test]
    public void BuiltInFunctionLibrary_TryResolve_KnownFunction_ReturnsSignature()
    {
        // Arrange
        QName trueName = new QName("fn", BuiltInFunctionLibrary.FnNamespace, "true");

        // Act
        bool found = BuiltInFunctionLibrary.Instance.TryResolve(trueName, 0, out FunctionSignature? sig);

        // Assert
        Assert.That(found, Is.True);
        Assert.That(sig, Is.Not.Null);
        Assert.That(sig!.Name, Is.EqualTo(trueName));
        Assert.That(sig.Arity, Is.EqualTo(0));
    }

    [Test]
    public void BuiltInFunctionLibrary_TryResolve_UnknownFunction_ReturnsFalse()
    {
        // Arrange
        QName unknown = new QName("fn", BuiltInFunctionLibrary.FnNamespace, "no-such-fn");

        // Act
        bool found = BuiltInFunctionLibrary.Instance.TryResolve(unknown, 0, out FunctionSignature? sig);

        // Assert
        Assert.That(found, Is.False);
        Assert.That(sig, Is.Null);
    }

    // ── CompileTimeOnly signatures still usable at compile time ──────────────

    [Test]
    public void FunctionSignature_CompileTimeOnly_UsableByCompiler()
    {
        // AcceptAllFunctionLibrary uses CompileTimeOnly internally.
        // Compiling expressions with arbitrary function calls should succeed.
        CompiledExpression ce = XPathCompiler.ParseAndCompile(
            "some-fn(1, 2)",
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            AcceptAllFunctionLibrary.Instance);

        // Assert: compile succeeded; root is a FunctionCallNode.
        Assert.That(ce.Root, Is.Not.Null);
    }
}
