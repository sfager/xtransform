using System;
using System.Globalization;
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmAtomicValueParseTests
{
    // -------------------------------------------------------------------------
    // Null guard
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_NullLexical_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.That(() => XdmAtomicValue.Parse(null!, XdmTypeCode.String), Throws.ArgumentNullException);
    }

    [Test]
    public void TryParse_NullLexical_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.That(() => XdmAtomicValue.TryParse(null!, XdmTypeCode.String, out _), Throws.ArgumentNullException);
    }

    // -------------------------------------------------------------------------
    // Whitespace normalization
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_String_PreservesWhitespace()
    {
        // Arrange
        string input = "  hello  ";

        // Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.String);

        // Assert — xs:string uses "preserve" facet
        Assert.That(v.AsString(), Is.EqualTo("  hello  "));
    }

    [Test]
    public void Parse_NormalizedString_ReplacesTabAndNewlines()
    {
        // Arrange — xs:normalizedString uses "replace" facet: tab/LF/CR → space, no trim
        string input = "  hello\tworld  ";

        // Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.NormalizedString);

        // Assert
        Assert.That(v.AsString(), Is.EqualTo("  hello world  "));
    }

    [Test]
    public void Parse_Token_CollapsesWhitespace()
    {
        // Arrange — xs:token uses "collapse" facet: replace + trim + merge
        string input = "  hello\tworld  ";

        // Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.Token);

        // Assert
        Assert.That(v.AsString(), Is.EqualTo("hello world"));
    }

    [Test]
    public void Parse_Integer_CollapsesLeadingTrailingWhitespace()
    {
        // Arrange
        string input = "  42  ";

        // Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.Integer);

        // Assert
        Assert.That(v.AsInt64(), Is.EqualTo(42L));
    }

    // -------------------------------------------------------------------------
    // Boolean
    // -------------------------------------------------------------------------

    [TestCase("true")]
    [TestCase("1")]
    public void Parse_Boolean_TrueValues_ProduceTrue(string lexical)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Boolean);

        // Assert
        Assert.That(v.AsBoolean(), Is.True);
    }

    [TestCase("false")]
    [TestCase("0")]
    public void Parse_Boolean_FalseValues_ProduceFalse(string lexical)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Boolean);

        // Assert
        Assert.That(v.AsBoolean(), Is.False);
    }

    [TestCase("True")]
    [TestCase("yes")]
    [TestCase("TRUE")]
    public void Parse_Boolean_InvalidValues_ThrowsFORG0001(string lexical)
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse(lexical, XdmTypeCode.Boolean));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_BooleanTrue_ReturnsCachedTrueInstance()
    {
        // Acceptance criterion: Parse("true", Boolean) === Parse("true", Boolean)
        // Arrange / Act
        var v1 = XdmAtomicValue.Parse("true", XdmTypeCode.Boolean);
        var v2 = XdmAtomicValue.Parse("true", XdmTypeCode.Boolean);

        // Assert
        Assert.That(ReferenceEquals(v1, v2), Is.True, "Parse boolean true must return cached instance.");
    }

    [Test]
    public void Parse_Boolean1_ReturnsCachedTrueInstance()
    {
        // Arrange / Act
        var v1 = XdmAtomicValue.Parse("1", XdmTypeCode.Boolean);
        var vTrue = XdmAtomicValue.Boolean(true);

        // Assert
        Assert.That(ReferenceEquals(v1, vTrue), Is.True, "Parse '1' as boolean must return cached true instance.");
    }

    // -------------------------------------------------------------------------
    // Integer
    // -------------------------------------------------------------------------

    [TestCase("0", 0L)]
    [TestCase("-0", 0L)]
    [TestCase("+123", 123L)]
    [TestCase("123", 123L)]
    [TestCase("-456", -456L)]
    [TestCase("9223372036854775807", long.MaxValue)] // long.MaxValue
    [TestCase("-9223372036854775808", long.MinValue)] // long.MinValue
    public void Parse_Integer_ValidLexical_ProducesExpectedValue(string lexical, long expected)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Integer);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(v.AsInt64(), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_Integer_HugeValue_ProducesBigIntegerPayload()
    {
        // Arrange
        string lexical = "999999999999999999999999";

        // Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Integer);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
        Assert.That(() => v.AsInt64(), Throws.InstanceOf<OverflowException>());
        Assert.That(v.AsBigInteger(), Is.EqualTo(BigInteger.Parse(lexical, CultureInfo.InvariantCulture)));
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1.5")]
    [TestCase("1e5")]
    public void Parse_Integer_InvalidLexical_ThrowsFORG0001(string lexical)
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse(lexical, XdmTypeCode.Integer));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Decimal
    // -------------------------------------------------------------------------

    [TestCase("1.0")]
    [TestCase("3.14")]
    [TestCase("-5.5")]
    [TestCase("0")]
    public void Parse_Decimal_ValidLexical_Succeeds(string lexical)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Decimal);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
    }

    [TestCase("1e5")]
    public void Parse_Decimal_ExponentNotAllowed_ThrowsFORG0001(string lexical)
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse(lexical, XdmTypeCode.Decimal));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Double / Float
    // -------------------------------------------------------------------------

    [TestCase("3.14")]
    [TestCase("1E10")]
    [TestCase("0.5")]
    public void Parse_Double_ValidLexical_Succeeds(string lexical)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(lexical, XdmTypeCode.Double);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }

    [Test]
    public void Parse_Double_INF_ProducesPositiveInfinity()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("INF", XdmTypeCode.Double);

        // Assert
        Assert.That(double.IsPositiveInfinity(v.AsDouble()), Is.True);
    }

    [Test]
    public void Parse_Double_NegativeINF_ProducesNegativeInfinity()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("-INF", XdmTypeCode.Double);

        // Assert
        Assert.That(double.IsNegativeInfinity(v.AsDouble()), Is.True);
    }

    [Test]
    public void Parse_Double_NaN_ProducesNaN()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("NaN", XdmTypeCode.Double);

        // Assert
        Assert.That(double.IsNaN(v.AsDouble()), Is.True);
    }

    [Test]
    public void Parse_Double_InvalidLexical_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse("not-a-double", XdmTypeCode.Double));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_Float_INF_ProducesPositiveInfinity()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("INF", XdmTypeCode.Float);

        // Assert
        Assert.That(float.IsPositiveInfinity(v.AsFloat()), Is.True);
    }

    // -------------------------------------------------------------------------
    // HexBinary
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HexBinary_Empty_ProducesEmptyBytes()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("", XdmTypeCode.HexBinary);

        // Assert
        Assert.That(v.AsHexBinary().Length, Is.EqualTo(0));
    }

    [Test]
    public void Parse_HexBinary_ValidUppercase_ProducesBytes()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("0A1B", XdmTypeCode.HexBinary);

        // Assert
        byte[] bytes = v.AsHexBinary().ToArray();
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x0A, 0x1B }));
    }

    [Test]
    public void Parse_HexBinary_Lowercase_ProducesSameBytes()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("0a1b", XdmTypeCode.HexBinary);

        // Assert
        byte[] bytes = v.AsHexBinary().ToArray();
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x0A, 0x1B }));
    }

    [Test]
    public void Parse_HexBinary_OddLength_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse("0A1", XdmTypeCode.HexBinary));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_HexBinary_InvalidCharacter_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse("0G", XdmTypeCode.HexBinary));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Base64Binary
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_Base64Binary_ValidInput_ProducesBytes()
    {
        // Arrange
        byte[] expected = new byte[] { 0x01, 0x02, 0x03 };
        string b64 = Convert.ToBase64String(expected);

        // Act
        var v = XdmAtomicValue.Parse(b64, XdmTypeCode.Base64Binary);

        // Assert
        Assert.That(v.AsBase64Binary().ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_Base64Binary_InvalidInput_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() => XdmAtomicValue.Parse("!!!!", XdmTypeCode.Base64Binary));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Duration family
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_Duration_Valid_Succeeds()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("P1Y2M3DT4H5M6S", XdmTypeCode.Duration);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Duration));
    }

    [Test]
    public void Parse_DayTimeDuration_WithMonths_ThrowsFORG0001()
    {
        // Arrange — P1Y is yearMonthDuration, not dayTimeDuration
        var ex = Assert.Throws<XPathDynamicException>(() =>
            XdmAtomicValue.Parse("P1Y1D", XdmTypeCode.DayTimeDuration));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_YearMonthDuration_WithSeconds_ThrowsFORG0001()
    {
        // Arrange — PT1H has seconds component, not valid yearMonthDuration
        var ex = Assert.Throws<XPathDynamicException>(() =>
            XdmAtomicValue.Parse("P1YT1H", XdmTypeCode.YearMonthDuration));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_DayTimeDuration_NoDays_Succeeds()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("PT4H30M", XdmTypeCode.DayTimeDuration);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
    }

    [Test]
    public void Parse_YearMonthDuration_NoSeconds_Succeeds()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("P2Y3M", XdmTypeCode.YearMonthDuration);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.YearMonthDuration));
    }

    // -------------------------------------------------------------------------
    // AnyAtomicType — cannot parse to abstract type
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_AnyAtomicType_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() =>
            XdmAtomicValue.Parse("hello", XdmTypeCode.AnyAtomicType));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // TryParse — returns false on invalid, never throws (except null)
    // -------------------------------------------------------------------------

    [Test]
    public void TryParse_InvalidBoolean_ReturnsFalse()
    {
        // Arrange / Act
        bool ok = XdmAtomicValue.TryParse("maybe", XdmTypeCode.Boolean, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryParse_ValidInteger_ReturnsTrueAndValue()
    {
        // Arrange / Act
        bool ok = XdmAtomicValue.TryParse("42", XdmTypeCode.Integer, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.AsInt64(), Is.EqualTo(42L));
    }

    [Test]
    public void TryParse_InvalidDouble_ReturnsFalse()
    {
        // Arrange / Act
        bool ok = XdmAtomicValue.TryParse("not-a-double", XdmTypeCode.Double, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
        Assert.That(result, Is.Null);
    }

    // -------------------------------------------------------------------------
    // QName
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_QName_LocalOnly_Succeeds()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("foo", XdmTypeCode.QName);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.QName));
        Assert.That(v.AsQName().LocalName, Is.EqualTo("foo"));
        Assert.That(v.AsQName().Prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Parse_QName_PrefixAndLocal_Succeeds()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("xs:string", XdmTypeCode.QName);

        // Assert
        Assert.That(v.AsQName().Prefix, Is.EqualTo("xs"));
        Assert.That(v.AsQName().LocalName, Is.EqualTo("string"));
    }

    [Test]
    public void Parse_QName_InvalidNcName_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<XPathDynamicException>(() =>
            XdmAtomicValue.Parse("123invalid", XdmTypeCode.QName));
        Assert.That(ex!.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // DateTime
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_DateTime_Valid_ProducesDateTimeTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("2023-05-15T10:30:00", XdmTypeCode.DateTime);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.DateTime));
    }

    [Test]
    public void Parse_Date_Valid_ProducesDateTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("2023-05-15", XdmTypeCode.Date);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Date));
    }
}
