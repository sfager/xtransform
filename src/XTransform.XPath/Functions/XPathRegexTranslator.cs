// F&O §7.6 — XPath 2.0 regular expressions.
//
// The dialect is the XML Schema 1.0 regular-expression language (Part 2, Appendix F) plus the
// extensions of F&O 1.0 §7.6.1: the anchors ^ and $, reluctant quantifiers (*? +? ?? {n,m}?),
// capturing groups with back-references (\1 .. \9 and longer), and the flags s, m, i and x.
// Everything else — non-capturing groups, (?i) options, \b, \A, \z, \x41, A, octal escapes,
// lookaround, atomic groups, conditionals, an unescaped { } ] — is an error (FORX0002); those
// are XPath 3.0 or .NET constructs. This class parses a pattern with that grammar, rejects what
// is invalid, and emits an equivalent .NET pattern that works on whole code points: a character
// outside the Basic Multilingual Plane (a UTF-16 surrogate pair) is matched as one character.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using XTransform.Xdm;

namespace XTransform.XPath.Functions;

/// <summary>Validates an XPath 2.0 regex and translates it to a .NET pattern.</summary>
internal static class XPathRegexTranslator
{
    private const string SurrogatePair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";

    // A single UTF-16 unit that is not half of a surrogate pair: guards against matching the high or
    // low half of a pair when matching starts mid-character.
    private const string UnitGuard = "(?!" + SurrogatePair + ")(?<![\\uD800-\\uDBFF])";

    private static readonly Lazy<List<CodePointRange>> s_punctuationSeparatorOther =
        new Lazy<List<CodePointRange>>(() =>
        {
            List<CodePointRange> all = new List<CodePointRange>();
            all.AddRange(RegexUnicodeTables.CategoryRanges("P"));
            all.AddRange(RegexUnicodeTables.CategoryRanges("Z"));
            all.AddRange(RegexUnicodeTables.CategoryRanges("C"));
            return AstralCategories.Union(all);
        });

    /// <summary>
    /// Translates an XPath 2.0 pattern and flag string to a .NET pattern plus <see cref="RegexOptions"/>.
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORX0001"/> for invalid
    /// flags and <see cref="XPathErrors.FORX0002"/> for invalid patterns.
    /// </summary>
    /// <param name="xpathPattern">The XPath 2.0 / XSD regex pattern string.</param>
    /// <param name="flags">The flag string (characters 's', 'm', 'i', 'x').</param>
    /// <param name="location">Source location for error reporting.</param>
    /// <param name="dotNetPattern">The translated .NET-compatible pattern string.</param>
    /// <param name="options">The computed <see cref="RegexOptions"/>.</param>
    // F&O §7.6.1
    public static void Translate(
        string xpathPattern, string flags, SourceSpan location,
        out string dotNetPattern, out RegexOptions options)
    {
        options = ParseFlags(flags, location);
        bool dotAll = (options & RegexOptions.Singleline) != 0;
        bool multiline = (options & RegexOptions.Multiline) != 0;
        bool extended = (options & RegexOptions.IgnorePatternWhitespace) != 0;

        // The x flag only removes whitespace outside character classes (F&O §7.6.1); .NET's own
        // IgnorePatternWhitespace would also make '#' start a comment, so it is not passed on.
        options &= ~RegexOptions.IgnorePatternWhitespace;
        if ((options & RegexOptions.IgnoreCase) != 0)
        {
            options |= RegexOptions.CultureInvariant;
        }

        string source = extended ? StripWhitespace(xpathPattern) : xpathPattern;
        dotNetPattern = new Parser(source, dotAll, multiline, location).Parse();
    }

    /// <summary>
    /// Parses the XPath 2.0 flag string. Valid flags: s (dot-all), m (multiline),
    /// i (case-insensitive), x (ignore whitespace). Throws <see cref="XPathDynamicException"/>
    /// with <see cref="XPathErrors.FORX0001"/> on unknown flags.
    /// </summary>
    /// <param name="flags">The flag string.</param>
    /// <param name="location">Source location for error reporting.</param>
    /// <returns>The corresponding <see cref="RegexOptions"/>.</returns>
    // F&O §7.6.1
    public static RegexOptions ParseFlags(string flags, SourceSpan location)
    {
        RegexOptions options = RegexOptions.None;
        for (int i = 0; i < flags.Length; i++)
        {
            char c = flags[i];
            switch (c)
            {
                case 's':
                    options |= RegexOptions.Singleline;
                    break;
                case 'm':
                    options |= RegexOptions.Multiline;
                    break;
                case 'i':
                    options |= RegexOptions.IgnoreCase;
                    break;
                case 'x':
                    options |= RegexOptions.IgnorePatternWhitespace;
                    break;
                default:
                    throw new XPathDynamicException(
                        XPathErrors.FORX0001,
                        $"Invalid regular expression flag '{c}'. Valid flags are: s, m, i, x.",
                        location);
            }
        }

        return options;
    }

    // Removes #x9 #xA #xD #x20 outside character classes (the x flag).
    private static string StripWhitespace(string pattern)
    {
        StringBuilder sb = new StringBuilder(pattern.Length);
        int depth = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                sb.Append(c).Append(pattern[++i]);
                continue;
            }

            if (c == '[')
            {
                depth++;
            }
            else if (c == ']' && depth > 0)
            {
                depth--;
            }

            if (depth == 0 && (c == ' ' || c == '\t' || c == '\n' || c == '\r'))
            {
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    // ── A character class under construction ─────────────────────────────────────

    private sealed class ClassBuilder
    {
        /// <summary>Items valid inside a .NET character class (basic-plane characters, ranges, \p{..}).</summary>
        public StringBuilder Bmp { get; } = new StringBuilder();

        /// <summary>Supplementary-plane members.</summary>
        public List<CodePointRange> Astral { get; } = new List<CodePointRange>();

        /// <summary>True when a BMP item could match half of a surrogate pair (\P{..}, \p{C}).</summary>
        public bool NeedsGuard { get; set; }

        public bool Negated { get; set; }

        public ClassBuilder? Subtract { get; set; }

        public void AddCodePoint(int cp)
        {
            if (cp >= 0x10000)
            {
                Astral.Add(new CodePointRange(cp, cp));
            }
            else
            {
                AppendUnit(cp);
            }
        }

        public void AddRange(int lo, int hi)
        {
            // Basic plane without the surrogate code points, then the supplementary planes.
            AddBmpRange(lo, Math.Min(hi, 0xD7FF));
            AddBmpRange(Math.Max(lo, 0xE000), Math.Min(hi, 0xFFFF));
            if (hi >= 0x10000)
            {
                Astral.Add(new CodePointRange(Math.Max(lo, 0x10000), hi));
            }
        }

        public void AddRanges(IEnumerable<CodePointRange> ranges)
        {
            foreach (CodePointRange r in ranges)
            {
                AddRange(r.First, r.Last);
            }
        }

        public void AddCategory(string name, bool negate)
        {
            if (negate)
            {
                Bmp.Append("\\P{").Append(name).Append('}');
                NeedsGuard = true;
                AddAstral(AstralCategories.GetRanges(name, negate: true));
            }
            else
            {
                Bmp.Append("\\p{").Append(name).Append('}');

                // .NET's C includes the surrogates (Cs), which XSD's C does not.
                NeedsGuard |= name == "C";
                AddAstral(AstralCategories.GetRanges(name, negate: false));
            }
        }

        public void AddBlock(string name, bool negate)
        {
            CodePointRange[]? own = RegexUnicodeTables.GetOwnBlock(name);
            if (own is not null)
            {
                AddRanges(negate ? RegexUnicodeTables.Complement(AstralCategories.Union(own)) : new List<CodePointRange>(own));
            }
            else if (negate)
            {
                Bmp.Append("\\P{").Append(name).Append('}');
                NeedsGuard = true;
                Astral.Add(new CodePointRange(0x10000, 0x10FFFF));
            }
            else
            {
                Bmp.Append("\\p{").Append(name).Append('}');
            }
        }

        private void AddAstral(List<CodePointRange>? ranges)
        {
            if (ranges is not null)
            {
                Astral.AddRange(ranges);
            }
        }

        private void AddBmpRange(int lo, int hi)
        {
            if (lo > hi)
            {
                return;
            }

            AppendUnit(lo);
            if (hi > lo)
            {
                Bmp.Append('-');
                AppendUnit(hi);
            }
        }

        private void AppendUnit(int unit) =>
            Bmp.Append("\\u").Append(unit.ToString("X4", CultureInfo.InvariantCulture));
    }

    // ── The parser ───────────────────────────────────────────────────────────────

    private sealed class Parser
    {
        private readonly string _s;
        private readonly bool _dotAll;
        private readonly bool _multiline;
        private readonly SourceSpan _location;
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<bool> _groupClosed = new List<bool>();
        private int _pos;

        public Parser(string source, bool dotAll, bool multiline, SourceSpan location)
        {
            _s = source;
            _dotAll = dotAll;
            _multiline = multiline;
            _location = location;
        }

        public string Parse()
        {
            ParseRegExp();
            if (_pos < _s.Length)
            {
                throw Error("unmatched ')'");
            }

            return _sb.ToString();
        }

        // regExp ::= branch ( '|' branch )*
        private void ParseRegExp()
        {
            ParseBranch();
            while (_pos < _s.Length && _s[_pos] == '|')
            {
                _pos++;
                _sb.Append('|');
                ParseBranch();
            }
        }

        // branch ::= piece*
        private void ParseBranch()
        {
            while (_pos < _s.Length && _s[_pos] != '|' && _s[_pos] != ')')
            {
                ParsePiece();
            }
        }

        // piece ::= atom quantifier?
        private void ParsePiece()
        {
            ParseAtom();
            ParseQuantifier();
        }

        private void ParseAtom()
        {
            char c = _s[_pos];
            switch (c)
            {
                case '(':
                    ParseGroup();
                    return;

                case '[':
                    _sb.Append(Emit(ParseClass()));
                    return;

                case '.':
                    _pos++;
                    _sb.Append("(?:").Append(SurrogatePair).Append('|').Append(UnitGuard)
                       .Append(_dotAll ? "[\\s\\S]" : "[^\\n\\r]").Append(')');
                    return;

                case '\\':
                    ParseEscape();
                    return;

                case '^':
                    _pos++;
                    _sb.Append('^');
                    return;

                case '$':
                    // Without the m flag, $ matches only at the very end of the string (.NET's $ would
                    // also match before a final newline).
                    _pos++;
                    _sb.Append(_multiline ? "$" : "\\z");
                    return;

                case '?':
                case '*':
                case '+':
                case '{':
                case '}':
                case ']':
                    throw Error($"'{c}' is not allowed here and must be escaped");

                default:
                    AppendLiteral(ReadCodePoint());
                    return;
            }
        }

        private void ParseGroup()
        {
            _pos++; // '('
            if (_pos < _s.Length && _s[_pos] == '?')
            {
                throw Error("'(?' constructs (non-capturing groups, options, lookaround, conditionals) are not part of XPath 2.0 regular expressions");
            }

            int index = _groupClosed.Count;
            _groupClosed.Add(false);
            _sb.Append('(');
            ParseRegExp();
            if (_pos >= _s.Length || _s[_pos] != ')')
            {
                throw Error("unclosed '('");
            }

            _pos++;
            _groupClosed[index] = true;
            _sb.Append(')');
        }

        // quantifier ::= ( [?*+] | '{' n (',' m?)? '}' ) '?'?
        private void ParseQuantifier()
        {
            if (_pos >= _s.Length)
            {
                return;
            }

            char c = _s[_pos];
            if (c == '?' || c == '*' || c == '+')
            {
                _pos++;
                _sb.Append(c);
            }
            else if (c == '{')
            {
                ParseBraceQuantifier();
            }
            else
            {
                return;
            }

            if (_pos < _s.Length && _s[_pos] == '?')
            {
                _pos++;
                _sb.Append('?'); // reluctant
            }
        }

        private void ParseBraceQuantifier()
        {
            _pos++; // '{'
            long min = ReadNumber("a quantifier needs a number after '{'");
            long max = min;
            bool unbounded = false;
            if (_pos < _s.Length && _s[_pos] == ',')
            {
                _pos++;
                if (_pos < _s.Length && _s[_pos] == '}')
                {
                    unbounded = true;
                }
                else
                {
                    max = ReadNumber("a quantifier needs a number after ','");
                }
            }

            if (_pos >= _s.Length || _s[_pos] != '}')
            {
                throw Error("unclosed quantifier '{'");
            }

            _pos++;
            if (!unbounded && max < min)
            {
                throw Error("the minimum of a quantifier exceeds its maximum");
            }

            _sb.Append('{').Append(min);
            if (unbounded)
            {
                _sb.Append(',');
            }
            else if (max != min)
            {
                _sb.Append(',').Append(max);
            }

            _sb.Append('}');
        }

        private long ReadNumber(string message)
        {
            int start = _pos;
            long value = 0;
            while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
            {
                value = (value * 10) + (_s[_pos] - '0');
                if (value > int.MaxValue)
                {
                    throw Error("a quantifier is too large");
                }

                _pos++;
            }

            if (_pos == start)
            {
                throw Error(message);
            }

            return value;
        }

        // ── Escapes outside a character class ───────────────────────────────────────

        private void ParseEscape()
        {
            _pos++; // '\'
            if (_pos >= _s.Length)
            {
                throw Error("a trailing backslash");
            }

            char c = _s[_pos];
            if (c >= '1' && c <= '9')
            {
                ParseBackReference();
                return;
            }

            ClassBuilder? set = TryParseSetEscape(c);
            if (set is not null)
            {
                _sb.Append(Emit(set));
                return;
            }

            AppendLiteral(ReadSingleCharEscape());
        }

        // F&O §7.6.1: \N refers to the Nth capturing group, which must already be closed.
        private void ParseBackReference()
        {
            int n = _s[_pos] - '0';
            _pos++;
            while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9' &&
                   (n * 10) + (_s[_pos] - '0') <= _groupClosed.Count)
            {
                n = (n * 10) + (_s[_pos] - '0');
                _pos++;
            }

            if (n > _groupClosed.Count || !_groupClosed[n - 1])
            {
                throw Error($"back-reference \\{n} refers to a group that is not closed");
            }

            _sb.Append("(?:\\").Append(n).Append(')');
        }

        // Parses \s \S \i \I \c \C \d \D \w \W \p{..} \P{..} when the escape (positioned at its letter) is one.
        private ClassBuilder? TryParseSetEscape(char c)
        {
            ClassBuilder cb = new ClassBuilder();
            switch (c)
            {
                case 's':
                    _pos++;
                    cb.Bmp.Append(" \\t\\n\\r");
                    return cb;
                case 'S':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.Complement(new[]
                    {
                        new CodePointRange(9, 10), new CodePointRange(13, 13), new CodePointRange(32, 32),
                    }));
                    return cb;
                case 'd':
                    _pos++;
                    cb.AddCategory("Nd", negate: false);
                    return cb;
                case 'D':
                    _pos++;
                    cb.AddCategory("Nd", negate: true);
                    return cb;
                case 'w':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.Complement(s_punctuationSeparatorOther.Value));
                    return cb;
                case 'W':
                    _pos++;
                    cb.AddRanges(s_punctuationSeparatorOther.Value);
                    return cb;
                case 'i':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.InitialNameChars());
                    return cb;
                case 'I':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.Complement(RegexUnicodeTables.InitialNameChars()));
                    return cb;
                case 'c':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.NameChars());
                    return cb;
                case 'C':
                    _pos++;
                    cb.AddRanges(RegexUnicodeTables.Complement(RegexUnicodeTables.NameChars()));
                    return cb;
                case 'p':
                case 'P':
                    _pos++;
                    ParseCategoryOrBlock(cb, negate: c == 'P');
                    return cb;
                default:
                    return null;
            }
        }

        // \p{Lu} or \p{IsBasicLatin}; positioned just after the 'p' / 'P'.
        private void ParseCategoryOrBlock(ClassBuilder cb, bool negate)
        {
            if (_pos >= _s.Length || _s[_pos] != '{')
            {
                throw Error("expected '{' after \\p or \\P");
            }

            int close = _s.IndexOf('}', _pos + 1);
            if (close < 0)
            {
                throw Error("unclosed \\p{...}");
            }

            string name = _s.Substring(_pos + 1, close - _pos - 1);
            _pos = close + 1;
            if (RegexUnicodeTables.IsCategory(name))
            {
                cb.AddCategory(name, negate);
            }
            else if (RegexUnicodeTables.IsBlock(name))
            {
                cb.AddBlock(name, negate);
            }
            else
            {
                throw Error($"'{name}' is not a recognised Unicode category or block");
            }
        }

        // SingleCharEsc: \n \r \t and \ | . - ^ ? * + { } ( ) [ ] $ ; positioned at the character after '\'.
        private int ReadSingleCharEscape()
        {
            char c = _s[_pos];
            _pos++;
            switch (c)
            {
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case '\\':
                case '|':
                case '.':
                case '-':
                case '^':
                case '?':
                case '*':
                case '+':
                case '{':
                case '}':
                case '(':
                case ')':
                case '[':
                case ']':
                case '$':
                    return c;
                default:
                    throw Error($"'\\{c}' is not a valid escape");
            }
        }

        // ── Character classes ──────────────────────────────────────────────────────

        // charClassExpr ::= '[' charGroup ']' with optional '^' and '-[' subtraction; positioned at '['.
        private ClassBuilder ParseClass()
        {
            _pos++; // '['
            ClassBuilder cb = new ClassBuilder();
            if (_pos < _s.Length && _s[_pos] == '^')
            {
                cb.Negated = true;
                _pos++;
            }

            bool any = false;
            while (true)
            {
                if (_pos >= _s.Length)
                {
                    throw Error("unclosed '['");
                }

                char c = _s[_pos];
                if (c == ']')
                {
                    if (!any)
                    {
                        throw Error("empty character class");
                    }

                    _pos++;
                    return cb;
                }

                if (c == '[')
                {
                    throw Error("'[' must be escaped inside a character class");
                }

                if (c == '-' && any && _pos + 1 < _s.Length && _s[_pos + 1] == '[')
                {
                    // Subtraction: [a-z-[aeiou]]
                    _pos++;
                    cb.Subtract = ParseClass();
                    if (_pos >= _s.Length || _s[_pos] != ']')
                    {
                        throw Error("a subtracted class must end the character class");
                    }

                    _pos++;
                    return cb;
                }

                ParseClassItem(cb);
                any = true;
            }
        }

        private void ParseClassItem(ClassBuilder cb)
        {
            int lo;
            if (_s[_pos] == '\\')
            {
                _pos++;
                if (_pos >= _s.Length)
                {
                    throw Error("a trailing backslash");
                }

                ClassBuilder? set = TryParseSetEscape(_s[_pos]);
                if (set is not null)
                {
                    Merge(cb, set);
                    return;
                }

                lo = ReadSingleCharEscape();
            }
            else
            {
                lo = ReadCodePoint();
            }

            // A range: lo '-' hi (the '-' is literal when last in the class or before a subtraction).
            if (_pos + 1 < _s.Length && _s[_pos] == '-' && _s[_pos + 1] != ']' && _s[_pos + 1] != '[')
            {
                _pos++;
                int hi;
                if (_s[_pos] == '\\')
                {
                    _pos++;
                    if (_pos >= _s.Length)
                    {
                        throw Error("a trailing backslash");
                    }

                    if (IsOneOf("sSdDwWiIcCpP", _s[_pos]))
                    {
                        throw Error("a multi-character escape cannot end a range");
                    }

                    hi = ReadSingleCharEscape();
                }
                else if (_s[_pos] == '-')
                {
                    throw Error("'-' must be escaped as the end of a range");
                }
                else
                {
                    hi = ReadCodePoint();
                }

                if (hi < lo)
                {
                    throw Error("the end of a character range precedes its start");
                }

                cb.AddRange(lo, hi);
                return;
            }

            cb.AddCodePoint(lo);
        }

        private static void Merge(ClassBuilder target, ClassBuilder source)
        {
            target.Bmp.Append(source.Bmp);
            target.Astral.AddRange(source.Astral);
            target.NeedsGuard |= source.NeedsGuard;
        }

        // ── Emission ────────────────────────────────────────────────────────────────

        private static string Emit(ClassBuilder cb)
        {
            string body = EmitGroup(cb);
            return cb.Subtract is null
                ? body
                : "(?:(?!" + Emit(cb.Subtract) + ")" + body + ")";
        }

        private static string EmitGroup(ClassBuilder cb)
        {
            string? astral = cb.Astral.Count == 0 ? null : AstralCategories.ToPattern(AstralCategories.Union(cb.Astral));
            string bmp = cb.Bmp.ToString();

            if (cb.Negated)
            {
                string unit = "[^" + bmp + "\\uD800-\\uDFFF]";
                string pair = astral is null ? SurrogatePair : "(?!" + astral + ")" + SurrogatePair;
                return "(?:" + unit + "|" + pair + ")";
            }

            if (bmp.Length == 0 && astral is null)
            {
                return "(?!)"; // an empty set matches nothing
            }

            if (astral is null)
            {
                return cb.NeedsGuard ? "(?:" + UnitGuard + "[" + bmp + "])" : "[" + bmp + "]";
            }

            if (bmp.Length == 0)
            {
                return "(?:" + astral + ")";
            }

            return "(?:" + (cb.NeedsGuard ? UnitGuard : string.Empty) + "[" + bmp + "]|" + astral + ")";
        }

        private void AppendLiteral(int cp)
        {
            if (cp >= 0x10000)
            {
                string pair = char.ConvertFromUtf32(cp);
                _sb.Append("(?:\\u").Append(((int)pair[0]).ToString("X4", CultureInfo.InvariantCulture))
                   .Append("\\u").Append(((int)pair[1]).ToString("X4", CultureInfo.InvariantCulture)).Append(')');
                return;
            }

            char c = (char)cp;
            if (IsOneOf("\\*+?|{}[]()^$.#", c))
            {
                _sb.Append('\\').Append(c);
            }
            else if (c == '\n')
            {
                _sb.Append("\\n");
            }
            else if (c == '\r')
            {
                _sb.Append("\\r");
            }
            else if (c == '\t')
            {
                _sb.Append("\\t");
            }
            else
            {
                _sb.Append(c);
            }
        }

        private static bool IsOneOf(string set, char c)
        {
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == c)
                {
                    return true;
                }
            }

            return false;
        }

        // Reads one code point (a surrogate pair counts as one) and advances.
        private int ReadCodePoint()
        {
            char c = _s[_pos];
            if (char.IsHighSurrogate(c) && _pos + 1 < _s.Length && char.IsLowSurrogate(_s[_pos + 1]))
            {
                int cp = char.ConvertToUtf32(c, _s[_pos + 1]);
                _pos += 2;
                return cp;
            }

            if (char.IsSurrogate(c))
            {
                throw Error("an unpaired surrogate");
            }

            _pos++;
            return c;
        }

        private XPathDynamicException Error(string message) =>
            new XPathDynamicException(
                XPathErrors.FORX0002,
                $"Invalid regular expression: {message} (at offset {_pos}).",
                _location);
    }
}
