using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath;

/// <summary>
/// One or more tests per <see cref="TokenKind"/> value, verifying that the lexer produces
/// the correct kind (and Text when applicable) for representative inputs.
/// </summary>
[TestFixture]
internal sealed class XPathLexerTokenKindTests
{
    private static XPathLexer Lex(string expr)
    {
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance();
        return lexer;
    }

    private static Token[] AllTokens(string expr)
    {
        var lexer = new XPathLexer(expr, SourceSpan.None);
        var list = new System.Collections.Generic.List<Token>();
        lexer.Advance();
        while (lexer.Current.Kind != TokenKind.EndOfInput)
        {
            list.Add(lexer.Current);
            lexer.Advance();
        }
        list.Add(lexer.Current); // EndOfInput
        return list.ToArray();
    }

    // ── EndOfInput ────────────────────────────────────────────────────────────────────────

    [Test]
    public void EndOfInput_EmptyExpression_FirstAdvanceIsEndOfInput()
    {
        // Arrange
        var lexer = new XPathLexer("", SourceSpan.None);

        // Act
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.EndOfInput));
    }

    // ── StringLiteral ─────────────────────────────────────────────────────────────────────

    [Test]
    public void StringLiteral_DoubleQuoted_ProducesCorrectToken()
    {
        // Arrange / Act
        var lexer = Lex("\"hello\"");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(lexer.Current.Text, Is.EqualTo("hello"));
    }

    [Test]
    public void StringLiteral_SingleQuoted_ProducesCorrectToken()
    {
        // Arrange / Act
        var lexer = Lex("'world'");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.StringLiteral));
        Assert.That(lexer.Current.Text, Is.EqualTo("world"));
    }

    // ── IntegerLiteral ────────────────────────────────────────────────────────────────────

    [Test]
    public void IntegerLiteral_Digits_ProducesCorrectToken()
    {
        // Arrange / Act
        var lexer = Lex("42");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
        Assert.That(lexer.Current.Text, Is.EqualTo("42"));
    }

    // ── DecimalLiteral ────────────────────────────────────────────────────────────────────

    [Test]
    public void DecimalLiteral_DigitsDot_ProducesCorrectToken()
    {
        // Arrange / Act
        var lexer = Lex("3.14");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.DecimalLiteral));
        Assert.That(lexer.Current.Text, Is.EqualTo("3.14"));
    }

    // ── DoubleLiteral ─────────────────────────────────────────────────────────────────────

    [Test]
    public void DoubleLiteral_WithExponent_ProducesCorrectToken()
    {
        // Arrange / Act
        var lexer = Lex("1.5e10");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.DoubleLiteral));
        Assert.That(lexer.Current.Text, Is.EqualTo("1.5e10"));
    }

    // ── Name ──────────────────────────────────────────────────────────────────────────────

    [Test]
    public void Name_SimpleName_ProducesNameToken()
    {
        // Arrange / Act
        var lexer = Lex("foo");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(lexer.Current.Text, Is.EqualTo("foo"));
    }

    [Test]
    public void Name_QName_ProducesSingleNameToken()
    {
        // Arrange / Act
        var lexer = Lex("xs:integer");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Name));
        Assert.That(lexer.Current.Text, Is.EqualTo("xs:integer"));
    }

    // ── PrefixWildcard ────────────────────────────────────────────────────────────────────

    [Test]
    public void PrefixWildcard_PrefixColonStar_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex("xs:*");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.PrefixWildcard));
        Assert.That(lexer.Current.Text, Is.EqualTo("xs"));
    }

    // ── LocalWildcard ─────────────────────────────────────────────────────────────────────

    [Test]
    public void LocalWildcard_StarColonLocal_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex("*:local");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.LocalWildcard));
        Assert.That(lexer.Current.Text, Is.EqualTo("local"));
    }

    // ── Star ──────────────────────────────────────────────────────────────────────────────

    [Test]
    public void Star_AloneAtStart_ProducesStarToken()
    {
        // Arrange / Act
        var lexer = Lex("*");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Star));
    }

    // ── VariableMarker ────────────────────────────────────────────────────────────────────

    [Test]
    public void VariableMarker_Dollar_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex("$x");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.VariableMarker));
    }

    // ── Punctuation ───────────────────────────────────────────────────────────────────────

    [TestCase("(", TokenKind.LeftParen)]
    [TestCase(")", TokenKind.RightParen)]
    [TestCase("[", TokenKind.LeftBracket)]
    [TestCase("]", TokenKind.RightBracket)]
    [TestCase(",", TokenKind.Comma)]
    [TestCase("@", TokenKind.At)]
    [TestCase("?", TokenKind.QuestionMark)]
    [TestCase("+", TokenKind.Plus)]
    [TestCase("-", TokenKind.Minus)]
    [TestCase("=", TokenKind.Equals)]
    [TestCase("|", TokenKind.Pipe)]
    [TestCase("$", TokenKind.VariableMarker)]
    public void Punctuation_SingleChar_ProducesExpectedToken(string expr, TokenKind expected)
    {
        // Arrange / Act
        var lexer = Lex(expr);

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(expected));
    }

    [Test]
    public void Dot_SingleDot_ProducesDotToken()
    {
        // Arrange / Act
        var lexer = Lex(".");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Dot));
    }

    [Test]
    public void DotDot_TwoDots_ProducesDotDotToken()
    {
        // Arrange / Act
        var lexer = Lex("..");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.DotDot));
    }

    [Test]
    public void Slash_SingleSlash_ProducesSlashToken()
    {
        // Arrange / Act
        var lexer = Lex("/");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Slash));
    }

    [Test]
    public void DoubleSlash_TwoSlashes_ProducesDoubleSlashToken()
    {
        // Arrange / Act
        var lexer = Lex("//");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.DoubleSlash));
    }

    [Test]
    public void DoubleColon_TwoColons_ProducesDoubleColonToken()
    {
        // Arrange / Act
        var lexer = Lex("::");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.DoubleColon));
    }

    [Test]
    public void Colon_SingleColon_ProducesColonToken()
    {
        // Arrange / Act
        var lexer = Lex(":");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Colon));
    }

    // ── Symbol operators ──────────────────────────────────────────────────────────────────

    [Test]
    public void NotEquals_BangEquals_ProducesNotEqualsToken()
    {
        // Arrange / Act
        var lexer = Lex("!=");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.NotEquals));
    }

    [Test]
    public void LessThan_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex("<");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.LessThan));
    }

    [Test]
    public void LessThanOrEqual_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex("<=");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.LessThanOrEqual));
    }

    [Test]
    public void GreaterThan_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex(">");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.GreaterThan));
    }

    [Test]
    public void GreaterThanOrEqual_ProducesToken()
    {
        // Arrange / Act
        var lexer = Lex(">=");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.GreaterThanOrEqual));
    }

    [Test]
    public void Precedes_DoubleLeftAngle_ProducesPrecedesToken()
    {
        // Arrange / Act
        var lexer = Lex("<<");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Precedes));
    }

    [Test]
    public void Follows_DoubleRightAngle_ProducesFollowsToken()
    {
        // Arrange / Act
        var lexer = Lex(">>");

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.Follows));
    }

    // ── Keyword operators (in operator position) ──────────────────────────────────────────

    [TestCase("and",       TokenKind.And)]
    [TestCase("or",        TokenKind.Or)]
    [TestCase("div",       TokenKind.Div)]
    [TestCase("idiv",      TokenKind.IDiv)]
    [TestCase("mod",       TokenKind.Mod)]
    [TestCase("union",     TokenKind.Union)]
    [TestCase("intersect", TokenKind.Intersect)]
    [TestCase("except",    TokenKind.Except)]
    [TestCase("to",        TokenKind.To)]
    [TestCase("eq",        TokenKind.Eq)]
    [TestCase("ne",        TokenKind.Ne)]
    [TestCase("lt",        TokenKind.Lt)]
    [TestCase("le",        TokenKind.Le)]
    [TestCase("gt",        TokenKind.Gt)]
    [TestCase("ge",        TokenKind.Ge)]
    [TestCase("is",        TokenKind.Is)]
    [TestCase("return",    TokenKind.Return)]
    [TestCase("satisfies", TokenKind.Satisfies)]
    [TestCase("then",      TokenKind.Then)]
    [TestCase("else",      TokenKind.Else)]
    [TestCase("in",        TokenKind.In)]
    public void KeywordOperator_AfterOperand_ProducesKeywordToken(string keyword, TokenKind expected)
    {
        // Arrange — "x <keyword>" puts keyword in operator position.
        string expr = "x " + keyword;
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance(); // Name("x")

        // Act
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(expected));
    }

    // ── Multi-word keyword operators ──────────────────────────────────────────────────────

    [Test]
    public void InstanceOf_AfterOperand_ProducesSingleToken()
    {
        // Arrange
        string expr = "x instance of xs:integer";
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance(); // Name("x")

        // Act
        lexer.Advance(); // should be InstanceOf

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.InstanceOf));
    }

    [Test]
    public void TreatAs_AfterOperand_ProducesSingleToken()
    {
        // Arrange
        string expr = "x treat as xs:integer";
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance(); // Name("x")

        // Act
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.TreatAs));
    }

    [Test]
    public void CastableAs_AfterOperand_ProducesSingleToken()
    {
        // Arrange
        string expr = "x castable as xs:integer";
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance(); // Name("x")

        // Act
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.CastableAs));
    }

    [Test]
    public void CastAs_AfterOperand_ProducesSingleToken()
    {
        // Arrange
        string expr = "x cast as xs:integer";
        var lexer = new XPathLexer(expr, SourceSpan.None);
        lexer.Advance(); // Name("x")

        // Act
        lexer.Advance();

        // Assert
        Assert.That(lexer.Current.Kind, Is.EqualTo(TokenKind.CastAs));
    }
}
