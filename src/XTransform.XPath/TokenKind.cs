// XPath 2.0 §A.2.2 — lexical analysis token categories
namespace XTransform.XPath;

/// <summary>
/// Identifies the syntactic category of a lexed token. Keyword tokens (And, Or, Div, etc.)
/// are emitted only in operator position; in operand position those names lex as
/// <see cref="Name"/>.
/// </summary>
internal enum TokenKind
{
    /// <summary>Sentinel returned after the last character of the expression has been consumed.</summary>
    EndOfInput,

    // ── literals and names ────────────────────────────────────────────────────────────────
    /// <summary>A single- or double-quoted string literal. Token.Text holds the decoded value.</summary>
    StringLiteral,
    /// <summary>A numeric literal with no decimal point and no exponent, e.g. <c>42</c>.</summary>
    IntegerLiteral,
    /// <summary>A numeric literal with a decimal point but no exponent, e.g. <c>3.14</c>.</summary>
    DecimalLiteral,
    /// <summary>A numeric literal with an exponent part, e.g. <c>1.5e10</c>.</summary>
    DoubleLiteral,
    /// <summary>An NCName or QName (prefix:local). Keywords are Names the parser re-interprets in context.</summary>
    Name,
    /// <summary>A wildcard with an explicit prefix: <c>prefix:*</c>. Token.Text holds the prefix.</summary>
    PrefixWildcard,
    /// <summary>A wildcard with an explicit local name: <c>*:local</c>. Token.Text holds the local name.</summary>
    LocalWildcard,
    /// <summary><c>*</c> — wildcard name test or multiply operator; the parser decides from context.</summary>
    Star,
    /// <summary><c>$</c> — introduces a variable reference.</summary>
    VariableMarker,

    // ── punctuation ───────────────────────────────────────────────────────────────────────
    /// <summary><c>(</c></summary>
    LeftParen,
    /// <summary><c>)</c></summary>
    RightParen,
    /// <summary><c>[</c></summary>
    LeftBracket,
    /// <summary><c>]</c></summary>
    RightBracket,
    /// <summary><c>,</c></summary>
    Comma,
    /// <summary><c>.</c></summary>
    Dot,
    /// <summary><c>..</c></summary>
    DotDot,
    /// <summary><c>/</c></summary>
    Slash,
    /// <summary><c>//</c></summary>
    DoubleSlash,
    /// <summary><c>@</c></summary>
    At,
    /// <summary><c>::</c></summary>
    DoubleColon,
    /// <summary><c>:</c> — lone colon (rarely valid in XPath 2.0; kept for error recovery).</summary>
    Colon,
    /// <summary><c>?</c></summary>
    QuestionMark,

    // ── symbol operators ──────────────────────────────────────────────────────────────────
    /// <summary><c>+</c></summary>
    Plus,
    /// <summary><c>-</c></summary>
    Minus,
    /// <summary><c>|</c></summary>
    Pipe,
    /// <summary><c>=</c></summary>
    Equals,
    /// <summary><c>!=</c></summary>
    NotEquals,
    /// <summary><c>&lt;</c></summary>
    LessThan,
    /// <summary><c>&lt;=</c></summary>
    LessThanOrEqual,
    /// <summary><c>&gt;</c></summary>
    GreaterThan,
    /// <summary><c>&gt;=</c></summary>
    GreaterThanOrEqual,
    /// <summary><c>&lt;&lt;</c> — node precedes operator.</summary>
    Precedes,
    /// <summary><c>&gt;&gt;</c> — node follows operator.</summary>
    Follows,

    // ── operator keywords (emitted only in operator position, per A.2.2) ──────────────────
    /// <summary><c>and</c></summary>
    And,
    /// <summary><c>or</c></summary>
    Or,
    /// <summary><c>div</c></summary>
    Div,
    /// <summary><c>idiv</c></summary>
    IDiv,
    /// <summary><c>mod</c></summary>
    Mod,
    /// <summary><c>union</c></summary>
    Union,
    /// <summary><c>intersect</c></summary>
    Intersect,
    /// <summary><c>except</c></summary>
    Except,
    /// <summary><c>to</c></summary>
    To,
    /// <summary><c>eq</c></summary>
    Eq,
    /// <summary><c>ne</c></summary>
    Ne,
    /// <summary><c>lt</c></summary>
    Lt,
    /// <summary><c>le</c></summary>
    Le,
    /// <summary><c>gt</c></summary>
    Gt,
    /// <summary><c>ge</c></summary>
    Ge,
    /// <summary><c>is</c></summary>
    Is,
    /// <summary><c>instance of</c> (two-word keyword).</summary>
    InstanceOf,
    /// <summary><c>treat as</c> (two-word keyword).</summary>
    TreatAs,
    /// <summary><c>castable as</c> (two-word keyword).</summary>
    CastableAs,
    /// <summary><c>cast as</c> (two-word keyword).</summary>
    CastAs,
    /// <summary><c>return</c></summary>
    Return,
    /// <summary><c>satisfies</c></summary>
    Satisfies,
    /// <summary><c>then</c></summary>
    Then,
    /// <summary><c>else</c></summary>
    Else,
    /// <summary><c>in</c></summary>
    In,
}
