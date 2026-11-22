using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmAtomicValueEqualityTests
{
    // -------------------------------------------------------------------------
    // Boolean equality
    // -------------------------------------------------------------------------

    [Test]
    public void Boolean_TrueEqualsTrue()
    {
        // Arrange
        var v1 = XdmAtomicValue.Boolean(true);
        var v2 = XdmAtomicValue.Boolean(true);

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
        Assert.That(v1.Equals(v2), Is.True);
    }

    [Test]
    public void Boolean_TrueNotEqualsFalse()
    {
        // Arrange
        var v1 = XdmAtomicValue.Boolean(true);
        var v2 = XdmAtomicValue.Boolean(false);

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
        Assert.That(v1 != v2, Is.True);
    }

    // -------------------------------------------------------------------------
    // Integer / Decimal equality (same primitive — Decimal)
    // -------------------------------------------------------------------------

    [Test]
    public void Integer_EqualValues_AreEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.Integer(5L);
        var v2 = XdmAtomicValue.Integer(5L);

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    [Test]
    public void Integer_And_Decimal_SameNumericValue_AreEqual()
    {
        // Arrange — both have primitive Decimal, so compared as XsdDecimal
        var v1 = XdmAtomicValue.Integer(5L);
        var v2 = XdmAtomicValue.Decimal(5m);

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    // -------------------------------------------------------------------------
    // Double equality — NaN != NaN (op:numeric-equal)
    // -------------------------------------------------------------------------

    [Test]
    public void Double_NaN_NotEqualsNaN()
    {
        // Arrange
        var v1 = XdmAtomicValue.Double(double.NaN);
        var v2 = XdmAtomicValue.Double(double.NaN);

        // Act / Assert — per op:numeric-equal, NaN != NaN
        Assert.That(v1 == v2, Is.False);
        Assert.That(v1.Equals(v2), Is.False);
    }

    [Test]
    public void Double_SameValues_AreEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.Double(3.14);
        var v2 = XdmAtomicValue.Double(3.14);

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    // -------------------------------------------------------------------------
    // Different primitives — always unequal
    // -------------------------------------------------------------------------

    [Test]
    public void String_AndUntypedAtomic_SameString_AreNotEqual()
    {
        // Arrange — different primitives (String vs UntypedAtomic)
        var v1 = XdmAtomicValue.String("a");
        var v2 = XdmAtomicValue.UntypedAtomic("a");

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
    }

    [Test]
    public void String_AndAnyUri_SameString_AreNotEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.String("a");
        var v2 = XdmAtomicValue.AnyUri("a");

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
    }

    [Test]
    public void Boolean_AndInteger_AreNotEqual()
    {
        // Arrange — different primitives (Boolean vs Decimal)
        var v1 = XdmAtomicValue.Boolean(true);
        var v2 = XdmAtomicValue.Integer(1L);

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
    }

    // -------------------------------------------------------------------------
    // Null handling
    // -------------------------------------------------------------------------

    [Test]
    public void Equality_NullLeft_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue? left = null;
        var right = XdmAtomicValue.Integer(1L);

        // Act / Assert
        Assert.That(left == right, Is.False);
    }

    [Test]
    public void Equality_NullRight_ReturnsFalse()
    {
        // Arrange
        var left = XdmAtomicValue.Integer(1L);
        XdmAtomicValue? right = null;

        // Act / Assert
        Assert.That(left == right, Is.False);
    }

    [Test]
    public void Equality_BothNull_ReturnsTrue()
    {
        // Arrange
        XdmAtomicValue? v1 = null;
        XdmAtomicValue? v2 = null;

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    [Test]
    public void Equals_Null_ReturnsFalse()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(1L);

        // Act / Assert
        Assert.That(v.Equals(null), Is.False);
    }

    [Test]
    public void Equals_SameReference_ReturnsTrue()
    {
        // Arrange
        var v = XdmAtomicValue.Integer(1L);

        // Act / Assert
        Assert.That(v.Equals(v), Is.True);
    }

    // -------------------------------------------------------------------------
    // GetHashCode — equal values have equal hash codes
    // -------------------------------------------------------------------------

    [Test]
    public void GetHashCode_EqualBooleans_SameHashCode()
    {
        // Arrange
        var v1 = XdmAtomicValue.Boolean(true);
        var v2 = XdmAtomicValue.Boolean(true);

        // Act / Assert
        Assert.That(v1.GetHashCode(), Is.EqualTo(v2.GetHashCode()));
    }

    [Test]
    public void GetHashCode_EqualIntegers_SameHashCode()
    {
        // Arrange
        var v1 = XdmAtomicValue.Integer(42L);
        var v2 = XdmAtomicValue.Integer(42L);

        // Act / Assert
        Assert.That(v1.GetHashCode(), Is.EqualTo(v2.GetHashCode()));
    }

    [Test]
    public void GetHashCode_EqualStrings_SameHashCode()
    {
        // Arrange
        var v1 = XdmAtomicValue.String("hello");
        var v2 = XdmAtomicValue.String("hello");

        // Act / Assert
        Assert.That(v1.GetHashCode(), Is.EqualTo(v2.GetHashCode()));
    }

    // -------------------------------------------------------------------------
    // HexBinary equality — byte-sequence comparison
    // -------------------------------------------------------------------------

    [Test]
    public void HexBinary_SameBytes_AreEqual()
    {
        // Arrange
        byte[] bytes = new byte[] { 0x01, 0x02, 0x03 };
        var v1 = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(bytes));
        var v2 = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>((byte[])bytes.Clone()));

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    [Test]
    public void HexBinary_DifferentBytes_AreNotEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(new byte[] { 0x01 }));
        var v2 = XdmAtomicValue.HexBinary(new ReadOnlyMemory<byte>(new byte[] { 0x02 }));

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
    }

    // -------------------------------------------------------------------------
    // Duration equality
    // -------------------------------------------------------------------------

    [Test]
    public void Duration_EqualDurations_AreEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.Parse("P1Y", XdmTypeCode.Duration);
        var v2 = XdmAtomicValue.Parse("P12M", XdmTypeCode.Duration);

        // Act / Assert — P1Y and P12M have same totalMonths
        Assert.That(v1 == v2, Is.True);
    }

    // -------------------------------------------------------------------------
    // QName equality
    // -------------------------------------------------------------------------

    [Test]
    public void QName_SameExpanded_AreEqual()
    {
        // Arrange — equality ignores prefix, based on {namespace}local
        var v1 = XdmAtomicValue.QNameValue(new QName("xs", "http://www.w3.org/2001/XMLSchema", "string"));
        var v2 = XdmAtomicValue.QNameValue(new QName("xsd", "http://www.w3.org/2001/XMLSchema", "string"));

        // Act / Assert
        Assert.That(v1 == v2, Is.True);
    }

    [Test]
    public void QName_DifferentLocal_AreNotEqual()
    {
        // Arrange
        var v1 = XdmAtomicValue.QNameValue(new QName("http://ns", "foo"));
        var v2 = XdmAtomicValue.QNameValue(new QName("http://ns", "bar"));

        // Act / Assert
        Assert.That(v1 == v2, Is.False);
    }
}
