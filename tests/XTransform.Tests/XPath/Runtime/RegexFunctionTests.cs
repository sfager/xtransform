// P2-13: Regex function tests (F&O §7.6).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §7.6 XPath 2.0 regular expression functions:
/// fn:matches, fn:replace, fn:tokenize.
/// </summary>
[TestFixture]
internal sealed class RegexFunctionTests
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

    private static bool BoolResult(string expr) => SingleAtomic(expr).AsBoolean();

    private static string StringResult(string expr) => SingleAtomic(expr).AsString();

    // =========================================================================
    // fn:matches — F&O §7.6.2
    // =========================================================================

    [Test]
    public void Matches_SimplePattern_ReturnsTrue()
    {
        // Arrange / Act / Assert
        Assert.That(BoolResult("fn:matches('abc', '^[a-z]+$')"), Is.True);
    }

    [Test]
    public void Matches_SimplePattern_ReturnsFalse()
    {
        // Arrange / Act / Assert
        Assert.That(BoolResult("fn:matches('abc', '^[A-Z]+$')"), Is.False);
    }

    [Test]
    public void Matches_EmptyPatternMatchesAnyString_ReturnsTrue()
    {
        // Arrange
        // F&O §7.6.2: empty pattern matches any string (vacuously).
        // Act / Assert
        Assert.That(BoolResult("fn:matches('abc', '')"), Is.True);
    }

    [Test]
    public void Matches_CaseInsensitiveFlag_ReturnsTrue()
    {
        // Arrange / Act
        // F&O §7.6.1: 'i' flag causes case-insensitive matching.
        bool result = BoolResult("fn:matches('Hello', 'hello', 'i')");
        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Matches_CharacterClassSubtraction_ReturnsFalse()
    {
        // Arrange
        // [a-z-[a]] matches all lowercase letters except 'a'.
        // .NET supports character class subtraction natively.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('a', '[a-z-[a]]')"), Is.False);
    }

    [Test]
    public void Matches_CharacterClassSubtraction_ReturnsTrue()
    {
        // Arrange / Act / Assert
        Assert.That(BoolResult("fn:matches('b', '[a-z-[a]]')"), Is.True);
    }

    [Test]
    public void Matches_UnicodeCategoryUppercaseLetter_ReturnsTrue()
    {
        // Arrange
        // \p{Lu} matches uppercase letters per Unicode category.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('A', '\\p{Lu}')"), Is.True);
    }

    [Test]
    public void Matches_UnicodeCategoryUppercaseLetter_ReturnsFalse()
    {
        // Arrange / Act / Assert
        Assert.That(BoolResult("fn:matches('a', '\\p{Lu}')"), Is.False);
    }

    [Test]
    public void Matches_DotDoesNotMatchNewlineByDefault_ReturnsFalse()
    {
        // Arrange
        // F&O §7.6: '.' without 's' flag does not match \n or \r.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('foo\nbar', 'foo.bar')"), Is.False);
    }

    [Test]
    public void Matches_DotMatchesNewlineWithSFlag_ReturnsTrue()
    {
        // Arrange
        // F&O §7.6.1: 's' flag makes '.' match any character including newline.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('foo\nbar', 'foo.bar', 's')"), Is.True);
    }

    [Test]
    public void Matches_MultilineFlagAnchorNoMatch_ReturnsFalse()
    {
        // Arrange
        // Without 'm' flag, ^ and $ match only the start and end of the whole string.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('Line1\nLine2', '^Line2$')"), Is.False);
    }

    [Test]
    public void Matches_MultilineFlagAnchorMatches_ReturnsTrue()
    {
        // Arrange
        // F&O §7.6.1: 'm' flag makes ^ and $ match start/end of each line.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('Line1\nLine2', '^Line2$', 'm')"), Is.True);
    }

    [Test]
    public void Matches_SimpleGrouping_ReturnsTrue()
    {
        // Arrange / Act / Assert
        Assert.That(BoolResult("fn:matches('abc', 'a(b)c')"), Is.True);
    }

    [Test]
    public void Matches_UnicodeCategoryUnicodeBlock_ReturnsTrue()
    {
        // Arrange
        // \p{IsBasicLatin} matches characters in the Basic Latin Unicode block.
        // .NET supports block names in \p{Is...} form.
        // Act / Assert
        Assert.That(BoolResult("fn:matches('A', '\\p{IsBasicLatin}')"), Is.True);
    }

    [Test]
    public void Matches_LookaheadRejection_ThrowsForx0002()
    {
        // Arrange / Act / Assert
        // F&O §7.6: lookahead assertions are not part of the XPath 2.0 regex grammar.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:matches('x', '(?=.)')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0002));
    }

    [Test]
    public void Matches_LookbehindRejection_ThrowsForx0002()
    {
        // Arrange / Act / Assert
        // F&O §7.6: lookbehind assertions are not part of the XPath 2.0 regex grammar.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:matches('x', '(?<=a)')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0002));
    }

    [Test]
    public void Matches_InvalidFlag_ThrowsForx0001()
    {
        // Arrange / Act / Assert
        // F&O §7.6.1: flag 'z' is not a valid XPath 2.0 regex flag.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:matches('a', 'a', 'z')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0001));
    }

    // =========================================================================
    // fn:replace — F&O §7.6.3
    // =========================================================================

    [Test]
    public void Replace_SimpleSubstitution_ReturnsReplacedString()
    {
        // Arrange / Act / Assert
        // F&O §7.6.3: fn:replace("abc", "b", "X") = "aXc"
        Assert.That(StringResult("fn:replace('abc', 'b', 'X')"), Is.EqualTo("aXc"));
    }

    [Test]
    public void Replace_MultipleMatches_ReplacesAll()
    {
        // Arrange / Act / Assert
        // F&O §7.6.3: replace replaces all non-overlapping matches.
        Assert.That(StringResult("fn:replace('abcabc', 'b', 'X')"), Is.EqualTo("aXcaXc"));
    }

    [Test]
    public void Replace_BackreferenceInReplacement_ReturnsWrappedMatch()
    {
        // Arrange
        // F&O §7.6.3: $1 in the replacement refers to the first captured group.
        // Act / Assert
        Assert.That(StringResult("fn:replace('abc', '(b)', '<$1>')"), Is.EqualTo("a<b>c"));
    }

    [Test]
    public void Replace_ZeroWidthPattern_ThrowsForx0003()
    {
        // Arrange / Act / Assert
        // F&O §7.6.3: a pattern that matches the empty string raises FORX0003.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:replace('abc', '', 'X')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0003));
    }

    [Test]
    public void Replace_InvalidReplacementDollarNonDigit_ThrowsForx0004()
    {
        // Arrange / Act / Assert
        // F&O §7.6.3: '$' in the replacement must be followed by a digit (0-9).
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:replace('abc', 'b', '$a')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0004));
    }

    // =========================================================================
    // fn:tokenize — F&O §7.6.4
    // =========================================================================

    [Test]
    public void Tokenize_CommaSeparator_ReturnsCorrectTokens()
    {
        // Arrange
        // F&O §7.6.4: fn:tokenize("a,b,,c", ",") = ("a", "b", "", "c")
        // Act
        List<XdmItem> items = DrainExpr("fn:tokenize('a,b,,c', ',')");

        // Assert
        Assert.That(items, Has.Count.EqualTo(4));
        Assert.That(((XdmAtomicValue)items[0]).AsString(), Is.EqualTo("a"));
        Assert.That(((XdmAtomicValue)items[1]).AsString(), Is.EqualTo("b"));
        Assert.That(((XdmAtomicValue)items[2]).AsString(), Is.EqualTo(""));
        Assert.That(((XdmAtomicValue)items[3]).AsString(), Is.EqualTo("c"));
    }

    [Test]
    public void Tokenize_WhitespaceSeparator_ReturnsCorrectTokens()
    {
        // Arrange
        // F&O §7.6.4: fn:tokenize("a b  c", "\s+") = ("a", "b", "c")
        // Act
        List<XdmItem> items = DrainExpr("fn:tokenize('a b  c', '\\s+')");

        // Assert
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(((XdmAtomicValue)items[0]).AsString(), Is.EqualTo("a"));
        Assert.That(((XdmAtomicValue)items[1]).AsString(), Is.EqualTo("b"));
        Assert.That(((XdmAtomicValue)items[2]).AsString(), Is.EqualTo("c"));
    }

    [Test]
    public void Tokenize_ZeroWidthPattern_ThrowsForx0003()
    {
        // Arrange / Act / Assert
        // F&O §7.6.4: a pattern that matches the empty string raises FORX0003.
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => DrainExpr("fn:tokenize('abc', '')"));
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORX0003));
    }
}
