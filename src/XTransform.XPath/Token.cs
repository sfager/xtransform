using System;

// XPath 2.0 §A.2.2 — lexical tokens
namespace XTransform.XPath;

/// <summary>
/// An immutable token produced by <see cref="XPathLexer"/>. Carry the token kind, its
/// character range within the original expression, and — for names and decoded literals —
/// the interpreted text.
/// </summary>
internal readonly struct Token : IEquatable<Token>
{
    /// <summary>Initialises a token with an optional decoded text payload.</summary>
    public Token(TokenKind kind, int start, int length, string? text = null)
    {
        Kind = kind;
        Start = start;
        Length = length;
        Text = text;
    }

    /// <summary>The syntactic category of this token.</summary>
    public TokenKind Kind { get; }

    /// <summary>0-based offset of the first character in the expression string.</summary>
    public int Start { get; }

    /// <summary>Number of raw UTF-16 code units (including quotes for string literals).</summary>
    public int Length { get; }

    /// <summary>
    /// Decoded literal value or name text; <see langword="null"/> for pure punctuation,
    /// symbol operators, keyword operators and <see cref="TokenKind.EndOfInput"/>.
    /// </summary>
    public string? Text { get; }

    // ── IEquatable<Token> ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool Equals(Token other) =>
        Kind == other.Kind &&
        Start == other.Start &&
        Length == other.Length &&
        string.Equals(Text, other.Text, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Token other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int h = (int)Kind;
        h = h * 31 + Start;
        h = h * 31 + Length;
        if (Text is not null)
        {
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Text);
        }
        return h;
    }

    /// <summary>Diagnostic representation: <c>Kind[Start..End]='Text'</c>.</summary>
    public override string ToString()
    {
        int end = Start + Length;
        return Text is null
            ? $"{Kind}[{Start}..{end}]"
            : $"{Kind}[{Start}..{end}]='{Text}'";
    }
}
