using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XsdDurationTests
{
    // -------------------------------------------------------------------------
    // Round-trip: lexical → canonical
    // -------------------------------------------------------------------------

    private static readonly object[][] RoundTripCases =
    {
        // Zero
        new object[] { "PT0S",           "PT0S"          },
        new object[] { "-PT0S",          "PT0S"          },
        // Year only
        new object[] { "P1Y",            "P1Y"           },
        new object[] { "P2Y",            "P2Y"           },
        // Month only
        new object[] { "P6M",            "P6M"           },
        // Year and month
        new object[] { "P1Y6M",          "P1Y6M"         },
        // 13 months → 1Y1M
        new object[] { "P13M",           "P1Y1M"         },
        // Day only
        new object[] { "P10D",           "P10D"          },
        // Combined date and time
        new object[] { "P1Y2M3DT4H5M6S", "P1Y2M3DT4H5M6S" },
        // Time part only
        new object[] { "PT1H",           "PT1H"          },
        new object[] { "PT30M",          "PT30M"         },
        new object[] { "PT90S",          "PT1M30S"       },
        // Fractional seconds
        new object[] { "PT1.5S",         "PT1.5S"        },
        new object[] { "PT0.001S",       "PT0.001S"      },
        // Negative
        new object[] { "-P1Y",           "-P1Y"          },
        new object[] { "-P1Y2M3DT4H5M6S", "-P1Y2M3DT4H5M6S" },
        // Large seconds that decompose into hours/minutes
        new object[] { "PT3661S",        "PT1H1M1S"      },
        // Days that decompose
        new object[] { "P1DT0H",         "P1D"           },
    };

    [TestCaseSource(nameof(RoundTripCases))]
    public void Parse_ValidInput_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDuration value = XsdDuration.Parse(input);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // Component accessors
    // -------------------------------------------------------------------------

    [Test]
    public void Months_P1Y6M_ReturnsEighteen()
    {
        // Arrange / Act
        XsdDuration d = XsdDuration.Parse("P1Y6M");

        // Assert — 1 year = 12 months, plus 6 = 18
        Assert.That(d.Months, Is.EqualTo(18));
    }

    [Test]
    public void Seconds_PT1H30M_Returns5400()
    {
        // Arrange / Act
        XsdDuration d = XsdDuration.Parse("PT1H30M");

        // Assert — 1*3600 + 30*60 = 5400
        Assert.That((decimal)d.Seconds, Is.EqualTo(5400m));
    }

    [Test]
    public void Seconds_P1D_Returns86400()
    {
        // Arrange / Act
        XsdDuration d = XsdDuration.Parse("P1D");

        // Assert
        Assert.That((decimal)d.Seconds, Is.EqualTo(86400m));
    }

    // -------------------------------------------------------------------------
    // Parse error cases
    // -------------------------------------------------------------------------

    [TestCase("")]
    [TestCase("1Y")]          // missing leading P
    [TestCase("PT")]          // T with no component
    [TestCase("P")]           // no component at all
    [TestCase("P1Y2M3D4H")]   // 4H in date part
    [TestCase("PXY")]         // non-digit before designator
    [TestCase("P1.5Y")]       // decimal not allowed for year
    public void Parse_InvalidInput_ThrowsFORG0001(string input)
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() => XsdDuration.Parse(input))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // TryParse
    // -------------------------------------------------------------------------

    [TestCase("P1Y")]
    [TestCase("PT1S")]
    [TestCase("P0D")]   // "P0D" is valid lexically though canonical is PT0S
    public void TryParse_ValidInput_ReturnsTrue(string input)
    {
        // Arrange / Act
        bool ok = XsdDuration.TryParse(input, out _);

        // Assert
        Assert.That(ok, Is.True);
    }

    [TestCase("")]
    [TestCase("PT")]
    [TestCase("garbage")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        // Arrange / Act
        bool ok = XsdDuration.TryParse(input, out _);

        // Assert
        Assert.That(ok, Is.False);
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Test]
    public void Equals_SameDuration_ReturnsTrue()
    {
        // Arrange
        XsdDuration a = XsdDuration.Parse("P1Y6M");
        XsdDuration b = new XsdDuration(18, new XsdDecimal(0m));

        // Act / Assert
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equals_DifferentDuration_ReturnsFalse()
    {
        // Arrange
        XsdDuration a = XsdDuration.Parse("P1Y");
        XsdDuration b = XsdDuration.Parse("P2Y");

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void GetHashCode_EqualDurations_SameHash()
    {
        // Arrange
        XsdDuration a = XsdDuration.Parse("PT3600S");
        XsdDuration b = XsdDuration.Parse("PT1H");

        // Act / Assert
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    // -------------------------------------------------------------------------
    // Object.Equals boxed
    // -------------------------------------------------------------------------

    [Test]
    public void Equals_BoxedObject_WorksCorrectly()
    {
        // Arrange
        XsdDuration a = XsdDuration.Parse("P1Y");
        object boxed = XsdDuration.Parse("P1Y");

        // Act / Assert
        Assert.That(a.Equals(boxed), Is.True);
    }
}
