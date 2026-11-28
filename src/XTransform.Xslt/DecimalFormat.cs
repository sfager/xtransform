// XSLT 2.0 §16.4.1 — xsl:decimal-format.
//
// Several declarations may share a name. For each property the effective value is the explicit one from
// the declaration with the highest import precedence; two declarations at that precedence that give
// different values are XTSE1290. The unnamed format always exists (all defaults if never declared).

using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>The resolved symbols of one decimal format; immutable.</summary>
internal sealed class DecimalFormat
{
    /// <summary>The format used when no declaration or name overrides a property.</summary>
    public static DecimalFormat Default { get; } = new DecimalFormat(
        decimalSeparator: '.',
        groupingSeparator: ',',
        percent: '%',
        perMille: 0x2030,
        zeroDigit: '0',
        digit: '#',
        patternSeparator: ';',
        minusSign: '-',
        infinity: "Infinity",
        nan: "NaN");

    /// <summary>Initializes a format from code points and strings.</summary>
    public DecimalFormat(
        int decimalSeparator,
        int groupingSeparator,
        int percent,
        int perMille,
        int zeroDigit,
        int digit,
        int patternSeparator,
        int minusSign,
        string infinity,
        string nan)
    {
        DecimalSeparator = decimalSeparator;
        GroupingSeparator = groupingSeparator;
        Percent = percent;
        PerMille = perMille;
        ZeroDigit = zeroDigit;
        Digit = digit;
        PatternSeparator = patternSeparator;
        MinusSign = minusSign;
        Infinity = infinity;
        NaN = nan;
    }

    /// <summary>The decimal separator (default <c>.</c>).</summary>
    public int DecimalSeparator { get; }

    /// <summary>The grouping separator (default <c>,</c>).</summary>
    public int GroupingSeparator { get; }

    /// <summary>The percent sign (default <c>%</c>).</summary>
    public int Percent { get; }

    /// <summary>The per-mille sign (default U+2030).</summary>
    public int PerMille { get; }

    /// <summary>The zero digit (default <c>0</c>); the ten digits of its family are zero-digit-signs in a picture.</summary>
    public int ZeroDigit { get; }

    /// <summary>The optional-digit sign (default <c>#</c>).</summary>
    public int Digit { get; }

    /// <summary>The pattern separator (default <c>;</c>).</summary>
    public int PatternSeparator { get; }

    /// <summary>The minus sign (default <c>-</c>).</summary>
    public int MinusSign { get; }

    /// <summary>The string for infinity (default <c>Infinity</c>).</summary>
    public string Infinity { get; }

    /// <summary>The string for NaN (default <c>NaN</c>).</summary>
    public string NaN { get; }
}

/// <summary>One <c>xsl:decimal-format</c> element before its attributes are merged.</summary>
internal sealed class DecimalFormatDecl
{
    /// <summary>Initializes a declaration.</summary>
    public DecimalFormatDecl(StylesheetElement element, int precedence, QName? name, IReadOnlyDictionary<string, string> values)
    {
        Element = element;
        Precedence = precedence;
        Name = name;
        Values = values;
    }

    /// <summary>The element.</summary>
    public StylesheetElement Element { get; }

    /// <summary>Import precedence of the declaring module.</summary>
    public int Precedence { get; }

    /// <summary>The declared name, or null for the unnamed format.</summary>
    public QName? Name { get; }

    /// <summary>The explicitly given attribute values (property name → value).</summary>
    public IReadOnlyDictionary<string, string> Values { get; }
}

/// <summary>The decimal formats of a stylesheet: the unnamed one plus any named ones.</summary>
internal sealed class DecimalFormatTable
{
    private static readonly string[] s_properties =
    {
        "decimal-separator", "grouping-separator", "percent", "per-mille", "zero-digit",
        "digit", "pattern-separator", "minus-sign", "infinity", "NaN",
    };

    private readonly Dictionary<QName, DecimalFormat> _named;

    private DecimalFormatTable(DecimalFormat unnamed, Dictionary<QName, DecimalFormat> named)
    {
        Unnamed = unnamed;
        _named = named;
    }

    /// <summary>A table holding only the default unnamed format.</summary>
    public static DecimalFormatTable Empty { get; } =
        new DecimalFormatTable(DecimalFormat.Default, new Dictionary<QName, DecimalFormat>());

    /// <summary>The unnamed decimal format.</summary>
    public DecimalFormat Unnamed { get; }

    /// <summary>Looks up a named format.</summary>
    public bool TryGetNamed(QName name, out DecimalFormat? format) => _named.TryGetValue(name, out format);

    /// <summary>Merges the declarations into formats, reporting XTSE0020, XTSE1290, XTSE1295 and XTSE1300.</summary>
    public static DecimalFormatTable Build(IReadOnlyList<DecimalFormatDecl> decls, DiagnosticBag bag)
    {
        if (decls.Count == 0)
        {
            return Empty;
        }

        Dictionary<QName, List<DecimalFormatDecl>> groups = new Dictionary<QName, List<DecimalFormatDecl>>();
        List<DecimalFormatDecl> unnamedGroup = new List<DecimalFormatDecl>();
        foreach (DecimalFormatDecl decl in decls)
        {
            if (decl.Name is null)
            {
                unnamedGroup.Add(decl);
                continue;
            }

            QName key = decl.Name.Value;
            if (!groups.TryGetValue(key, out List<DecimalFormatDecl>? group))
            {
                group = new List<DecimalFormatDecl>();
                groups[key] = group;
            }

            group.Add(decl);
        }

        DecimalFormat unnamed = unnamedGroup.Count == 0 ? DecimalFormat.Default : Merge(unnamedGroup, bag);
        Dictionary<QName, DecimalFormat> named = new Dictionary<QName, DecimalFormat>();
        foreach (KeyValuePair<QName, List<DecimalFormatDecl>> group in groups)
        {
            named[group.Key] = Merge(group.Value, bag);
        }

        return new DecimalFormatTable(unnamed, named);
    }

    private static DecimalFormat Merge(List<DecimalFormatDecl> group, DiagnosticBag bag)
    {
        Dictionary<string, string> effective = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string property in s_properties)
        {
            // The effective value comes from the highest import precedence that gives one; only conflicts
            // among declarations at that precedence are errors (a higher-precedence declaration overrides).
            int best = int.MinValue;
            foreach (DecimalFormatDecl decl in group)
            {
                if (decl.Values.ContainsKey(property) && decl.Precedence > best)
                {
                    best = decl.Precedence;
                }
            }

            string? value = null;
            foreach (DecimalFormatDecl decl in group)
            {
                if (decl.Precedence != best || !decl.Values.TryGetValue(property, out string? v))
                {
                    continue;
                }

                if (value is null)
                {
                    value = v;
                }
                else if (!string.Equals(value, v, StringComparison.Ordinal))
                {
                    // XTSE1290: conflicting values at the same import precedence.
                    bag.Error(
                        XsltErrors.XTSE1290,
                        $"xsl:decimal-format: conflicting values for '{property}' ('{value}' and '{v}') at the same import precedence",
                        decl.Element.Location);
                }
            }

            if (value is not null)
            {
                effective[property] = value;
            }
        }

        SourceSpan where = group[0].Element.Location;
        DecimalFormat d = DecimalFormat.Default;
        int decimalSeparator = Symbol(effective, "decimal-separator", d.DecimalSeparator, where, bag);
        int groupingSeparator = Symbol(effective, "grouping-separator", d.GroupingSeparator, where, bag);
        int percent = Symbol(effective, "percent", d.Percent, where, bag);
        int perMille = Symbol(effective, "per-mille", d.PerMille, where, bag);
        int zeroDigit = Symbol(effective, "zero-digit", d.ZeroDigit, where, bag);
        int digit = Symbol(effective, "digit", d.Digit, where, bag);
        int patternSeparator = Symbol(effective, "pattern-separator", d.PatternSeparator, where, bag);
        int minusSign = Symbol(effective, "minus-sign", d.MinusSign, where, bag);

        // XTSE1295: zero-digit must be a decimal digit with value zero.
        string zeroString = char.ConvertFromUtf32(zeroDigit);
        if (CharUnicodeInfo.GetUnicodeCategory(zeroString, 0) != UnicodeCategory.DecimalDigitNumber ||
            CharUnicodeInfo.GetDecimalDigitValue(zeroString, 0) != 0)
        {
            bag.Error(XsltErrors.XTSE1295, $"xsl:decimal-format: zero-digit '{zeroString}' is not a digit with value zero", where);
        }

        // XTSE1300: the seven picture characters must be distinct.
        int[] picture = { decimalSeparator, groupingSeparator, percent, perMille, zeroDigit, digit, patternSeparator };
        for (int i = 0; i < picture.Length; i++)
        {
            for (int j = i + 1; j < picture.Length; j++)
            {
                if (picture[i] == picture[j])
                {
                    bag.Error(
                        XsltErrors.XTSE1300,
                        $"xsl:decimal-format: the character '{char.ConvertFromUtf32(picture[i])}' is used for more than one picture symbol",
                        where);
                    i = picture.Length;
                    break;
                }
            }
        }

        effective.TryGetValue("infinity", out string? infinity);
        effective.TryGetValue("NaN", out string? nan);
        return new DecimalFormat(
            decimalSeparator, groupingSeparator, percent, perMille, zeroDigit, digit, patternSeparator, minusSign,
            infinity ?? d.Infinity, nan ?? d.NaN);
    }

    // A single-character property is exactly one Unicode code point (XTSE0020 otherwise).
    private static int Symbol(Dictionary<string, string> values, string property, int fallback, SourceSpan where, DiagnosticBag bag)
    {
        if (!values.TryGetValue(property, out string? text))
        {
            return fallback;
        }

        if (text.Length == 1 && !char.IsSurrogate(text[0]))
        {
            return text[0];
        }

        if (text.Length == 2 && char.IsHighSurrogate(text[0]) && char.IsLowSurrogate(text[1]))
        {
            return char.ConvertToUtf32(text[0], text[1]);
        }

        bag.Error(XsltErrors.XTSE0020, $"xsl:decimal-format: {property} must be a single character, not '{text}'", where);
        return fallback;
    }
}
