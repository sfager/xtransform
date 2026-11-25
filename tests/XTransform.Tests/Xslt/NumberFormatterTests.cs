using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §12.3 — number to string conversion.</summary>
[TestFixture]
internal sealed class NumberFormatterTests
{
    private static readonly long[] s_oneMillionish = { 1234567L };

    private static readonly string[] s_expectedTokens = { "A", "001", "i" };

    private static readonly string[] s_expectedSeparators = { string.Empty, "-", "(" };

    private static string Format(string format, params long[] numbers) =>
        NumberFormatter.Format(numbers, format, lang: null, letterValue: null, ordinal: null, groupingSeparator: null, groupingSize: 0);

    // ── Decimal tokens ──────────────────────────────────────────────────────────

    [TestCase("1", 7L, "7")]
    [TestCase("01", 7L, "07")]
    [TestCase("001", 7L, "007")]
    [TestCase("01", 123L, "123")]
    [TestCase("1", 0L, "0")]
    [TestCase("١", 12L, "١٢")]       // Arabic-Indic one: Arabic-Indic digits
    [TestCase("١", 0L, "٠")]
    public void Format_DecimalToken_UsesTheDigitFamily(string format, long n, string expected)
    {
        // Arrange / Act
        string actual = Format(format, n);

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // ── Alphabetic and roman ─────────────────────────────────────────────────────

    [TestCase("a", 1L, "a")]
    [TestCase("a", 26L, "z")]
    [TestCase("a", 27L, "aa")]
    [TestCase("a", 52L, "az")]
    [TestCase("a", 53L, "ba")]
    [TestCase("A", 703L, "AAA")]
    [TestCase("i", 4L, "iv")]
    [TestCase("i", 9L, "ix")]
    [TestCase("I", 1994L, "MCMXCIV")]
    [TestCase("i", 3999L, "mmmcmxcix")]
    public void Format_AlphabeticAndRoman_FollowTheSequences(string format, long n, string expected)
    {
        Assert.That(Format(format, n), Is.EqualTo(expected));
    }

    [TestCase("a", 0L, "0")]          // below the lower bound of the sequence: token "1"
    [TestCase("i", 0L, "0")]
    [TestCase("I", 4000L, "4000")]    // above the range: token "1"
    public void Format_NumberOutsideTheSequenceRange_FallsBackToDecimal(string format, long n, string expected)
    {
        Assert.That(Format(format, n), Is.EqualTo(expected));
    }

    // ── Words and ordinals ───────────────────────────────────────────────────────

    [TestCase("w", 0L, "zero")]
    [TestCase("w", 13L, "thirteen")]
    [TestCase("w", 21L, "twenty-one")]
    [TestCase("w", 100L, "one hundred")]
    [TestCase("w", 101L, "one hundred and one")]
    [TestCase("w", 1000L, "one thousand")]
    [TestCase("w", 1234L, "one thousand two hundred and thirty-four")]
    [TestCase("W", 21L, "TWENTY-ONE")]
    [TestCase("Ww", 21L, "Twenty-One")]
    [TestCase("Ww", 105L, "One Hundred And Five")]
    public void Format_WordTokens_ProduceEnglishWords(string format, long n, string expected)
    {
        Assert.That(Format(format, n), Is.EqualTo(expected));
    }

    [TestCase(1L, "1st")]
    [TestCase(2L, "2nd")]
    [TestCase(3L, "3rd")]
    [TestCase(4L, "4th")]
    [TestCase(11L, "11th")]
    [TestCase(12L, "12th")]
    [TestCase(13L, "13th")]
    [TestCase(21L, "21st")]
    [TestCase(112L, "112th")]
    public void Format_OrdinalWithDecimalToken_AddsEnglishSuffix(long n, string expected)
    {
        string actual = NumberFormatter.Format(new[] { n }, "1", "en", null, "yes", null, 0);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(1L, "first")]
    [TestCase(2L, "second")]
    [TestCase(3L, "third")]
    [TestCase(5L, "fifth")]
    [TestCase(8L, "eighth")]
    [TestCase(9L, "ninth")]
    [TestCase(12L, "twelfth")]
    [TestCase(20L, "twentieth")]
    [TestCase(21L, "twenty-first")]
    [TestCase(100L, "one hundredth")]
    public void Format_OrdinalWithWordToken_ProducesOrdinalWords(long n, string expected)
    {
        string actual = NumberFormatter.Format(new[] { n }, "w", "en", null, "yes", null, 0);

        Assert.That(actual, Is.EqualTo(expected));
    }

    // ── Format-string structure ───────────────────────────────────────────────────

    [Test]
    public void Format_PrefixSeparatorsAndSuffix_FollowTheSpecExample()
    {
        // XSLT 2.0 §12.3: numbers 5, 13, 7 with format "A-001(i)" give "E-013(vii)".
        Assert.That(Format("A-001(i)", 5, 13, 7), Is.EqualTo("E-013(vii)"));
    }

    [TestCase("1.1", new[] { 1L, 2L, 3L }, "1.2.3")]
    [TestCase("A.1", new[] { 3L, 2L }, "C.2")]
    [TestCase("1", new[] { 1L, 2L, 3L }, "1.2.3")]           // a lone token is separated by "."
    [TestCase("(1) ", new[] { 5L }, "(5) ")]
    [TestCase("[1]", new[] { 3L }, "[3]")]
    [TestCase("-", new[] { 5L }, "-5-")]                       // a lone non-alphanumeric token is prefix and suffix
    [TestCase("", new[] { 7L }, "7")]
    [TestCase("1-1", new[] { 1L, 2L, 3L }, "1-2-3")]
    [TestCase("a.1", new[] { 1L, 2L, 3L }, "a.2.3")]          // extra numbers reuse the last token and its separator
    public void Format_MultipleNumbers_UseSeparatorsBetweenTokens(string format, long[] numbers, string expected)
    {
        Assert.That(Format(format, numbers), Is.EqualTo(expected));
    }

    // ── Grouping ──────────────────────────────────────────────────────────────────

    [TestCase(1234567L, ",", 3, "1,234,567")]
    [TestCase(1000000L, ".", 2, "1.00.00.00")]
    [TestCase(12L, ",", 3, "12")]
    public void Format_GroupingSeparatorAndSize_GroupFromTheRight(long n, string separator, int size, string expected)
    {
        string actual = NumberFormatter.Format(new[] { n }, "1", null, null, null, separator, size);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Format_GroupingSeparatorWithoutSize_IsIgnored()
    {
        string actual = NumberFormatter.Format(s_oneMillionish, "1", null, null, null, ",", 0);

        Assert.That(actual, Is.EqualTo("1234567"));
    }

    // ── Other sequences ───────────────────────────────────────────────────────────

    [TestCase("①", 1L, "①")]
    [TestCase("①", 20L, "⑳")]
    [TestCase("①", 21L, "㉑")]
    [TestCase("①", 50L, "㊿")]
    [TestCase("①", 0L, "⓪")]
    [TestCase("①", 51L, "51")]           // beyond the repertoire: token "1"
    [TestCase("❶", 11L, "⓫")]
    [TestCase("❶", 0L, "⓿")]
    public void Format_OtherSequences_UseTheirRepertoire(string token, long n, string expected)
    {
        Assert.That(Format(token, n), Is.EqualTo(expected));
    }

    // Every element of every supported sequence must carry the Unicode numeric value of its position
    // (checked where the runtime's Unicode data defines one).
    [TestCase(0x2460, 50)]
    [TestCase(0x2474, 20)]
    [TestCase(0x2488, 20)]
    [TestCase(0x1F102, 9)]
    [TestCase(0x2776, 20)]
    [TestCase(0x2780, 10)]
    [TestCase(0x278A, 10)]
    [TestCase(0x24F5, 10)]
    [TestCase(0x3220, 10)]
    [TestCase(0x3280, 10)]
    [TestCase(0x10107, 10)]
    [TestCase(0x111E1, 10)]
    [TestCase(0x10E60, 10)]
    [TestCase(0x1E8C7, 9)]
    [TestCase(0x1D360, 9)]
    [TestCase(0x102E1, 10)]
    [TestCase(0x11052, 10)]
    public void Format_OtherSequences_MatchUnicodeNumericValues(int token, int max)
    {
        for (int n = 1; n <= max; n++)
        {
            string formatted = Format(char.ConvertFromUtf32(token), n);
            double value = System.Globalization.CharUnicodeInfo.GetNumericValue(formatted, 0);
            if (value >= 0)
            {
                Assert.That(value, Is.EqualTo(n), $"token U+{token:X} number {n} -> U+{char.ConvertToUtf32(formatted, 0):X}");
            }

            Assert.That(formatted, Is.Not.EqualTo(n.ToString(System.Globalization.CultureInfo.InvariantCulture)), $"token U+{token:X} number {n} fell back to decimal");
        }
    }

    [Test]
    public void Format_UnsupportedToken_UsesFormatTokenOne()
    {
        // XSLT 2.0 §12.3: an unsupported sequence is formatted with the token "1".
        Assert.That(Format("а", 3), Is.EqualTo("3"));
    }

    [Test]
    public void ParseFormat_AlphanumericRuns_AreTokens()
    {
        // Arrange / Act
        NumberFormatter.ParseFormat("[A-001(i)]", out string prefix, out List<string> tokens, out List<string> separators, out string suffix);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(prefix, Is.EqualTo("["));
            Assert.That(tokens, Is.EqualTo(s_expectedTokens));
            Assert.That(separators, Is.EqualTo(s_expectedSeparators));
            Assert.That(suffix, Is.EqualTo(")]"));
        });
    }
}
