// Tests for XPathCompiler static error reporting.

using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Compiler;

/// <summary>
/// Verifies that the XPath compiler raises the correct W3C error codes for static errors.
/// </summary>
[TestFixture]
internal sealed class XPathCompilerErrorTests
{
    // ── Mock library with a known signature ───────────────────────────────────

    /// <summary>
    /// A test function library that knows exactly one function: xs:string(value) → xs:string.
    /// Used to test arity-mismatch detection.
    /// </summary>
    private sealed class TestFunctionLibrary : FunctionLibrary
    {
        private readonly System.Collections.Generic.Dictionary<string, FunctionSignature> _sigs;

        internal TestFunctionLibrary(params FunctionSignature[] signatures)
        {
            _sigs = new System.Collections.Generic.Dictionary<string, FunctionSignature>(
                System.StringComparer.Ordinal);
            foreach (FunctionSignature sig in signatures)
            {
                _sigs[sig.Name.ClarkName + "/" + sig.Arity] = sig;
            }
        }

        public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
        {
            string key = name.ClarkName + "/" + arity;
            if (_sigs.TryGetValue(key, out FunctionSignature? sig))
            {
                signature = sig;
                return true;
            }

            signature = null;
            return false;
        }
    }

    private static readonly QName s_xsStringName =
        new QName("xs", "http://www.w3.org/2001/XMLSchema", "string");

    private static readonly FunctionSignature s_xsStringArity1 = new FunctionSignature(
        s_xsStringName,
        1,
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ExactlyOne),
        System.Collections.Immutable.ImmutableArray.Create(SequenceType.AnySequence));

    // ── XPST0008 ──────────────────────────────────────────────────────────────

    [Test]
    public void Compile_UndeclaredVariable_ThrowsXPST0008()
    {
        // Arrange — no external variables provided
        // Act / Assert
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                "$undeclared",
                SourceSpan.None,
                DictionaryNamespaceResolver.Default,
                EmptyFunctionLibrary.Instance));

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0008));
    }

    [Test]
    public void Compile_UndeclaredVariable_ErrorMessageContainsName()
    {
        // Arrange / Act
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                "$myVar",
                SourceSpan.None,
                DictionaryNamespaceResolver.Default,
                EmptyFunctionLibrary.Instance));

        // Assert
        Assert.That(ex.Message, Does.Contain("myVar"));
    }

    // ── XPST0017 ──────────────────────────────────────────────────────────────

    [Test]
    public void Compile_UnknownFunction_ThrowsXPST0017()
    {
        // Arrange — empty function library
        // Act / Assert
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                "foo()",
                SourceSpan.None,
                DictionaryNamespaceResolver.Default,
                EmptyFunctionLibrary.Instance));

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0017));
    }

    [Test]
    public void Compile_KnownFunctionWrongArity_ThrowsXPST0017()
    {
        // Arrange — library knows xs:string/1 but we call it with 2 args
        TestFunctionLibrary lib = new TestFunctionLibrary(s_xsStringArity1);

        // Act / Assert
        XPathStaticException ex = Assert.Throws<XPathStaticException>(
            () => XPathCompiler.ParseAndCompile(
                "xs:string(\"a\", \"b\")",
                SourceSpan.None,
                DictionaryNamespaceResolver.Default,
                lib));

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPST0017));
    }

    [Test]
    public void Compile_KnownFunctionCorrectArity_DoesNotThrow()
    {
        // Arrange
        TestFunctionLibrary lib = new TestFunctionLibrary(s_xsStringArity1);

        // Act / Assert — should compile cleanly
        CompiledExpression result = XPathCompiler.ParseAndCompile(
            "xs:string(\"hello\")",
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            lib);

        Assert.That(result.Root, Is.Not.Null);
    }
}
