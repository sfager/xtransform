using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XsdDateTimeTests
{
    // -------------------------------------------------------------------------
    // xs:dateTime round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] DateTimeRoundTripCases =
    {
        // Basic
        new object[] { "2026-11-21T10:30:00",         "2026-11-21T10:30:00"         },
        // With Z timezone
        new object[] { "2026-11-21T10:30:00Z",        "2026-11-21T10:30:00Z"        },
        // With positive offset
        new object[] { "2026-11-21T10:30:00+05:30",   "2026-11-21T10:30:00+05:30"   },
        // With negative offset
        new object[] { "2026-11-21T10:30:00-08:00",   "2026-11-21T10:30:00-08:00"   },
        // Fractional seconds
        new object[] { "2000-01-01T00:00:00.5",       "2000-01-01T00:00:00.5"       },
        new object[] { "2000-01-01T12:00:00.123456789012345678", "2000-01-01T12:00:00.123456789012345678" },
        // 24:00:00 — end of day
        new object[] { "2000-01-01T24:00:00",         "2000-01-01T24:00:00"         },
        // Negative year (year < 0: proleptic Gregorian)
        new object[] { "-0001-01-01T00:00:00",        "-0001-01-01T00:00:00"        },
        // Year 0 — input "-0000" stores as year 0; canonical output uses "0000"
        new object[] { "-0000-06-15T12:00:00",        "0000-06-15T12:00:00"         },
        // Year > 9999
        new object[] { "10000-01-01T00:00:00",        "10000-01-01T00:00:00"        },
        // Seconds with zero-padded integer part
        new object[] { "2000-01-01T00:00:05.5Z",      "2000-01-01T00:00:05.5Z"      },
    };

    [TestCaseSource(nameof(DateTimeRoundTripCases))]
    public void DateTime_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.DateTime);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:date round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] DateRoundTripCases =
    {
        new object[] { "2026-11-21",        "2026-11-21"        },
        new object[] { "2026-11-21Z",       "2026-11-21Z"       },
        new object[] { "2026-11-21+05:30",  "2026-11-21+05:30"  },
        new object[] { "-0001-12-31",       "-0001-12-31"       },
        new object[] { "0001-01-01",        "0001-01-01"        },
    };

    [TestCaseSource(nameof(DateRoundTripCases))]
    public void Date_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.Date);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:time round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] TimeRoundTripCases =
    {
        new object[] { "10:30:00",         "10:30:00"         },
        new object[] { "10:30:00Z",        "10:30:00Z"        },
        new object[] { "10:30:00.5",       "10:30:00.5"       },
        new object[] { "24:00:00",         "24:00:00"         },
        new object[] { "00:00:00.123456",  "00:00:00.123456"  },
    };

    [TestCaseSource(nameof(TimeRoundTripCases))]
    public void Time_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.Time);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:gYearMonth round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] GYearMonthRoundTripCases =
    {
        new object[] { "2026-11",    "2026-11"    },
        new object[] { "2026-11Z",   "2026-11Z"   },
        new object[] { "-0001-01",   "-0001-01"   },
    };

    [TestCaseSource(nameof(GYearMonthRoundTripCases))]
    public void GYearMonth_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.GYearMonth);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:gYear round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] GYearRoundTripCases =
    {
        new object[] { "2026",    "2026"    },
        new object[] { "2026Z",   "2026Z"   },
        new object[] { "-0001",   "-0001"   },
    };

    [TestCaseSource(nameof(GYearRoundTripCases))]
    public void GYear_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.GYear);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:gMonthDay round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] GMonthDayRoundTripCases =
    {
        new object[] { "--11-21",    "--11-21"    },
        new object[] { "--11-21Z",   "--11-21Z"   },
        new object[] { "--02-29",    "--02-29"    },
    };

    [TestCaseSource(nameof(GMonthDayRoundTripCases))]
    public void GMonthDay_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.GMonthDay);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:gDay round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] GDayRoundTripCases =
    {
        new object[] { "---21",    "---21"    },
        new object[] { "---01Z",   "---01Z"   },
    };

    [TestCaseSource(nameof(GDayRoundTripCases))]
    public void GDay_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.GDay);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // xs:gMonth round-trip
    // -------------------------------------------------------------------------

    private static readonly object[][] GMonthRoundTripCases =
    {
        new object[] { "--11",    "--11"    },
        new object[] { "--11Z",   "--11Z"   },
        new object[] { "--01",    "--01"    },
    };

    [TestCaseSource(nameof(GMonthRoundTripCases))]
    public void GMonth_Parse_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDateTime value = XsdDateTime.Parse(input, XsdDateTimeKind.GMonth);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // Property accessors (xs:dateTime)
    // -------------------------------------------------------------------------

    [Test]
    public void Properties_DateTimeParsed_AllComponentsCorrect()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2026-11-21T10:30:45.5+05:30", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.Kind, Is.EqualTo(XsdDateTimeKind.DateTime));
        Assert.That(dt.Year, Is.EqualTo(2026L));
        Assert.That(dt.Month, Is.EqualTo(11));
        Assert.That(dt.Day, Is.EqualTo(21));
        Assert.That(dt.Hour, Is.EqualTo(10));
        Assert.That(dt.Minute, Is.EqualTo(30));
        Assert.That((decimal)dt.Second, Is.EqualTo(45.5m));
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(330));
        Assert.That(dt.HasTimezone, Is.True);
    }

    [Test]
    public void Properties_NoTimezone_HasTimezoneIsFalse()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2026-01-01T00:00:00", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.HasTimezone, Is.False);
        Assert.That(dt.TimezoneMinutes, Is.Null);
    }

    [Test]
    public void Properties_ZTimezone_TimezoneMinutesIsZero()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2026-01-01T00:00:00Z", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.HasTimezone, Is.True);
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(0));
    }

    [Test]
    public void Properties_NegativeTimezone_TimezoneMinutesIsNegative()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2026-01-01T00:00:00-14:00", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.TimezoneMinutes, Is.EqualTo(-840));
    }

    // -------------------------------------------------------------------------
    // Parse error cases
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_EmptyString_ThrowsFORG0001()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => XsdDateTime.Parse("", XsdDateTimeKind.DateTime))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [TestCase("not-a-date",           XsdDateTimeKind.DateTime)]
    [TestCase("2026-13-01T00:00:00",  XsdDateTimeKind.DateTime)]  // month 13
    [TestCase("2026-11-21",           XsdDateTimeKind.DateTime)]  // date without time
    [TestCase("2026-11-32",           XsdDateTimeKind.Date)]      // day 32
    [TestCase("25:00:00",             XsdDateTimeKind.Time)]      // hour 25
    [TestCase("10:60:00",             XsdDateTimeKind.Time)]      // minute 60
    [TestCase("26-11",                XsdDateTimeKind.GYearMonth)] // only 2 year digits — needs at least 4
    [TestCase("-11-21",               XsdDateTimeKind.GMonthDay)]  // missing leading --
    [TestCase("--11-21",              XsdDateTimeKind.GDay)]       // wrong prefix for gDay
    public void Parse_InvalidInput_ThrowsFORG0001(string input, XsdDateTimeKind kind)
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => XsdDateTime.Parse(input, kind))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // TryParse
    // -------------------------------------------------------------------------

    [Test]
    public void TryParse_ValidDateTime_ReturnsTrue()
    {
        // Arrange / Act
        bool ok = XsdDateTime.TryParse("2026-01-01T00:00:00", XsdDateTimeKind.DateTime, out _);

        // Assert
        Assert.That(ok, Is.True);
    }

    [Test]
    public void TryParse_InvalidDateTime_ReturnsFalse()
    {
        // Arrange / Act
        bool ok = XsdDateTime.TryParse("not-valid", XsdDateTimeKind.DateTime, out _);

        // Assert
        Assert.That(ok, Is.False);
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Test]
    public void Equals_SameDateTime_ReturnsTrue()
    {
        // Arrange
        XsdDateTime a = XsdDateTime.Parse("2026-11-21T10:30:00Z", XsdDateTimeKind.DateTime);
        XsdDateTime b = XsdDateTime.Parse("2026-11-21T10:30:00Z", XsdDateTimeKind.DateTime);

        // Act / Assert
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equals_DifferentTimezone_ReturnsFalse()
    {
        // Arrange
        XsdDateTime a = XsdDateTime.Parse("2026-11-21T10:30:00Z", XsdDateTimeKind.DateTime);
        XsdDateTime b = XsdDateTime.Parse("2026-11-21T10:30:00",  XsdDateTimeKind.DateTime);

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void GetHashCode_EqualDateTimes_SameHash()
    {
        // Arrange
        XsdDateTime a = XsdDateTime.Parse("2026-11-21T10:30:00Z", XsdDateTimeKind.DateTime);
        XsdDateTime b = XsdDateTime.Parse("2026-11-21T10:30:00Z", XsdDateTimeKind.DateTime);

        // Act / Assert
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    // -------------------------------------------------------------------------
    // Edge: 24:00:00 round-trip
    // -------------------------------------------------------------------------

    [Test]
    public void DateTime_24Hour_RoundTrips()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2000-01-01T24:00:00", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.Hour, Is.EqualTo(24));
        Assert.That(dt.Minute, Is.EqualTo(0));
        Assert.That((decimal)dt.Second, Is.EqualTo(0m));
        Assert.That(dt.ToString(), Is.EqualTo("2000-01-01T24:00:00"));
    }

    // -------------------------------------------------------------------------
    // Edge: year with more than 4 digits
    // -------------------------------------------------------------------------

    [Test]
    public void DateTime_FiveDigitYear_RoundTrips()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("12345-06-07T08:09:10", XsdDateTimeKind.DateTime);

        // Assert
        Assert.That(dt.Year, Is.EqualTo(12345L));
        Assert.That(dt.ToString(), Is.EqualTo("12345-06-07T08:09:10"));
    }

    // -------------------------------------------------------------------------
    // Edge: year 0 (proleptic) — both "-0000" and "0000" parse to year 0
    // -------------------------------------------------------------------------

    [Test]
    public void GYear_YearZero_ParsesAndProducesCanonicalForm()
    {
        // Arrange — "-0000" is a valid lexical form (year 0 with sign); canonical output is "0000"
        XsdDateTime dtNeg = XsdDateTime.Parse("-0000", XsdDateTimeKind.GYear);
        XsdDateTime dtPos = XsdDateTime.Parse("0000",  XsdDateTimeKind.GYear);

        // Act
        string sNeg = dtNeg.ToString();
        string sPos = dtPos.ToString();

        // Assert — both parse to year 0; output "0000"
        Assert.That(dtNeg.Year, Is.EqualTo(0L));
        Assert.That(dtPos.Year, Is.EqualTo(0L));
        Assert.That(sNeg, Is.EqualTo("0000"));
        Assert.That(sPos, Is.EqualTo("0000"));
    }

    // -------------------------------------------------------------------------
    // Edge: seconds padding — "05.5" not "5.5"
    // -------------------------------------------------------------------------

    [Test]
    public void DateTime_SecondsBelowTen_AreZeroPadded()
    {
        // Arrange / Act
        XsdDateTime dt = XsdDateTime.Parse("2000-01-01T00:00:05.5Z", XsdDateTimeKind.DateTime);
        string s = dt.ToString();

        // Assert — integer part of seconds must be padded to 2 digits
        Assert.That(s, Does.Contain(":05.5Z"));
    }
}
