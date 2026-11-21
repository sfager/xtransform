using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// Context-sensitivity tests: operator/operand position determines whether a name token
/// is classified as a keyword, and whether <c>*</c> is a wildcard or multiply.
/// Also covers QName/axis disambiguation and variable references.
/// XPath 2.0 §A.2.2.
/// </summary>
[TestFixture]
internal sealed class XPathLexerPositionTests
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

    // ── A.2.2 acceptance #1: a-b is one Name token ───────────────────────────────────────

    [Test]
    public void Hyphenated_Name_IsOneSingleToken()
    {
        // Arrange / Act
        Token[] tokens = AllNonEof("a-b");

        // Assert — exactly one Name with text "a-b"
        Assert.That(tokens, Has.Length.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("a-b"));
    }

    // ── A.2.2 acceptance #2: a - b is three tokens ───────────────────────────────────────

    [Test]
    public void SpacedMinus_ThreeTokens_NameMinusName()
    {
        // Arrange / Act
        Token[] tokens = AllNonEof("a - b");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(3));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("a"));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Minus));
        Assert.That(tokens[2].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[2].Text, Is.EqualTo("b"));
    }

    // ── A.2.2 acceptance #3: child::div div 2 ────────────────────────────────────────────

    [Test]
    public void AxisDivExpr_FiveTokens_WithKeywordDiv()
    {
        // Arrange / Act
        Token[] tokens = AllNonEof("child::div div 2");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(5));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("child"));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.DoubleColon));
        Assert.That(tokens[2].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[2].Text, Is.EqualTo("div"));
        Assert.That(tokens[3].Kind, Is.EqualTo(TokenKind.Div));
        Assert.That(tokens[4].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(tokens[4].Text, Is.EqualTo("2"));
    }

    // ── div as Name in operand position ──────────────────────────────────────────────────

    [Test]
    public void Div_InOperandPosition_IsName()
    {
        // Arrange — "div" at start of expression is operand position → Name
        Token[] tokens = AllNonEof("div");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(1));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("div"));
    }

    [Test]
    public void Div_InOperatorPosition_IsKeyword()
    {
        // Arrange
        Token[] tokens = AllNonEof("a div b");

        // Assert
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Div));
    }

    // ── and / or in operand vs operator position ──────────────────────────────────────────

    [Test]
    public void And_InOperandPosition_IsName()
    {
        // Arrange
        Token[] tokens = AllNonEof("and");

        // Assert
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
    }

    [Test]
    public void Or_AfterCloseParen_IsKeyword()
    {
        // Arrange — ")" puts lexer in operator position
        Token[] tokens = AllNonEof("(a) or b");

        Assert.That(tokens[3].Kind, Is.EqualTo(TokenKind.Or));
    }

    // ── Star: wildcard vs multiply context ───────────────────────────────────────────────

    [Test]
    public void Star_InOperandPosition_IsStarToken()
    {
        // Arrange — start of expression
        Token[] tokens = AllNonEof("*");

        // Assert
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Star));
    }

    [Test]
    public void Star_AfterCloseBracket_IsStarToken()
    {
        // Arrange — "]" leaves lexer in operator position; * after is multiply
        // "a[1]*b" → Name("a"), [, IntegerLiteral("1"), ], Star, Name("b")
        Token[] tokens = AllNonEof("a[1]*b");

        Assert.That(tokens, Has.Length.EqualTo(6));
        Assert.That(tokens[4].Kind, Is.EqualTo(TokenKind.Star));
    }

    // ── Axis step: child::foo ─────────────────────────────────────────────────────────────

    [Test]
    public void AxisStep_ChildColon_FourTokens()
    {
        // Arrange / Act
        Token[] tokens = AllNonEof("child::foo");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(3));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo("child"));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.DoubleColon));
        Assert.That(tokens[2].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[2].Text, Is.EqualTo("foo"));
    }

    // ── Variable reference: $foo ──────────────────────────────────────────────────────────

    [Test]
    public void VariableReference_DollarName_TwoTokens()
    {
        // Arrange / Act
        Token[] tokens = AllNonEof("$foo");

        // Assert
        Assert.That(tokens, Has.Length.EqualTo(2));
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.VariableMarker));
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[1].Text, Is.EqualTo("foo"));
    }

    // ── Keyword in operand position stays as Name ─────────────────────────────────────────

    [TestCase("return")]
    [TestCase("then")]
    [TestCase("else")]
    [TestCase("satisfies")]
    [TestCase("in")]
    public void Keyword_AtStartOfExpression_IsName(string keyword)
    {
        // Arrange / Act
        Token[] tokens = AllNonEof(keyword);

        // Assert
        Assert.That(tokens[0].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[0].Text, Is.EqualTo(keyword));
    }

    // ── Peek does not consume ─────────────────────────────────────────────────────────────

    [Test]
    public void Peek_DoesNotAdvanceCurrent()
    {
        // Arrange
        var lexer = new XPathLexer("a b", SourceSpan.None);
        lexer.Advance();

        // Act
        Token peeked = lexer.Peek(1);
        Token current = lexer.Current;

        // Assert
        Assert.That(current.Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(current.Text, Is.EqualTo("a"));
        Assert.That(peeked.Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(peeked.Text, Is.EqualTo("b"));
    }

    [Test]
    public void Peek0_IsCurrentToken()
    {
        // Arrange
        var lexer = new XPathLexer("x", SourceSpan.None);
        lexer.Advance();

        // Act / Assert
        Assert.That(lexer.Peek(0).Kind, Is.EqualTo(lexer.Current.Kind));
        Assert.That(lexer.Peek(0).Text, Is.EqualTo(lexer.Current.Text));
    }

    // ── instance of NOT matched when second word is not "of" ─────────────────────────────

    [Test]
    public void Instance_WithoutOf_IsName()
    {
        // Arrange — "x instance offset" — "instance" in op position but next word is "offset", not "of"
        Token[] tokens = AllNonEof("x instance offset");

        // Assert — tokens: Name("x"), Name("instance"), Name("offset")
        Assert.That(tokens[1].Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(tokens[1].Text, Is.EqualTo("instance"));
    }
}
