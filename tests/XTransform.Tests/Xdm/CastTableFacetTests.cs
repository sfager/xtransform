// Tests for CastTable.ValidateDerivedFacets and derived-type range/pattern checks.
using System.Numerics;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
[Category("Cast")]
public sealed class CastTableFacetTests
{
    // -------------------------------------------------------------------------
    // Integer range facets — xs:byte
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_IntegerToByte_OutOfRange_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(200L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.Byte, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_IntegerToByte_InRange_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(127L);

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.Byte, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Byte));
        Assert.That(result.AsBigInteger(), Is.EqualTo(new BigInteger(127)));
    }

    [Test]
    public void Cast_IntegerToByte_NegativeInRange_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(-128L);

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.Byte, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Byte));
        Assert.That(result.AsBigInteger(), Is.EqualTo(new BigInteger(-128)));
    }

    [Test]
    public void Cast_IntegerToByte_NegativeOutOfRange_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(-129L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.Byte, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // xs:positiveInteger
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_IntegerToPositiveInteger_Zero_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(0L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.PositiveInteger, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_IntegerToPositiveInteger_One_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(1L);

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.PositiveInteger, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.PositiveInteger));
        Assert.That(result.AsBigInteger(), Is.EqualTo(BigInteger.One));
    }

    // -------------------------------------------------------------------------
    // xs:nonNegativeInteger
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_IntegerToNonNegativeInteger_NegativeOne_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(-1L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NonNegativeInteger, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_IntegerToNonNegativeInteger_Zero_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(0L);

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.NonNegativeInteger, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.NonNegativeInteger));
    }

    // -------------------------------------------------------------------------
    // xs:nonPositiveInteger
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_IntegerToNonPositiveInteger_One_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(1L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NonPositiveInteger, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // xs:negativeInteger
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_IntegerToNegativeInteger_Zero_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(0L);

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NegativeInteger, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // xs:short
    // -------------------------------------------------------------------------

    [TestCase(32767, true)]
    [TestCase(32768, false)]
    [TestCase(-32768, true)]
    [TestCase(-32769, false)]
    public void Cast_IntegerToShort_RangeBoundary(int input, bool shouldSucceed)
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer((long)input);

        // Act / Assert
        if (shouldSucceed)
        {
            XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.Short, SourceSpan.None);
            Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Short));
        }
        else
        {
            XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
                () => CastTable.Cast(value, XdmTypeCode.Short, SourceSpan.None))!;
            Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
        }
    }

    // -------------------------------------------------------------------------
    // xs:unsignedByte
    // -------------------------------------------------------------------------

    [TestCase("0", true)]
    [TestCase("255", true)]
    [TestCase("256", false)]
    public void Cast_IntegerToUnsignedByte_RangeBoundary(string lexical, bool shouldSucceed)
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Parse(lexical, XdmTypeCode.Integer);

        // Act / Assert
        if (shouldSucceed)
        {
            XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.UnsignedByte, SourceSpan.None);
            Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.UnsignedByte));
        }
        else
        {
            XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
                () => CastTable.Cast(value, XdmTypeCode.UnsignedByte, SourceSpan.None))!;
            Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
        }
    }

    // -------------------------------------------------------------------------
    // String-derived: xs:NCName
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_StringToNCName_WithSpace_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("foo bar");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NcName, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_StringToNCName_StartsWithDigit_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("1foo");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NcName, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_StringToNCName_Valid_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("foo");

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.NcName, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.NcName));
        Assert.That(result.AsString(), Is.EqualTo("foo"));
    }

    // -------------------------------------------------------------------------
    // String-derived: xs:normalizedString (tab violation)
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_StringToNormalizedString_WithTab_ThrowsFORG0001()
    {
        // Arrange — xs:string "preserve" mode keeps the tab
        XdmAtomicValue value = XdmAtomicValue.String("hello\tworld");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.NormalizedString, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_StringToNormalizedString_NoControlChars_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("hello world");

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.NormalizedString, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.NormalizedString));
    }

    // -------------------------------------------------------------------------
    // xs:token
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_StringToToken_LeadingSpace_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String(" hello");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.Token, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_StringToToken_Valid_Succeeds()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("hello world");

        // Act
        XdmAtomicValue result = CastTable.Cast(value, XdmTypeCode.Token, SourceSpan.None);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Token));
    }

    [Test]
    public void Cast_StringToToken_ConsecutiveSpaces_ThrowsFORG0001()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("hello  world");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.Token, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Parse-with-facets: xs:byte range check in Parse path
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ByteOutOfRange_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => XdmAtomicValue.Parse("300", XdmTypeCode.Byte))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Parse_ByteInRange_Succeeds()
    {
        // Arrange / Act
        XdmAtomicValue result = XdmAtomicValue.Parse("100", XdmTypeCode.Byte);

        // Assert
        Assert.That(result.TypeCode, Is.EqualTo(XdmTypeCode.Byte));
    }

    // -------------------------------------------------------------------------
    // AnyAtomicType as target — always XPTY0004
    // -------------------------------------------------------------------------

    [Test]
    public void Cast_ToAnyAtomicType_ThrowsXPTY0004()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.String("hello");

        // Act
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => CastTable.Cast(value, XdmTypeCode.AnyAtomicType, SourceSpan.None))!;

        // Assert
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    // -------------------------------------------------------------------------
    // TryCast never throws
    // -------------------------------------------------------------------------

    [Test]
    public void TryCast_NullValue_ReturnsFalse()
    {
        // Arrange / Act
        bool result = CastTable.TryCast(null, XdmTypeCode.String, out XdmAtomicValue? output);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(output, Is.Null);
    }

    [Test]
    public void TryCast_IncompatibleTypes_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Boolean(true);

        // Act
        bool result = CastTable.TryCast(value, XdmTypeCode.DateTime, out XdmAtomicValue? output);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(output, Is.Null);
    }

    [Test]
    public void TryCast_ValidCast_ReturnsTrueWithResult()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(42L);

        // Act
        bool result = CastTable.TryCast(value, XdmTypeCode.Double, out XdmAtomicValue? output);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(output, Is.Not.Null);
        Assert.That(output!.AsDouble(), Is.EqualTo(42.0));
    }

    [Test]
    public void TryCast_OutOfRangeForDerivedType_ReturnsFalse()
    {
        // Arrange
        XdmAtomicValue value = XdmAtomicValue.Integer(200L);

        // Act
        bool result = CastTable.TryCast(value, XdmTypeCode.Byte, out XdmAtomicValue? output);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(output, Is.Null);
    }
}
