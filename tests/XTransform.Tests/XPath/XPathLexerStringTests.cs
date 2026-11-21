using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// String literal lexing: single-quoted and double-quoted, escape sequences,
/// and unterminated-literal error cases.
/// XPath 2.0 §3.1.1 and §A.2.1.
/// </summary>
[TestFixture]
internal sealed class XPathLexerStringTests
{
    private static Token Single(string expr)
    {
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance();
        return lexer.Current;
    }

    // ── A.2.2 acceptance #6: 'can''t' → "can't" ──────────────────────────────────────────

    [Test]
    public void SingleQuoted_EscapedQuote_DecodesCorrectly()
    {
        // Arrange / Act
        Token t = Single("'can''t'");

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(t.Text, Is.EqualTo("can't"));
        Assert.That(t.Start, Is.EqualTo(0));
        Assert.That(t.Length, Is.EqualTo(8)); // raw source including quotes
    }

    // ── A.2.2 acceptance #7: "she said ""hi""" → she said "hi" ──────────────────────────

    [Test]
    public void DoubleQuoted_EscapedQuote_DecodesCorrectly()
    {
        // Arrange / Act
        Token t = Single("\"she said \\\"hi\\\"\"".Replace("\\\"", "\"\""));
        // The XPath source is: "she said ""hi"""
        // Construct it properly:
        string xpath = "\"she said \"\"hi\"\"\"";

        // Act
        t = Single(xpath);

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(t.Text, Is.EqualTo("she said \"hi\""));
    }

    // ── Empty strings ─────────────────────────────────────────────────────────────────────

    [Test]
    public void SingleQuoted_EmptyString_ProducesEmptyText()
    {
        // Arrange / Act
        Token t = Single("''");

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(t.Text, Is.EqualTo(""));
        Assert.That(t.Length, Is.EqualTo(2));
    }

    [Test]
    public void DoubleQuoted_EmptyString_ProducesEmptyText()
    {
        // Arrange / Act
        Token t = Single("\"\"");

        // Assert
        Assert.That(t.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(t.Text, Is.EqualTo(""));
    }

    // ── Offset tracking ───────────────────────────────────────────────────────────────────

    [Test]
    public void StringLiteral_Offset_IsCorrect()
    {
        // Arrange — whitespace then string
        var lexer = new XPathLexer("  'hi'", SourceSpan.None);
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Start, Is.EqualTo(2));
        Assert.That(lexer.Current.Length, Is.EqualTo(4)); // includes quotes
    }

    // ── Mixed quote types are independent ─────────────────────────────────────────────────

    [Test]
    public void SingleQuoted_ContainsDoubleQuotes_NoEscapeNeeded()
    {
        // Arrange — 'say "hello"' — double quotes inside single-quoted need no escape
        Token t = Single("'say \"hello\"'");

        // Assert
        Assert.That(t.Text, Is.EqualTo("say \"hello\""));
    }

    [Test]
    public void DoubleQuoted_ContainsSingleQuotes_NoEscapeNeeded()
    {
        // Arrange — "it's fine"
        Token t = Single("\"it's fine\"");

        // Assert
        Assert.That(t.Text, Is.EqualTo("it's fine"));
    }

    // ── Error: unterminated single-quoted string ──────────────────────────────────────────

    [Test]
    public void UnterminatedSingleQuote_ThrowsXPST0003()
    {
        // Arrange
        var lexer = new XPathLexer("'unterminated", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
        Assert.That(ex.Location.Start, Is.EqualTo(0));
    }

    // ── Error: unterminated double-quoted string ──────────────────────────────────────────

    [Test]
    public void UnterminatedDoubleQuote_ThrowsXPST0003()
    {
        // Arrange
        var lexer = new XPathLexer("\"unterminated", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
        Assert.That(ex.Location.Start, Is.EqualTo(0));
    }
}
