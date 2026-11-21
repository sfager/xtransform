using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XsdDecimalTests
{
    // -------------------------------------------------------------------------
    // Round-trip: lexical → canonical
    // -------------------------------------------------------------------------

    // Format: input | expectedCanonical
    private static readonly object[][] RoundTripCases =
    {
        new object[] { "0",           "0"      },
        new object[] { "-0",          "0"      },
        new object[] { "0.0",         "0"      },
        new object[] { "1",           "1"      },
        new object[] { "-1",          "-1"     },
        new object[] { "+1",          "1"      },
        new object[] { "3.14",        "3.14"   },
        new object[] { "+1.5",        "1.5"    },
        new object[] { "-1.5",        "-1.5"   },
        new object[] { "03.14",       "3.14"   },
        new object[] { "1.500",       "1.5"    },
        new object[] { "3.",          "3"      },
        new object[] { "0.001",       "0.001"  },
        new object[] { "100.00",      "100"    },
        new object[] { "1234567890",  "1234567890" },
        new object[] { "0.1",         "0.1"    },
        new object[] { "-0.001",      "-0.001" },
        new object[] { "9999999.9999999", "9999999.9999999" },
        new object[] { "1.0000000",   "1"      },
        new object[] { "-3.",         "-3"     },
    };

    [TestCaseSource(nameof(RoundTripCases))]
    public void Parse_ValidInput_ProducesCanonicalToString(string input, string expected)
    {
        // Arrange / Act
        XsdDecimal value = XsdDecimal.Parse(input);
        string actual = value.ToString();

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    // -------------------------------------------------------------------------
    // TryParse success / failure
    // -------------------------------------------------------------------------

    [TestCase("42")]
    [TestCase("-42")]
    [TestCase("3.14")]
    [TestCase("+0")]
    public void TryParse_ValidInput_ReturnsTrue(string input)
    {
        // Arrange / Act
        bool ok = XsdDecimal.TryParse(input, out _);

        // Assert
        Assert.That(ok, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1e5")]
    [TestCase("1E5")]
    [TestCase("abc")]
    [TestCase("1.2.3")]
    [TestCase("+")]
    [TestCase("-")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        // Arrange / Act
        bool ok = XsdDecimal.TryParse(input, out _);

        // Assert
        Assert.That(ok, Is.False);
    }

    // -------------------------------------------------------------------------
    // Parse throws FORG0001
    // -------------------------------------------------------------------------

    [TestCase("1e5")]
    [TestCase("not-a-number")]
    [TestCase("")]
    public void Parse_InvalidInput_ThrowsFORG0001(string input)
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() => XsdDecimal.Parse(input))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    // -------------------------------------------------------------------------
    // Value property
    // -------------------------------------------------------------------------

    [Test]
    public void Value_AfterParse_ReturnsUnderlyingDecimal()
    {
        // Arrange
        XsdDecimal d = XsdDecimal.Parse("3.14");

        // Act
        decimal v = d.Value;

        // Assert
        Assert.That(v, Is.EqualTo(3.14m));
    }

    // -------------------------------------------------------------------------
    // Equality and hashing
    // -------------------------------------------------------------------------

    [Test]
    public void Equals_SameValue_ReturnsTrue()
    {
        // Arrange
        XsdDecimal a = XsdDecimal.Parse("1.5");
        XsdDecimal b = new XsdDecimal(1.5m);

        // Act / Assert
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        // Arrange
        XsdDecimal a = XsdDecimal.Parse("1.5");
        XsdDecimal b = XsdDecimal.Parse("2.5");

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void GetHashCode_EqualValues_SameHash()
    {
        // Arrange
        XsdDecimal a = XsdDecimal.Parse("01.50");
        XsdDecimal b = new XsdDecimal(1.5m);

        // Act / Assert
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    // -------------------------------------------------------------------------
    // Comparison / ordering
    // -------------------------------------------------------------------------

    [TestCase("1.0", "2.0", -1)]
    [TestCase("2.0", "1.0",  1)]
    [TestCase("1.5", "1.5",  0)]
    [TestCase("-1",  "0",   -1)]
    [TestCase("0",   "0.0",  0)]
    public void CompareTo_ReturnsExpectedSign(string leftStr, string rightStr, int expectedSign)
    {
        // Arrange
        XsdDecimal l = XsdDecimal.Parse(leftStr);
        XsdDecimal r = XsdDecimal.Parse(rightStr);

        // Act
        int cmp = l.CompareTo(r);

        // Assert
        int actual = cmp < 0 ? -1 : cmp > 0 ? 1 : 0;
        Assert.That(actual, Is.EqualTo(expectedSign));
    }

    [Test]
    public void Operators_LessThanGreaterThan_WorkCorrectly()
    {
        // Arrange
        XsdDecimal small = new XsdDecimal(1m);
        XsdDecimal large = new XsdDecimal(2m);

        // Act / Assert
        XsdDecimal alsoSmall = new XsdDecimal(1m);
        XsdDecimal alsoLarge = new XsdDecimal(2m);
        Assert.That(small < large, Is.True);
        Assert.That(large > small, Is.True);
        Assert.That(small <= alsoSmall, Is.True);   // 1 <= 1
        Assert.That(large >= alsoLarge, Is.True);   // 2 >= 2
    }

    // -------------------------------------------------------------------------
    // Conversion operators
    // -------------------------------------------------------------------------

    [Test]
    public void ImplicitConversion_FromDecimal_Works()
    {
        // Arrange / Act
        XsdDecimal d = 3.14m;

        // Assert
        Assert.That(d.Value, Is.EqualTo(3.14m));
    }

    [Test]
    public void ExplicitConversion_ToDecimal_Works()
    {
        // Arrange
        XsdDecimal d = new XsdDecimal(99.9m);

        // Act
        decimal v = (decimal)d;

        // Assert
        Assert.That(v, Is.EqualTo(99.9m));
    }

    // -------------------------------------------------------------------------
    // Object.Equals boxed
    // -------------------------------------------------------------------------

    [Test]
    public void Equals_BoxedObject_WorksCorrectly()
    {
        // Arrange
        XsdDecimal a = new XsdDecimal(5m);
        object boxed = new XsdDecimal(5m);

        // Act / Assert
        Assert.That(a.Equals(boxed), Is.True);
    }

    [Test]
    public void Equals_NonXsdDecimalObject_ReturnsFalse()
    {
        // Arrange
        XsdDecimal a = new XsdDecimal(5m);

        // Act / Assert
        Assert.That(a.Equals("5"), Is.False);
        Assert.That(a.Equals(null), Is.False);
    }
}
