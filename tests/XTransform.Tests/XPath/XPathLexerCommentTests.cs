using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// Comment handling: nested comments, transparent skipping, and unterminated-comment errors.
/// XPath 2.0 §A.2.1.
/// </summary>
[TestFixture]
internal sealed class XPathLexerCommentTests
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

    // ── A.2.2 acceptance #5a: nested comment is skipped entirely ─────────────────────────

    [Test]
    public void NestedComment_IsSkippedCompletely()
    {
        // Arrange — the entire expression is a nested comment; expect zero tokens
        // "(: outer (: inner :) outer :)"
        string expr = "(: outer (: inner :) outer :)";

        // Act
        Token[] tokens = AllNonEof(expr);

        // Assert
        Assert.That(tokens, Is.Empty);
    }

    // ── A.2.2 acceptance #5b: "a (: comment :) b" lexes as two Names ────────────────────

    [Test]
    public void CommentBetweenNames_SkippedTransparently()
    {
        // Arrange
        string expr = "a (: comment :) b";

        // Act
        Token[] tokens = AllNonEof(expr);

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("a"));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[1].Text, Is.EqualTo("b"));
    }

    // ── Deeply nested comment ─────────────────────────────────────────────────────────────

    [Test]
    public void DeeplyNestedComment_IsSkipped()
    {
        // Arrange — three nesting levels
        string expr = "x (: level1 (: level2 (: level3 :) level2 :) level1 :) y";

        // Act
        Token[] tokens = AllNonEof(expr);

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Text, Is.EqualTo("x"));
        Assert.That(tokens[1].Text, Is.EqualTo("y"));
    }

    // ── Comment at start of expression ───────────────────────────────────────────────────

    [Test]
    public void CommentAtStart_FollowedByName_ProducesName()
    {
        // Arrange
        string expr = "(: skip me :) foo";

        // Act
        Token[] tokens = AllNonEof(expr);

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(1));
        Assert.That(tokens[0].Text, Is.EqualTo("foo"));
    }

    // ── Comment at end of expression ─────────────────────────────────────────────────────

    [Test]
    public void CommentAtEnd_AfterName_TokenNotAffected()
    {
        // Arrange
        string expr = "foo (: trailing comment :)";

        // Act
        Token[] tokens = AllNonEof(expr);

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(1));
        Assert.That(tokens[0].Text, Is.EqualTo("foo"));
    }

    // ── Error: unterminated comment ───────────────────────────────────────────────────────

    [Test]
    public void UnterminatedComment_ThrowsXPST0003()
    {
        // Arrange
        var lexer = new XPathLexer("(: not closed", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    [Test]
    public void UnterminatedNestedComment_ThrowsXPST0003()
    {
        // Arrange — outer closes but inner does not
        var lexer = new XPathLexer("(: outer (: inner :)", SourceSpan.None);

        // Act / Assert — depth goes to 2 then 1 on ":)", but then EOF reached
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
    }

    // ── Comment offset: start of comment is recorded correctly ───────────────────────────

    [Test]
    public void UnterminatedComment_ErrorSpanStartsAtOpenParen()
    {
        // Arrange — 3 chars of whitespace before the comment
        var lexer = new XPathLexer("   (: oops", SourceSpan.None);

        // Act / Assert
        var ex = Assert.Throws<XPathStaticException>(() => lexer.Advance());
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.XPST0003));
        // The comment starts at offset 3
        Assert.That(ex.Location.Start, Is.EqualTo(3));
    }
}
