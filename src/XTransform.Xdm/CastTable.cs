using System.Collections.Generic;

namespace XTransform.Xdm;

// XSD 1.0 Part 2 §3: built-in type hierarchy; XPath 2.0 §2.5.3: primitive types.
// IsCastAllowed / Cast / TryCast / TryPromote are implemented in P1-06 (need XdmAtomicValue).
internal static partial class CastTable
{
    private static readonly XdmTypeCode[] s_primitive = BuildPrimitiveLookup();
    private static readonly XdmTypeCode[] s_parent = BuildParentLookup();
    private static readonly Dictionary<string, XdmTypeCode> s_nameToCode = BuildNameToCodeMap();
    private static readonly string[] s_codeToName = BuildCodeToNameArray();

    // XPath 2.0 §2.5.3: the primitive type is the type reached by following base-type links
    // until a type whose base type is xs:anyAtomicType is reached (or anyAtomicType itself).
    public static XdmTypeCode PrimitiveTypeOf(XdmTypeCode type)
    {
        var index = (int)(byte)type;
        return index < s_primitive.Length ? s_primitive[index] : type;
    }

    // XSD 1.0 Part 2 §3: walk up the derivation chain.
    public static bool IsSubtypeOf(XdmTypeCode type, XdmTypeCode ancestor)
    {
        if (ancestor == XdmTypeCode.AnyAtomicType)
        {
            return true;
        }

        var current = type;
        while (true)
        {
            if (current == ancestor)
            {
                return true;
            }
            var parent = s_parent[(int)(byte)current];
            if (parent == current)
            {
                return false; // reached root without match
            }
            current = parent;
        }
    }

    /// <summary>Looks up an xs: local name (e.g. "string", "NMTOKEN") and returns the type code.</summary>
    public static bool TryGetTypeCode(string xsLocalName, out XdmTypeCode typeCode)
        => s_nameToCode.TryGetValue(xsLocalName, out typeCode);

    /// <summary>Returns the xs: local name for a type code (e.g. XdmTypeCode.NmToken → "NMTOKEN").</summary>
    public static string GetXsLocalName(XdmTypeCode typeCode)
    {
        var index = (int)(byte)typeCode;
        return index < s_codeToName.Length ? s_codeToName[index] : typeCode.ToString();
    }

    private static XdmTypeCode[] BuildPrimitiveLookup()
    {
        var t = new XdmTypeCode[45];

        t[(int)XdmTypeCode.AnyAtomicType] = XdmTypeCode.AnyAtomicType;
        t[(int)XdmTypeCode.UntypedAtomic] = XdmTypeCode.UntypedAtomic;

        // String family → String
        t[(int)XdmTypeCode.String] = XdmTypeCode.String;
        t[(int)XdmTypeCode.NormalizedString] = XdmTypeCode.String;
        t[(int)XdmTypeCode.Token] = XdmTypeCode.String;
        t[(int)XdmTypeCode.Language] = XdmTypeCode.String;
        t[(int)XdmTypeCode.NmToken] = XdmTypeCode.String;
        t[(int)XdmTypeCode.Name] = XdmTypeCode.String;
        t[(int)XdmTypeCode.NcName] = XdmTypeCode.String;
        t[(int)XdmTypeCode.Id] = XdmTypeCode.String;
        t[(int)XdmTypeCode.IdRef] = XdmTypeCode.String;
        t[(int)XdmTypeCode.Entity] = XdmTypeCode.String;

        t[(int)XdmTypeCode.Boolean] = XdmTypeCode.Boolean;

        // Decimal family → Decimal
        t[(int)XdmTypeCode.Decimal] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.Integer] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.NonPositiveInteger] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.NegativeInteger] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.Long] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.Int] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.Short] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.Byte] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.NonNegativeInteger] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.UnsignedLong] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.UnsignedInt] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.UnsignedShort] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.UnsignedByte] = XdmTypeCode.Decimal;
        t[(int)XdmTypeCode.PositiveInteger] = XdmTypeCode.Decimal;

        t[(int)XdmTypeCode.Double] = XdmTypeCode.Double;
        t[(int)XdmTypeCode.Float] = XdmTypeCode.Float;

        // Duration family → Duration
        t[(int)XdmTypeCode.Duration] = XdmTypeCode.Duration;
        t[(int)XdmTypeCode.DayTimeDuration] = XdmTypeCode.Duration;
        t[(int)XdmTypeCode.YearMonthDuration] = XdmTypeCode.Duration;

        // Date/time primitives → themselves
        t[(int)XdmTypeCode.DateTime] = XdmTypeCode.DateTime;
        t[(int)XdmTypeCode.Date] = XdmTypeCode.Date;
        t[(int)XdmTypeCode.Time] = XdmTypeCode.Time;
        t[(int)XdmTypeCode.GYearMonth] = XdmTypeCode.GYearMonth;
        t[(int)XdmTypeCode.GYear] = XdmTypeCode.GYear;
        t[(int)XdmTypeCode.GMonthDay] = XdmTypeCode.GMonthDay;
        t[(int)XdmTypeCode.GDay] = XdmTypeCode.GDay;
        t[(int)XdmTypeCode.GMonth] = XdmTypeCode.GMonth;

        t[(int)XdmTypeCode.HexBinary] = XdmTypeCode.HexBinary;
        t[(int)XdmTypeCode.Base64Binary] = XdmTypeCode.Base64Binary;
        t[(int)XdmTypeCode.AnyUri] = XdmTypeCode.AnyUri;
        t[(int)XdmTypeCode.QName] = XdmTypeCode.QName;
        t[(int)XdmTypeCode.Notation] = XdmTypeCode.Notation;

        return t;
    }

    private static XdmTypeCode[] BuildParentLookup()
    {
        // Parent in the XSD derivation hierarchy. AnyAtomicType is its own parent (sentinel for root).
        var p = new XdmTypeCode[45];

        p[(int)XdmTypeCode.AnyAtomicType] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.UntypedAtomic] = XdmTypeCode.AnyAtomicType;

        p[(int)XdmTypeCode.String] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.NormalizedString] = XdmTypeCode.String;
        p[(int)XdmTypeCode.Token] = XdmTypeCode.NormalizedString;
        p[(int)XdmTypeCode.Language] = XdmTypeCode.Token;
        p[(int)XdmTypeCode.NmToken] = XdmTypeCode.Token;
        p[(int)XdmTypeCode.Name] = XdmTypeCode.Token;
        p[(int)XdmTypeCode.NcName] = XdmTypeCode.Name;
        p[(int)XdmTypeCode.Id] = XdmTypeCode.NcName;
        p[(int)XdmTypeCode.IdRef] = XdmTypeCode.NcName;
        p[(int)XdmTypeCode.Entity] = XdmTypeCode.NcName;

        p[(int)XdmTypeCode.Boolean] = XdmTypeCode.AnyAtomicType;

        p[(int)XdmTypeCode.Decimal] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Integer] = XdmTypeCode.Decimal;
        p[(int)XdmTypeCode.NonPositiveInteger] = XdmTypeCode.Integer;
        p[(int)XdmTypeCode.NegativeInteger] = XdmTypeCode.NonPositiveInteger;
        p[(int)XdmTypeCode.Long] = XdmTypeCode.Integer;
        p[(int)XdmTypeCode.Int] = XdmTypeCode.Long;
        p[(int)XdmTypeCode.Short] = XdmTypeCode.Int;
        p[(int)XdmTypeCode.Byte] = XdmTypeCode.Short;
        p[(int)XdmTypeCode.NonNegativeInteger] = XdmTypeCode.Integer;
        p[(int)XdmTypeCode.UnsignedLong] = XdmTypeCode.NonNegativeInteger;
        p[(int)XdmTypeCode.UnsignedInt] = XdmTypeCode.UnsignedLong;
        p[(int)XdmTypeCode.UnsignedShort] = XdmTypeCode.UnsignedInt;
        p[(int)XdmTypeCode.UnsignedByte] = XdmTypeCode.UnsignedShort;
        p[(int)XdmTypeCode.PositiveInteger] = XdmTypeCode.NonNegativeInteger;

        p[(int)XdmTypeCode.Double] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Float] = XdmTypeCode.AnyAtomicType;

        p[(int)XdmTypeCode.Duration] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.DayTimeDuration] = XdmTypeCode.Duration;
        p[(int)XdmTypeCode.YearMonthDuration] = XdmTypeCode.Duration;

        p[(int)XdmTypeCode.DateTime] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Date] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Time] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.GYearMonth] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.GYear] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.GMonthDay] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.GDay] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.GMonth] = XdmTypeCode.AnyAtomicType;

        p[(int)XdmTypeCode.HexBinary] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Base64Binary] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.AnyUri] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.QName] = XdmTypeCode.AnyAtomicType;
        p[(int)XdmTypeCode.Notation] = XdmTypeCode.AnyAtomicType;

        return p;
    }

    private static Dictionary<string, XdmTypeCode> BuildNameToCodeMap()
    {
        // XSD local names are case-sensitive per the spec (NMTOKEN, ID, IDREF, ENTITY, QName, NOTATION)
        return new Dictionary<string, XdmTypeCode>(System.StringComparer.Ordinal)
        {
            ["anyAtomicType"] = XdmTypeCode.AnyAtomicType,
            ["untypedAtomic"] = XdmTypeCode.UntypedAtomic,
            ["string"] = XdmTypeCode.String,
            ["normalizedString"] = XdmTypeCode.NormalizedString,
            ["token"] = XdmTypeCode.Token,
            ["language"] = XdmTypeCode.Language,
            ["NMTOKEN"] = XdmTypeCode.NmToken,
            ["Name"] = XdmTypeCode.Name,
            ["NCName"] = XdmTypeCode.NcName,
            ["ID"] = XdmTypeCode.Id,
            ["IDREF"] = XdmTypeCode.IdRef,
            ["ENTITY"] = XdmTypeCode.Entity,
            ["boolean"] = XdmTypeCode.Boolean,
            ["decimal"] = XdmTypeCode.Decimal,
            ["integer"] = XdmTypeCode.Integer,
            ["nonPositiveInteger"] = XdmTypeCode.NonPositiveInteger,
            ["negativeInteger"] = XdmTypeCode.NegativeInteger,
            ["long"] = XdmTypeCode.Long,
            ["int"] = XdmTypeCode.Int,
            ["short"] = XdmTypeCode.Short,
            ["byte"] = XdmTypeCode.Byte,
            ["nonNegativeInteger"] = XdmTypeCode.NonNegativeInteger,
            ["unsignedLong"] = XdmTypeCode.UnsignedLong,
            ["unsignedInt"] = XdmTypeCode.UnsignedInt,
            ["unsignedShort"] = XdmTypeCode.UnsignedShort,
            ["unsignedByte"] = XdmTypeCode.UnsignedByte,
            ["positiveInteger"] = XdmTypeCode.PositiveInteger,
            ["double"] = XdmTypeCode.Double,
            ["float"] = XdmTypeCode.Float,
            ["duration"] = XdmTypeCode.Duration,
            ["dayTimeDuration"] = XdmTypeCode.DayTimeDuration,
            ["yearMonthDuration"] = XdmTypeCode.YearMonthDuration,
            ["dateTime"] = XdmTypeCode.DateTime,
            ["date"] = XdmTypeCode.Date,
            ["time"] = XdmTypeCode.Time,
            ["gYearMonth"] = XdmTypeCode.GYearMonth,
            ["gYear"] = XdmTypeCode.GYear,
            ["gMonthDay"] = XdmTypeCode.GMonthDay,
            ["gDay"] = XdmTypeCode.GDay,
            ["gMonth"] = XdmTypeCode.GMonth,
            ["hexBinary"] = XdmTypeCode.HexBinary,
            ["base64Binary"] = XdmTypeCode.Base64Binary,
            ["anyURI"] = XdmTypeCode.AnyUri,
            ["QName"] = XdmTypeCode.QName,
            ["NOTATION"] = XdmTypeCode.Notation,
        };
    }

    private static string[] BuildCodeToNameArray()
    {
        var names = new string[45];
        names[(int)XdmTypeCode.AnyAtomicType] = "anyAtomicType";
        names[(int)XdmTypeCode.UntypedAtomic] = "untypedAtomic";
        names[(int)XdmTypeCode.String] = "string";
        names[(int)XdmTypeCode.NormalizedString] = "normalizedString";
        names[(int)XdmTypeCode.Token] = "token";
        names[(int)XdmTypeCode.Language] = "language";
        names[(int)XdmTypeCode.NmToken] = "NMTOKEN";
        names[(int)XdmTypeCode.Name] = "Name";
        names[(int)XdmTypeCode.NcName] = "NCName";
        names[(int)XdmTypeCode.Id] = "ID";
        names[(int)XdmTypeCode.IdRef] = "IDREF";
        names[(int)XdmTypeCode.Entity] = "ENTITY";
        names[(int)XdmTypeCode.Boolean] = "boolean";
        names[(int)XdmTypeCode.Decimal] = "decimal";
        names[(int)XdmTypeCode.Integer] = "integer";
        names[(int)XdmTypeCode.NonPositiveInteger] = "nonPositiveInteger";
        names[(int)XdmTypeCode.NegativeInteger] = "negativeInteger";
        names[(int)XdmTypeCode.Long] = "long";
        names[(int)XdmTypeCode.Int] = "int";
        names[(int)XdmTypeCode.Short] = "short";
        names[(int)XdmTypeCode.Byte] = "byte";
        names[(int)XdmTypeCode.NonNegativeInteger] = "nonNegativeInteger";
        names[(int)XdmTypeCode.UnsignedLong] = "unsignedLong";
        names[(int)XdmTypeCode.UnsignedInt] = "unsignedInt";
        names[(int)XdmTypeCode.UnsignedShort] = "unsignedShort";
        names[(int)XdmTypeCode.UnsignedByte] = "unsignedByte";
        names[(int)XdmTypeCode.PositiveInteger] = "positiveInteger";
        names[(int)XdmTypeCode.Double] = "double";
        names[(int)XdmTypeCode.Float] = "float";
        names[(int)XdmTypeCode.Duration] = "duration";
        names[(int)XdmTypeCode.DayTimeDuration] = "dayTimeDuration";
        names[(int)XdmTypeCode.YearMonthDuration] = "yearMonthDuration";
        names[(int)XdmTypeCode.DateTime] = "dateTime";
        names[(int)XdmTypeCode.Date] = "date";
        names[(int)XdmTypeCode.Time] = "time";
        names[(int)XdmTypeCode.GYearMonth] = "gYearMonth";
        names[(int)XdmTypeCode.GYear] = "gYear";
        names[(int)XdmTypeCode.GMonthDay] = "gMonthDay";
        names[(int)XdmTypeCode.GDay] = "gDay";
        names[(int)XdmTypeCode.GMonth] = "gMonth";
        names[(int)XdmTypeCode.HexBinary] = "hexBinary";
        names[(int)XdmTypeCode.Base64Binary] = "base64Binary";
        names[(int)XdmTypeCode.AnyUri] = "anyURI";
        names[(int)XdmTypeCode.QName] = "QName";
        names[(int)XdmTypeCode.Notation] = "NOTATION";
        return names;
    }
}
