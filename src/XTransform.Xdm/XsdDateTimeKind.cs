namespace XTransform.Xdm;

// XSD 1.0 Part 2 §3.2.7–3.2.14 — the eight date/time primitive types
/// <summary>
/// Identifies which of the eight XSD date/time primitive types an <see cref="XsdDateTime"/> represents.
/// </summary>
public enum XsdDateTimeKind : byte
{
    /// <summary>xs:dateTime — year, month, day, hour, minute, second, optional timezone.</summary>
    DateTime = 0,

    /// <summary>xs:date — year, month, day, optional timezone.</summary>
    Date = 1,

    /// <summary>xs:time — hour, minute, second, optional timezone.</summary>
    Time = 2,

    /// <summary>xs:gYearMonth — year and month, optional timezone.</summary>
    GYearMonth = 3,

    /// <summary>xs:gYear — year only, optional timezone.</summary>
    GYear = 4,

    /// <summary>xs:gMonthDay — month and day, optional timezone.</summary>
    GMonthDay = 5,

    /// <summary>xs:gDay — day only, optional timezone.</summary>
    GDay = 6,

    /// <summary>xs:gMonth — month only, optional timezone.</summary>
    GMonth = 7,
}
