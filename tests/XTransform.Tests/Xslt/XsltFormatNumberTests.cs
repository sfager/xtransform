using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §16.4 — fn:format-number and xsl:decimal-format.</summary>
[TestFixture]
internal sealed class XsltFormatNumberTests
{
    // ── Pictures and rounding (§16.4.2 – §16.4.4) ────────────────────────────

    [TestCase("0", "0", "0")]
    [TestCase("12345", "#,###", "12,345")]
    [TestCase("1234567", "#,###", "1,234,567")]
    [TestCase("123456789", "#,###,###", "123,456,789")]
    [TestCase("1234567", "#,##,##0", "12,34,567")]
    [TestCase("12345.6789", "#,###.00", "12,345.68")]
    [TestCase("1234567.891", "#,##0.0", "1,234,567.9")]
    [TestCase("0.5", "0.0", "0.5")]
    [TestCase("0.5", "#.0", ".5")]
    [TestCase("0.5", "#.00", ".50")]
    [TestCase("5", "#", "5")]
    [TestCase("0", "#", "0")]
    [TestCase("1", "00", "01")]
    [TestCase("5", "000.00", "005.00")]
    [TestCase("1.0e0", "0.00", "1.00")]
    [TestCase("12", "0.##", "12")]
    [TestCase("12.3", "0.##", "12.3")]
    [TestCase("12.345", "0.##", "12.34")]
    [TestCase("12.355", "0.##", "12.36")]
    [TestCase("12", "0.0#", "12.0")]
    [TestCase("0.123456", "0.000,000", "0.123,456")]
    public void FormatNumber_Pictures(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    // fn:round-half-to-even is the rounding rule (§16.4.4 step 5).
    [TestCase("0.5", "0", "0")]
    [TestCase("1.5", "0", "2")]
    [TestCase("2.5", "0", "2")]
    [TestCase("3.5", "0", "4")]
    [TestCase("1234.5", "0", "1234")]
    [TestCase("1235.5", "0", "1236")]
    [TestCase("0.125", "0.00", "0.12")]
    [TestCase("0.135", "0.00", "0.14")]
    [TestCase("0.1351", "0.00", "0.14")]
    [TestCase("0.1349", "0.00", "0.13")]
    [TestCase("9.995", "0.00", "10.00")]
    [TestCase("99.5", "0", "100")]
    [TestCase("0.999", "0.0", "1.0")]
    [TestCase("0.04", "0.0", "0.0")]
    public void FormatNumber_RoundsHalfToEven(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    [TestCase("-5", "0", "-5")]
    [TestCase("-5", "0;(0)", "(5)")]
    [TestCase("5", "0;(0)", "5")]
    [TestCase("-1234.5", "#,##0.00;(#,##0.00)", "(1,234.50)")]
    [TestCase("1234.5", "#,##0.00;(#,##0.00)", "1,234.50")]
    [TestCase("-3", "$0", "-$3")]
    [TestCase("-3", "$0;-$0.00", "-$3.00")]
    [TestCase("-0.4", "0", "-0")]
    public void FormatNumber_NegativeNumbers(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    [TestCase("12", "$0.00", "$12.00")]
    [TestCase("12", "0.00 USD", "12.00 USD")]
    [TestCase("12", "[0]", "[12]")]
    public void FormatNumber_PrefixAndSuffix(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    [TestCase("0.25", "0%", "25%")]
    [TestCase("0.256", "0.0%", "25.6%")]
    [TestCase("1", "#%", "100%")]
    [TestCase("0.5", "0‰", "500‰")]
    [TestCase("0.0123", "0.0‰", "12.3‰")]
    [TestCase("-0.5", "0%;(0%)", "(50%)")]
    public void FormatNumber_PercentAndPerMille(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    // Doubles use their shortest round-trip digits; large integers are exact.
    [TestCase("1e20", "0", "100000000000000000000")]
    [TestCase("1.5e-5", "0.000000", "0.000015")]
    [TestCase("123456789.125e0", "#,##0.00", "123,456,789.12")]
    [TestCase("xs:integer('123456789012345678901234567890')", "#,##0", "123,456,789,012,345,678,901,234,567,890")]
    [TestCase("xs:float('0.1')", "0.0000", "0.1000")]
    [TestCase("xs:untypedAtomic('42.5')", "0.0", "42.5")]
    public void FormatNumber_NumericTypes(string value, string picture, string expected)
    {
        Assert.That(Eval($"format-number({value}, '{picture}')"), Is.EqualTo(expected));
    }

    // ── NaN, infinity and negative zero ──────────────────────────────────────

    [Test]
    public void FormatNumber_NaNAndEmptySequence_ProduceTheNaNSymbol()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("format-number(xs:double('NaN'), '0')"), Is.EqualTo("NaN"));
            Assert.That(Eval("format-number((), '0')"), Is.EqualTo("NaN"));
            Assert.That(Eval("format-number(xs:double('NaN'), '$0;(0)')"), Is.EqualTo("NaN"));
        });
    }

    [Test]
    public void FormatNumber_Infinity_UsesThePrefixAndSuffixOfTheSubPicture()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("format-number(xs:double('INF'), '0')"), Is.EqualTo("Infinity"));
            Assert.That(Eval("format-number(xs:double('-INF'), '0')"), Is.EqualTo("-Infinity"));
            Assert.That(Eval("format-number(xs:double('-INF'), '0;(0)')"), Is.EqualTo("(Infinity)"));
            Assert.That(Eval("format-number(xs:double('INF'), '$0.00')"), Is.EqualTo("$Infinity"));
        });
    }

    [Test]
    public void FormatNumber_NegativeZero_UsesTheNegativeSubPicture()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("format-number((-0e0), '0;(0)')"), Is.EqualTo("(0)"));
            Assert.That(Eval("format-number(0e0, '0;(0)')"), Is.EqualTo("0"));
            Assert.That(Eval("format-number((-0e0), '0')"), Is.EqualTo("-0"));
        });
    }

    [Test]
    public void CastingMinusZeroFromAString_GivesNegativeZero()
    {
        // 1 div -0 is -INF only when the cast kept the sign (.NET Framework parses "-0" as +0).
        Assert.That(Eval("format-number(1 div xs:double('-0'), '0')"), Is.EqualTo("-Infinity"));
    }

    // ── Invalid pictures: XTDE1310 ───────────────────────────────────────────

    [TestCase("")]
    [TestCase("abc")]
    [TestCase(".")]
    [TestCase("0.0.0")]
    [TestCase("0;0;0")]
    [TestCase("0%%")]
    [TestCase("0%‰")]
    [TestCase("0%0")]
    [TestCase("0x0")]
    [TestCase("0#")]
    [TestCase("0.#0")]
    [TestCase("0,.0")]
    [TestCase("0.,0")]
    [TestCase("0;abc")]
    public void FormatNumber_InvalidPicture_RaisesXtde1310(string picture)
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval($"format-number(1, '{picture}')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1310));
    }

    [Test]
    public void FormatNumber_NonNumericValue_RaisesXpty0004()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("format-number('abc', '0')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // ── Named and customised decimal formats ─────────────────────────────────

    [Test]
    public void DecimalFormat_Named_ChangesTheSeparators()
    {
        const string decl = "<xsl:decimal-format name='eu' decimal-separator=',' grouping-separator='.'/>";

        Assert.That(Run(decl, "format-number(1234.5, '#.##0,00', 'eu')"), Is.EqualTo("1.234,50"));
    }

    [Test]
    public void DecimalFormat_Unnamed_OverridesTheDefaults()
    {
        const string decl = "<xsl:decimal-format NaN='not-a-number' infinity='inf' minus-sign='~'/>";

        Assert.Multiple(() =>
        {
            Assert.That(Run(decl, "format-number(-1, '0')"), Is.EqualTo("~1"));
            Assert.That(Run(decl, "format-number(xs:double('NaN'), '0')"), Is.EqualTo("not-a-number"));
            Assert.That(Run(decl, "format-number(xs:double('INF'), '0')"), Is.EqualTo("inf"));
        });
    }

    [Test]
    public void DecimalFormat_ZeroDigit_MapsDigitsAndTheFamilyIsRecognisedInThePicture()
    {
        // Arabic-Indic digits: the picture uses zero-digit-signs from the family.
        const string decl = "<xsl:decimal-format name='ar' zero-digit='&#x660;'/>";

        Assert.That(
            Run(decl, "format-number(123.5, '٠٠٠.٠٠', 'ar')"),
            Is.EqualTo("١٢٣.٥٠"));
    }

    [Test]
    public void DecimalFormat_PercentAndDigitSigns_CanBeChanged()
    {
        const string decl = "<xsl:decimal-format name='x' percent='p' digit='D'/>";

        Assert.Multiple(() =>
        {
            Assert.That(Run(decl, "format-number(0.5, '0p', 'x')"), Is.EqualTo("50p"));
            Assert.That(Run(decl, "format-number(7, 'DD0', 'x')"), Is.EqualTo("7"));
        });
    }

    [Test]
    public void DecimalFormat_SeveralDeclarationsOfOneName_AreMerged()
    {
        const string decl =
            "<xsl:decimal-format name='m' decimal-separator=','/>" +
            "<xsl:decimal-format name='m' grouping-separator=' '/>";

        Assert.That(Run(decl, "format-number(1234.5, '# ##0,00', 'm')"), Is.EqualTo("1 234,50"));
    }

    [Test]
    public void DecimalFormat_NamedFormat_DoesNotAffectTheUnnamedOne()
    {
        const string decl = "<xsl:decimal-format name='eu' decimal-separator=',' grouping-separator='.'/>";

        Assert.That(Run(decl, "format-number(1234.5, '#,##0.00')"), Is.EqualTo("1,234.50"));
    }

    [Test]
    public void DecimalFormat_HigherImportPrecedence_WinsOverTheImportedOne()
    {
        // The importing module's value is used; the imported module's conflicting one is not an error.
        string dir = Path.Combine(Path.GetTempPath(), "xt-df-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "imported.xsl"),
                Wrap("<xsl:decimal-format name='n' decimal-separator='!'/>"));
            string main = Path.Combine(dir, "main.xsl");
            File.WriteAllText(
                main,
                "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
                "<xsl:import href='imported.xsl'/><xsl:output method='text'/>" +
                "<xsl:decimal-format name='n' decimal-separator=',' grouping-separator='.'/>" +
                "<xsl:template match='/'><xsl:value-of select=\"format-number(1.5, '0,0', 'n')\"/></xsl:template></xsl:stylesheet>");

            XsltExecutable exe = new XsltCompiler().Compile(new System.Uri(main));
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
            using MemoryStream output = new MemoryStream();
            exe.CreateTransformer().Transform(input, output);

            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo("1,5"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Declaration errors ───────────────────────────────────────────────────

    [Test]
    public void DecimalFormat_ConflictingValuesAtTheSamePrecedence_RaisesXtse1290()
    {
        const string decl =
            "<xsl:decimal-format name='c' decimal-separator=','/><xsl:decimal-format name='c' decimal-separator=';'/>";

        Assert.That(CompileError(decl), Is.EqualTo(XsltErrors.XTSE1290));
    }

    [Test]
    public void DecimalFormat_IdenticalRepeatedValues_AreNotAnError()
    {
        const string decl =
            "<xsl:decimal-format name='c' decimal-separator=',' grouping-separator='.'/><xsl:decimal-format name='c' decimal-separator=','/>";

        Assert.That(Run(decl, "format-number(1.5, '0,0', 'c')"), Is.EqualTo("1,5"));
    }

    [Test]
    public void DecimalFormat_ZeroDigitThatIsNotAZeroDigit_RaisesXtse1295()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CompileError("<xsl:decimal-format zero-digit='1'/>"), Is.EqualTo(XsltErrors.XTSE1295));
            Assert.That(CompileError("<xsl:decimal-format zero-digit='a'/>"), Is.EqualTo(XsltErrors.XTSE1295));
        });
    }

    [Test]
    public void DecimalFormat_TwoSymbolsWithTheSameCharacter_RaisesXtse1300()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CompileError("<xsl:decimal-format decimal-separator=',' grouping-separator=','/>"), Is.EqualTo(XsltErrors.XTSE1300));
            Assert.That(CompileError("<xsl:decimal-format digit='0'/>"), Is.EqualTo(XsltErrors.XTSE1300));
        });
    }

    [TestCase("decimal-separator='ab'")]
    [TestCase("grouping-separator=''")]
    [TestCase("minus-sign='--'")]
    public void DecimalFormat_SingleCharacterPropertyWithAnotherLength_RaisesXtse0020(string attribute)
    {
        Assert.That(CompileError($"<xsl:decimal-format {attribute}/>"), Is.EqualTo(XsltErrors.XTSE0020));
    }

    [Test]
    public void DecimalFormat_UnknownAttribute_RaisesXtse0090()
    {
        // exponent-separator is XSLT 3.0, not 2.0.
        Assert.That(CompileError("<xsl:decimal-format exponent-separator='E'/>"), Is.EqualTo(XsltErrors.XTSE0090));
    }

    [Test]
    public void DecimalFormat_ConflictInAnImportedModule_IsNotAnErrorWhenAHigherPrecedenceDeclarationOverrides()
    {
        string dir = Path.Combine(Path.GetTempPath(), "xt-dfc-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "imported.xsl"),
                Wrap("<xsl:decimal-format name='q' decimal-separator=';'/><xsl:decimal-format name='q' decimal-separator='!'/>"));
            string main = Path.Combine(dir, "main.xsl");
            File.WriteAllText(
                main,
                "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>" +
                "<xsl:import href='imported.xsl'/><xsl:output method='text'/>" +
                "<xsl:decimal-format name='q' decimal-separator=',' grouping-separator='.'/>" +
                "<xsl:template match='/'><xsl:value-of select=\"format-number(1.5, '0,0', 'q')\"/></xsl:template></xsl:stylesheet>");

            XsltExecutable exe = new XsltCompiler().Compile(new System.Uri(main));
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
            using MemoryStream output = new MemoryStream();
            exe.CreateTransformer().Transform(input, output);

            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo("1,5"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Naming a format: XTDE1280 ────────────────────────────────────────────

    [Test]
    public void FormatNumber_UndeclaredFormatName_RaisesXtde1280()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("format-number(1, '0', 'nope')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1280));
    }

    [Test]
    public void FormatNumber_UnboundPrefixOrInvalidName_RaisesXtde1280()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<XsltDynamicException>(() => Eval("format-number(1, '0', 'p:nope')"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTDE1280));
            Assert.That(
                Assert.Throws<XsltDynamicException>(() => Eval("format-number(1, '0', '1bad')"))!.ErrorCode,
                Is.EqualTo(XsltErrors.XTDE1280));
        });
    }

    [Test]
    public void FormatNumber_FormatNameWithAPrefix_UsesTheNamespacesInScopeAtTheCall()
    {
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:a='urn:x' xmlns:b='urn:x'>" +
            "<xsl:output method='text'/><xsl:decimal-format name='a:f' decimal-separator=',' grouping-separator='.'/>" +
            "<xsl:template match='/'><xsl:value-of select=\"format-number(1.5, '0,0', 'b:f')\"/></xsl:template></xsl:stylesheet>";

        Assert.That(RunStylesheet(xsl), Is.EqualTo("1,5"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
        "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";

    private static string Eval(string xpath) => Run(string.Empty, xpath);

    private static string Run(string declarations, string xpath) =>
        RunStylesheet(Wrap(declarations + "<xsl:template match='/'><xsl:value-of select=\"" + xpath + "\"/></xsl:template>"));

    private static string RunStylesheet(string xsl)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<d/>"));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static QName CompileError(string declarations)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => new XsltCompiler().Compile(new StringReader(Wrap(declarations + "<xsl:template match='/'/>"))))!;
        return ex.ErrorCode;
    }
}
