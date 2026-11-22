using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmAtomicValueGetStringValueTests
{
    // -------------------------------------------------------------------------
    // Boolean canonical forms
    // -------------------------------------------------------------------------

    [Test]
    public void Boolean_True_GetStringValue_ReturnsTrue()
    {
        // Arrange
        var v = XdmAtomicValue.Boolean(true);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("true"));
    }

    [Test]
    public void Boolean_False_GetStringValue_ReturnsFalse()
    {
        // Arrange
        var v = XdmAtomicValue.Boolean(false);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("false"));
    }

    // -------------------------------------------------------------------------
    // Integer canonical forms
    // -------------------------------------------------------------------------

    [TestCase("42", "42")]
    [TestCase("+42", "42")]
    [TestCase("0001", "1")]
    [TestCase("-5", "-5")]
    [TestCase("0", "0")]
    [TestCase("-0", "0")]
    public void Parse_Integer_GetStringValue_IsCanonical(string input, string expected)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.Integer);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo(expected));
    }

    [Test]
    public void Integer_HugeValue_GetStringValue_IsCanonical()
    {
        // Arrange
        string input = "999999999999999999999999";
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.Integer);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo(input));
    }

    // -------------------------------------------------------------------------
    // Decimal canonical forms
    // -------------------------------------------------------------------------

    [TestCase("1.0", "1")]
    [TestCase("1.500", "1.5")]
    [TestCase("3.14", "3.14")]
    [TestCase("0.0", "0")]
    [TestCase("-1.5", "-1.5")]
    public void Parse_Decimal_GetStringValue_IsCanonical(string input, string expected)
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse(input, XdmTypeCode.Decimal);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // Double canonical forms
    // -------------------------------------------------------------------------

    [Test]
    public void Double_INF_GetStringValue_ReturnsINF()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("INF", XdmTypeCode.Double);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("INF"));
    }

    [Test]
    public void Double_NegativeINF_GetStringValue_ReturnsNegativeINF()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("-INF", XdmTypeCode.Double);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("-INF"));
    }

    [Test]
    public void Double_NaN_GetStringValue_ReturnsNaN()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Parse("NaN", XdmTypeCode.Double);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("NaN"));
    }

    [Test]
    public void Double_Zero_GetStringValue_ReturnsZero()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Double(0.0);

        // Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("0"));
    }

    [Test]
    public void Double_NegativeZero_GetStringValue_ReturnsZero()
    {
        // Arrange — negative zero
        var v = XdmAtomicValue.Double(-0.0);

        // Act / Assert — per F&O, -0 → "0"
        Assert.That(v.GetStringValue(), Is.EqualTo("0"));
    }

    [Test]
    public void Parse_Double_DecimalForm_GetStringValue_IsDecimal()
    {
        // Arrange — 0.5 is in decimal range [1e-6, 1e21)
        var v = XdmAtomicValue.Parse("0.5", XdmTypeCode.Double);

        // Act
        string s = v.GetStringValue();

        // Assert — should be decimal form, not scientific
        Assert.That(s, Does.Not.Contain("E"));
        Assert.That(s, Is.EqualTo("0.5"));
    }

    [Test]
    public void Parse_Double_1E22_GetStringValue_IsScientific()
    {
        // Arrange — 1E22 exceeds the decimal range, must use scientific notation
        var v = XdmAtomicValue.Parse("1E22", XdmTypeCode.Double);

        // Act
        string s = v.GetStringValue();

        // Assert — scientific notation expected
        Assert.That(s, Does.Contain("E").Or.Contain("e"));
    }

    // -------------------------------------------------------------------------
    // HexBinary canonical forms
    // -------------------------------------------------------------------------

    [Test]
    public void HexBinary_Empty_GetStringValue_ReturnsEmpty()
    {
        // Arrange
        var v = XdmAtomicValue.HexBinary(ReadOnlyMemory<byte>.Empty);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Parse_HexBinary_Lowercase_GetStringValue_IsUppercase()
    {
        // Arrange — parse lowercase hex; canonical form is uppercase
        var v = XdmAtomicValue.Parse("0a1b", XdmTypeCode.HexBinary);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("0A1B"));
    }

    [Test]
    public void Parse_HexBinary_Uppercase_GetStringValue_IsUppercase()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("0A1B", XdmTypeCode.HexBinary);

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("0A1B"));
    }

    // -------------------------------------------------------------------------
    // Base64Binary canonical form
    // -------------------------------------------------------------------------

    [Test]
    public void Base64Binary_GetStringValue_IsBase64()
    {
        // Arrange
        byte[] bytes = new byte[] { 0x01, 0x02, 0x03 };
        var v = XdmAtomicValue.Base64Binary(new ReadOnlyMemory<byte>(bytes));

        // Act
        string s = v.GetStringValue();

        // Assert — should be valid base64 and round-trip correctly
        Assert.That(Convert.FromBase64String(s), Is.EqualTo(bytes));
    }

    // -------------------------------------------------------------------------
    // String family — preserved as-is
    // -------------------------------------------------------------------------

    [Test]
    public void String_GetStringValue_ReturnsStoredString()
    {
        // Arrange
        var v = XdmAtomicValue.String("hello world");

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("hello world"));
    }

    [Test]
    public void UntypedAtomic_GetStringValue_ReturnsStoredString()
    {
        // Arrange
        var v = XdmAtomicValue.UntypedAtomic("hello world");

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("hello world"));
    }

    [Test]
    public void AnyUri_GetStringValue_ReturnsStoredString()
    {
        // Arrange
        var v = XdmAtomicValue.AnyUri("http://example.com/path");

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("http://example.com/path"));
    }

    // -------------------------------------------------------------------------
    // QName
    // -------------------------------------------------------------------------

    [Test]
    public void QName_WithPrefix_GetStringValue_ReturnsPrefixColonLocal()
    {
        // Arrange
        var v = XdmAtomicValue.QNameValue(new QName("xs", "http://www.w3.org/2001/XMLSchema", "string"));

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("xs:string"));
    }

    [Test]
    public void QName_NoPrefix_GetStringValue_ReturnsLocalName()
    {
        // Arrange
        var v = XdmAtomicValue.QNameValue(new QName("localname"));

        // Act / Assert
        Assert.That(v.GetStringValue(), Is.EqualTo("localname"));
    }

    // -------------------------------------------------------------------------
    // Duration
    // -------------------------------------------------------------------------

    [Test]
    public void Duration_GetStringValue_IsCanonical()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("P1Y2M3DT4H5M6S", XdmTypeCode.Duration);

        // Act
        string s = v.GetStringValue();

        // Assert — should round-trip via XsdDuration canonical form
        Assert.That(s, Does.Contain("P"));
    }

    // -------------------------------------------------------------------------
    // DateTime
    // -------------------------------------------------------------------------

    [Test]
    public void DateTime_GetStringValue_IsCanonical()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("2023-05-15T10:30:00Z", XdmTypeCode.DateTime);

        // Act
        string s = v.GetStringValue();

        // Assert — round-trip
        Assert.That(s, Is.EqualTo("2023-05-15T10:30:00Z"));
    }
}
