using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class SourceSpanTests
{
    // --- None ---

    [Test]
    public void None_ToString_ReturnsUnknown()
    {
        // Arrange / Act / Assert
        Assert.That(SourceSpan.None.ToString(), Is.EqualTo("(unknown)"));
    }

    [Test]
    public void None_Properties_AreDefaults()
    {
        // Arrange
        var span = SourceSpan.None;

        // Act / Assert
        Assert.That(span.ModuleUri, Is.Null);
        Assert.That(span.Line, Is.EqualTo(0));
        Assert.That(span.Column, Is.EqualTo(0));
        Assert.That(span.Start, Is.EqualTo(0));
        Assert.That(span.Length, Is.EqualTo(0));
    }

    // --- ToString ---

    [TestCase("file:///styles/style.xsl", 12, 5, "style.xsl(12,5)")]
    [TestCase("style.xsl", 1, 1, "style.xsl(1,1)")]
    [TestCase("http://example.com/transforms/main.xsl", 100, 3, "main.xsl(100,3)")]
    public void ToString_KnownLocation_FormatsFilenameAndCoords(
        string moduleUri, int line, int col, string expected)
    {
        // Arrange
        var span = new SourceSpan(moduleUri, line, col);

        // Act / Assert
        Assert.That(span.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void ToString_NullModuleUri_ReturnsUnknown()
    {
        // Arrange
        var span = new SourceSpan(null, 5, 3);

        // Act / Assert
        Assert.That(span.ToString(), Is.EqualTo("(unknown)"));
    }

    [Test]
    public void ToString_ZeroLine_ReturnsUnknown()
    {
        // Arrange
        var span = new SourceSpan("style.xsl", 0, 0);

        // Act / Assert
        Assert.That(span.ToString(), Is.EqualTo("(unknown)"));
    }

    // --- WithRange ---

    [Test]
    public void WithRange_ReplacesStartAndLength_PreservesOtherFields()
    {
        // Arrange
        var original = new SourceSpan("style.xsl", 10, 4, start: 0, length: 0);

        // Act
        var updated = original.WithRange(5, 12);

        // Assert
        Assert.That(updated.ModuleUri, Is.EqualTo("style.xsl"));
        Assert.That(updated.Line, Is.EqualTo(10));
        Assert.That(updated.Column, Is.EqualTo(4));
        Assert.That(updated.Start, Is.EqualTo(5));
        Assert.That(updated.Length, Is.EqualTo(12));
    }
}
