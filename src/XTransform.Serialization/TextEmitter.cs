// XSLT and XQuery Serialization 1.0 — method="text".
// Only the string values of text nodes (and atomic values, already turned into text by the
// SequenceNormalizer) are written; element, attribute, comment and PI markup is dropped.

using System;
using System.IO;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>Serialization parameters for <see cref="TextEmitter"/>.</summary>
internal sealed class TextEmitterOptions
{
    /// <summary>Output encoding. Default UTF-8.</summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    /// <summary><c>byte-order-mark</c>; <see langword="null"/> means the encoding's default (see <see cref="EncodingResolver"/>).</summary>
    public bool? ByteOrderMark { get; init; }
}

/// <summary>
/// <see cref="IResultReceiver"/> for <c>method="text"</c>. Place a
/// <see cref="SequenceNormalizer"/> in front of it.
/// </summary>
internal sealed class TextEmitter : IResultReceiver, IDisposable
{
    private readonly TextWriter _text;
    private readonly bool _ownsText;

    /// <summary>Initializes an emitter over <paramref name="text"/>.</summary>
    public TextEmitter(TextWriter text, bool ownsWriter = false)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }
#pragma warning restore CA1510

        _text = text;
        _ownsText = ownsWriter;
    }

    /// <summary>
    /// Creates an emitter that encodes to <paramref name="stream"/>; the stream is left open
    /// when the emitter is disposed.
    /// </summary>
    public static TextEmitter Create(Stream stream, TextEmitterOptions options)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
#pragma warning restore CA1510

        StreamWriter writer = new StreamWriter(
            stream, EncodingResolver.Resolve(options.Encoding, options.ByteOrderMark), 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };
        return new TextEmitter(writer, ownsWriter: true);
    }

    /// <inheritdoc/>
    public void StartDocument()
    {
    }

    /// <inheritdoc/>
    public void EndDocument() => _text.Flush();

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
    }

    /// <inheritdoc/>
    public void EndElement()
    {
    }

    /// <inheritdoc/>
    public void Text(string text) => _text.Write(text);

    /// <inheritdoc/>
    public void Comment(string text)
    {
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        switch (item)
        {
            case XdmAtomicValue atomic:
                _text.Write(atomic.GetStringValue());
                return;

            case XdmNode node when node.NodeKind is XdmNodeKind.Attribute or XdmNodeKind.Namespace:
                // Not part of the text output.
                return;

            case XdmNode node:
                // Walk the subtree: its text nodes are written through Text(); markup is dropped.
                TinyTreeEmitter.EmitSubtree(node.Handle.Tree, node.Handle.Index, this);
                return;
        }
    }

    /// <summary>Flushes the writer and disposes it when owned.</summary>
    public void Dispose()
    {
        _text.Flush();
        if (_ownsText)
        {
            _text.Dispose();
        }
    }
}
