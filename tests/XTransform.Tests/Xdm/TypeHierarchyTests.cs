using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class TypeHierarchyTests
{
    // --- PrimitiveTypeOf ---

    // XSD 1.0 Part 2 §3: primitive = type whose base is anyAtomicType (or anyAtomicType itself).
    [TestCase(XdmTypeCode.AnyAtomicType,        XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.UntypedAtomic,        XdmTypeCode.UntypedAtomic)]
    [TestCase(XdmTypeCode.String,               XdmTypeCode.String)]
    [TestCase(XdmTypeCode.NormalizedString,      XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Token,                XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Language,             XdmTypeCode.String)]
    [TestCase(XdmTypeCode.NmToken,              XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Name,                 XdmTypeCode.String)]
    [TestCase(XdmTypeCode.NcName,               XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Id,                   XdmTypeCode.String)]
    [TestCase(XdmTypeCode.IdRef,                XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Entity,               XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Boolean,              XdmTypeCode.Boolean)]
    [TestCase(XdmTypeCode.Decimal,              XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Integer,              XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.NonPositiveInteger,   XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.NegativeInteger,      XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Long,                 XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Int,                  XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Short,                XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Byte,                 XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.NonNegativeInteger,   XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.UnsignedLong,         XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.UnsignedInt,          XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.UnsignedShort,        XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.UnsignedByte,         XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.PositiveInteger,      XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Double,               XdmTypeCode.Double)]
    [TestCase(XdmTypeCode.Float,                XdmTypeCode.Float)]
    [TestCase(XdmTypeCode.Duration,             XdmTypeCode.Duration)]
    [TestCase(XdmTypeCode.DayTimeDuration,      XdmTypeCode.Duration)]
    [TestCase(XdmTypeCode.YearMonthDuration,    XdmTypeCode.Duration)]
    [TestCase(XdmTypeCode.DateTime,             XdmTypeCode.DateTime)]
    [TestCase(XdmTypeCode.Date,                 XdmTypeCode.Date)]
    [TestCase(XdmTypeCode.Time,                 XdmTypeCode.Time)]
    [TestCase(XdmTypeCode.GYearMonth,           XdmTypeCode.GYearMonth)]
    [TestCase(XdmTypeCode.GYear,                XdmTypeCode.GYear)]
    [TestCase(XdmTypeCode.GMonthDay,            XdmTypeCode.GMonthDay)]
    [TestCase(XdmTypeCode.GDay,                 XdmTypeCode.GDay)]
    [TestCase(XdmTypeCode.GMonth,               XdmTypeCode.GMonth)]
    [TestCase(XdmTypeCode.HexBinary,            XdmTypeCode.HexBinary)]
    [TestCase(XdmTypeCode.Base64Binary,         XdmTypeCode.Base64Binary)]
    [TestCase(XdmTypeCode.AnyUri,               XdmTypeCode.AnyUri)]
    [TestCase(XdmTypeCode.QName,                XdmTypeCode.QName)]
    [TestCase(XdmTypeCode.Notation,             XdmTypeCode.Notation)]
    public void PrimitiveTypeOf_AllTypes_ReturnsExpectedPrimitive(XdmTypeCode type, XdmTypeCode expected)
    {
        // Arrange / Act
        var actual = CastTable.PrimitiveTypeOf(type);

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // --- IsSubtypeOf (direct parents) ---

    // XSD 1.0 Part 2 §3: every type is a direct subtype of its declared base type.
    [TestCase(XdmTypeCode.AnyAtomicType,        XdmTypeCode.AnyAtomicType)]  // reflexive
    [TestCase(XdmTypeCode.UntypedAtomic,        XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.String,               XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.NormalizedString,      XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Token,                XdmTypeCode.NormalizedString)]
    [TestCase(XdmTypeCode.Language,             XdmTypeCode.Token)]
    [TestCase(XdmTypeCode.NmToken,              XdmTypeCode.Token)]
    [TestCase(XdmTypeCode.Name,                 XdmTypeCode.Token)]
    [TestCase(XdmTypeCode.NcName,               XdmTypeCode.Name)]
    [TestCase(XdmTypeCode.Id,                   XdmTypeCode.NcName)]
    [TestCase(XdmTypeCode.IdRef,                XdmTypeCode.NcName)]
    [TestCase(XdmTypeCode.Entity,               XdmTypeCode.NcName)]
    [TestCase(XdmTypeCode.Boolean,              XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Decimal,              XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Integer,              XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.NonPositiveInteger,   XdmTypeCode.Integer)]
    [TestCase(XdmTypeCode.NegativeInteger,      XdmTypeCode.NonPositiveInteger)]
    [TestCase(XdmTypeCode.Long,                 XdmTypeCode.Integer)]
    [TestCase(XdmTypeCode.Int,                  XdmTypeCode.Long)]
    [TestCase(XdmTypeCode.Short,                XdmTypeCode.Int)]
    [TestCase(XdmTypeCode.Byte,                 XdmTypeCode.Short)]
    [TestCase(XdmTypeCode.NonNegativeInteger,   XdmTypeCode.Integer)]
    [TestCase(XdmTypeCode.UnsignedLong,         XdmTypeCode.NonNegativeInteger)]
    [TestCase(XdmTypeCode.UnsignedInt,          XdmTypeCode.UnsignedLong)]
    [TestCase(XdmTypeCode.UnsignedShort,        XdmTypeCode.UnsignedInt)]
    [TestCase(XdmTypeCode.UnsignedByte,         XdmTypeCode.UnsignedShort)]
    [TestCase(XdmTypeCode.PositiveInteger,      XdmTypeCode.NonNegativeInteger)]
    [TestCase(XdmTypeCode.Double,               XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Float,                XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Duration,             XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.DayTimeDuration,      XdmTypeCode.Duration)]
    [TestCase(XdmTypeCode.YearMonthDuration,    XdmTypeCode.Duration)]
    [TestCase(XdmTypeCode.DateTime,             XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Date,                 XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Time,                 XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.GYearMonth,           XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.GYear,                XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.GMonthDay,            XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.GDay,                 XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.GMonth,               XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.HexBinary,            XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Base64Binary,         XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.AnyUri,               XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.QName,                XdmTypeCode.AnyAtomicType)]
    [TestCase(XdmTypeCode.Notation,             XdmTypeCode.AnyAtomicType)]
    public void IsSubtypeOf_DirectParent_ReturnsTrue(XdmTypeCode type, XdmTypeCode parent)
    {
        // Arrange / Act / Assert
        Assert.That(CastTable.IsSubtypeOf(type, parent), Is.True,
            $"{type} should be subtype of {parent}");
    }

    [Test]
    public void IsSubtypeOf_AllTypes_AreSubtypeOfAnyAtomicType()
    {
        // Arrange
#if NET5_0_OR_GREATER
        var allTypes = System.Enum.GetValues<XdmTypeCode>();
#else
        var allTypes = (XdmTypeCode[])System.Enum.GetValues(typeof(XdmTypeCode));
#endif

        // Act / Assert
        foreach (var type in allTypes)
        {
            Assert.That(CastTable.IsSubtypeOf(type, XdmTypeCode.AnyAtomicType), Is.True,
                $"{type} should be subtype of AnyAtomicType");
        }
    }

    // Transitive subtype relationships
    [TestCase(XdmTypeCode.Byte,          XdmTypeCode.Integer)]  // byte → short → int → long → integer
    [TestCase(XdmTypeCode.Byte,          XdmTypeCode.Decimal)]  // byte → … → integer → decimal
    [TestCase(XdmTypeCode.Id,            XdmTypeCode.String)]   // ID → NCName → … → string
    [TestCase(XdmTypeCode.NegativeInteger, XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.UnsignedByte,  XdmTypeCode.NonNegativeInteger)]
    public void IsSubtypeOf_TransitiveAncestor_ReturnsTrue(XdmTypeCode type, XdmTypeCode ancestor)
    {
        // Arrange / Act / Assert
        Assert.That(CastTable.IsSubtypeOf(type, ancestor), Is.True,
            $"{type} should transitively be subtype of {ancestor}");
    }

    // Non-subtype relationships
    [TestCase(XdmTypeCode.String,  XdmTypeCode.Decimal)]
    [TestCase(XdmTypeCode.Boolean, XdmTypeCode.String)]
    [TestCase(XdmTypeCode.Date,    XdmTypeCode.DateTime)]
    [TestCase(XdmTypeCode.Float,   XdmTypeCode.Double)]
    [TestCase(XdmTypeCode.Integer, XdmTypeCode.Long)]
    public void IsSubtypeOf_NonAncestor_ReturnsFalse(XdmTypeCode type, XdmTypeCode nonAncestor)
    {
        // Arrange / Act / Assert
        Assert.That(CastTable.IsSubtypeOf(type, nonAncestor), Is.False,
            $"{type} should NOT be subtype of {nonAncestor}");
    }

    // --- Name ↔ code map ---

    // XSD local names are case-sensitive per the spec.
    [TestCase("anyAtomicType",      XdmTypeCode.AnyAtomicType)]
    [TestCase("untypedAtomic",      XdmTypeCode.UntypedAtomic)]
    [TestCase("string",             XdmTypeCode.String)]
    [TestCase("normalizedString",   XdmTypeCode.NormalizedString)]
    [TestCase("token",              XdmTypeCode.Token)]
    [TestCase("language",           XdmTypeCode.Language)]
    [TestCase("NMTOKEN",            XdmTypeCode.NmToken)]
    [TestCase("Name",               XdmTypeCode.Name)]
    [TestCase("NCName",             XdmTypeCode.NcName)]
    [TestCase("ID",                 XdmTypeCode.Id)]
    [TestCase("IDREF",              XdmTypeCode.IdRef)]
    [TestCase("ENTITY",             XdmTypeCode.Entity)]
    [TestCase("boolean",            XdmTypeCode.Boolean)]
    [TestCase("decimal",            XdmTypeCode.Decimal)]
    [TestCase("integer",            XdmTypeCode.Integer)]
    [TestCase("nonPositiveInteger", XdmTypeCode.NonPositiveInteger)]
    [TestCase("negativeInteger",    XdmTypeCode.NegativeInteger)]
    [TestCase("long",               XdmTypeCode.Long)]
    [TestCase("int",                XdmTypeCode.Int)]
    [TestCase("short",              XdmTypeCode.Short)]
    [TestCase("byte",               XdmTypeCode.Byte)]
    [TestCase("nonNegativeInteger", XdmTypeCode.NonNegativeInteger)]
    [TestCase("unsignedLong",       XdmTypeCode.UnsignedLong)]
    [TestCase("unsignedInt",        XdmTypeCode.UnsignedInt)]
    [TestCase("unsignedShort",      XdmTypeCode.UnsignedShort)]
    [TestCase("unsignedByte",       XdmTypeCode.UnsignedByte)]
    [TestCase("positiveInteger",    XdmTypeCode.PositiveInteger)]
    [TestCase("double",             XdmTypeCode.Double)]
    [TestCase("float",              XdmTypeCode.Float)]
    [TestCase("duration",           XdmTypeCode.Duration)]
    [TestCase("dayTimeDuration",    XdmTypeCode.DayTimeDuration)]
    [TestCase("yearMonthDuration",  XdmTypeCode.YearMonthDuration)]
    [TestCase("dateTime",           XdmTypeCode.DateTime)]
    [TestCase("date",               XdmTypeCode.Date)]
    [TestCase("time",               XdmTypeCode.Time)]
    [TestCase("gYearMonth",         XdmTypeCode.GYearMonth)]
    [TestCase("gYear",              XdmTypeCode.GYear)]
    [TestCase("gMonthDay",          XdmTypeCode.GMonthDay)]
    [TestCase("gDay",               XdmTypeCode.GDay)]
    [TestCase("gMonth",             XdmTypeCode.GMonth)]
    [TestCase("hexBinary",          XdmTypeCode.HexBinary)]
    [TestCase("base64Binary",       XdmTypeCode.Base64Binary)]
    [TestCase("anyURI",             XdmTypeCode.AnyUri)]
    [TestCase("QName",              XdmTypeCode.QName)]
    [TestCase("NOTATION",           XdmTypeCode.Notation)]
    public void TryGetTypeCode_KnownXsName_ReturnsTrueWithCorrectCode(string xsName, XdmTypeCode expected)
    {
        // Arrange / Act
        var found = CastTable.TryGetTypeCode(xsName, out var actual);

        // Assert
        Assert.That(found, Is.True, $"xs:{xsName} should be found");
        Assert.That(actual, Is.EqualTo(expected));
    }

    // Wrong-case variants that must NOT match (case-sensitive lookup)
    [TestCase("String")]
    [TestCase("INTEGER")]
    [TestCase("nmtoken")]
    [TestCase("ncname")]
    [TestCase("id")]
    [TestCase("idref")]
    [TestCase("entity")]
    [TestCase("notation")]
    [TestCase("qname")]
    [TestCase("AnyURI")]
    [TestCase("DateTime")]
    [TestCase("unknownType")]
    public void TryGetTypeCode_UnknownOrWrongCaseName_ReturnsFalse(string xsName)
    {
        // Arrange / Act
        var found = CastTable.TryGetTypeCode(xsName, out _);

        // Assert
        Assert.That(found, Is.False, $"xs:{xsName} should not be found");
    }

    // GetXsLocalName round-trips with TryGetTypeCode
    [Test]
    public void GetXsLocalName_AllTypes_RoundTripWithTryGetTypeCode()
    {
        // Arrange
#if NET5_0_OR_GREATER
        var allTypes = System.Enum.GetValues<XdmTypeCode>();
#else
        var allTypes = (XdmTypeCode[])System.Enum.GetValues(typeof(XdmTypeCode));
#endif

        // Act / Assert
        foreach (var type in allTypes)
        {
            var name = CastTable.GetXsLocalName(type);
            var found = CastTable.TryGetTypeCode(name, out var roundTripped);
            Assert.That(found, Is.True, $"xs:{name} should round-trip");
            Assert.That(roundTripped, Is.EqualTo(type), $"xs:{name} should map back to {type}");
        }
    }
}
