using System;
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmAtomicValueFactoriesTests
{
    // -------------------------------------------------------------------------
    // Boolean caching
    // -------------------------------------------------------------------------

    [Test]
    public void Boolean_True_ReturnsCachedInstance()
    {
        // Arrange / Act
        var v1 = XdmAtomicValue.Boolean(true);
        var v2 = XdmAtomicValue.Boolean(true);

        // Assert
        Assert.That(ReferenceEquals(v1, v2), Is.True, "Boolean(true) must return reference-identical instance every call.");
    }

    [Test]
    public void Boolean_False_ReturnsCachedInstance()
    {
        // Arrange / Act
        var v1 = XdmAtomicValue.Boolean(false);
        var v2 = XdmAtomicValue.Boolean(false);

        // Assert
        Assert.That(ReferenceEquals(v1, v2), Is.True, "Boolean(false) must return reference-identical instance every call.");
    }

    [Test]
    public void Boolean_TrueAndFalse_AreDifferentInstances()
    {
        // Arrange / Act
        var vTrue = XdmAtomicValue.Boolean(true);
        var vFalse = XdmAtomicValue.Boolean(false);

        // Assert
        Assert.That(ReferenceEquals(vTrue, vFalse), Is.False);
    }

    // -------------------------------------------------------------------------
    // Type codes
    // -------------------------------------------------------------------------

    [Test]
    public void String_ProducesStringTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.String("hello");

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.String));
    }

    [Test]
    public void UntypedAtomic_ProducesUntypedAtomicTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.UntypedAtomic("hello");

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.UntypedAtomic));
    }

    [Test]
    public void AnyUri_ProducesAnyUriTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.AnyUri("http://example.com");

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.AnyUri));
    }

    [Test]
    public void Boolean_ProducesBooleanTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Boolean(true);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Boolean));
    }

    [Test]
    public void IntegerLong_ProducesIntegerTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Integer(42L);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
    }

    [Test]
    public void IntegerBigInteger_ProducesIntegerTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Integer(new BigInteger(42));

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
    }

    [Test]
    public void Decimal_decimal_ProducesDecimalTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Decimal(3.14m);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
    }

    [Test]
    public void Decimal_XsdDecimal_ProducesDecimalTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Decimal(new XsdDecimal(3.14m));

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
    }

    [Test]
    public void Double_ProducesDoubleTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Double(3.14);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }

    [Test]
    public void Float_ProducesFloatTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Float(3.14f);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Float));
    }

    [Test]
    public void QNameValue_ProducesQNameTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.QNameValue(new QName("foo"));

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.QName));
    }

    [Test]
    public void Duration_ProducesDurationTypeCode()
    {
        // Arrange / Act
        var d = XsdDuration.Parse("P1Y");
        var v = XdmAtomicValue.Duration(d);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Duration));
    }

    [Test]
    public void DayTimeDuration_ProducesDayTimeDurationTypeCode()
    {
        // Arrange / Act
        var d = XsdDuration.Parse("P1D");
        var v = XdmAtomicValue.DayTimeDuration(d);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.DayTimeDuration));
    }

    [Test]
    public void YearMonthDuration_ProducesYearMonthDurationTypeCode()
    {
        // Arrange / Act
        var d = XsdDuration.Parse("P1Y");
        var v = XdmAtomicValue.YearMonthDuration(d);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.YearMonthDuration));
    }

    [Test]
    public void HexBinary_ProducesHexBinaryTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(new byte[] { 0x0A, 0x1B }));

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.HexBinary));
    }

    [Test]
    public void Base64Binary_ProducesBase64BinaryTypeCode()
    {
        // Arrange / Act
        var v = XdmAtomicValue.Base64Binary(new ReadOnlyMemory<byte>(new byte[] { 0x01, 0x02 }));

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(XdmTypeCode.Base64Binary));
    }

    // -------------------------------------------------------------------------
    // IsNode / IsAtomic
    // -------------------------------------------------------------------------

    [Test]
    public void AtomicValue_IsNodeFalse_IsAtomicTrue()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(1L);

        // Assert
        Assert.That(v.IsNode, Is.False);
        Assert.That(v.IsAtomic, Is.True);
    }

    // -------------------------------------------------------------------------
    // Integer long vs BigInteger payload
    // -------------------------------------------------------------------------

    [Test]
    public void IntegerLong_AsInt64_ReturnsValue()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(12345L);

        // Act
        long result = v.AsInt64();

        // Assert
        Assert.That(result, Is.EqualTo(12345L));
    }

    [Test]
    public void IntegerBigInteger_InLongRange_ShrinkToLong_AsInt64Works()
    {
        // Arrange — BigInteger with value in long range should shrink to long payload
        var big = new BigInteger(long.MaxValue);
        var v = XdmAtomicValue.Integer(big);

        // Act
        long result = v.AsInt64();

        // Assert
        Assert.That(result, Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void IntegerBigInteger_HugeValue_StaysBigInteger_AsInt64Throws()
    {
        // Arrange — value too large for long
        BigInteger huge = new BigInteger(long.MaxValue) + BigInteger.One;
        var v = XdmAtomicValue.Integer(huge);

        // Act / Assert
        Assert.That(() => v.AsInt64(), Throws.InstanceOf<OverflowException>());
    }

    [Test]
    public void IntegerBigInteger_HugeValue_AsBigIntegerWorks()
    {
        // Arrange
        BigInteger huge = new BigInteger(long.MaxValue) + BigInteger.One;
        var v = XdmAtomicValue.Integer(huge);

        // Act
        BigInteger result = v.AsBigInteger();

        // Assert
        Assert.That(result, Is.EqualTo(huge));
    }

    // -------------------------------------------------------------------------
    // DateTimeValue type code inference
    // -------------------------------------------------------------------------

    [TestCase(XsdDateTimeKind.DateTime, XdmTypeCode.DateTime)]
    [TestCase(XsdDateTimeKind.Date, XdmTypeCode.Date)]
    [TestCase(XsdDateTimeKind.Time, XdmTypeCode.Time)]
    [TestCase(XsdDateTimeKind.GYearMonth, XdmTypeCode.GYearMonth)]
    [TestCase(XsdDateTimeKind.GYear, XdmTypeCode.GYear)]
    [TestCase(XsdDateTimeKind.GMonthDay, XdmTypeCode.GMonthDay)]
    [TestCase(XsdDateTimeKind.GDay, XdmTypeCode.GDay)]
    [TestCase(XsdDateTimeKind.GMonth, XdmTypeCode.GMonth)]
    public void DateTimeValue_KindToTypeCode_IsCorrect(XsdDateTimeKind kind, XdmTypeCode expectedCode)
    {
        // Arrange
        var lexicals = new string[] {
            "2023-05-15T10:30:00", "2023-05-15", "10:30:00",
            "2023-05", "2023", "--05-15", "---15", "--05"
        };
        string lex = lexicals[(int)kind];
        var dt = XsdDateTime.Parse(lex, kind);

        // Act
        var v = XdmAtomicValue.DateTimeValue(dt);

        // Assert
        Assert.That(v.TypeCode, Is.EqualTo(expectedCode));
    }

    // -------------------------------------------------------------------------
    // Null argument guards
    // -------------------------------------------------------------------------

    [Test]
    public void String_NullValue_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.That(() => XdmAtomicValue.String(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void UntypedAtomic_NullValue_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.That(() => XdmAtomicValue.UntypedAtomic(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void AnyUri_NullValue_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.That(() => XdmAtomicValue.AnyUri(null!), Throws.ArgumentNullException);
    }
}
