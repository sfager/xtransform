using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Tests.Serialization;

/// <summary>P3-14: <see cref="TextEmitter"/> (method="text").</summary>
[TestFixture]
internal sealed class TextEmitterTests
{
    private static readonly byte[] s_utf8Bom = { 0xEF, 0xBB, 0xBF };

    [Test]
    public void Text_OnlyTextNodesAreWritten_MarkupIsDropped()
    {
        // Arrange
        NamePool pool = new NamePool();
        using StringWriter sw = new StringWriter();
        using (TextEmitter emitter = new TextEmitter(sw))
        {
            // Act
            emitter.StartDocument();
            emitter.StartElement(pool.Allocate(string.Empty, "a"), SourceSpan.None);
            emitter.Attribute(pool.Allocate(string.Empty, "id"), "dropped", SourceSpan.None);
            emitter.Text("one ");
            emitter.Comment("dropped");
            emitter.ProcessingInstruction("dropped", "dropped");
            emitter.StartElement(pool.Allocate(string.Empty, "b"), SourceSpan.None);
            emitter.Text("two");
            emitter.EndElement();
            emitter.EndElement();
            emitter.EndDocument();
        }

        // Assert
        Assert.That(sw.ToString(), Is.EqualTo("one two"));
    }

    [Test]
    public void Text_SpecialCharacters_AreNotEscaped()
    {
        NamePool pool = new NamePool();
        using StringWriter sw = new StringWriter();
        using (TextEmitter emitter = new TextEmitter(sw))
        {
            emitter.Text("a < b & c");
        }

        Assert.That(sw.ToString(), Is.EqualTo("a < b & c"));
    }

    [Test]
    public void Create_Utf8_HasNoBomByDefault()
    {
        byte[] bytes = EmitBytes(new TextEmitterOptions());
        Assert.That(Head(bytes, 3), Is.Not.EqualTo(s_utf8Bom));
    }

    [Test]
    public void Create_Utf8_WithByteOrderMarkYes_WritesBom()
    {
        byte[] bytes = EmitBytes(new TextEmitterOptions { ByteOrderMark = true });
        Assert.That(Head(bytes, 3), Is.EqualTo(s_utf8Bom));
    }

    private static byte[] EmitBytes(TextEmitterOptions options)
    {
        using MemoryStream ms = new MemoryStream();
        using (TextEmitter emitter = TextEmitter.Create(ms, options))
        {
            emitter.Text("hi");
        }

        return ms.ToArray();
    }

    private static byte[] Head(byte[] bytes, int count)
    {
        byte[] head = new byte[count];
        Array.Copy(bytes, head, Math.Min(count, bytes.Length));
        return head;
    }
}
