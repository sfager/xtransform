// Tests for CastTable.TryPromote — F&O §3.1.5 function conversion rules.
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
[Category("Cast")]
public sealed class TryPromoteTests
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_SameType_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(5L);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Integer, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result, Is.SameAs(value));
    }

    // -------------------------------------------------------------------------
    // Subtype → supertype
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_IntToInteger_SubtypeUpcast_ReturnsTrue()
    {
        // Arrange — xs:int is a subtype of xs:integer
        XdmAtomicValue value = XdmAtomicValue.Parse("5", XdmTypeCode.Int);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Integer, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Integer));
    }

    [Test]
    public void TryPromote_IntegerToAnyAtomicType_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(1L);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.AnyAtomicType, out XdmAtomicValue? result);

        // Assert — xs:integer IS-A xs:anyAtomicType
        Assert.That(ok, Is.True);
    }

    // -------------------------------------------------------------------------
    // Numeric promotion — F&O §3.1.5
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_IntegerToDecimal_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(7L);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Decimal, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
    }

    [Test]
    public void TryPromote_IntegerToFloat_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(3L);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Float, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Float));
    }

    [Test]
    public void TryPromote_IntegerToDouble_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(9L);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Double, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }

    [Test]
    public void TryPromote_DecimalToFloat_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Decimal(1.5m);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Float, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Float));
    }

    [Test]
    public void TryPromote_DecimalToDouble_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Decimal(2.5m);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Double, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }

    [Test]
    public void TryPromote_FloatToDouble_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Float(1.5f);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Double, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }

    // -------------------------------------------------------------------------
    // Promotion that should NOT work (not in F&O §3.1.5 promotion rules)
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_DecimalToInteger_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Decimal(3.7m);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Integer, out XdmAtomicValue? result);

        // Assert — Decimal is NOT a subtype of Integer (it's the other way round)
        Assert.That(ok, Is.False);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void TryPromote_DoubleToFloat_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Double(1.5);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Float, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
    }

    [Test]
    public void TryPromote_StringToAnyUri_ReturnsFalse()
    {
        // Arrange — promotion is one-way: AnyUri→String, not String→AnyUri
        XdmAtomicValue value = XdmAtomicValue.String("http://example.com");

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.AnyUri, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
    }

    // -------------------------------------------------------------------------
    // AnyURI → String (F&O §3.1.5)
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_AnyUriToString_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.AnyUri("http://example.org");

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.String, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.String));
        Assert.That(result.AsString(), Is.EqualTo("http://example.org"));
    }

    // -------------------------------------------------------------------------
    // Null value
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_NullValue_ReturnsFalse()
    {
        // Arrange / Act
        bool ok = CastTable.TryPromote(null, XdmTypeCode.String, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.False);
        Assert.That(result, Is.Null);
    }

    // -------------------------------------------------------------------------
    // Integer-subtype → Decimal (verify the whole chain)
    // -------------------------------------------------------------------------

    [Test]
    public void TryPromote_ByteToDecimal_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Parse("10", XdmTypeCode.Byte);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Decimal, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Decimal));
    }

    [Test]
    public void TryPromote_ByteToDouble_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Parse("10", XdmTypeCode.Byte);

        // Act
        bool ok = CastTable.TryPromote(value, XdmTypeCode.Double, out XdmAtomicValue? result);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(result!.TypeCode, Is.EqualTo(XdmTypeCode.Double));
    }
}
