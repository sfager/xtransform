// Unicode data for the XPath 2.0 regex dialect (F&O §7.6.1, XML Schema Part 2 Appendix F and G):
// the legal block names, ranges for the blocks .NET lacks, and the XML name-character classes
// behind \i and \c.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace XTransform.XPath.Functions;

internal static class RegexUnicodeTables
{
    /// <summary>Block names allowed after <c>\p{Is…}</c> (XML Schema 1.0, Appendix G).</summary>
    private static readonly HashSet<string> s_blockNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "IsBasicLatin", "IsLatin-1Supplement", "IsLatinExtended-A", "IsLatinExtended-B", "IsIPAExtensions",
        "IsSpacingModifierLetters", "IsCombiningDiacriticalMarks", "IsGreek", "IsCyrillic", "IsArmenian",
        "IsHebrew", "IsArabic", "IsSyriac", "IsThaana", "IsDevanagari", "IsBengali", "IsGurmukhi",
        "IsGujarati", "IsOriya", "IsTamil", "IsTelugu", "IsKannada", "IsMalayalam", "IsSinhala", "IsThai",
        "IsLao", "IsTibetan", "IsMyanmar", "IsGeorgian", "IsHangulJamo", "IsEthiopic", "IsCherokee",
        "IsUnifiedCanadianAboriginalSyllabics", "IsOgham", "IsRunic", "IsKhmer", "IsMongolian",
        "IsLatinExtendedAdditional", "IsGreekExtended", "IsGeneralPunctuation", "IsSuperscriptsandSubscripts",
        "IsCurrencySymbols", "IsCombiningMarksforSymbols", "IsLetterlikeSymbols", "IsNumberForms", "IsArrows",
        "IsMathematicalOperators", "IsMiscellaneousTechnical", "IsControlPictures",
        "IsOpticalCharacterRecognition", "IsEnclosedAlphanumerics", "IsBoxDrawing", "IsBlockElements",
        "IsGeometricShapes", "IsMiscellaneousSymbols", "IsDingbats", "IsBraillePatterns",
        "IsCJKRadicalsSupplement", "IsKangxiRadicals", "IsIdeographicDescriptionCharacters",
        "IsCJKSymbolsandPunctuation", "IsHiragana", "IsKatakana", "IsBopomofo", "IsHangulCompatibilityJamo",
        "IsKanbun", "IsBopomofoExtended", "IsEnclosedCJKLettersandMonths", "IsCJKCompatibility",
        "IsCJKUnifiedIdeographsExtensionA", "IsCJKUnifiedIdeographs", "IsYiSyllables", "IsYiRadicals",
        "IsHangulSyllables", "IsHighSurrogates", "IsHighPrivateUseSurrogates", "IsLowSurrogates",
        "IsPrivateUse", "IsCJKCompatibilityIdeographs", "IsAlphabeticPresentationForms",
        "IsArabicPresentationForms-A", "IsCombiningHalfMarks", "IsCJKCompatibilityForms",
        "IsSmallFormVariants", "IsArabicPresentationForms-B", "IsSpecials", "IsHalfwidthandFullwidthForms",
        "IsOldItalic", "IsGothic", "IsDeseret", "IsByzantineMusicalSymbols", "IsMusicalSymbols",
        "IsMathematicalAlphanumericSymbols", "IsCJKUnifiedIdeographsExtensionB",
        "IsCJKCompatibilityIdeographsSupplement", "IsTags",
    };

    // Blocks that .NET does not provide (or defines differently); ranges are inclusive.
    private static readonly Dictionary<string, CodePointRange[]> s_ownBlocks =
        new Dictionary<string, CodePointRange[]>(StringComparer.Ordinal)
        {
            ["IsOldItalic"] = new[] { new CodePointRange(0x10300, 0x1032F) },
            ["IsGothic"] = new[] { new CodePointRange(0x10330, 0x1034F) },
            ["IsDeseret"] = new[] { new CodePointRange(0x10400, 0x1044F) },
            ["IsByzantineMusicalSymbols"] = new[] { new CodePointRange(0x1D000, 0x1D0FF) },
            ["IsMusicalSymbols"] = new[] { new CodePointRange(0x1D100, 0x1D1FF) },
            ["IsMathematicalAlphanumericSymbols"] = new[] { new CodePointRange(0x1D400, 0x1D7FF) },
            ["IsCJKUnifiedIdeographsExtensionB"] = new[] { new CodePointRange(0x20000, 0x2A6DF) },
            ["IsCJKCompatibilityIdeographsSupplement"] = new[] { new CodePointRange(0x2F800, 0x2FA1F) },
            ["IsTags"] = new[] { new CodePointRange(0xE0000, 0xE007F) },

            // XML Schema lists three PrivateUse ranges under one block name.
            ["IsPrivateUse"] = new[]
            {
                new CodePointRange(0xE000, 0xF8FF),
                new CodePointRange(0xF0000, 0xFFFFD),
                new CodePointRange(0x100000, 0x10FFFD),
            },

            // Surrogate blocks can never match a character of an XML string.
            ["IsHighSurrogates"] = Array.Empty<CodePointRange>(),
            ["IsHighPrivateUseSurrogates"] = Array.Empty<CodePointRange>(),
            ["IsLowSurrogates"] = Array.Empty<CodePointRange>(),
        };

    private static readonly HashSet<string> s_categories = new HashSet<string>(StringComparer.Ordinal)
    {
        "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No",
        "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Z", "Zs", "Zl", "Zp",
        "S", "Sm", "Sc", "Sk", "So", "C", "Cc", "Cf", "Co", "Cn",
    };

    // XML 1.0 (Fifth Edition) NameStartChar and NameChar, as code point ranges.
    private static readonly CodePointRange[] s_nameStart =
    {
        new CodePointRange(':', ':'), new CodePointRange('A', 'Z'), new CodePointRange('_', '_'),
        new CodePointRange('a', 'z'), new CodePointRange(0xC0, 0xD6), new CodePointRange(0xD8, 0xF6),
        new CodePointRange(0xF8, 0x2FF), new CodePointRange(0x370, 0x37D), new CodePointRange(0x37F, 0x1FFF),
        new CodePointRange(0x200C, 0x200D), new CodePointRange(0x2070, 0x218F), new CodePointRange(0x2C00, 0x2FEF),
        new CodePointRange(0x3001, 0xD7FF), new CodePointRange(0xF900, 0xFDCF), new CodePointRange(0xFDF0, 0xFFFD),
        new CodePointRange(0x10000, 0xEFFFF),
    };

    private static readonly CodePointRange[] s_nameChar =
    {
        new CodePointRange('-', '.'), new CodePointRange('0', '9'), new CodePointRange(0xB7, 0xB7),
        new CodePointRange(0x300, 0x36F), new CodePointRange(0x203F, 0x2040),
    };

    /// <summary>True when <paramref name="name"/> is an XSD general category (<c>L</c>, <c>Lu</c>, …).</summary>
    public static bool IsCategory(string name) => s_categories.Contains(name);

    /// <summary>True when <paramref name="name"/> is a block name allowed by XML Schema (<c>IsBasicLatin</c>, …).</summary>
    public static bool IsBlock(string name) => s_blockNames.Contains(name);

    /// <summary>The explicit ranges of a block that .NET does not support, or null when .NET handles it.</summary>
    public static CodePointRange[]? GetOwnBlock(string name) =>
        s_ownBlocks.TryGetValue(name, out CodePointRange[]? ranges) ? ranges : null;

    /// <summary>Ranges matched by <c>\i</c>: XML name start characters (XML Schema Appendix F).</summary>
    public static List<CodePointRange> InitialNameChars() => new List<CodePointRange>(s_nameStart);

    /// <summary>Ranges matched by <c>\c</c>: XML name characters.</summary>
    public static List<CodePointRange> NameChars()
    {
        List<CodePointRange> all = new List<CodePointRange>(s_nameStart);
        all.AddRange(s_nameChar);
        return AstralCategories.Union(all);
    }

    /// <summary>
    /// The complement of <paramref name="sorted"/> (normalised, ascending ranges) within the legal
    /// character range U+0000..U+10FFFF, excluding the surrogate code points.
    /// </summary>
    public static List<CodePointRange> Complement(IReadOnlyList<CodePointRange> sorted)
    {
        List<CodePointRange> input = new List<CodePointRange>(sorted);
        input.Add(new CodePointRange(0xD800, 0xDFFF));
        input = AstralCategories.Union(input);

        List<CodePointRange> result = new List<CodePointRange>();
        int next = 0;
        foreach (CodePointRange r in input)
        {
            if (r.First > next)
            {
                result.Add(new CodePointRange(next, r.First - 1));
            }

            next = r.Last + 1;
        }

        if (next <= 0x10FFFF)
        {
            result.Add(new CodePointRange(next, 0x10FFFF));
        }

        return result;
    }

    /// <summary>All code points (BMP and supplementary) whose category is in <paramref name="categoryName"/>.</summary>
    public static List<CodePointRange> CategoryRanges(string categoryName)
    {
        HashSet<UnicodeCategory> set = AstralCategories.CategorySet(categoryName)
            ?? throw new ArgumentException("Not a category: " + categoryName, nameof(categoryName));
        List<CodePointRange> ranges = new List<CodePointRange>();
        int start = -1;
        for (int cp = 0; cp <= 0xFFFF; cp++)
        {
            bool inSet = !(cp >= 0xD800 && cp <= 0xDFFF) && set.Contains(CharUnicodeInfo.GetUnicodeCategory((char)cp));
            if (inSet)
            {
                if (start < 0)
                {
                    start = cp;
                }
            }
            else if (start >= 0)
            {
                ranges.Add(new CodePointRange(start, cp - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            ranges.Add(new CodePointRange(start, 0xFFFF));
        }

        List<CodePointRange>? astral = AstralCategories.GetRanges(categoryName, negate: false);
        if (astral is not null)
        {
            ranges.AddRange(astral);
        }

        return AstralCategories.Union(ranges);
    }
}
