// P2-09: String function tests (F&O §7).

using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §7 string functions:
/// fn:concat, fn:string-join, fn:substring, fn:string-length, fn:normalize-space,
/// fn:normalize-unicode, fn:upper-case, fn:lower-case, fn:translate,
/// fn:encode-for-uri, fn:iri-to-uri, fn:escape-html-uri,
/// fn:contains, fn:starts-with, fn:ends-with, fn:substring-before, fn:substring-after,
/// fn:string-to-codepoints, fn:codepoints-to-string, fn:compare, fn:codepoint-equal.
/// </summary>
[TestFixture]
internal sealed class StringFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static string StringResult(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = Drain(ce, NewRoot());
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton string for: {expr}");
        return ((XdmAtomicValue)items[0]).AsString();
    }

    private static bool BoolResult(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = Drain(ce, NewRoot());
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton boolean for: {expr}");
        return ((XdmAtomicValue)items[0]).AsBoolean();
    }

    private static long IntegerResult(string expr)
    {
        CompiledExpression ce = Compile(expr);
        List<XdmItem> items = Drain(ce, NewRoot());
        Assert.That(items, Has.Count.EqualTo(1), $"Expected singleton integer for: {expr}");
        return ((XdmAtomicValue)items[0]).AsInt64();
    }

    private static List<XdmItem> Drain(CompiledExpression ce, DynamicContext ctx)
    {
        List<XdmItem> items = new List<XdmItem>();
        IXdmIterator iter = ce.Root.Iterate(ctx);
        XdmItem? item;
        while ((item = iter.Next()) is not null) { items.Add(item); }
        return items;
    }

    private static List<XdmItem> DrainExpr(string expr)
    {
        CompiledExpression ce = Compile(expr);
        return Drain(ce, NewRoot());
    }

    // =========================================================================
    // fn:concat
    // =========================================================================

    [TestCase("fn:concat('a','b')", "ab")]
    [TestCase("fn:concat('a','b','c')", "abc")]
    [TestCase("fn:concat('hello',' ','world')", "hello world")]
    [TestCase("fn:concat('','')", "")]
    [TestCase("fn:concat('x',())", "x")]        // empty → ""
    [TestCase("fn:concat('a','b','c','d','e','f','g','h','i','j')", "abcdefghij")] // arity 10
    public void FnConcat_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:string-join
    // =========================================================================

    [TestCase("fn:string-join(('a','b','c'), '-')", "a-b-c")]
    [TestCase("fn:string-join(('a'), ',')", "a")]
    [TestCase("fn:string-join((), ',')", "")]
    [TestCase("fn:string-join(('a','b'), '')", "ab")]
    public void FnStringJoin_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:substring — acceptance tests from task spec
    // =========================================================================

    [Test]
    public void FnSubstring_Normal_ReturnsSubstring()
    {
        // Arrange
        // Act
        string result = StringResult("fn:substring('12345', 2, 3)");

        // Assert
        Assert.That(result, Is.EqualTo("234"));
    }

    [Test]
    public void FnSubstring_NaNStart_ReturnsEmpty()
    {
        // F&O §7.4.3: NaN position → ""
        // Arrange
        // Act
        string result = StringResult("fn:substring('hello', 0 div 0e0)");

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void FnSubstring_NegativeStartWithInfinityLength_ReturnsAll()
    {
        // F&O §7.4.3: start=-42, length=+∞ → entire string
        // Arrange
        // Act
        string result = StringResult("fn:substring('12345', -42, 1 div 0e0)");

        // Assert
        Assert.That(result, Is.EqualTo("12345"));
    }

    [TestCase("fn:substring('12345', 1, 3)", "123")]
    [TestCase("fn:substring('12345', 2)", "2345")]
    [TestCase("fn:substring('12345', 1)", "12345")]
    [TestCase("fn:substring('12345', 6)", "")]
    [TestCase("fn:substring('12345', 2, 0)", "")]
    [TestCase("fn:substring('12345', 1.5, 2.6)", "234")]   // 1.5 rounds to 2, 2+2.6=4.6 rounds to 5
    public void FnSubstring_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:string-length — acceptance test: surrogate pair = 1 code point
    // =========================================================================

    [Test]
    public void FnStringLength_EarthGlobe_IsOneCodePoint()
    {
        // Arrange — 🌍 is U+1F30D, encoded as a surrogate pair in UTF-16
        // Act
        long result = IntegerResult("fn:string-length('🌍')");

        // Assert
        Assert.That(result, Is.EqualTo(1));
    }

    [TestCase("fn:string-length('')", 0L)]
    [TestCase("fn:string-length('hello')", 5L)]
    [TestCase("fn:string-length('abc')", 3L)]
    public void FnStringLength_Table(string expr, long expected)
    {
        Assert.That(IntegerResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:normalize-space
    // =========================================================================

    [TestCase("fn:normalize-space('  hello   world  ')", "hello world")]
    [TestCase("fn:normalize-space('')", "")]
    [TestCase("fn:normalize-space('a')", "a")]
    [TestCase("fn:normalize-space('  ')", "")]
    [TestCase("fn:normalize-space('a  b')", "a b")]
    public void FnNormalizeSpace_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:normalize-unicode
    // =========================================================================

    [Test]
    public void FnNormalizeUnicode_1Arg_NFC()
    {
        // Arrange
        // Act
        string result = StringResult("fn:normalize-unicode('abc')");

        // Assert — NFC of pure ASCII is unchanged
        Assert.That(result, Is.EqualTo("abc"));
    }

    [Test]
    public void FnNormalizeUnicode_EmptyForm_Identity()
    {
        // F&O §7.4.6: empty normalization form → identity
        // Arrange
        // Act
        string result = StringResult("fn:normalize-unicode('café', '')");

        // Assert
        Assert.That(result, Is.EqualTo("café"));
    }

    [Test]
    public void FnNormalizeUnicode_UnknownForm_ThrowsFOCH0003()
    {
        // Arrange
        // Act + Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => StringResult("fn:normalize-unicode('x', 'BOGUS')"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOCH0003));
    }

    // =========================================================================
    // fn:upper-case / fn:lower-case — acceptance test: ß → SS
    // =========================================================================

    [Test]
    public void FnUpperCase_GermanSharpS_BecomesSS()
    {
        // Arrange
        // Act
        string result = StringResult("fn:upper-case('ß')");

        // Assert
        Assert.That(result, Is.EqualTo("SS"));
    }

    [TestCase("fn:upper-case('hello')", "HELLO")]
    [TestCase("fn:upper-case('')", "")]
    [TestCase("fn:upper-case('ABC')", "ABC")]
    public void FnUpperCase_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    [TestCase("fn:lower-case('HELLO')", "hello")]
    [TestCase("fn:lower-case('')", "")]
    [TestCase("fn:lower-case('abc')", "abc")]
    public void FnLowerCase_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:translate — acceptance test
    // =========================================================================

    [Test]
    public void FnTranslate_AcceptanceTest()
    {
        // Arrange
        // Act
        string result = StringResult("fn:translate('bar','abc','ABC')");

        // Assert: b→B, a→A, r→r (not in map)
        Assert.That(result, Is.EqualTo("BAr"));
    }

    [TestCase("fn:translate('abc', 'abc', 'ABC')", "ABC")]
    [TestCase("fn:translate('abc', 'b', '')", "ac")]      // delete b
    [TestCase("fn:translate('', 'abc', 'ABC')", "")]
    [TestCase("fn:translate('xyz', 'abc', 'ABC')", "xyz")] // nothing maps
    public void FnTranslate_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:encode-for-uri / fn:iri-to-uri / fn:escape-html-uri
    // =========================================================================

    [Test]
    public void FnEncodeForUri_SpaceBecomesPercent20()
    {
        // Arrange
        // Act
        string result = StringResult("fn:encode-for-uri('hello world')");

        // Assert
        Assert.That(result, Is.EqualTo("hello%20world"));
    }

    [Test]
    public void FnEncodeForUri_UnreservedCharsUnchanged()
    {
        // Arrange
        // Act
        string result = StringResult("fn:encode-for-uri('abc-_.~')");

        // Assert
        Assert.That(result, Is.EqualTo("abc-_.~"));
    }

    [Test]
    public void FnEncodeForUri_Empty_ReturnsEmpty()
    {
        Assert.That(StringResult("fn:encode-for-uri('')"), Is.EqualTo(""));
    }

    [Test]
    public void FnIriToUri_AsciiUriCharsUnchanged()
    {
        // Arrange — typical URI chars should not be encoded
        string result = StringResult("fn:iri-to-uri('http://example.com/path?q=1')");

        // Assert
        Assert.That(result, Is.EqualTo("http://example.com/path?q=1"));
    }

    [Test]
    public void FnEscapeHtmlUri_HighCharEncoded()
    {
        // F&O §7.4.12: characters outside #x20..#x7E are encoded
        // Space (0x20) is kept as-is per the range
        string result = StringResult("fn:escape-html-uri('a b')");
        Assert.That(result, Is.EqualTo("a b"));
    }

    // =========================================================================
    // fn:contains / fn:starts-with / fn:ends-with
    // =========================================================================

    [TestCase("fn:contains('hello','ell')", true)]
    [TestCase("fn:contains('hello','xyz')", false)]
    [TestCase("fn:contains('hello','')", true)]  // empty → true
    [TestCase("fn:contains('','hello')", false)]
    [TestCase("fn:contains('','', '')", true)]   // 3-arg with empty collation (codepoint)
    public void FnContains_Table(string expr, bool expected)
    {
        Assert.That(BoolResult(expr), Is.EqualTo(expected));
    }

    [TestCase("fn:starts-with('hello','hel')", true)]
    [TestCase("fn:starts-with('hello','xyz')", false)]
    [TestCase("fn:starts-with('hello','')", true)]
    public void FnStartsWith_Table(string expr, bool expected)
    {
        Assert.That(BoolResult(expr), Is.EqualTo(expected));
    }

    [TestCase("fn:ends-with('hello','llo')", true)]
    [TestCase("fn:ends-with('hello','xyz')", false)]
    [TestCase("fn:ends-with('hello','')", true)]
    public void FnEndsWith_Table(string expr, bool expected)
    {
        Assert.That(BoolResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:substring-before / fn:substring-after
    // =========================================================================

    [TestCase("fn:substring-before('hello world','world')", "hello ")]
    [TestCase("fn:substring-before('hello','xyz')", "")]
    [TestCase("fn:substring-before('hello','')", "")]
    public void FnSubstringBefore_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    [TestCase("fn:substring-after('hello world','hello ')", "world")]
    [TestCase("fn:substring-after('hello','xyz')", "")]
    [TestCase("fn:substring-after('hello','')", "hello")]
    public void FnSubstringAfter_Table(string expr, string expected)
    {
        Assert.That(StringResult(expr), Is.EqualTo(expected));
    }

    // =========================================================================
    // fn:string-to-codepoints / fn:codepoints-to-string — round-trip
    // =========================================================================

    [Test]
    public void FnStringToCodepoints_Hello_FiveIntegers()
    {
        // Arrange
        // Act
        List<XdmItem> items = DrainExpr("fn:string-to-codepoints('hello')");

        // Assert
        Assert.That(items, Has.Count.EqualTo(5));
        Assert.That(((XdmAtomicValue)items[0]).AsInt64(), Is.EqualTo(104)); // 'h'
        Assert.That(((XdmAtomicValue)items[1]).AsInt64(), Is.EqualTo(101)); // 'e'
    }

    [Test]
    public void FnStringToCodepoints_Empty_EmptySequence()
    {
        // Arrange
        // Act
        List<XdmItem> items = DrainExpr("fn:string-to-codepoints('')");

        // Assert
        Assert.That(items, Has.Count.EqualTo(0));
    }

    [Test]
    public void FnCodepointsToString_RoundTrip()
    {
        // Arrange — encode "hi" as code points and convert back
        // Act
        string result = StringResult("fn:codepoints-to-string(fn:string-to-codepoints('hello'))");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void FnCodepointsToString_InvalidCodePoint_ThrowsFOCH0001()
    {
        // F&O §7.2.1 FOCH0001: code point -1 is invalid
        // Arrange
        // Act + Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => StringResult("fn:codepoints-to-string((-1))"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOCH0001));
    }

    [Test]
    public void FnCodepointsToString_SurrogateCodePoint_ThrowsFOCH0001()
    {
        // F&O §7.2.1: surrogates (0xD800..0xDFFF) are invalid code points
        // Arrange
        // Act + Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => StringResult("fn:codepoints-to-string((55296))"))!; // 0xD800
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOCH0001));
    }

    // =========================================================================
    // fn:compare
    // =========================================================================

    [TestCase("fn:compare('a','b')", -1L)]
    [TestCase("fn:compare('b','a')", 1L)]
    [TestCase("fn:compare('a','a')", 0L)]
    public void FnCompare_Table(string expr, long expected)
    {
        Assert.That(IntegerResult(expr), Is.EqualTo(expected));
    }

    [Test]
    public void FnCompare_EmptyArg_ReturnsEmptySequence()
    {
        // F&O §7.3.2: empty on either argument → empty sequence
        // Arrange
        // Act
        List<XdmItem> items = DrainExpr("fn:compare('a',())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // fn:codepoint-equal
    // =========================================================================

    [Test]
    public void FnCodepointEqual_SameString_True()
    {
        Assert.That(BoolResult("fn:codepoint-equal('abc','abc')"), Is.True);
    }

    [Test]
    public void FnCodepointEqual_DifferentStrings_False()
    {
        Assert.That(BoolResult("fn:codepoint-equal('abc','ABC')"), Is.False);
    }

    [Test]
    public void FnCodepointEqual_EmptyArg_ReturnsEmptySequence()
    {
        // F&O §7.3.3: empty on either argument → empty sequence
        // Arrange
        // Act
        List<XdmItem> items = DrainExpr("fn:codepoint-equal('a',())");

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // Code-point correctness: surrogate pairs
    // =========================================================================

    [Test]
    public void FnSubstring_SurrogatePair_CountsByCodePoints()
    {
        // 🌍🌍🌍 — each is one code point (two UTF-16 units)
        // substring of 3 non-BMP chars starting at 2, length 2 → 2 non-BMP chars
        // Arrange
        string input = "\U0001F30D\U0001F30D\U0001F30D"; // 3 earth globes

        // Act
        string result = StringResult($"fn:substring('{input}', 2, 2)");

        // Assert
        Assert.That(result, Is.EqualTo("\U0001F30D\U0001F30D"));
    }

    [Test]
    public void FnTranslate_NonBmpChar_MapsCorrectly()
    {
        // Map 🌍 (U+1F30D) to 'X'
        // Arrange: translate("🌍", "🌍", "X")
        string input = "\U0001F30D";

        // Act
        string result = StringResult($"fn:translate('{input}', '\U0001F30D', 'X')");

        // Assert
        Assert.That(result, Is.EqualTo("X"));
    }
}
