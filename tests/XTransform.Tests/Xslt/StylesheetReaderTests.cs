using System;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Table-driven tests for <see cref="StylesheetReader"/> (XSLT 2.0 §3.6 / §3.7).
/// </summary>
[TestFixture]
internal sealed class StylesheetReaderTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";
    private const string XmlNs = "http://www.w3.org/XML/1998/namespace";

    // ── Minimal shapes ────────────────────────────────────────────────────

    [Test]
    public void Read_EmptyStylesheet_ReadsRoot()
    {
        // Arrange
        string xml = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'/>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(module.Root.Name.LocalName, Is.EqualTo("stylesheet"));
            Assert.That(module.Root.Name.NamespaceUri, Is.EqualTo(XsltNs));
            Assert.That(module.Root.Children, Is.Empty);
            Assert.That(module.DeclaredVersion, Is.EqualTo("2.0"));
            Assert.That(module.IsFullStylesheet, Is.True);
        });
    }

    [Test]
    public void Read_TransformSynonym_IsFullStylesheet()
    {
        // Arrange
        string xml = $"<xsl:transform version='2.0' xmlns:xsl='{XsltNs}'/>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Assert.That(module.IsFullStylesheet, Is.True);
    }

    [Test]
    public void Read_SimplifiedStylesheet_LiteralRoot()
    {
        // Arrange — XSLT 2.0 §3.7 simplified stylesheet uses a literal result element as root.
        string xml = $"<html xsl:version='2.0' xmlns:xsl='{XsltNs}'><body/></html>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(module.Root.Name.LocalName, Is.EqualTo("html"));
            Assert.That(module.Root.Name.NamespaceUri, Is.Empty);
            Assert.That(module.Root.DeclaredVersion, Is.EqualTo("2.0"));
            Assert.That(module.IsFullStylesheet, Is.False);
        });
    }

    // ── Source spans / line info ──────────────────────────────────────────

    [Test]
    public void Read_ElementLocation_RecordsLineAndColumn()
    {
        // Arrange — the <xsl:template> is on line 2, position 3.
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>\n" +
            "  <xsl:template match='/'/>\n" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children.OfType<StylesheetElement>().First();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(template.Location.Line, Is.EqualTo(2));
            Assert.That(template.Location.Column, Is.EqualTo(4)); // XmlReader reports post-'<' column
        });
    }

    // ── Namespace bindings ────────────────────────────────────────────────

    [Test]
    public void Read_InScopeNamespaces_IncludesImplicitXml()
    {
        // Arrange
        string xml = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'/>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Assert.That(module.Root.InScopeNamespaces.Resolve("xml"), Is.EqualTo(XmlNs));
    }

    [Test]
    public void Read_InScopeNamespaces_InheritFromParent()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xmlns:my='urn:my'>" +
            "<xsl:template match='/'/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(template.InScopeNamespaces.Resolve("my"), Is.EqualTo("urn:my"));
            Assert.That(template.InScopeNamespaces.Resolve("xsl"), Is.EqualTo(XsltNs));
        });
    }

    [Test]
    public void Read_InScopeNamespaces_ChildOverridesParent()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xmlns:my='urn:a'>" +
            "<xsl:template match='/' xmlns:my='urn:b'/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];

        // Assert
        Assert.That(template.InScopeNamespaces.Resolve("my"), Is.EqualTo("urn:b"));
    }

    [Test]
    public void Read_DefaultNamespace_BoundOnEmptyPrefix()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xmlns='urn:default'>" +
            "<ext/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement ext = (StylesheetElement)module.Root.Children[0];

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ext.InScopeNamespaces.Resolve(""), Is.EqualTo("urn:default"));
            Assert.That(ext.Name.NamespaceUri, Is.EqualTo("urn:default"));
        });
    }

    // ── Attributes ────────────────────────────────────────────────────────

    [Test]
    public void Read_Attributes_NamespaceDeclarationsExcluded()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xmlns:my='urn:x'/>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert — only 'version' remains; xmlns:xsl / xmlns:my filtered out.
        Assert.That(module.Root.Attributes.Count, Is.EqualTo(1));
        Assert.That(module.Root.Attributes[0].Name.LocalName, Is.EqualTo("version"));
    }

    [Test]
    public void Read_Attributes_PrefixedResolvesToNamespaceUri()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/' xml:space='preserve'/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];
        StylesheetAttribute xmlSpace = template.Attributes
            .Single(a => string.Equals(a.Name.LocalName, "space", StringComparison.Ordinal));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(xmlSpace.Name.NamespaceUri, Is.EqualTo(XmlNs));
            Assert.That(xmlSpace.Value, Is.EqualTo("preserve"));
        });
    }

    // ── Text content ──────────────────────────────────────────────────────

    [Test]
    public void Read_TextBetweenElements_PreservedAsText()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'>hello</xsl:template>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];

        // Assert
        Assert.That(template.Children.OfType<StylesheetText>().Single().Text, Is.EqualTo("hello"));
    }

    [Test]
    public void Read_WhitespaceOnlyText_FlaggedAsWhitespace()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>\n" +
            "  <xsl:template match='/'/>\n" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetText? first = module.Root.Children.OfType<StylesheetText>().FirstOrDefault();

        // Assert
        Assert.That(first, Is.Not.Null);
        Assert.That(first!.IsWhitespaceOnly, Is.True);
    }

    [Test]
    public void Read_CdataSection_PreservedAsText()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'><![CDATA[<raw>]]></xsl:template>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];

        // Assert
        Assert.That(template.Children.OfType<StylesheetText>().Single().Text, Is.EqualTo("<raw>"));
    }

    [Test]
    public void Read_CommentsAndPIs_Discarded()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<!-- comment --><?target data?>" +
            "<xsl:template match='/'/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert — only one child, the template; comment and PI are gone.
        Assert.That(module.Root.Children.OfType<StylesheetElement>().Count(), Is.EqualTo(1));
        Assert.That(module.Root.Children.OfType<StylesheetText>(), Is.Empty);
    }

    // ── Foreign-namespace children ────────────────────────────────────────

    [Test]
    public void Read_ForeignNamespaceTopLevel_Accepted()
    {
        // Arrange — xsl:stylesheet may contain elements from other namespaces (XSLT 2.0 §3.6).
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}' xmlns:ext='urn:ext'>" +
            "<ext:config/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert — no errors, child kept.
        Assert.Multiple(() =>
        {
            Assert.That(module.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
            Assert.That(module.Root.Children.OfType<StylesheetElement>().Single().Name.NamespaceUri,
                Is.EqualTo("urn:ext"));
        });
    }

    [Test]
    public void Read_NullNamespaceTopLevelChild_ThrowsXtse0130()
    {
        // Arrange — a top-level element in no namespace is XTSE0130.
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<badroot/>" +
            "</xsl:stylesheet>";

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => ReadInline(xml))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0130));
    }

    // ── Unknown xsl:* behaviour ──────────────────────────────────────────

    [Test]
    public void Read_UnknownXsltElement_UnderVersion2_ThrowsXtse0010()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:not-a-real-instruction/>" +
            "</xsl:stylesheet>";

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => ReadInline(xml))!;
        Assert.That(ex.Errors.Any(e => e.Code == XsltErrors.XTSE0010), Is.True);
    }

    [Test]
    public void Read_UnknownXsltElement_UnderVersion3_WarnsXtse0150()
    {
        // Arrange — a version above 2.0 triggers forwards-compatible mode.
        string xml =
            $"<xsl:stylesheet version='3.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:not-a-real-instruction/>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Diagnostic warn = module.Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(warn.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(warn.Code, Is.EqualTo(XsltErrors.XTSE0150));
        });
    }

    [TestCase("0.5")]
    [TestCase("1.0")]
    [TestCase("2.0")]
    public void Read_UnknownXsltElement_UnderAVersionOfAtMost2_ThrowsXtse0010(string version)
    {
        // Arrange — only a version above 2.0 is forwards-compatible (XSLT 2.0 §3.9).
        string xml =
            $"<xsl:stylesheet version='{version}' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'><xsl:not-a-real-instruction/></xsl:template>" +
            "</xsl:stylesheet>";

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => ReadInline(xml))!;
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0010));
    }

    [Test]
    public void Read_UnknownXsltElement_UnderXslVersion3OnLiteralRoot_WarnsXtse0150()
    {
        // Arrange — forwards-compat via xsl:version on a literal result element.
        string xml =
            $"<html xsl:version='3.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:unknown/>" +
            "</html>";

        // Act
        StylesheetModule module = ReadInline(xml);

        // Assert
        Diagnostic warn = module.Diagnostics.Single();
        Assert.That(warn.Code, Is.EqualTo(XsltErrors.XTSE0150));
    }

    // ── Deep recursion / nesting ──────────────────────────────────────────

    [Test]
    public void Read_NestedInstructions_PreservedInOrder()
    {
        // Arrange
        string xml =
            $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>" +
            "<xsl:template match='/'>" +
            "<xsl:if test='true()'><xsl:value-of select='.'/></xsl:if>" +
            "</xsl:template>" +
            "</xsl:stylesheet>";

        // Act
        StylesheetModule module = ReadInline(xml);
        StylesheetElement template = (StylesheetElement)module.Root.Children[0];
        StylesheetElement ifInstr = (StylesheetElement)template.Children[0];
        StylesheetElement valueOf = (StylesheetElement)ifInstr.Children[0];

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(template.Name.LocalName, Is.EqualTo("template"));
            Assert.That(ifInstr.Name.LocalName, Is.EqualTo("if"));
            Assert.That(valueOf.Name.LocalName, Is.EqualTo("value-of"));
        });
    }

    // ── ModuleUri / arguments ─────────────────────────────────────────────

    [Test]
    public void Read_BaseUriOverridesReaderBaseUri_SetsModuleUri()
    {
        // Arrange
        string xml = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'/>";
        Uri baseUri = new Uri("file:///pretend/style.xsl");

        // Act
        StylesheetModule module;
        using (StringReader sr = new StringReader(xml))
        {
            module = StylesheetReader.Read(sr, baseUri);
        }

        // Assert
        Assert.That(module.ModuleUri, Is.EqualTo("file:///pretend/style.xsl"));
    }

    [Test]
    public void Read_NullArguments_Throws()
    {
        // Arrange + Act + Assert
        Assert.Multiple(() =>
        {
            Assert.That(() => StylesheetReader.Read((XmlReader)null!), Throws.ArgumentNullException);
            Assert.That(() => StylesheetReader.Read((TextReader)null!), Throws.ArgumentNullException);
            Assert.That(() => StylesheetReader.Read((Stream)null!), Throws.ArgumentNullException);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static StylesheetModule ReadInline(string xml)
    {
        using StringReader sr = new StringReader(xml);
        return StylesheetReader.Read(sr, new Uri("file:///inline.xsl"));
    }
}
