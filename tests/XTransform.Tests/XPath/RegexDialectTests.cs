using System.Text.RegularExpressions;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Functions;

namespace XTransform.Tests.XPath;

/// <summary>
/// F&amp;O §7.6.1 — the XPath 2.0 regular-expression dialect: XML Schema regex plus anchors, reluctant
/// quantifiers, back-references and the s m i x flags. Constructs that are .NET or XPath 3.0 additions
/// (non-capturing groups, (?i), \b, \z, \x41, A, an unescaped { } ]) are FORX0002.
/// </summary>
[TestFixture]
internal sealed class RegexDialectTests
{
    private static bool Matches(string pattern, string input, string flags = "")
    {
        // Arrange / Act
        XPathRegexTranslator.Translate(pattern, flags, SourceSpan.None, out string dotNet, out RegexOptions options);
        return new Regex(dotNet, options).IsMatch(input);
    }

    // ── Valid patterns: (pattern, flags, input, expected) ───────────────────────────

    [TestCase("a*b", "", "aaab", true)]
    [TestCase("^a", "", "abc", true)]
    [TestCase("^b", "", "abc", false)]
    [TestCase("c$", "", "abc", true)]
    [TestCase("a$", "", "a\n", false)]                    // without m, $ is the very end only
    [TestCase("^b", "m", "a\nb", true)]
    [TestCase("a$", "m", "a\nb", true)]
    [TestCase(".", "", "\n", false)]
    [TestCase(".", "s", "\n", true)]
    [TestCase("^.$", "", "\U0001F600", true)]              // a supplementary character is one character
    [TestCase("^..$", "", "\U0001F600", false)]
    [TestCase("a+?", "", "aaa", true)]
    [TestCase("(a)\\1", "", "aa", true)]
    [TestCase("(a)\\1", "", "ab", false)]
    [TestCase("(a)(b)\\2\\1", "", "abba", true)]
    [TestCase("(.)\\1", "", "xx", true)]
    [TestCase("a{2}", "", "aa", true)]
    [TestCase("^a{2}$", "", "aaa", false)]
    [TestCase("^a{2,3}$", "", "aaaa", false)]
    [TestCase("^a{2,}$", "", "aaaaa", true)]
    [TestCase("^a{0}$", "", "", true)]
    [TestCase("^(a|b)*$", "", "abba", true)]
    [TestCase("^$", "", "", true)]
    [TestCase("^()$", "", "", true)]
    [TestCase("a|", "", "b", true)]                        // an empty alternative matches the empty string
    [TestCase("\\.", "", ".", true)]
    [TestCase("\\.", "", "x", false)]
    [TestCase("\\$", "", "$", true)]
    [TestCase("\\^\\-\\{\\}", "", "^-{}", true)]
    [TestCase("a\\nb", "", "a\nb", true)]
    [TestCase("a#b", "x", "a#b", true)]                    // x removes whitespace only; # is an ordinary character
    [TestCase("a b", "x", "ab", true)]
    [TestCase("a b", "", "a b", true)]
    [TestCase("[ ]", "x", " ", true)]                      // whitespace inside a class is kept
    [TestCase("a", "i", "A", true)]
    [TestCase("[a-c]+", "i", "ABC", true)]
    [TestCase("\\p{Lu}", "", "A", true)]
    [TestCase("\\p{Lu}", "", "a", false)]
    [TestCase("\\P{Lu}", "", "a", true)]
    [TestCase("\\P{Lu}", "", "A", false)]
    [TestCase("\\p{L}", "", "\U00010400", true)]           // DESERET CAPITAL LETTER LONG I
    [TestCase("\\P{L}", "", "\U00010400", false)]
    [TestCase("\\P{Ll}", "", "\U00010428", false)]         // a lone half of a surrogate pair must not match
    [TestCase("\\p{IsBasicLatin}", "", "a", true)]
    [TestCase("\\p{IsBasicLatin}", "", "é", false)]
    [TestCase("\\P{IsBasicLatin}", "", "é", true)]
    [TestCase("\\p{IsGreek}", "", "α", true)]
    [TestCase("\\p{IsGothic}", "", "\U00010330", true)]
    [TestCase("\\p{IsGothic}", "", "a", false)]
    [TestCase("\\P{IsGothic}", "", "a", true)]
    [TestCase("\\p{IsPrivateUse}", "", "", true)]
    [TestCase("\\p{IsPrivateUse}", "", "\U000F0000", true)]
    [TestCase("\\p{IsPrivateUse}", "", "a", false)]
    [TestCase("\\p{IsMusicalSymbols}", "", "\U0001D11E", true)]
    [TestCase("[a-z]", "", "m", true)]
    [TestCase("[^a-z]", "", "m", false)]
    [TestCase("[^a-z]", "", "M", true)]
    [TestCase("[^a]", "", "\U00010428", true)]             // a negated class consumes a whole character
    [TestCase("^[^a]$", "", "\U00010428", true)]
    [TestCase("[\U00010400-\U0001044F]", "", "\U00010428", true)]
    [TestCase("[\U00010400-\U0001044F]", "", "a", false)]
    [TestCase("[-a]", "", "-", true)]
    [TestCase("[a-]", "", "-", true)]
    [TestCase("[\\-a]", "", "-", true)]
    [TestCase("[\\]]", "", "]", true)]
    [TestCase("[\\[]", "", "[", true)]
    [TestCase("[a-z-[aeiou]]", "", "b", true)]
    [TestCase("[a-z-[aeiou]]", "", "e", false)]
    [TestCase("[\\p{L}-[a-z]]", "", "m", false)]
    [TestCase("[\\p{L}-[a-z]]", "", "M", true)]
    [TestCase("[^a-z-[A-Z]]", "", "5", true)]
    [TestCase("[a-c-[b]]+", "", "ac", true)]
    [TestCase("[\\d]", "", "\U0001D7CE", true)]            // MATHEMATICAL BOLD DIGIT ZERO
    [TestCase("[\\D]", "", "a", true)]
    [TestCase("[\\s]", "", " ", true)]
    [TestCase("[\\S]", "", " ", false)]
    [TestCase("[\\S]", "", "\U0001F600", true)]
    [TestCase("[\\w]", "", "a", true)]
    [TestCase("[\\w]", "", "!", false)]
    [TestCase("[\\W]", "", "!", true)]
    [TestCase("[\\i]", "", "a", true)]
    [TestCase("[\\i]", "", "1", false)]
    [TestCase("[\\c]", "", "1", true)]
    [TestCase("[\\i\\c]+:[\\i\\c]+", "", "ns:name", true)]
    [TestCase("[\\s\\i]*", "", " a b", true)]
    [TestCase("\\s", "", "\t", true)]
    [TestCase("\\s", "", " ", false)]                // only the four XML whitespace characters
    [TestCase("\\S", "", " ", true)]
    [TestCase("\\d", "", "7", true)]
    [TestCase("\\d", "", "٣", true)]                  // Arabic-Indic digit three
    [TestCase("\\d", "", "a", false)]
    [TestCase("\\D", "", "a", true)]
    [TestCase("\\w", "", "é", true)]
    [TestCase("\\w", "", "!", false)]
    [TestCase("\\w", "", " ", false)]
    [TestCase("\\W", "", "!", true)]
    [TestCase("\\i", "", "a", true)]
    [TestCase("\\i", "", "1", false)]
    [TestCase("\\I", "", "1", true)]
    [TestCase("\\c", "", "-", true)]
    [TestCase("\\C", "", "-", false)]
    [TestCase("\\C", "", " ", true)]
    public void Translate_ValidPattern_MatchesAsSpecified(string pattern, string flags, string input, bool expected)
    {
        Assert.That(Matches(pattern, input, flags), Is.EqualTo(expected));
    }

    // ── Invalid patterns: FORX0002 ─────────────────────────────────────────────────

    [TestCase("(?:a)")]                // non-capturing group: XPath 3.0
    [TestCase("(?i)a")]
    [TestCase("(?i:a)")]
    [TestCase("(?<n>a)")]
    [TestCase("(?=a)")]
    [TestCase("(?!a)")]
    [TestCase("(?<=a)b")]
    [TestCase("(?>a)")]
    [TestCase("(?#comment)a")]
    [TestCase("(?(1)a|b)(x)?")]
    [TestCase("\\b")]
    [TestCase("\\B")]
    [TestCase("\\A")]
    [TestCase("\\z")]
    [TestCase("\\Z")]
    [TestCase("\\G")]
    [TestCase("\\x41")]
    [TestCase("\\u0041")]
    [TestCase("\\U00000041")]
    [TestCase("\\0")]
    [TestCase("\\012")]
    [TestCase("\\a")]
    [TestCase("\\e")]
    [TestCase("\\f")]
    [TestCase("\\v")]
    [TestCase("\\k<n>")]
    [TestCase("\\_")]
    [TestCase("\\ ")]
    [TestCase("\\1")]                  // no such group
    [TestCase("(a)\\2")]
    [TestCase("(a\\1)")]               // the group is not closed yet
    [TestCase("\\1(a)")]               // forward reference
    [TestCase("a{5")]
    [TestCase("a{5,")]
    [TestCase("a{5,6")]
    [TestCase("a{,2}")]
    [TestCase("a{2,1}")]
    [TestCase("a{}")]
    [TestCase("a{x}")]
    [TestCase("a{-1}")]
    [TestCase("{5}")]
    [TestCase("a{2}{3}")]
    [TestCase("*a")]
    [TestCase("+a")]
    [TestCase("?a")]
    [TestCase("a**")]
    [TestCase("a+*")]
    [TestCase("a???")]
    [TestCase("(*a)")]
    [TestCase("|*")]
    [TestCase("a]")]
    [TestCase("]")]
    [TestCase("a}")]
    [TestCase("{")]
    [TestCase("}")]
    [TestCase("[")]
    [TestCase("[a")]
    [TestCase("[]")]
    [TestCase("[^]")]
    [TestCase("[]a]")]
    [TestCase("[^]a]")]
    [TestCase("[z-a]")]
    [TestCase("[a-\\d]")]
    [TestCase("[a--]")]
    [TestCase("[a-z-[aeiou]")]
    [TestCase("[a-z-[aeiou]x]")]
    [TestCase("[[a]]")]
    [TestCase("[a[b]]")]
    [TestCase("[^[a-b]]")]
    [TestCase("[\\")]
    [TestCase("[\\u0041]")]
    [TestCase("[\\x41]")]
    [TestCase("[\\b]")]
    [TestCase("[\\1]")]
    [TestCase("[\\p{Foo}]")]
    [TestCase("\\p{Foo}")]
    [TestCase("\\P{Foo}")]
    [TestCase("\\p{IsFoo}")]
    [TestCase("\\p{Cs}")]              // not an XML Schema category
    [TestCase("\\pL")]
    [TestCase("\\p")]
    [TestCase("\\p{L")]
    [TestCase("\\p{}")]
    [TestCase("(")]
    [TestCase(")")]
    [TestCase("a)")]
    [TestCase("(a")]
    [TestCase("((a)")]
    [TestCase("\\")]
    [TestCase("a\\")]
    public void Translate_InvalidPattern_RaisesFORX0002(string pattern)
    {
        // Arrange / Act
        XPathDynamicException? ex = Assert.Throws<XPathDynamicException>(
            () => XPathRegexTranslator.Translate(pattern, string.Empty, SourceSpan.None, out _, out _));

        // Assert
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORX0002));
    }

    [TestCase("q")]
    [TestCase("g")]
    [TestCase("S")]
    [TestCase("sx!")]
    public void ParseFlags_UnknownFlag_RaisesFORX0001(string flags)
    {
        XPathDynamicException? ex = Assert.Throws<XPathDynamicException>(
            () => XPathRegexTranslator.Translate("a", flags, SourceSpan.None, out _, out _));

        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORX0001));
    }

    [Test]
    public void Translate_ReluctantQuantifier_MatchesTheShortestString()
    {
        // Arrange
        XPathRegexTranslator.Translate("a+?", string.Empty, SourceSpan.None, out string dotNet, out RegexOptions options);

        // Act
        Match m = new Regex(dotNet, options).Match("aaa");

        // Assert
        Assert.That(m.Value, Is.EqualTo("a"));
    }

    [Test]
    public void Translate_SupplementaryCharacterQuantified_RepeatsTheWholeCharacter()
    {
        Assert.That(Matches("^\U0001F600{3}$", "\U0001F600\U0001F600\U0001F600"), Is.True);
    }
}
