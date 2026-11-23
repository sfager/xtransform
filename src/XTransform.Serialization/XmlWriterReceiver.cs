// Minimal IResultReceiver that writes events through a System.Xml.XmlWriter. Stand-in for
// the full XmlEmitter that lands in P3-13; sufficient for byte-identical golden tests of the
// P3-07 control-flow and output instructions.

using System;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Minimal <see cref="IResultReceiver"/> that streams its events through an
/// <see cref="System.Xml.XmlWriter"/>. Pending attributes/namespaces are buffered until the
/// first non-attribute event on the current element.
/// </summary>
internal sealed class XmlWriterReceiver : IResultReceiver, IDisposable
{
    private readonly XmlWriter _writer;
    private readonly NamePool _namePool;
    private readonly bool _ownsWriter;

    /// <summary>Initializes a receiver that writes into <paramref name="writer"/>.</summary>
    public XmlWriterReceiver(XmlWriter writer, NamePool namePool, bool ownsWriter = false)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        if (namePool is null)
        {
            throw new ArgumentNullException(nameof(namePool));
        }
#pragma warning restore CA1510

        _writer = writer;
        _namePool = namePool;
        _ownsWriter = ownsWriter;
    }

    /// <inheritdoc/>
    public void StartDocument()
    {
        // Suppress WriteStartDocument so callers can configure ConformanceLevel.Fragment
        // and get content-only output; the full XmlEmitter in P3-13 will handle XML
        // declaration generation per xsl:output.
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        // No-op; see StartDocument.
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        QName q = _namePool.GetQName(nameCode);
        _writer.WriteStartElement(q.Prefix, q.LocalName, q.NamespaceUri);
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            _writer.WriteAttributeString("xmlns", uri);
        }
        else
        {
            _writer.WriteAttributeString("xmlns", prefix, "http://www.w3.org/2000/xmlns/", uri);
        }
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        QName q = _namePool.GetQName(nameCode);
        _writer.WriteAttributeString(q.Prefix, q.LocalName, q.NamespaceUri, value);
    }

    /// <inheritdoc/>
    public void EndElement() => _writer.WriteEndElement();

    /// <inheritdoc/>
    public void Text(string text) => _writer.WriteString(text);

    /// <inheritdoc/>
    public void Comment(string text) => _writer.WriteComment(text);

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data) =>
        _writer.WriteProcessingInstruction(target, data);

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        switch (item)
        {
            case XdmAtomicValue atomic:
                _writer.WriteString(atomic.GetStringValue());
                return;

            case XdmNode node:
                TinyTreeEmitter.EmitSubtree(node.Handle.Tree, node.Handle.Index, this);
                return;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsWriter)
        {
            _writer.Dispose();
        }
    }
}
