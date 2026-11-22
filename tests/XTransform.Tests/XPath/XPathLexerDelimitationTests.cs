using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// Terminal delimitation tests per XPath 2.0 §A.2.4 and §A.2.2.
/// Each test corresponds to one acceptance scenario from the task spec.
/// </summary>
[TestFixture]
internal sealed class XPathLexerDelimitationTests
{
    private static Token[] AllNonEof(string expr)
    {
        var lexer = new XPathLexer(expr, SourceSpan.None);
        var list = new System.Collections.Generic.List<Token>();
        lexer.Advance();
        while (lexer.Current.Kind != TokenKind.EndOfInput)
        {
            list.Add(lexer.Current);
            lexer.Advance();
        }
        return list.ToArray();
    }

    // ── A.2.4: numeric literal + name-start char without whitespace is an error ──────────

    [Test]
    public void NumericThenNameChar_NoWhitespace_ThrowsXPST0003()
    {
        // Arrange — "1foo" per §A.2.4 is a lexing error
        var lexer = new XPathLexer("1foo", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    // ── Numeric then operator (no whitespace required) ────────────────────────────────────

    [Test]
    public void NumericThenRightParen_TwoTokens()
    {
        // Arrange — "1)" is two tokens
        Token[] tokens = AllNonEof("1)");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.RightParen));
    }

    // ── A.2.2: acceptance #4 — error token carries exact offset ──────────────────────────

    [Test]
    public void InvalidChar_ErrorOffsetIsExact()
    {
        // Arrange — "foo !" — the '!' is at offset 4 (0-based); lone '!' is invalid
        var lexer = new XPathLexer("foo !", SourceSpan.None);
        lexer.Advance(); // Name("foo") — succeeds

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
        Assert.That(ex.Location.Start, Is.EqualTo(4)); // '!' is at offset 4
    }

    // ── *)  : Star then RightParen ────────────────────────────────────────────────────────

    [Test]
    public void StarRightParen_TwoTokens()
    {
        // Arrange
        Token[] tokens = AllNonEof("*)");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Star));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.RightParen));
    }

    // ── Whitespace-delimited numeric then name ────────────────────────────────────────────

    [Test]
    public void IntegerSpaceName_TwoTokens()
    {
        // Arrange — "1 foo" is two tokens
        Token[] tokens = AllNonEof("1 foo");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
    }

    // ── Slash followed by name ────────────────────────────────────────────────────────────

    [Test]
    public void SlashThenName_TwoTokens()
    {
        // Arrange
        Token[] tokens = AllNonEof("/foo");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Slash));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
    }

    // ── At-sign followed by name ──────────────────────────────────────────────────────────

    [Test]
    public void AtThenName_TwoTokens()
    {
        // Arrange
        Token[] tokens = AllNonEof("@href");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.At));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
    }

    // ── Comma delimits operands ───────────────────────────────────────────────────────────

    [Test]
    public void CommaBetweenNames_ThreeTokens()
    {
        // Arrange
        Token[] tokens = AllNonEof("a,b");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(3));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Comma));
        Assert.That(tokens[2].Kind, Is.EqualTo(TokenKind.Name));
    }

    // ── QName followed by left-paren (function call) ─────────────────────────────────────

    [Test]
    public void QNameLeftParen_TwoTokens()
    {
        // Arrange
        Token[] tokens = AllNonEof("fn:string(");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("fn:string"));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.LeftParen));
    }

    // ── Predicate: name[integer] ──────────────────────────────────────────────────────────

    [Test]
    public void Predicate_FourTokens()
    {
        // Arrange — "foo[1]"
        Token[] tokens = AllNonEof("foo[1]");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(4));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.LeftBracket));
        Assert.That(tokens[2].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(tokens[3].Kind, Is.EqualTo(TokenKind.RightBracket));
    }

    // ── Token.ToString format ─────────────────────────────────────────────────────────────

    [Test]
    public void Token_ToString_IncludesKindRangeAndText()
    {
        // Arrange
        var t = new Token(TokenKind.Name, 3, 5, "hello");

        // Act
        string s = t.ToString();

        // Assert
        Assert.That(s, Does.Contain("Name"));
        Assert.That(s, Does.Contain("3"));
        Assert.That(s, Does.Contain("hello"));
    }

    [Test]
    public void Token_ToString_NullText_OmitsTextPart()
    {
        // Arrange
        var t = new Token(TokenKind.Plus, 0, 1);

        // Act
        string s = t.ToString();

        // Assert
        Assert.That(s, Does.Contain("Plus"));
        Assert.That(s, Does.Not.Contain("='"));
    }
}
