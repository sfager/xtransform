using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for <see cref="StylesheetProcessor"/> — the post-reader pass that evaluates use-when,
/// parses AVTs, and strips whitespace (XSLT 2.0 §3.12 / §5.6.1 / §4.4).
/// </summary>
[TestFixture]
internal sealed class StylesheetProcessorTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── use-when ─────────────────────────────────────────────────────────────

    [Test]
    public void Process_UseWhenFalse_RemovesElement()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/' use-when='false()'/>" +
            "<xsl:template match='b'/>" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert — only the second template survives.
        var remaining = processed.Root.Children.OfType<StylesheetElement>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(remaining.Count, Is.EqualTo(1));
            Assert.That(remaining[0].Attributes[0].Value, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Process_UseWhenTrueViaSystemProperty_KeepsElement()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/' use-when=\"system-property('xsl:version') = '2.0'\"/>" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        Assert.That(processed.Root.Children.OfType<StylesheetElement>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void Process_UseWhenReferencesUserVariable_RaisesXpst0008()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/' use-when=\"$x\"/>" +
            "</xsl:stylesheet>");

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => StylesheetProcessor.Process(module))!;
        Assert.That(ex.Errors.Any(d => d.Code.LocalName == "XPST0008"), Is.True);
    }

    [Test]
    public void Process_XslUseWhenOnLiteralResultElement_EvaluatedUnderXsltNamespace()
    {
        // Arrange — on an LRE, the attribute must be xsl:use-when.
        StylesheetModule module = Read(
            $"<html xsl:version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<p xsl:use-when='false()'/>" +
            "<p xsl:use-when='true()'/>" +
            "</html>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert — only the second <p> remains.
        Assert.That(processed.Root.Children.OfType<StylesheetElement>().Count(), Is.EqualTo(1));
    }

    // ── AVT parsing ─────────────────────────────────────────────────────────

    [Test]
    public void Process_HrefOnXslResultDocument_ParsesAsAvt()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'>" +
            "<xsl:result-document href='{$base}/{$path}'/>" +
            "</xsl:template>" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        StylesheetElement rd = (StylesheetElement)processed.Root
            .Children.OfType<StylesheetElement>().First() // template
            .Children.OfType<StylesheetElement>().First(); // result-document
        AttributeValueTemplate avt = rd.Attributes.Single().ParsedTemplate!;
        Assert.That(avt.Parts.Count, Is.EqualTo(3));
    }

    [Test]
    public void Process_SelectOnXslValueOf_NotParsedAsAvt()
    {
        // Arrange — select is an XPath expression, not an AVT; { would be an XTSE0370 if parsed.
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'><xsl:value-of select='.'/></xsl:template>" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        StylesheetElement vo = (StylesheetElement)processed.Root
            .Children.OfType<StylesheetElement>().First()
            .Children.OfType<StylesheetElement>().First();
        StylesheetAttribute selectAttr = vo.Attributes.Single(a => a.Name.LocalName == "select");
        Assert.That(selectAttr.ParsedTemplate, Is.Null);
    }

    [Test]
    public void Process_LiteralResultElementAttributes_AllParsedAsAvts()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<html xsl:version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<a href='x'/>" +
            "</html>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        StylesheetElement anchor = processed.Root.Children.OfType<StylesheetElement>().Single();
        Assert.That(anchor.Attributes.Single().ParsedTemplate, Is.Not.Null);
    }

    // ── Whitespace stripping ─────────────────────────────────────────────────

    [Test]
    public void Process_WhitespaceBetweenInstructions_Stripped()
    {
        // Arrange
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>\n" +
            "  <xsl:template match='/'/>\n" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        Assert.That(processed.Root.Children.OfType<StylesheetText>(), Is.Empty);
    }

    [Test]
    public void Process_WhitespaceInsideXslText_Preserved()
    {
        // Arrange — xsl:text content is preserved even when it's whitespace-only.
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'><xsl:text>   </xsl:text></xsl:template>" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        StylesheetElement text = (StylesheetElement)processed.Root
            .Children.OfType<StylesheetElement>().First()
            .Children.OfType<StylesheetElement>().First();
        Assert.That(text.Children.OfType<StylesheetText>().Single().Text, Is.EqualTo("   "));
    }

    [Test]
    public void Process_XmlSpacePreserveOnAncestor_PreservesChildWhitespace()
    {
        // Arrange — xml:space on the stylesheet root preserves ws-only text throughout.
        StylesheetModule module = Read(
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xml:space='preserve'>\n" +
            "  <xsl:template match='/'/>\n" +
            "</xsl:stylesheet>");

        // Act
        StylesheetModule processed = StylesheetProcessor.Process(module);

        // Assert
        Assert.That(processed.Root.Children.OfType<StylesheetText>().Any(), Is.True);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static StylesheetModule Read(string xml)
    {
        using StringReader sr = new StringReader(xml);
        return StylesheetReader.Read(sr, new Uri("file:///inline.xsl"));
    }
}
