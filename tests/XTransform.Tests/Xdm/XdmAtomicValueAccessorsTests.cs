using System;
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmAtomicValueAccessorsTests
{
    // -------------------------------------------------------------------------
    // AsString — string family
    // -------------------------------------------------------------------------

    [Test]
    public void AsString_String_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.String("hello");

        // Act / Assert
        Assert.That(v.AsString(), Is.EqualTo("hello"));
    }

    [Test]
    public void AsString_NormalizedString_ReturnsStoredValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("hello world", XdmTypeCode.NormalizedString);

        // Act / Assert
        Assert.That(v.AsString(), Is.EqualTo("hello world"));
    }

    [Test]
    public void AsString_Token_ReturnsStoredValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("hello", XdmTypeCode.Token);

        // Act / Assert
        Assert.That(v.AsString(), Is.EqualTo("hello"));
    }

    [Test]
    public void AsString_UntypedAtomic_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.UntypedAtomic("text");

        // Act / Assert
        Assert.That(v.AsString(), Is.EqualTo("text"));
    }

    [Test]
    public void AsString_AnyUri_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.AnyUri("http://example.com");

        // Act / Assert
        Assert.That(v.AsString(), Is.EqualTo("http://example.com"));
    }

    [Test]
    public void AsString_Boolean_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Boolean(true);

        // Act / Assert
        Assert.That(() => v.AsString(), Throws.InstanceOf<InvalidCastException>());
    }

    [Test]
    public void AsString_Integer_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(42L);

        // Act / Assert
        Assert.That(() => v.AsString(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsBoolean
    // -------------------------------------------------------------------------

    [Test]
    public void AsBoolean_Boolean_ReturnsValue()
    {
        // Arrange
        var vTrue = XdmAtomicValue.Boolean(true);
        var vFalse = XdmAtomicValue.Boolean(false);

        // Act / Assert
        Assert.That(vTrue.AsBoolean(), Is.True);
        Assert.That(vFalse.AsBoolean(), Is.False);
    }

    [Test]
    public void AsBoolean_Integer_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(1L);

        // Act / Assert
        Assert.That(() => v.AsBoolean(), Throws.InstanceOf<InvalidCastException>());
    }

    [Test]
    public void AsBoolean_String_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.String("true");

        // Act / Assert
        Assert.That(() => v.AsBoolean(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsInt64
    // -------------------------------------------------------------------------

    [Test]
    public void AsInt64_Integer_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(42L);

        // Act / Assert
        Assert.That(v.AsInt64(), Is.EqualTo(42L));
    }

    [Test]
    public void AsInt64_IntegerDerivedType_ReturnsValue()
    {
        // Arrange — xs:nonNegativeInteger
        var v = XdmAtomicValue.Parse("100", XdmTypeCode.NonNegativeInteger);

        // Act / Assert
        Assert.That(v.AsInt64(), Is.EqualTo(100L));
    }

    [Test]
    public void AsInt64_HugeInteger_ThrowsOverflowException()
    {
        // Arrange
        BigInteger huge = new BigInteger(long.MaxValue) + BigInteger.One;
        var v = XdmAtomicValue.Integer(huge);

        // Act / Assert
        Assert.That(() => v.AsInt64(), Throws.InstanceOf<OverflowException>());
    }

    [Test]
    public void AsInt64_Boolean_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Boolean(true);

        // Act / Assert
        Assert.That(() => v.AsInt64(), Throws.InstanceOf<InvalidCastException>());
    }

    [Test]
    public void AsInt64_Decimal_ThrowsInvalidCastException()
    {
        // Arrange — xs:decimal (not integer family)
        var v = XdmAtomicValue.Decimal(5m);

        // Act / Assert
        Assert.That(() => v.AsInt64(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsBigInteger
    // -------------------------------------------------------------------------

    [Test]
    public void AsBigInteger_LongPayload_PromotesToBigInteger()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(42L);

        // Act
        BigInteger result = v.AsBigInteger();

        // Assert
        Assert.That(result, Is.EqualTo(new BigInteger(42)));
    }

    [Test]
    public void AsBigInteger_BigIntegerPayload_ReturnsValue()
    {
        // Arrange
        BigInteger huge = new BigInteger(long.MaxValue) + BigInteger.One;
        var v = XdmAtomicValue.Integer(huge);

        // Act
        BigInteger result = v.AsBigInteger();

        // Assert
        Assert.That(result, Is.EqualTo(huge));
    }

    [Test]
    public void AsBigInteger_String_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.String("123");

        // Act / Assert
        Assert.That(() => v.AsBigInteger(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsDecimal
    // -------------------------------------------------------------------------

    [Test]
    public void AsDecimal_Decimal_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Decimal(3.14m);

        // Act / Assert
        Assert.That((decimal)v.AsDecimal(), Is.EqualTo(3.14m));
    }

    [Test]
    public void AsDecimal_Integer_PromotesToDecimal()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(5L);

        // Act
        XsdDecimal d = v.AsDecimal();

        // Assert
        Assert.That((decimal)d, Is.EqualTo(5m));
    }

    [Test]
    public void AsDecimal_Double_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Double(3.14);

        // Act / Assert
        Assert.That(() => v.AsDecimal(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsDouble
    // -------------------------------------------------------------------------

    [Test]
    public void AsDouble_Double_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Double(3.14);

        // Act / Assert
        Assert.That(v.AsDouble(), Is.EqualTo(3.14));
    }

    [Test]
    public void AsDouble_Float_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Float(3.14f);

        // Act / Assert
        Assert.That(() => v.AsDouble(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsFloat
    // -------------------------------------------------------------------------

    [Test]
    public void AsFloat_Float_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Float(1.5f);

        // Act / Assert
        Assert.That(v.AsFloat(), Is.EqualTo(1.5f));
    }

    [Test]
    public void AsFloat_Double_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Double(1.5);

        // Act / Assert
        Assert.That(() => v.AsFloat(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsQName
    // -------------------------------------------------------------------------

    [Test]
    public void AsQName_QName_ReturnsValue()
    {
        // Arrange
        var qname = new QName("xs", "http://www.w3.org/2001/XMLSchema", "string");
        var v = XdmAtomicValue.QNameValue(qname);

        // Act
        QName result = v.AsQName();

        // Assert
        Assert.That(result, Is.EqualTo(qname));
    }

    [Test]
    public void AsQName_Notation_ReturnsValue()
    {
        // Arrange — xs:NOTATION is treated like QName
        var v = XdmAtomicValue.Parse("prefix:local", XdmTypeCode.Notation);

        // Act
        QName result = v.AsQName();

        // Assert
        Assert.That(result.Prefix, Is.EqualTo("prefix"));
        Assert.That(result.LocalName, Is.EqualTo("local"));
    }

    [Test]
    public void AsQName_String_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.String("foo");

        // Act / Assert
        Assert.That(() => v.AsQName(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsDateTime
    // -------------------------------------------------------------------------

    [Test]
    public void AsDateTime_DateTime_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("2023-05-15T10:30:00Z", XdmTypeCode.DateTime);

        // Act
        XsdDateTime dt = v.AsDateTime();

        // Assert
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.DateTime));
        Assert.That(dt.Year, Is.EqualTo(2023L));
    }

    [Test]
    public void AsDateTime_Date_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("2023-05-15", XdmTypeCode.Date);

        // Act
        XsdDateTime dt = v.AsDateTime();

        // Assert
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.Date));
    }

    [Test]
    public void AsDateTime_Integer_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(42L);

        // Act / Assert
        Assert.That(() => v.AsDateTime(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsDuration
    // -------------------------------------------------------------------------

    [Test]
    public void AsDuration_Duration_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("P1Y2M", XdmTypeCode.Duration);

        // Act
        XsdDuration d = v.AsDuration();

        // Assert — P1Y2M = 14 months, 0 seconds
        Assert.That(d.Months, Is.EqualTo(14));
    }

    [Test]
    public void AsDuration_DayTimeDuration_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Parse("PT1H", XdmTypeCode.DayTimeDuration);

        // Act
        XsdDuration d = v.AsDuration();

        // Assert — 1 hour = 3600 seconds
        Assert.That((decimal)d.Seconds, Is.EqualTo(3600m));
    }

    [Test]
    public void AsDuration_String_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.String("P1Y");

        // Act / Assert
        Assert.That(() => v.AsDuration(), Throws.InstanceOf<InvalidCastException>());
    }

    // -------------------------------------------------------------------------
    // AsHexBinary / AsBase64Binary
    // -------------------------------------------------------------------------

    [Test]
    public void AsHexBinary_HexBinary_ReturnsBytes()
    {
        // Arrange
        byte[] bytes = new byte[] { 0xAB, 0xCD };
        var v = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(bytes));

        // Act
        byte[] result = v.AsHexBinary().ToArray();

        // Assert
        Assert.That(result, Is.EqualTo(bytes));
    }

    [Test]
    public void AsHexBinary_String_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.String("ABCD");

        // Act / Assert
        Assert.That(() => v.AsHexBinary(), Throws.InstanceOf<InvalidCastException>());
    }

    [Test]
    public void AsBase64Binary_Base64Binary_ReturnsBytes()
    {
        // Arrange
        byte[] bytes = new byte[] { 0x01, 0x02 };
        var v = XdmAtomicValue.Base64Binary(new ReadOnlyMemory<byte>(bytes));

        // Act
        byte[] result = v.AsBase64Binary().ToArray();

        // Assert
        Assert.That(result, Is.EqualTo(bytes));
    }

    [Test]
    public void AsBase64Binary_HexBinary_ThrowsInvalidCastException()
    {
        // Arrange
        var v = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(new byte[] { 0x01 }));

        // Act / Assert
        Assert.That(() => v.AsBase64Binary(), Throws.InstanceOf<InvalidCastException>());
    }
}
