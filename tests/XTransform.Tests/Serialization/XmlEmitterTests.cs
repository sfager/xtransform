using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Tests.Serialization;

/// <summary>P3-13: <see cref="XmlEmitter"/> — declaration, DOCTYPE, CDATA, indent, BOM.</summary>
[TestFixture]
internal sealed class XmlEmitterTests
{
    private static readonly byte[] s_utf16LeBom = { 0xFF, 0xFE };
    private static readonly byte[] s_utf8Bom = { 0xEF, 0xBB, 0xBF };

    // ── Declaration ─────────────────────────────────────────────────────────

    [Test]
    public void Declaration_Default_WritesVersionAndUtf8Encoding()
    {
        // Arrange + Act
        string xml = Emit(new XmlEmitterOptions(), (e, p) => Element(e, p, "a"));

        // Assert
        Assert.That(xml, Is.EqualTo("<?xml version=\"1.0\" encoding=\"UTF-8\"?><a />"));
    }

    [Test]
    public void Declaration_Omitted_WhenOmitXmlDeclarationIsYes()
    {
        string xml = Emit(new XmlEmitterOptions { OmitXmlDeclaration = true }, (e, p) => Element(e, p, "a"));
        Assert.That(xml, Is.EqualTo("<a />"));
    }

    [TestCase(true, "yes")]
    [TestCase(false, "no")]
    public void Declaration_Standalone_WrittenWhenSpecified(bool standalone, string expected)
    {
        string xml = Emit(new XmlEmitterOptions { Standalone = standalone }, (e, p) => Element(e, p, "a"));
        Assert.That(xml, Does.StartWith($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"{expected}\"?>"));
    }

    // ── DOCTYPE ─────────────────────────────────────────────────────────────

    [Test]
    public void Doctype_SystemOnly_WritesSystemIdentifier()
    {
        string xml = Emit(
            new XmlEmitterOptions { OmitXmlDeclaration = true, DoctypeSystem = "a.dtd" },
            (e, p) => Element(e, p, "a"));
        Assert.That(xml, Is.EqualTo("<!DOCTYPE a SYSTEM \"a.dtd\"><a />"));
    }

    [Test]
    public void Doctype_PublicAndSystem_WritesBoth()
    {
        string xml = Emit(
            new XmlEmitterOptions { OmitXmlDeclaration = true, DoctypePublic = "-//X//EN", DoctypeSystem = "x.dtd" },
            (e, p) => Element(e, p, "a"));
        Assert.That(xml, Is.EqualTo("<!DOCTYPE a PUBLIC \"-//X//EN\" \"x.dtd\"><a />"));
    }

    [Test]
    public void Doctype_PublicWithoutSystem_IsIgnored()
    {
        string xml = Emit(
            new XmlEmitterOptions { OmitXmlDeclaration = true, DoctypePublic = "-//X//EN" },
            (e, p) => Element(e, p, "a"));
        Assert.That(xml, Is.EqualTo("<a />"));
    }

    // ── cdata-section-elements ──────────────────────────────────────────────

    [Test]
    public void Cdata_ListedElement_WrapsTextInCdataSection()
    {
        XmlEmitterOptions options = CdataFor("code");
        string xml = Emit(options, (e, p) => TextElement(e, p, "code", "a<b"));
        Assert.That(xml, Is.EqualTo("<code><![CDATA[a<b]]></code>"));
    }

    [Test]
    public void Cdata_TextContainingTerminator_SplitsAcrossSections()
    {
        XmlEmitterOptions options = CdataFor("code");
        string xml = Emit(options, (e, p) => TextElement(e, p, "code", "x]]>y"));
        Assert.That(xml, Is.EqualTo("<code><![CDATA[x]]]]><![CDATA[>y]]></code>"));
    }

    [Test]
    public void Cdata_UnlistedElement_EscapesNormally()
    {
        XmlEmitterOptions options = CdataFor("code");
        string xml = Emit(options, (e, p) => TextElement(e, p, "p", "a<b"));
        Assert.That(xml, Is.EqualTo("<p>a&lt;b</p>"));
    }

    // ── indent ──────────────────────────────────────────────────────────────

    [Test]
    public void Indent_ElementOnlyContent_IndentsEachLevel()
    {
        string xml = Emit(new XmlEmitterOptions { Indent = true, OmitXmlDeclaration = true }, (e, p) =>
        {
            e.StartElement(p.Allocate(string.Empty, "a"), SourceSpan.None);
            Element(e, p, "b");
            e.StartElement(p.Allocate(string.Empty, "c"), SourceSpan.None);
            Element(e, p, "d");
            e.EndElement();
            e.EndElement();
        });

        Assert.That(xml, Is.EqualTo("<a>\n  <b />\n  <c>\n    <d />\n  </c>\n</a>"));
    }

    [Test]
    public void Indent_MixedContent_LeftAlone()
    {
        string xml = Emit(new XmlEmitterOptions { Indent = true, OmitXmlDeclaration = true }, (e, p) =>
        {
            e.StartElement(p.Allocate(string.Empty, "p"), SourceSpan.None);
            e.Text("hi ");
            TextElement(e, p, "b", "x");
            e.Text(" there");
            e.EndElement();
        });

        Assert.That(xml, Is.EqualTo("<p>hi <b>x</b> there</p>"));
    }

    [Test]
    public void Indent_TextLeafInsideIndentedParent_OnlyParentIndented()
    {
        string xml = Emit(new XmlEmitterOptions { Indent = true, OmitXmlDeclaration = true }, (e, p) =>
        {
            e.StartElement(p.Allocate(string.Empty, "a"), SourceSpan.None);
            TextElement(e, p, "b", "text");
            e.EndElement();
        });

        Assert.That(xml, Is.EqualTo("<a>\n  <b>text</b>\n</a>"));
    }

    // ── encoding / byte-order mark ──────────────────────────────────────────

    [Test]
    public void Utf16_WithDeclaration_EmitsBomAndDeclaredEncoding()
    {
        // Arrange
        XmlEmitterOptions options = new XmlEmitterOptions { Encoding = Encoding.Unicode };

        // Act
        byte[] bytes = EmitBytes(options, (e, p) => Element(e, p, "a"));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(Head(bytes, 2), Is.EqualTo(s_utf16LeBom));
            Assert.That(
                Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2),
                Is.EqualTo("<?xml version=\"1.0\" encoding=\"UTF-16\"?><a />"));
        });
    }

    [Test]
    public void Utf8_NoBomByDefault()
    {
        byte[] bytes = EmitBytes(new XmlEmitterOptions(), (e, p) => Element(e, p, "a"));
        Assert.That(Head(bytes, 3), Is.Not.EqualTo(s_utf8Bom));
    }

    [Test]
    public void Utf8_BomRequested_EmitsBom()
    {
        byte[] bytes = EmitBytes(new XmlEmitterOptions { ByteOrderMark = true }, (e, p) => Element(e, p, "a"));
        Assert.That(Head(bytes, 3), Is.EqualTo(s_utf8Bom));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static byte[] Head(byte[] bytes, int count)
    {
        byte[] head = new byte[count];
        Array.Copy(bytes, head, count);
        return head;
    }

    private static XmlEmitterOptions CdataFor(string local) => new XmlEmitterOptions
    {
        OmitXmlDeclaration = true,
        CdataSectionElements = new[] { new QName(string.Empty, string.Empty, local) },
    };

    private static void Element(XmlEmitter e, NamePool p, string name)
    {
        e.StartElement(p.Allocate(string.Empty, name), SourceSpan.None);
        e.EndElement();
    }

    private static void TextElement(XmlEmitter e, NamePool p, string name, string text)
    {
        e.StartElement(p.Allocate(string.Empty, name), SourceSpan.None);
        e.Text(text);
        e.EndElement();
    }

    private static string Emit(XmlEmitterOptions options, Action<XmlEmitter, NamePool> script)
    {
        NamePool pool = new NamePool();
        using StringWriter sw = new StringWriter();
        using (XmlEmitter emitter = new XmlEmitter(sw, pool, options))
        {
            emitter.StartDocument();
            script(emitter, pool);
            emitter.EndDocument();
        }

        return sw.ToString();
    }

    private static byte[] EmitBytes(XmlEmitterOptions options, Action<XmlEmitter, NamePool> script)
    {
        NamePool pool = new NamePool();
        using MemoryStream ms = new MemoryStream();
        using (XmlEmitter emitter = XmlEmitter.Create(ms, pool, options))
        {
            emitter.StartDocument();
            script(emitter, pool);
            emitter.EndDocument();
        }

        return ms.ToArray();
    }
}
