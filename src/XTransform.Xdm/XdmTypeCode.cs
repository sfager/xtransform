// XSD built-in type names (String, Decimal, Integer, Long, Int, Short, Double, Float) are
// mandated by the XSD 1.0 Part 2 §3 spec; CA1720 (identifier contains type name) does not apply.
#pragma warning disable CA1720

namespace XTransform.Xdm;

/// <summary>
/// Built-in atomic types supported in v1, in the XSD derivation order. The numeric
/// values are stable and index the cast table; append new members at the end only.
/// </summary>
public enum XdmTypeCode : byte
{
    /// <summary>xs:anyAtomicType — the abstract root of all atomic types.</summary>
    AnyAtomicType,
    /// <summary>xs:untypedAtomic — untyped atomic value.</summary>
    UntypedAtomic,

    /// <summary>xs:string.</summary>
    String,
    /// <summary>xs:normalizedString — derived from xs:string.</summary>
    NormalizedString,
    /// <summary>xs:token — derived from xs:normalizedString.</summary>
    Token,
    /// <summary>xs:language — derived from xs:token.</summary>
    Language,
    /// <summary>xs:NMTOKEN — derived from xs:token.</summary>
    NmToken,
    /// <summary>xs:Name — derived from xs:token.</summary>
    Name,
    /// <summary>xs:NCName — derived from xs:Name.</summary>
    NcName,
    /// <summary>xs:ID — derived from xs:NCName.</summary>
    Id,
    /// <summary>xs:IDREF — derived from xs:NCName.</summary>
    IdRef,
    /// <summary>xs:ENTITY — derived from xs:NCName.</summary>
    Entity,

    /// <summary>xs:boolean.</summary>
    Boolean,

    /// <summary>xs:decimal.</summary>
    Decimal,
    /// <summary>xs:integer — derived from xs:decimal.</summary>
    Integer,
    /// <summary>xs:nonPositiveInteger — derived from xs:integer.</summary>
    NonPositiveInteger,
    /// <summary>xs:negativeInteger — derived from xs:nonPositiveInteger.</summary>
    NegativeInteger,
    /// <summary>xs:long — derived from xs:integer.</summary>
    Long,
    /// <summary>xs:int — derived from xs:long.</summary>
    Int,
    /// <summary>xs:short — derived from xs:int.</summary>
    Short,
    /// <summary>xs:byte — derived from xs:short.</summary>
    Byte,
    /// <summary>xs:nonNegativeInteger — derived from xs:integer.</summary>
    NonNegativeInteger,
    /// <summary>xs:unsignedLong — derived from xs:nonNegativeInteger.</summary>
    UnsignedLong,
    /// <summary>xs:unsignedInt — derived from xs:unsignedLong.</summary>
    UnsignedInt,
    /// <summary>xs:unsignedShort — derived from xs:unsignedInt.</summary>
    UnsignedShort,
    /// <summary>xs:unsignedByte — derived from xs:unsignedShort.</summary>
    UnsignedByte,
    /// <summary>xs:positiveInteger — derived from xs:nonNegativeInteger.</summary>
    PositiveInteger,

    /// <summary>xs:double.</summary>
    Double,
    /// <summary>xs:float.</summary>
    Float,

    /// <summary>xs:duration.</summary>
    Duration,
    /// <summary>xs:dayTimeDuration — derived from xs:duration.</summary>
    DayTimeDuration,
    /// <summary>xs:yearMonthDuration — derived from xs:duration.</summary>
    YearMonthDuration,

    /// <summary>xs:dateTime.</summary>
    DateTime,
    /// <summary>xs:date.</summary>
    Date,
    /// <summary>xs:time.</summary>
    Time,
    /// <summary>xs:gYearMonth.</summary>
    GYearMonth,
    /// <summary>xs:gYear.</summary>
    GYear,
    /// <summary>xs:gMonthDay.</summary>
    GMonthDay,
    /// <summary>xs:gDay.</summary>
    GDay,
    /// <summary>xs:gMonth.</summary>
    GMonth,

    /// <summary>xs:hexBinary.</summary>
    HexBinary,
    /// <summary>xs:base64Binary.</summary>
    Base64Binary,

    /// <summary>xs:anyURI.</summary>
    AnyUri,
    /// <summary>xs:QName.</summary>
    QName,
    /// <summary>xs:NOTATION.</summary>
    Notation,
}

#pragma warning restore CA1720
