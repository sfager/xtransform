using System;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class QNameTests
{
    // --- Constructor and property tests ---

    [Test]
    public void Constructor_LocalOnly_SetsEmptyNamespaceAndPrefix()
    {
        // Arrange / Act
        var q = new QName("local");

        // Assert
        Assert.That(q.LocalName, Is.EqualTo("local"));
        Assert.That(q.NamespaceUri, Is.EqualTo(""));
        Assert.That(q.Prefix, Is.EqualTo(""));
    }

    [Test]
    public void Constructor_UriAndLocal_SetsEmptyPrefix()
    {
        // Arrange / Act
        var q = new QName("http://example.com", "local");

        // Assert
        Assert.That(q.NamespaceUri, Is.EqualTo("http://example.com"));
        Assert.That(q.LocalName, Is.EqualTo("local"));
        Assert.That(q.Prefix, Is.EqualTo(""));
    }

    [Test]
    public void Constructor_PrefixUriLocal_SetsAllProperties()
    {
        // Arrange / Act
        var q = new QName("ex", "http://example.com", "local");

        // Assert
        Assert.That(q.Prefix, Is.EqualTo("ex"));
        Assert.That(q.NamespaceUri, Is.EqualTo("http://example.com"));
        Assert.That(q.LocalName, Is.EqualTo("local"));
    }

    [TestCase("localName", null, typeof(ArgumentNullException))]
    [TestCase(null, "localName", typeof(ArgumentNullException))]
    public void Constructor_NullArgument_ThrowsArgumentNullException(string? prefix, string? local, Type exType)
    {
        // Arrange / Act / Assert
        Assert.Throws(exType, () =>
        {
            if (prefix is null)
            {
                _ = new QName(null!, "uri", "local");
            }
            else
            {
                _ = new QName("prefix", "uri", null!);
            }
        });
    }

    [Test]
    public void Constructor_NullNamespaceUri_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => _ = new QName("prefix", null!, "local"));
    }

    // --- ClarkName ---

    [TestCase("http://example.com", "local", "{http://example.com}local")]
    [TestCase("", "local", "local")]
    public void ClarkName_ReturnsExpectedFormat(string uri, string local, string expected)
    {
        // Arrange
        var q = new QName(uri, local);

        // Act / Assert
        Assert.That(q.ClarkName, Is.EqualTo(expected));
    }

    // --- ParseClark ---

    [TestCase("{http://example.com}local", "http://example.com", "local")]
    [TestCase("local", "", "local")]
    public void ParseClark_ValidInput_RoundTrips(string clark, string expectedUri, string expectedLocal)
    {
        // Arrange / Act
        var q = QName.ParseClark(clark);

        // Assert
        Assert.That(q.NamespaceUri, Is.EqualTo(expectedUri));
        Assert.That(q.LocalName, Is.EqualTo(expectedLocal));
        Assert.That(q.ClarkName, Is.EqualTo(clark));
    }

    [Test]
    public void ParseClark_NullInput_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => QName.ParseClark(null!));
    }

    [Test]
    public void ParseClark_MissingCloseBrace_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => QName.ParseClark("{no-close"));
    }

    // --- Equality ---

    [Test]
    public void Equals_SameUriAndLocal_ReturnsTrue()
    {
        // Arrange
        var a = new QName("px", "http://example.com", "foo");
        var b = new QName("other", "http://example.com", "foo");

        // Act / Assert
        Assert.That(a.Equals(b), Is.True);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void Equals_DifferentLocal_ReturnsFalse()
    {
        // Arrange
        var a = new QName("http://example.com", "foo");
        var b = new QName("http://example.com", "bar");

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void Equals_DifferentUri_ReturnsFalse()
    {
        // Arrange
        var a = new QName("http://a.com", "foo");
        var b = new QName("http://b.com", "foo");

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void GetHashCode_EqualQNames_SameHash()
    {
        // Arrange
        var a = new QName("px", "http://example.com", "foo");
        var b = new QName("other", "http://example.com", "foo");

        // Act / Assert
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    // --- ToString ---

    [TestCase("ex", "http://example.com", "local", "ex:local")]
    [TestCase("", "http://example.com", "local", "local")]
    public void ToString_FormatsCorrectly(string prefix, string uri, string local, string expected)
    {
        // Arrange
        var q = new QName(prefix, uri, local);

        // Act / Assert
        Assert.That(q.ToString(), Is.EqualTo(expected));
    }
}
