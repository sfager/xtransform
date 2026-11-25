// F&O §7.6 — XPath 2.0 / XSD regex dialect translation to .NET regex.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using XTransform.Xdm;

namespace XTransform.XPath.Functions;

/// <summary>
/// Translates an XPath 2.0 / XSD regex pattern to a .NET
/// <see cref="System.Text.RegularExpressions.Regex"/> pattern plus a <see cref="RegexOptions"/> value.
/// XPath 2.0 F&amp;O §7.6 defines the regex dialect.
/// </summary>
// F&O §7.6 / XSD 1.0 Part 2 Appendix F
internal static class XPathRegexTranslator
{
    /// <summary>
    /// Translates an XPath 2.0 regex pattern and flag string to a .NET-compatible
    /// pattern and <see cref="RegexOptions"/>.
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORX0001"/>
    /// for invalid flags and <see cref="XPathErrors.FORX0002"/> for invalid patterns.
    /// Uses out parameters rather than a ValueTuple return to remain compatible with net462.
    /// </summary>
    /// <param name="xpathPattern">The XPath 2.0 / XSD regex pattern string.</param>
    /// <param name="flags">The flag string (characters 's', 'm', 'i', 'x').</param>
    /// <param name="location">Source location for error reporting.</param>
    /// <param name="dotNetPattern">The translated .NET-compatible pattern string.</param>
    /// <param name="options">The computed <see cref="RegexOptions"/>.</param>
    // F&O §7.6
    public static void Translate(
        string xpathPattern, string flags, SourceSpan location,
        out string dotNetPattern, out RegexOptions options)
    {
        // Parse flags first so we know if 's' (dot-all) is active.
        options = ParseFlags(flags, location);
        bool dotAll = (options & RegexOptions.Singleline) != 0;

        dotNetPattern = TranslatePattern(xpathPattern, dotAll, location);
    }

    /// <summary>
    /// Parses the XPath 2.0 flag string. Valid flags: s (dot-all), m (multiline),
    /// i (case-insensitive), x (ignore-whitespace/comment mode).
    /// Throws <see cref="XPathDynamicException"/> with <see cref="XPathErrors.FORX0001"/>
    /// on unknown flags.
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
                    // F&O §7.6.1: 's' makes '.' match any character including newline.
                    // .NET equivalent: RegexOptions.Singleline
                    options |= RegexOptions.Singleline;
                    break;
                case 'm':
                    // F&O §7.6.1: 'm' makes ^ and $ match start/end of line.
                    options |= RegexOptions.Multiline;
                    break;
                case 'i':
                    // F&O §7.6.1: 'i' makes matching case-insensitive.
                    options |= RegexOptions.IgnoreCase;
                    break;
                case 'x':
                    // F&O §7.6.1: 'x' causes whitespace to be ignored, allows # comments.
                    options |= RegexOptions.IgnorePatternWhitespace;
                    break;
                default:
                    // F&O §7.6.1: any other flag character is an error (FORX0001).
                    throw new XPathDynamicException(
                        XPathErrors.FORX0001,
                        $"Invalid regular expression flag '{c}'. Valid flags are: s, m, i, x.",
                        location);
            }
        }

        return options;
    }

    /// <summary>
    /// Translates the XPath 2.0 / XSD regex pattern to a .NET-compatible pattern.
    /// </summary>
    // F&O §7.6 / XSD 1.0 Part 2 Appendix F
    private static string TranslatePattern(string pattern, bool dotAll, SourceSpan location)
    {
        if (pattern.Length == 0)
        {
            return pattern;
        }

        StringBuilder sb = new StringBuilder(pattern.Length + 8);
        int i = 0;
        int len = pattern.Length;

        while (i < len)
        {
            char c = pattern[i];

            switch (c)
            {
                case '.':
                    // F&O §7.6: '.' matches any character except \n and \r by default.
                    // With 's' flag, it matches everything — .NET Singleline handles that.
                    // '.' matches one code point, so a surrogate pair is consumed as a unit.
                    if (dotAll)
                    {
                        sb.Append("(?:").Append(SurrogatePair).Append('|').Append(UnitGuard).Append("[\\s\\S])");
                    }
                    else
                    {
                        // XSD 1.0 Part 2 §F.1.4: any character except \n and \r.
                        sb.Append("(?:").Append(SurrogatePair).Append('|').Append(UnitGuard).Append("[^\\n\\r])");
                    }

                    i++;
                    break;

                case '\\':
                    // Escape sequence: process the next character.
                    i = TranslateEscape(pattern, i, sb, location);
                    break;

                case '[':
                    // Character class: copy until closing ']', handling nesting and escapes.
                    // .NET supports XSD character class subtraction [a-z-[aeiou]] natively.
                    i = TranslateCharClass(pattern, i, sb, location);
                    break;

                case '(':
                    // Check for lookaround assertions (not allowed in XPath 2.0).
                    // F&O §7.6: lookaround is not part of the XPath 2.0 regex grammar.
                    if (i + 1 < len && pattern[i + 1] == '?')
                    {
                        if (i + 2 < len)
                        {
                            char next2 = pattern[i + 2];
                            if (next2 == '=' || next2 == '!')
                            {
                                // (?= lookahead or (?! negative lookahead
                                throw new XPathDynamicException(
                                    XPathErrors.FORX0002,
                                    "Lookahead assertions (?= and (?! are not allowed in XPath 2.0 regular expressions.",
                                    location);
                            }

                            if (next2 == '<' && i + 3 < len)
                            {
                                char next3 = pattern[i + 3];
                                if (next3 == '=' || next3 == '!')
                                {
                                    // (?<= lookbehind or (?<! negative lookbehind
                                    throw new XPathDynamicException(
                                        XPathErrors.FORX0002,
                                        "Lookbehind assertions (?<= and (?<! are not allowed in XPath 2.0 regular expressions.",
                                        location);
                                }
                            }
                        }
                    }

                    sb.Append(c);
                    i++;
                    break;

                default:
                    sb.Append(c);
                    i++;
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Processes an escape sequence starting at <paramref name="pos"/> (the backslash position).
    /// Returns the new position after the escape.
    /// </summary>
    // XSD 1.0 Part 2 Appendix F §F.1.1
    private static int TranslateEscape(string pattern, int pos, StringBuilder sb, SourceSpan location)
    {
        int len = pattern.Length;
        // pos is the backslash; pos+1 is the escape char.
        if (pos + 1 >= len)
        {
            // Trailing backslash — invalid pattern.
            throw new XPathDynamicException(
                XPathErrors.FORX0002,
                "Trailing backslash in regular expression pattern.",
                location);
        }

        char escaped = pattern[pos + 1];

        switch (escaped)
        {
            case 's':
                // XSD §F.1.1: \s = [ \t\n\r] (exactly these 4 chars, not Unicode whitespace).
                // .NET \s is Unicode-aware so we translate explicitly.
                // XSD 1.0 Part 2 §F.1.1
                sb.Append("[ \\t\\r\\n]");
                return pos + 2;

            case 'S':
                // XSD §F.1.1: \S = complement of \s = [^ \t\n\r]
                sb.Append("(?:").Append(SurrogatePair).Append("|[^ \\t\\r\\n\\uD800-\\uDFFF])");
                return pos + 2;

            case 'd':
                // XSD §F.1.1: \d = \p{Nd} (decimal digit), including supplementary-plane digits.
                TryAppendAstralCategory("\\p{Nd}", 0, 6, false, sb);
                return pos + 2;

            case 'D':
                // XSD §F.1.1: \D = complement of \d = \P{Nd}
                TryAppendAstralCategory("\\P{Nd}", 0, 6, true, sb);
                return pos + 2;

            case 'w':
                // XSD §F.1.1: \w = everything except \p{P}, \p{Z}, \p{C}.
                // .NET \w is roughly [a-zA-Z0-9_] unless Unicode mode is used.
                // For P2-13 scope: pass through .NET \w as an approximation.
                // Note: .NET \w with no ECMAScript flag matches Unicode letters/digits/connector punctuation.
                // This is a reasonable approximation; exact XSD semantics would require
                // [^\p{P}\p{Z}\p{C}] but that is equivalent for most practical patterns.
                sb.Append("\\w");
                return pos + 2;

            case 'W':
                // XSD §F.1.1: \W = complement of \w
                sb.Append("\\W");
                return pos + 2;

            case 'i':
                // XSD §F.1.3: \i = initial name character (letter | _ | :)
                // Approximate: [a-zA-Z_:À-˿Ͱ-ͽͿ-῿‌-‍⁰-↏Ⰰ-⿯、-퟿豈-﷏ﷰ-�]
                // For P2-13: use \p{L}|[_:] as a reasonable approximation.
                sb.Append("[\\p{L}_:]");
                return pos + 2;

            case 'I':
                // XSD §F.1.3: \I = complement of \i
                sb.Append("[^\\p{L}_:]");
                return pos + 2;

            case 'c':
                // XSD §F.1.3: \c = name character (letter | digit | . | - | _ | : | combining | extender)
                // Approximate: \p{L}|\p{Nd}|[.\-_:·̀-ͯ‿-⁀]
                sb.Append("[\\p{L}\\p{Nd}.\\-_:\\u00B7\\u0300-\\u036F\\u203F-\\u2040]");
                return pos + 2;

            case 'C':
                // XSD §F.1.3: \C = complement of \c
                sb.Append("[^\\p{L}\\p{Nd}.\\-_:\\u00B7\\u0300-\\u036F\\u203F-\\u2040]");
                return pos + 2;

            case 'p':
            case 'P':
            {
                // Unicode category or block: \p{X} or \P{X}
                // Both XPath 2.0 and .NET use the same syntax; pass through unchanged.
                // XSD 1.0 Part 2 §F.1.1 / F&O §7.6
                int end = FindCategoryEnd(pattern, pos + 2, location);
                if (!TryAppendAstralCategory(pattern, pos, end, escaped == 'P', sb))
                {
                    sb.Append(pattern, pos, end - pos);
                }

                return end;
            }

            case '0':
                // F&O §7.6: \0 is not a valid backreference in XPath 2.0.
                throw new XPathDynamicException(
                    XPathErrors.FORX0002,
                    "\\0 is not a valid escape in XPath 2.0 regular expressions. Backreferences start at \\1.",
                    location);

            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                // Backreferences \1..\9 are supported. Pass through.
                sb.Append('\\');
                sb.Append(escaped);
                return pos + 2;

            default:
                // All other escapes: pass through to .NET (e.g. \n, \r, \t, \^, \$, etc.)
                sb.Append('\\');
                sb.Append(escaped);
                return pos + 2;
        }
    }

    /// <summary>
    /// Finds the end position (exclusive) of a \p{...} or \P{...} category escape
    /// starting at <paramref name="bracePos"/> (the character after \p or \P).
    /// </summary>
    private static int FindCategoryEnd(string pattern, int bracePos, SourceSpan location)
    {
        if (bracePos >= pattern.Length || pattern[bracePos] != '{')
        {
            throw new XPathDynamicException(
                XPathErrors.FORX0002,
                "Expected '{' after \\p or \\P in regular expression pattern.",
                location);
        }

        int close = pattern.IndexOf('}', bracePos + 1);
        if (close < 0)
        {
            throw new XPathDynamicException(
                XPathErrors.FORX0002,
                "Unclosed \\p{...} or \\P{...} in regular expression pattern.",
                location);
        }

        return close + 1;
    }

    /// <summary>
    /// Copies a character class <c>[...]</c> starting at <paramref name="pos"/> (the '[' position)
    /// into <paramref name="sb"/>. Handles nested character classes (for subtraction)
    /// and escape sequences within the class.
    /// .NET supports XSD character class subtraction <c>[a-z-[aeiou]]</c> natively.
    /// Returns the new position after the closing ']'.
    /// </summary>
    // XSD 1.0 Part 2 §F.1.2
    private static int TranslateCharClass(string pattern, int pos, StringBuilder sb, SourceSpan location)
    {
        if (TryTranslateAstralClass(pattern, pos, sb, location, out int astralEnd))
        {
            return astralEnd;
        }

        int len = pattern.Length;
        // Track nesting depth: '[' increases, ']' decreases.
        int depth = 0;

        while (pos < len)
        {
            char c = pattern[pos];

            if (c == '[')
            {
                depth++;
                sb.Append(c);
                pos++;
            }
            else if (c == ']')
            {
                depth--;
                sb.Append(c);
                pos++;
                if (depth == 0)
                {
                    // End of outermost character class.
                    return pos;
                }
            }
            else if (c == '\\')
            {
                // Inside a character class, process escape sequences.
                // Note: '.' inside a character class is a literal dot, so only escapes matter.
                if (pos + 1 >= len)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORX0002,
                        "Trailing backslash inside character class in regular expression pattern.",
                        location);
                }

                char escaped = pattern[pos + 1];
                switch (escaped)
                {
                    case 's':
                        // \s inside a class: expand to literal set (no wrapping [] needed in a class).
                        sb.Append(" \\t\\r\\n");
                        pos += 2;
                        break;
                    case 'S':
                        // \S is [^ \t\r\n]; but inside a class we can't nest a negated class.
                        // .NET does not support [^\S] inline — this is a known limitation.
                        // For P2-13: pass through \S and let .NET handle it inside the class.
                        // This may not be exactly correct for deeply nested cases but handles common patterns.
                        sb.Append("\\S");
                        pos += 2;
                        break;
                    case 'p':
                    case 'P':
                    {
                        int end = FindCategoryEnd(pattern, pos + 2, location);
                        sb.Append(pattern, pos, end - pos);
                        pos = end;
                        break;
                    }
                    case '0':
                        throw new XPathDynamicException(
                            XPathErrors.FORX0002,
                            "\\0 is not a valid escape in XPath 2.0 regular expressions.",
                            location);
                    default:
                        sb.Append('\\');
                        sb.Append(escaped);
                        pos += 2;
                        break;
                }
            }
            else
            {
                sb.Append(c);
                pos++;
            }
        }

        // Reached end of pattern without closing ']'.
        throw new XPathDynamicException(
            XPathErrors.FORX0002,
            "Unclosed character class '[' in regular expression pattern.",
            location);
    }

    // ── Supplementary-plane (surrogate pair) support ─────────────────────────────

    private const string SurrogatePair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";

    // A single UTF-16 unit that is not part of a surrogate pair: guards against matching the
    // high or low half of a pair when matching starts mid-character.
    private const string UnitGuard = "(?!" + SurrogatePair + ")(?<![\\uD800-\\uDBFF])";

    /// <summary>
    /// Translates <c>\p{X}</c> / <c>\P{X}</c> (outside a class) so that it also matches
    /// supplementary-plane code points. Returns false for non-category names (blocks).
    /// </summary>
    private static bool TryAppendAstralCategory(string pattern, int pos, int end, bool negate, StringBuilder sb)
    {
        string name = pattern.Substring(pos + 3, end - pos - 4);
        List<CodePointRange>? ranges = AstralCategories.GetRanges(name, negate);
        if (ranges is null)
        {
            return false;
        }

        string? astral = AstralCategories.ToPattern(ranges);
        sb.Append("(?:").Append(UnitGuard).Append('\\').Append(negate ? 'P' : 'p').Append('{').Append(name).Append('}');
        if (astral is not null)
        {
            sb.Append('|').Append(astral);
        }

        sb.Append(')');
        return true;
    }

    /// <summary>
    /// Translates a character class that mentions Unicode categories or supplementary-plane
    /// characters into an alternation of a BMP class and surrogate-pair patterns. Classes with
    /// subtraction (nested '[') and classes needing no astral handling return false and are
    /// translated by the ordinary path.
    /// </summary>
    private static bool TryTranslateAstralClass(string pattern, int pos, StringBuilder sb, SourceSpan location, out int newPos)
    {
        newPos = pos;
        int len = pattern.Length;
        int i = pos + 1;
        bool negated = i < len && pattern[i] == '^';
        if (negated)
        {
            i++;
        }

        StringBuilder bmp = new StringBuilder();
        List<CodePointRange> astral = new List<CodePointRange>();
        bool needsAstral = false;
        bool first = true;

        while (i < len)
        {
            char c = pattern[i];
            if (c == ']' && !first)
            {
                break;
            }

            first = false;
            if (c == '[')
            {
                return false; // subtraction: leave to the ordinary path
            }

            if (c == '\\')
            {
                if (i + 1 >= len)
                {
                    return false;
                }

                char e = pattern[i + 1];
                if (e == 'p' || e == 'P')
                {
                    int end = FindCategoryEnd(pattern, i + 2, location);
                    string name = pattern.Substring(i + 3, end - i - 4);
                    List<CodePointRange>? ranges = AstralCategories.GetRanges(name, e == 'P');
                    bmp.Append(pattern, i, end - i);
                    if (ranges is not null)
                    {
                        astral.AddRange(ranges);
                        needsAstral = true;
                    }

                    i = end;
                    continue;
                }

                if (e == 's')
                {
                    bmp.Append(" \\t\\r\\n");
                }
                else if (e == '0')
                {
                    return false;
                }
                else
                {
                    bmp.Append('\\').Append(e);
                }

                i += 2;
                continue;
            }

            // Literal code point (possibly a surrogate pair), possibly the start of a range.
            int cp = ReadCodePoint(pattern, i, out int width);
            int next = i + width;
            if (next + 1 < len && pattern[next] == '-' && pattern[next + 1] != ']' && pattern[next + 1] != '[')
            {
                int lastCp = pattern[next + 1] == '\\' ? -1 : ReadCodePoint(pattern, next + 1, out int w2);
                if (lastCp >= 0)
                {
                    int rangeEnd = next + 1 + (lastCp >= 0x10000 ? 2 : 1);
                    if (cp >= 0x10000 || lastCp >= 0x10000)
                    {
                        needsAstral = true;
                        if (cp < 0x10000)
                        {
                            AppendUnitRange(bmp, cp, 0xFFFF);
                        }

                        astral.Add(new CodePointRange(Math.Max(cp, 0x10000), lastCp));
                    }
                    else
                    {
                        bmp.Append(pattern, i, rangeEnd - i);
                    }

                    i = rangeEnd;
                    continue;
                }
            }

            if (cp >= 0x10000)
            {
                needsAstral = true;
                astral.Add(new CodePointRange(cp, cp));
            }
            else
            {
                bmp.Append(pattern, i, width);
            }

            i = next;
        }

        // A negated class must consume a whole code point even when it names no astral members.
        if (i >= len || (!needsAstral && !negated))
        {
            return false;
        }

        string? astralPattern = astral.Count == 0 ? null : AstralCategories.ToPattern(AstralCategories.Union(astral));
        if (negated)
        {
            sb.Append("(?:[^").Append(bmp).Append("\\uD800-\\uDFFF]|");
            if (astralPattern is not null)
            {
                sb.Append("(?!").Append(astralPattern).Append(')');
            }

            sb.Append(SurrogatePair).Append(')');
        }
        else
        {
            sb.Append("(?:");
            if (bmp.Length > 0)
            {
                sb.Append('[').Append(bmp).Append(']');
                if (astralPattern is not null)
                {
                    sb.Append('|');
                }
            }

            if (astralPattern is not null)
            {
                sb.Append(astralPattern);
            }
            else if (bmp.Length == 0)
            {
                sb.Append("(?!)");
            }

            sb.Append(')');
        }

        newPos = i + 1;
        return true;
    }

    private static int ReadCodePoint(string s, int index, out int width)
    {
        if (char.IsHighSurrogate(s[index]) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
        {
            width = 2;
            return char.ConvertToUtf32(s[index], s[index + 1]);
        }

        width = 1;
        return s[index];
    }

    private static void AppendUnitRange(StringBuilder sb, int first, int last)
    {
        sb.Append("\\u").Append(first.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))
          .Append("-\\u").Append(last.ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
    }
}
