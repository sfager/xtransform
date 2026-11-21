using System;
using System.Text;
using System.Xml;
using XTransform.Xdm;

// XPath 2.0 §A.2 — Lexical structure
namespace XTransform.XPath;

/// <summary>
/// Context-sensitive XPath 2.0 lexer. Tracks whether the next token is in operator or
/// operand position (XPath 2.0 §A.2.2) so <c>div</c>, <c>*</c>, <c>-</c> and keyword
/// operators lex correctly. Comments <c>(: … :)</c> nest and are skipped transparently.
/// </summary>
internal sealed class XPathLexer
{
    // ── constants ─────────────────────────────────────────────────────────────────────────

    /// <summary>Size of the look-ahead ring buffer (must be >= 2 for multi-word keywords).</summary>
    private const int BufferSize = 8;

    // ── state ─────────────────────────────────────────────────────────────────────────────

    private readonly string _expr;
    private readonly SourceSpan _baseSpan;

    /// <summary>Current scan position within <see cref="_expr"/>.</summary>
    private int _pos;

    /// <summary>
    /// Whether the next token should be lexed in OPERATOR position.
    /// True  = previous token was an operand (name, literal, ), ], ., ..) — the NEXT
    ///         token may be a binary operator keyword.
    /// False = previous token was an operator or start-of-input — the next token begins
    ///         an operand.
    /// </summary>
    private bool _operatorExpected;

    // Ring buffer for lookahead.  _buffered is the count of valid tokens starting at
    // _bufHead.  _bufHead..(_bufHead+_buffered-1) mod BufferSize are valid.
    // Index 0 of the logical buffer is _current (returned by Current).
    private readonly Token[] _ring = new Token[BufferSize];
    private int _bufHead;   // index in _ring of Current
    private int _buffered;  // how many tokens are in the ring (including Current)

    // ── construction ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the lexer. <see cref="Current"/> is <see cref="TokenKind.EndOfInput"/>
    /// until the first call to <see cref="Advance"/>.
    /// </summary>
    public XPathLexer(string expression, SourceSpan baseSpan)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }
#pragma warning restore CA1510

        _expr = expression;
        _baseSpan = baseSpan;
        _pos = 0;
        _operatorExpected = false;  // start of input = operand position

        // Prime Current with EndOfInput at offset 0 (length 0).
        _ring[0] = new Token(TokenKind.EndOfInput, 0, 0);
        _bufHead = 0;
        _buffered = 1;  // Current is "valid" but will be replaced on first Advance
    }

    // ── public API ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The last token returned by <see cref="Advance"/>. Before the first advance this
    /// is a synthetic <see cref="TokenKind.EndOfInput"/> at offset 0.
    /// </summary>
    public Token Current => _ring[_bufHead];

    /// <summary>
    /// Returns the token that is <paramref name="lookahead"/> steps ahead of
    /// <see cref="Current"/> without consuming any tokens.
    /// <c>Peek(0)</c> == <see cref="Current"/>;
    /// <c>Peek(1)</c> is the token that the next <see cref="Advance"/> would produce.
    /// </summary>
    public Token Peek(int lookahead = 1)
    {
#pragma warning disable CA1512 // ArgumentOutOfRangeException.ThrowIfNegative not available on netstandard2.0
        if (lookahead < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lookahead));
        }
#pragma warning restore CA1512

        // Ensure the ring contains at least lookahead+1 tokens (Current + lookahead ahead).
        while (_buffered <= lookahead)
        {
            AppendNextRaw();
        }

        int idx = (_bufHead + lookahead) % BufferSize;
        return _ring[idx];
    }

    /// <summary>Advances to the next token, making it <see cref="Current"/>.</summary>
    public void Advance()
    {
        // Ensure there is at least one look-ahead token ready.
        if (_buffered < 2)
        {
            AppendNextRaw();
        }

        // Discard Current; move head forward.
        _bufHead = (_bufHead + 1) % BufferSize;
        _buffered--;
    }

    /// <summary>
    /// Returns (does not throw) an <see cref="XPathStaticException"/> with
    /// <see cref="XPathErrors.XPST0003"/> placed at the start of <see cref="Current"/>.
    /// The caller is expected to throw the returned exception.
    /// </summary>
    public Exception SyntaxError(string message)
    {
        return SyntaxError(message, Current.Start);
    }

    /// <summary>
    /// Returns (does not throw) an <see cref="XPathStaticException"/> with
    /// <see cref="XPathErrors.XPST0003"/> placed at <paramref name="atOffset"/>.
    /// </summary>
    public Exception SyntaxError(string message, int atOffset)
    {
        SourceSpan span = _baseSpan.WithRange(atOffset, 0);
        return new XPathStaticException(XPathErrors.XPST0003, message, span);
    }

    // ── internal lexing machinery ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lexes the next token from <see cref="_pos"/> (which is advanced past the token)
    /// and appends it to the ring buffer. Also updates <see cref="_operatorExpected"/>
    /// immediately after deciding which token was produced.
    /// </summary>
    private void AppendNextRaw()
    {
        // Skip whitespace and comments before the next token.
        SkipWhitespaceAndComments();

        int start = _pos;
        Token tok;

        if (_pos >= _expr.Length)
        {
            tok = new Token(TokenKind.EndOfInput, start, 0);
            // EndOfInput does not change operator/operand state.
        }
        else
        {
            tok = ReadToken(start);
            UpdateOperatorState(tok.Kind);
        }

        // Append to the ring.
        int slot = (_bufHead + _buffered) % BufferSize;
        _ring[slot] = tok;
        _buffered++;
    }

    // XPath 2.0 §A.2.1 — ExplicitWhitespace and comments
    private void SkipWhitespaceAndComments()
    {
        while (_pos < _expr.Length)
        {
            char c = _expr[_pos];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                _pos++;
            }
            else if (_pos + 1 < _expr.Length && c == '(' && _expr[_pos + 1] == ':')
            {
                SkipComment();
            }
            else
            {
                break;
            }
        }
    }

    // XPath 2.0 §A.2.1 — nested comments (: … :)
    private void SkipComment()
    {
        int commentStart = _pos;
        _pos += 2; // skip '(:'
        int depth = 1;

        while (_pos < _expr.Length)
        {
            if (_pos + 1 < _expr.Length && _expr[_pos] == '(' && _expr[_pos + 1] == ':')
            {
                depth++;
                _pos += 2;
            }
            else if (_pos + 1 < _expr.Length && _expr[_pos] == ':' && _expr[_pos + 1] == ')')
            {
                depth--;
                _pos += 2;
                if (depth == 0)
                {
                    return;
                }
            }
            else
            {
                _pos++;
            }
        }

        // EOF inside comment
        throw new XPathStaticException(
            XPathErrors.XPST0003,
            "Unterminated comment: missing ':)'",
            _baseSpan.WithRange(commentStart, _expr.Length - commentStart));
    }

    // ── token dispatch ────────────────────────────────────────────────────────────────────

    private Token ReadToken(int start)
    {
        char c = _expr[_pos];

        // ── string literals ───────────────────────────────────────────────────────────────
        if (c == '\'' || c == '"')
        {
            return ReadStringLiteral(start);
        }

        // ── numeric literals ──────────────────────────────────────────────────────────────
        if (c >= '0' && c <= '9')
        {
            return ReadNumericLiteral(start);
        }

        // ── dot: could be '.', '..' or decimal literal '.digits' ─────────────────────────
        if (c == '.')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] >= '0' && _expr[_pos + 1] <= '9')
            {
                return ReadNumericLiteral(start);
            }

            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '.')
            {
                _pos += 2;
                return new Token(TokenKind.DotDot, start, 2);
            }

            _pos++;
            return new Token(TokenKind.Dot, start, 1);
        }

        // ── names (NCName / QName) and keyword detection ──────────────────────────────────
        if (IsNameStartChar(c))
        {
            return ReadNameOrKeyword(start);
        }

        // ── star / wildcard ───────────────────────────────────────────────────────────────
        if (c == '*')
        {
            return ReadStar(start);
        }

        // ── two-character operators ───────────────────────────────────────────────────────
        if (c == '<')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '<')
            {
                _pos += 2;
                return new Token(TokenKind.Precedes, start, 2);
            }
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '=')
            {
                _pos += 2;
                return new Token(TokenKind.LessThanOrEqual, start, 2);
            }
            _pos++;
            return new Token(TokenKind.LessThan, start, 1);
        }

        if (c == '>')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '>')
            {
                _pos += 2;
                return new Token(TokenKind.Follows, start, 2);
            }
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '=')
            {
                _pos += 2;
                return new Token(TokenKind.GreaterThanOrEqual, start, 2);
            }
            _pos++;
            return new Token(TokenKind.GreaterThan, start, 1);
        }

        if (c == '!')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '=')
            {
                _pos += 2;
                return new Token(TokenKind.NotEquals, start, 2);
            }
            // Bare '!' is not valid XPath 2.0 — raise immediately.
            throw new XPathStaticException(
                XPathErrors.XPST0003,
                $"Unexpected character '!' at offset {_pos}",
                _baseSpan.WithRange(_pos, 1));
        }

        if (c == '/')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '/')
            {
                _pos += 2;
                return new Token(TokenKind.DoubleSlash, start, 2);
            }
            _pos++;
            return new Token(TokenKind.Slash, start, 1);
        }

        if (c == ':')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == ':')
            {
                _pos += 2;
                return new Token(TokenKind.DoubleColon, start, 2);
            }
            _pos++;
            return new Token(TokenKind.Colon, start, 1);
        }

        // ── single-character tokens ───────────────────────────────────────────────────────
        _pos++;
        return c switch
        {
            '(' => new Token(TokenKind.LeftParen, start, 1),
            ')' => new Token(TokenKind.RightParen, start, 1),
            '[' => new Token(TokenKind.LeftBracket, start, 1),
            ']' => new Token(TokenKind.RightBracket, start, 1),
            ',' => new Token(TokenKind.Comma, start, 1),
            '@' => new Token(TokenKind.At, start, 1),
            '?' => new Token(TokenKind.QuestionMark, start, 1),
            '+' => new Token(TokenKind.Plus, start, 1),
            '-' => new Token(TokenKind.Minus, start, 1),
            '=' => new Token(TokenKind.Equals, start, 1),
            '|' => new Token(TokenKind.Pipe, start, 1),
            '$' => new Token(TokenKind.VariableMarker, start, 1),
            _ => throw new XPathStaticException(
                     XPathErrors.XPST0003,
                     $"Unexpected character '{c}' (U+{(int)c:X4}) at offset {start}",
                     _baseSpan.WithRange(start, 1)),
        };
    }

    // ── string literals ───────────────────────────────────────────────────────────────────

    // XPath 2.0 §3.1.1 and §A.2.1 — StringLiteral
    private Token ReadStringLiteral(int start)
    {
        char quote = _expr[_pos];
        _pos++; // skip opening quote

        var sb = new StringBuilder();

        while (_pos < _expr.Length)
        {
            char c = _expr[_pos];
            if (c == quote)
            {
                _pos++; // skip the quote character
                // Escaped quote: two consecutive quote chars → one literal quote
                if (_pos < _expr.Length && _expr[_pos] == quote)
                {
                    sb.Append(quote);
                    _pos++;
                }
                else
                {
                    // End of string literal.
                    return new Token(TokenKind.StringLiteral, start, _pos - start, sb.ToString());
                }
            }
            else
            {
                sb.Append(c);
                _pos++;
            }
        }

        // Reached end of expression without closing quote.
        throw new XPathStaticException(
            XPathErrors.XPST0003,
            $"Unterminated string literal starting at offset {start}",
            _baseSpan.WithRange(start, _expr.Length - start));
    }

    // ── numeric literals ──────────────────────────────────────────────────────────────────

    // XPath 2.0 §3.1.1, §A.2.1, and §A.2.4
    private Token ReadNumericLiteral(int start)
    {
        bool hasDot = false;
        bool hasExponent = false;

        // Optionally leading dot (e.g. .5)
        if (_pos < _expr.Length && _expr[_pos] == '.')
        {
            hasDot = true;
            _pos++; // skip '.'
        }

        // Integer part (or fractional part when leading dot)
        while (_pos < _expr.Length && _expr[_pos] >= '0' && _expr[_pos] <= '9')
        {
            _pos++;
        }

        // Decimal point (only when we started with digits, not a leading dot already handled)
        if (!hasDot && _pos < _expr.Length && _expr[_pos] == '.')
        {
            // Distinguish '..' (DotDot) from decimal: a lone '.' followed by another '.' is not decimal.
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '.')
            {
                // e.g. "1.." — the '.' is part of the number only if the token already has digits.
                // "1.." → IntegerLiteral(1) then DotDot. We stop here.
            }
            else
            {
                hasDot = true;
                _pos++; // skip '.'
                while (_pos < _expr.Length && _expr[_pos] >= '0' && _expr[_pos] <= '9')
                {
                    _pos++;
                }
            }
        }

        // Exponent part
        if (_pos < _expr.Length && (_expr[_pos] == 'e' || _expr[_pos] == 'E'))
        {
            int ePos = _pos;
            _pos++; // skip 'e'/'E'
            if (_pos < _expr.Length && (_expr[_pos] == '+' || _expr[_pos] == '-'))
            {
                _pos++;
            }

            if (_pos >= _expr.Length || _expr[_pos] < '0' || _expr[_pos] > '9')
            {
                // Invalid exponent: no digits follow
                throw new XPathStaticException(
                    XPathErrors.XPST0003,
                    $"Invalid numeric literal: exponent has no digits at offset {ePos}",
                    _baseSpan.WithRange(ePos, _pos - ePos));
            }

            while (_pos < _expr.Length && _expr[_pos] >= '0' && _expr[_pos] <= '9')
            {
                _pos++;
            }

            hasExponent = true;
        }

        // XPath 2.0 §A.2.4 — a numeric literal must be delimited from a following name-start char.
        if (_pos < _expr.Length && IsNameStartChar(_expr[_pos]))
        {
            throw new XPathStaticException(
                XPathErrors.XPST0003,
                $"Numeric literal at offset {start} must be followed by whitespace or an operator, not a name character",
                _baseSpan.WithRange(start, _pos - start + 1));
        }

        int length = _pos - start;
        string raw = _expr.Substring(start, length);

        if (hasExponent)
        {
            return new Token(TokenKind.DoubleLiteral, start, length, raw);
        }

        if (hasDot)
        {
            return new Token(TokenKind.DecimalLiteral, start, length, raw);
        }

        return new Token(TokenKind.IntegerLiteral, start, length, raw);
    }

    // ── names and keyword detection ───────────────────────────────────────────────────────

    // XPath 2.0 §A.2.2 and XML §2.3 NCName rules
    private Token ReadNameOrKeyword(int start)
    {
        // Scan the maximal NCName (or QName prefix up to ':').
        ScanNcName();

        // Peek at the character after the name fragment.
        if (_pos < _expr.Length && _expr[_pos] == ':')
        {
            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == ':')
            {
                // "prefix::" — emit Name for the prefix, leave '::' in stream.
                string ncName = _expr.Substring(start, _pos - start);
                return ClassifyName(start, ncName);
            }

            if (_pos + 1 < _expr.Length && _expr[_pos + 1] == '*')
            {
                // "prefix:*" — PrefixWildcard
                string prefix = _expr.Substring(start, _pos - start);
                _pos += 2; // skip ':*'
                return new Token(TokenKind.PrefixWildcard, start, _pos - start, prefix);
            }

            if (_pos + 1 < _expr.Length && IsNameStartChar(_expr[_pos + 1]))
            {
                // "prefix:local" — QName
                _pos++; // skip ':'
                ScanNcName();
                string qname = _expr.Substring(start, _pos - start);
                return ClassifyName(start, qname);
            }

            // Lone ':' after a name — emit as Name; parser will error if needed.
            string name = _expr.Substring(start, _pos - start);
            return ClassifyName(start, name);
        }

        {
            string name = _expr.Substring(start, _pos - start);
            return ClassifyName(start, name);
        }
    }

    /// <summary>Advances <see cref="_pos"/> over a maximal NCName (letters, digits, '.', '-', '_').</summary>
    private void ScanNcName()
    {
        // The first character was already verified as a name-start char by the caller.
        _pos++;
        while (_pos < _expr.Length && IsNameChar(_expr[_pos]))
        {
            _pos++;
        }
    }

    /// <summary>
    /// Applies operator-position keyword recognition to a raw name, handling multi-word
    /// keywords (<c>instance of</c>, <c>treat as</c>, <c>castable as</c>, <c>cast as</c>).
    /// </summary>
    private Token ClassifyName(int start, string name)
    {
        if (!_operatorExpected)
        {
            // Operand position: all keywords are plain Names.
            return new Token(TokenKind.Name, start, _pos - start, name);
        }

        // Operator position: check for keyword.
        // XPath 2.0 §A.2.2 Table 10 — operator keywords
        switch (name)
        {
            case "and":        return new Token(TokenKind.And, start, _pos - start);
            case "or":         return new Token(TokenKind.Or, start, _pos - start);
            case "div":        return new Token(TokenKind.Div, start, _pos - start);
            case "idiv":       return new Token(TokenKind.IDiv, start, _pos - start);
            case "mod":        return new Token(TokenKind.Mod, start, _pos - start);
            case "union":      return new Token(TokenKind.Union, start, _pos - start);
            case "intersect":  return new Token(TokenKind.Intersect, start, _pos - start);
            case "except":     return new Token(TokenKind.Except, start, _pos - start);
            case "to":         return new Token(TokenKind.To, start, _pos - start);
            case "eq":         return new Token(TokenKind.Eq, start, _pos - start);
            case "ne":         return new Token(TokenKind.Ne, start, _pos - start);
            case "lt":         return new Token(TokenKind.Lt, start, _pos - start);
            case "le":         return new Token(TokenKind.Le, start, _pos - start);
            case "gt":         return new Token(TokenKind.Gt, start, _pos - start);
            case "ge":         return new Token(TokenKind.Ge, start, _pos - start);
            case "is":         return new Token(TokenKind.Is, start, _pos - start);
            case "return":     return new Token(TokenKind.Return, start, _pos - start);
            case "satisfies":  return new Token(TokenKind.Satisfies, start, _pos - start);
            case "then":       return new Token(TokenKind.Then, start, _pos - start);
            case "else":       return new Token(TokenKind.Else, start, _pos - start);
            case "in":         return new Token(TokenKind.In, start, _pos - start);

            // Multi-word keywords: peek for the second word.
            case "instance":
            {
                Token? second = TryPeekWordAhead("of");
                if (second.HasValue)
                {
                    int end = second.Value.Start + second.Value.Length;
                    return new Token(TokenKind.InstanceOf, start, end - start);
                }
                return new Token(TokenKind.Name, start, _pos - start, name);
            }
            case "treat":
            {
                Token? second = TryPeekWordAhead("as");
                if (second.HasValue)
                {
                    int end = second.Value.Start + second.Value.Length;
                    return new Token(TokenKind.TreatAs, start, end - start);
                }
                return new Token(TokenKind.Name, start, _pos - start, name);
            }
            case "castable":
            {
                Token? second = TryPeekWordAhead("as");
                if (second.HasValue)
                {
                    int end = second.Value.Start + second.Value.Length;
                    return new Token(TokenKind.CastableAs, start, end - start);
                }
                return new Token(TokenKind.Name, start, _pos - start, name);
            }
            case "cast":
            {
                Token? second = TryPeekWordAhead("as");
                if (second.HasValue)
                {
                    int end = second.Value.Start + second.Value.Length;
                    return new Token(TokenKind.CastAs, start, end - start);
                }
                return new Token(TokenKind.Name, start, _pos - start, name);
            }

            default:
                return new Token(TokenKind.Name, start, _pos - start, name);
        }
    }

    /// <summary>
    /// Skips whitespace from <see cref="_pos"/> and checks whether the next characters
    /// form the given <paramref name="word"/> (case-sensitive, must be delimited).
    /// If yes, advances <see cref="_pos"/> past the word and returns a name-token for it;
    /// otherwise leaves <see cref="_pos"/> unchanged and returns null.
    /// </summary>
    private Token? TryPeekWordAhead(string word)
    {
        int savedPos = _pos;

        // Skip whitespace.
        while (_pos < _expr.Length)
        {
            char w = _expr[_pos];
            if (w == ' ' || w == '\t' || w == '\r' || w == '\n')
            {
                _pos++;
            }
            else if (_pos + 1 < _expr.Length && w == '(' && _expr[_pos + 1] == ':')
            {
                SkipComment();
            }
            else
            {
                break;
            }
        }

        int wordStart = _pos;
        int wordLen = word.Length;

        // Match the word characters exactly.
        if (wordStart + wordLen > _expr.Length)
        {
            _pos = savedPos;
            return null;
        }

        for (int i = 0; i < wordLen; i++)
        {
            if (_expr[wordStart + i] != word[i])
            {
                _pos = savedPos;
                return null;
            }
        }

        // The character after the word must NOT be a name char (so "instance offset" doesn't match "of").
        int after = wordStart + wordLen;
        if (after < _expr.Length && IsNameChar(_expr[after]))
        {
            _pos = savedPos;
            return null;
        }

        _pos = after;
        return new Token(TokenKind.Name, wordStart, wordLen, word);
    }

    // ── star / wildcard ───────────────────────────────────────────────────────────────────

    // XPath 2.0 §A.2.2 and §A.3 — Wildcard token
    private Token ReadStar(int start)
    {
        _pos++; // skip '*'

        // "*:local" — LocalWildcard
        if (_pos < _expr.Length && _expr[_pos] == ':')
        {
            if (_pos + 1 < _expr.Length && IsNameStartChar(_expr[_pos + 1]))
            {
                _pos++; // skip ':'
                int localStart = _pos;
                ScanNcName();
                string local = _expr.Substring(localStart, _pos - localStart);
                return new Token(TokenKind.LocalWildcard, start, _pos - start, local);
            }
        }

        return new Token(TokenKind.Star, start, 1);
    }

    // ── operator-position state machine ──────────────────────────────────────────────────

    // XPath 2.0 §A.2.2 Table 10
    private void UpdateOperatorState(TokenKind kind)
    {
        // After these tokens, the NEXT token is in OPERAND position.
        // (i.e., _operatorExpected = false after operators / start-of-input / open brackets)
        switch (kind)
        {
            // Tokens after which an operator keyword may appear next.
            case TokenKind.Name:
            case TokenKind.StringLiteral:
            case TokenKind.IntegerLiteral:
            case TokenKind.DecimalLiteral:
            case TokenKind.DoubleLiteral:
            case TokenKind.RightParen:
            case TokenKind.RightBracket:
            case TokenKind.Dot:
            case TokenKind.DotDot:
            case TokenKind.Star:          // multiply
            case TokenKind.PrefixWildcard:
            case TokenKind.LocalWildcard:
                _operatorExpected = true;
                break;

            // All other tokens leave the lexer in OPERAND position.
            default:
                _operatorExpected = false;
                break;
        }
    }

    // ── character classification helpers ─────────────────────────────────────────────────

    // XML 1.0 §2.3 — NameStartChar (using XmlConvert for correctness)
    private static bool IsNameStartChar(char c)
    {
        // Fast ASCII path first (covers the vast majority of XPath expressions).
        if (c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
        {
            return true;
        }

        if (c < 128)
        {
            return false; // other ASCII chars are not name-start chars
        }

        return XmlConvert.IsStartNCNameChar(c);
    }

    // XML 1.0 §2.3 — NameChar (NameStartChar | digits | '.', '-', combining chars, extenders)
    private static bool IsNameChar(char c)
    {
        // Fast ASCII path.
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
            || c == '_' || c == '-' || c == '.')
        {
            return true;
        }

        if (c < 128)
        {
            return false;
        }

        return XmlConvert.IsNCNameChar(c);
    }
}
