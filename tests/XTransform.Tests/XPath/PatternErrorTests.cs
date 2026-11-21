using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

// XSLT 2.0 §5.5.2 — patterns that are valid XPath but invalid as patterns
namespace XTransform.Tests.XPath;

/// <summary>
/// Verifies that constructs that are valid XPath 2.0 but invalid as XSLT 2.0 patterns
/// raise <see cref="XsltStaticException"/> with <see cref="XsltErrors.XTSE0340"/>.
/// </summary>
[TestFixture]
internal sealed class PatternErrorTests
{
    private static readonly IStaticNamespaceResolver Ns = DictionaryNamespaceResolver.Default;

    private static XsltStaticException ParseAndExpectError(string pattern)
    {
        return Assert.Throws<XsltStaticException>(
            () => XPathParser.ParsePattern(pattern, SourceSpan.None, Ns))!;
    }

    // ── Disallowed axes ────────────────────────────────────────────────────────

    [TestCase("../x",             TestName = "ParentDotDot")]
    [TestCase("parent::x",        TestName = "ParentAxis")]
    [TestCase("following::x",     TestName = "FollowingAxis")]
    [TestCase("preceding::x",     TestName = "PrecedingAxis")]
    [TestCase("ancestor::x",      TestName = "AncestorAxis")]
    [TestCase("descendant::x",    TestName = "DescendantAxis")]
    [TestCase("self::x",          TestName = "SelfAxis")]
    [TestCase("following-sibling::x", TestName = "FollowingSiblingAxis")]
    [TestCase("preceding-sibling::x", TestName = "PrecedingSiblingAxis")]
    [TestCase("ancestor-or-self::x",  TestName = "AncestorOrSelfAxis")]
    [TestCase("descendant-or-self::x", TestName = "DescendantOrSelfAxis")]
    public void DisallowedAxis_RaisesXTSE0340(string pattern)
    {
        // Arrange / Act
        XsltStaticException ex = ParseAndExpectError(pattern);

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0340));
        Assert.That(ex.Errors[0].Location.Start, Is.GreaterThanOrEqualTo(0));
    }

    // ── Other top-level invalid constructs ────────────────────────────────────

    [TestCase("1 + 2",   TestName = "ArithmeticAtTopLevel")]
    [TestCase("$var",    TestName = "VariableRefAtTopLevel")]
    [TestCase("func()",  TestName = "FunctionCallAtTopLevel")]
    public void InvalidTopLevelConstruct_RaisesXTSE0340(string pattern)
    {
        // Arrange / Act
        XsltStaticException ex = ParseAndExpectError(pattern);

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0340));
        Assert.That(ex.Errors[0].Location.Start, Is.GreaterThanOrEqualTo(0));
    }

    // ── Predicates may contain full XPath (including other axes) ──────────────

    [Test]
    public void Predicate_WithFollowingSiblingAxis_IsValid()
    {
        // Arrange / Act — following-sibling is allowed INSIDE a predicate
        PatternAst ast = XPathParser.ParsePattern("foo[following-sibling::bar]", SourceSpan.None, Ns);

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Predicates.Length, Is.EqualTo(1));
    }

    [Test]
    public void Predicate_WithFunctionCall_IsValid()
    {
        // Arrange / Act — position() is allowed inside a predicate
        PatternAst ast = XPathParser.ParsePattern("foo[position() > 1]", SourceSpan.None, Ns);

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Predicates.Length, Is.EqualTo(1));
    }

    // ── Error has non-empty location ──────────────────────────────────────────

    [Test]
    public void Error_HasNonEmptyLocation()
    {
        // Arrange / Act
        XsltStaticException ex = ParseAndExpectError("parent::x");

        // Assert
        Assert.That(ex.Errors[0].Location.Start, Is.GreaterThanOrEqualTo(0));
    }
}
