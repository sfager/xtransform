// F&O §7.6 — XPath 2.0 regex functions: fn:matches, fn:replace, fn:tokenize.

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §7.6 XPath 2.0 regular expression functions:
/// fn:matches, fn:replace, fn:tokenize.
/// The XPath 2.0 / XSD regex dialect is translated to .NET
/// <see cref="System.Text.RegularExpressions.Regex"/> by <see cref="XPathRegexTranslator"/>.
/// </summary>
// F&O §7.6
internal static class RegexFunctions
{
    // Compiled Regex cache keyed by "pattern\x00flags".
    // Uses a string key rather than a ValueTuple to avoid the net462 ValueTuple dependency.
    // Static: shared across all runs; patterns are immutable so thread-safe.
    private static readonly ConcurrentDictionary<string, Regex>
        s_regexCache = new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);

    // =========================================================================
    // F&O §7.6.2 — fn:matches
    // =========================================================================

    /// <summary>
    /// Implements fn:matches($input as xs:string?, $pattern as xs:string) as xs:boolean.
    /// </summary>
    // F&O §7.6.2
    internal static IXdmIterator FnMatches2(DynamicContext ctx, XdmSequence[] args)
    {
        string input = StringArgOrEmpty(args[0]);
        string pattern = RequiredString(args[1]);
        Regex rx = GetOrCompile(pattern, string.Empty, default);
        bool matched = rx.IsMatch(input);
        return new SingleItemIterator(XdmAtomicValue.Boolean(matched));
    }

    /// <summary>
    /// Implements fn:matches($input as xs:string?, $pattern as xs:string,
    /// $flags as xs:string) as xs:boolean.
    /// </summary>
    // F&O §7.6.2
    internal static IXdmIterator FnMatches3(DynamicContext ctx, XdmSequence[] args)
    {
        string input = StringArgOrEmpty(args[0]);
        string pattern = RequiredString(args[1]);
        string flags = RequiredString(args[2]);
        Regex rx = GetOrCompile(pattern, flags, default);
        bool matched = rx.IsMatch(input);
        return new SingleItemIterator(XdmAtomicValue.Boolean(matched));
    }

    // =========================================================================
    // F&O §7.6.3 — fn:replace
    // =========================================================================

    /// <summary>
    /// Implements fn:replace($input as xs:string?, $pattern as xs:string,
    /// $replacement as xs:string) as xs:string.
    /// </summary>
    // F&O §7.6.3
    internal static IXdmIterator FnReplace3(DynamicContext ctx, XdmSequence[] args)
    {
        return ReplaceImpl(args[0], args[1], args[2], null);
    }

    /// <summary>
    /// Implements fn:replace($input as xs:string?, $pattern as xs:string,
    /// $replacement as xs:string, $flags as xs:string) as xs:string.
    /// </summary>
    // F&O §7.6.3
    internal static IXdmIterator FnReplace4(DynamicContext ctx, XdmSequence[] args)
    {
        return ReplaceImpl(args[0], args[1], args[2], args[3]);
    }

    private static SingleItemIterator ReplaceImpl(
        XdmSequence inputSeq, XdmSequence patternSeq, XdmSequence replacementSeq,
        XdmSequence? flagsSeq)
    {
        string input = StringArgOrEmpty(inputSeq);
        string pattern = RequiredString(patternSeq);
        string replacement = RequiredString(replacementSeq);
        string flags = flagsSeq is null ? string.Empty : RequiredString(flagsSeq);

        Regex rx = GetOrCompile(pattern, flags, default);

        // F&O §7.6.3: if the pattern matches the empty string it is a dynamic error FORX0003.
        if (rx.IsMatch(string.Empty))
        {
            throw new XPathDynamicException(
                XPathErrors.FORX0003,
                "fn:replace: the regular expression pattern matches the empty string.",
                default);
        }

        // Translate the XPath 2.0 replacement string to .NET replacement string.
        // F&O §7.6.3
        string netReplacement = TranslateReplacement(replacement, default);

        string result = rx.Replace(input, netReplacement);
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    // =========================================================================
    // F&O §7.6.4 — fn:tokenize
    // =========================================================================

    /// <summary>
    /// Implements fn:tokenize($input as xs:string?, $pattern as xs:string) as xs:string*.
    /// </summary>
    // F&O §7.6.4
    internal static IXdmIterator FnTokenize2(DynamicContext ctx, XdmSequence[] args)
    {
        return TokenizeImpl(args[0], args[1], null);
    }

    /// <summary>
    /// Implements fn:tokenize($input as xs:string?, $pattern as xs:string,
    /// $flags as xs:string) as xs:string*.
    /// </summary>
    // F&O §7.6.4
    internal static IXdmIterator FnTokenize3(DynamicContext ctx, XdmSequence[] args)
    {
        return TokenizeImpl(args[0], args[1], args[2]);
    }

    private static IXdmIterator TokenizeImpl(
        XdmSequence inputSeq, XdmSequence patternSeq, XdmSequence? flagsSeq)
    {
        string input = StringArgOrEmpty(inputSeq);
        string pattern = RequiredString(patternSeq);
        string flags = flagsSeq is null ? string.Empty : RequiredString(flagsSeq);

        Regex rx = GetOrCompile(pattern, flags, default);

        // F&O §7.6.4: if the pattern matches the empty string it is a dynamic error FORX0003.
        if (rx.IsMatch(string.Empty))
        {
            throw new XPathDynamicException(
                XPathErrors.FORX0003,
                "fn:tokenize: the regular expression pattern matches the empty string.",
                default);
        }

        // F&O §7.6.4: if the input is the empty string, the result is the empty sequence.
        if (input.Length == 0)
        {
            return EmptyIterator.Instance;
        }

        // Regex.Split preserves empty tokens; F&O §7.6.4 preserves them too.
        string[] tokens = rx.Split(input);

        // F&O §7.6.4: empty last token due to trailing separator is NOT discarded by the spec.
        // However, if input ends with the separator, Regex.Split adds an extra empty string
        // which is consistent with the spec (the token after the final separator is empty).
        int count = tokens.Length;
        XdmItem[] items = new XdmItem[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = XdmAtomicValue.String(tokens[i]);
        }

        return new ArrayIterator(items);
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Returns the compiled <see cref="Regex"/> for the given XPath pattern and flags,
    /// using the static cache. The cache key is "pattern\x00flags".
    /// </summary>
    // F&O §7.6
    internal static Regex GetOrCompile(string xpathPattern, string flags, SourceSpan location)
    {
        // Use '\x00' (NUL) as separator since it cannot appear in XPath patterns.
        string key = xpathPattern + "\x00" + flags;
        if (s_regexCache.TryGetValue(key, out Regex? cached))
        {
            return cached;
        }

        string dotNetPattern;
        RegexOptions options;
        XPathRegexTranslator.Translate(xpathPattern, flags, location, out dotNetPattern, out options);

        // Add RegexOptions.Compiled for net8.0+ hot paths where it is beneficial.
        // On .NET Framework 4.6.2 this still works but may be slightly slower to construct.
#if NET8_0_OR_GREATER
        options |= RegexOptions.Compiled;
#endif

        Regex rx;
        try
        {
            rx = new Regex(dotNetPattern, options);
        }
        catch (ArgumentException ex)
        {
            throw new XPathDynamicException(
                XPathErrors.FORX0002,
                "Invalid regular expression pattern: " + ex.Message,
                location);
        }

        // Store in cache; if another thread already stored a value, that is fine — use the cached one.
        s_regexCache.TryAdd(key, rx);
        return rx;
    }

    /// <summary>
    /// Translates an XPath 2.0 replacement string to a .NET replacement string.
    /// XPath replacement syntax per F&amp;O §7.6.3:
    /// <list type="bullet">
    ///   <item>$0 → whole match; $1..$9 → captured groups</item>
    ///   <item>\$ → literal dollar sign (translated to .NET $$)</item>
    ///   <item>\\ → literal backslash</item>
    ///   <item>$ not followed by a digit → FORX0004</item>
    /// </list>
    /// </summary>
    // F&O §7.6.3
    private static string TranslateReplacement(string replacement, SourceSpan location)
    {
        if (replacement.Length == 0)
        {
            return replacement;
        }

        StringBuilder sb = new StringBuilder(replacement.Length + 4);
        int i = 0;
        int len = replacement.Length;

        while (i < len)
        {
            char c = replacement[i];

            if (c == '\\')
            {
                // Escape sequence in replacement.
                if (i + 1 >= len)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORX0004,
                        "Trailing backslash in fn:replace replacement string.",
                        location);
                }

                char next = replacement[i + 1];
                switch (next)
                {
                    case '$':
                        // \$ → literal dollar sign: .NET replacement uses $$ for literal $.
                        sb.Append("$$");
                        i += 2;
                        break;
                    case '\\':
                        // \\ → literal backslash: in .NET replacement, backslash is literal.
                        sb.Append('\\');
                        i += 2;
                        break;
                    default:
                        // F&O §7.6.3: \x for other x is an error in the replacement string.
                        throw new XPathDynamicException(
                            XPathErrors.FORX0004,
                            $"Invalid escape '\\{next}' in fn:replace replacement string. Only \\$ and \\\\ are valid escapes.",
                            location);
                }
            }
            else if (c == '$')
            {
                // F&O §7.6.3: $ must be followed by a digit (0-9) or it is an error (FORX0004).
                if (i + 1 >= len)
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORX0004,
                        "Trailing '$' in fn:replace replacement string; '$' must be followed by a digit.",
                        location);
                }

                char next = replacement[i + 1];
                if (next < '0' || next > '9')
                {
                    throw new XPathDynamicException(
                        XPathErrors.FORX0004,
                        $"'$' in fn:replace replacement string must be followed by a digit (0-9), not '{next}'.",
                        location);
                }

                // $N → .NET ${N} syntax — pass through $N directly; .NET supports $0..$9.
                sb.Append('$');
                sb.Append(next);
                i += 2;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns the string value of the first item in <paramref name="seq"/>,
    /// or <see cref="string.Empty"/> if the sequence is empty.
    /// Used for the optional xs:string? input parameter per F&amp;O §7.6.
    /// </summary>
    // F&O §7.6.2: empty sequence input is treated as the empty string.
    private static string StringArgOrEmpty(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }

    /// <summary>
    /// Returns the string value of the first item in <paramref name="seq"/>.
    /// Used for required string parameters.
    /// </summary>
    private static string RequiredString(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }
}
