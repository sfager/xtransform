using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// Numeric literal lexing: integer, decimal, double, edge cases, and error cases.
/// XPath 2.0 §3.1.1 and §A.2.1.
/// </summary>
[TestFixture]
internal sealed class XPathLexerNumericTests
{
    private static Token Single(string expr)
    {
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance();
        return lexer.Current;
    }

    // ── IntegerLiteral ────────────────────────────────────────────────────────────────────

    [TestCase("0",   "0")]
    [TestCase("42",  "42")]
    [TestCase("100", "100")]
    public void IntegerLiteral_Digits_ProducesToken(string expr, string expectedText)
    {
        // Arrange / Act
        Token t = Single(expr);

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(t.Text, Is.EqualTo(expectedText));
    }

    // ── DecimalLiteral ────────────────────────────────────────────────────────────────────

    [TestCase("3.14",  "3.14")]
    [TestCase("0.5",   "0.5")]
    [TestCase("1.",    "1.")]
    [TestCase(".5",    ".5")]
    [TestCase(".0",    ".0")]
    public void DecimalLiteral_WithDot_ProducesToken(string expr, string expectedText)
    {
        // Arrange / Act
        Token t = Single(expr);

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.DecimalLiteral));
        Assert.That(t.Text, Is.EqualTo(expectedText));
    }

    // ── DoubleLiteral ─────────────────────────────────────────────────────────────────────

    [TestCase("1e5",    "1e5")]
    [TestCase("1E5",    "1E5")]
    [TestCase("1.5e10", "1.5e10")]
    [TestCase("1.5E-3", "1.5E-3")]
    [TestCase("1.5e+3", "1.5e+3")]
    [TestCase(".5e2",   ".5e2")]
    public void DoubleLiteral_WithExponent_ProducesToken(string expr, string expectedText)
    {
        // Arrange / Act
        Token t = Single(expr);

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.DoubleLiteral));
        Assert.That(t.Text, Is.EqualTo(expectedText));
    }

    // ── Leading minus is NOT part of numeric literal ──────────────────────────────────────

    [Test]
    public void Minus_BeforeInteger_IsSeparateMinusToken()
    {
        // Arrange
        var lexer = new XPathLexer("-5", SourceSpan.None);
        lexer.Advance();

        // Act
        Token first = lexer.Current;
        lexer.Advance();
        Token second = lexer.Current;

        // Assert
        Assert.That(first.Kind, Is.EqualTo(TokenKind.Minus));
        Assert.That(second.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(second.Text, Is.EqualTo("5"));
    }

    // ── Offset tracking ──────────────────────────────────────────────────────────────────

    [Test]
    public void IntegerLiteral_Offset_IsCorrect()
    {
        // Arrange
        var lexer = new XPathLexer("  42", SourceSpan.None);
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Start, Is.EqualTo(2));
        Assert.That(lexer.Current.Length, Is.EqualTo(2));
    }

    // ── 1.. should produce IntegerLiteral then DotDot ─────────────────────────────────────

    [Test]
    public void IntegerThenDotDot_ProducesTwoTokens()
    {
        // Arrange
        var lexer = new XPathLexer("1..", SourceSpan.None);
        lexer.Advance();
        Token first = lexer.Current;
        lexer.Advance();
        Token second = lexer.Current;

        // Assert
        Assert.That(first.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(first.Text, Is.EqualTo("1"));
        Assert.That(second.Kind, Is.EqualTo(TokenKind.DotDot));
    }

    // ── Error: exponent without digits ───────────────────────────────────────────────────

    [Test]
    public void DoubleLiteral_ExponentNoDigits_ThrowsXPST0003()
    {
        // Arrange
        var lexer = new XPathLexer("1e", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    [Test]
    public void DoubleLiteral_ExponentWithSignNoDigits_ThrowsXPST0003()
    {
        // Arrange
        var lexer = new XPathLexer("1e+", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    // ── Error: numeric literal followed immediately by name char (§A.2.4) ─────────────────

    [Test]
    public void NumericLiteral_FollowedByNameChar_ThrowsXPST0003()
    {
        // Arrange — "1foo" is not valid per §A.2.4
        var lexer = new XPathLexer("1foo", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    // ── Whitespace-separated numeric then name is fine ────────────────────────────────────

    [Test]
    public void IntegerThenSpace_ThenName_TwoTokens()
    {
        // Arrange
        var lexer = new XPathLexer("1 foo", SourceSpan.None);
        lexer.Advance();
        Token first = lexer.Current;
        lexer.Advance();
        Token second = lexer.Current;

        // Assert
        Assert.That(first.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(second.Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(second.Text, Is.EqualTo("foo"));
    }
}
