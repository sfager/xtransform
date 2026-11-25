// XSLT and XQuery Serialization 1.0 — method="xml".
// Writes the normalised event stream to a TextWriter. Escaping and namespace fix-up are
// delegated to System.Xml.XmlWriter (fragment mode); the XML declaration, DOCTYPE,
// cdata-section-elements and indentation are handled here.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// <see cref="IResultReceiver"/> that serialises to XML text. Place a
/// <see cref="SequenceNormalizer"/> in front of it. With <see cref="XmlEmitterOptions.Indent"/>
/// set the emitter buffers the document and replays it with indentation at
/// <see cref="EndDocument"/>; otherwise it streams.
/// </summary>
internal sealed class XmlEmitter : IResultReceiver, IDisposable
{
    private const string IndentUnit = "  ";

    private readonly TextWriter _text;
    private readonly bool _ownsText;
    private readonly NamePool _pool;
    private readonly XmlEmitterOptions _options;
    private readonly XmlWriter _writer;
    private readonly HashSet<QName>? _cdata;
    private readonly Stack<bool> _cdataStack = new Stack<bool>();
    private readonly List<RecordedEvent>? _recorded;
    private int _docDepth;
    private int _elementDepth;
    private bool _headerWritten;
    private bool _doctypeWritten;
    private bool _finished;

    /// <summary>
    /// Initializes an emitter over <paramref name="text"/>. The caller owns the encoding of
    /// the writer; <see cref="XmlEmitterOptions.Encoding"/> only names it in the declaration.
    /// </summary>
    public XmlEmitter(TextWriter text, NamePool pool, XmlEmitterOptions options, bool ownsWriter = false)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (pool is null)
        {
            throw new ArgumentNullException(nameof(pool));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
#pragma warning restore CA1510

        _text = text;
        _ownsText = ownsWriter;
        _pool = pool;
        _options = options;
        _cdata = options.CdataSectionElements is { Count: > 0 } set ? new HashSet<QName>(set) : null;
        _recorded = options.Indent ? new List<RecordedEvent>() : null;

        _writer = XmlWriter.Create(text, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Indent = false,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            CloseOutput = false,
        });
    }

    /// <summary>
    /// Creates an emitter that encodes to <paramref name="stream"/> using
    /// <see cref="XmlEmitterOptions.Encoding"/> and the byte-order-mark rule. The stream is
    /// left open when the emitter is disposed.
    /// </summary>
    public static XmlEmitter Create(Stream stream, NamePool pool, XmlEmitterOptions options)
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

        StreamWriter writer = new StreamWriter(stream, ResolveEncoding(options), 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };
        return new XmlEmitter(writer, pool, options, ownsWriter: true);
    }

    // ── IResultReceiver ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void StartDocument()
    {
        _docDepth++;
        if (_docDepth == 1 && _recorded is null)
        {
            EnsureHeader();
        }
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        if (_docDepth == 0)
        {
            return;
        }

        _docDepth--;
        if (_docDepth == 0)
        {
            Finish();
        }
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.Start, nameCode, null, null));
            return;
        }

        CoreStartElement(nameCode);
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.Namespace, 0, prefix, uri));
            return;
        }

        CoreNamespace(prefix, uri);
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.Attribute, nameCode, value, null));
            return;
        }

        CoreAttribute(nameCode, value);
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.End, 0, null, null));
            return;
        }

        CoreEndElement();
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.Text, 0, text, null));
            return;
        }

        CoreText(text);
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.Comment, 0, text, null));
            return;
        }

        CoreComment(text);
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        if (_recorded is not null)
        {
            _recorded.Add(new RecordedEvent(EventKind.ProcessingInstruction, 0, target, data));
            return;
        }

        CoreProcessingInstruction(target, data);
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        switch (item)
        {
            case XdmAtomicValue atomic:
                Text(atomic.GetStringValue());
                return;

            case XdmNode node when node.NodeKind == XdmNodeKind.Attribute && node.NodeName is QName attrName:
                Attribute(_pool.Allocate(attrName.NamespaceUri, attrName.LocalName), node.GetStringValue(), SourceSpan.None);
                return;

            case XdmNode node when node.NodeKind == XdmNodeKind.Namespace && node.NodeName is QName nsName:
                Namespace(nsName.LocalName, node.GetStringValue());
                return;

            case XdmNode node:
                TinyTreeEmitter.EmitSubtree(node.Handle.Tree, node.Handle.Index, this);
                return;
        }
    }

    /// <summary>Flushes any buffered output. Disposes the underlying writer when owned.</summary>
    public void Dispose()
    {
        Finish();
        if (_ownsText)
        {
            _text.Dispose();
        }
    }

    // ── Header / finish ────────────────────────────────────────────────────

    private void EnsureHeader()
    {
        if (_headerWritten)
        {
            return;
        }

        _headerWritten = true;
        if (_options.OmitXmlDeclaration)
        {
            return;
        }

        StringBuilder sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"");
        sb.Append(_options.Encoding.WebName.ToUpperInvariant()).Append('"');
        if (_options.Standalone is bool standalone)
        {
            sb.Append(" standalone=\"").Append(standalone ? "yes" : "no").Append('"');
        }

        sb.Append("?>");
        if (_options.Indent)
        {
            sb.Append('\n');
        }

        _text.Write(sb.ToString());
    }

    private void WriteDoctypeBeforeRoot(QName root)
    {
        _doctypeWritten = true;
        if (_options.DoctypeSystem is null)
        {
            return;
        }

        string rootName = root.Prefix.Length > 0 ? root.Prefix + ":" + root.LocalName : root.LocalName;
        StringBuilder sb = new StringBuilder("<!DOCTYPE ").Append(rootName);
        if (_options.DoctypePublic is not null)
        {
            sb.Append(" PUBLIC ").Append(Quote(_options.DoctypePublic)).Append(' ').Append(Quote(_options.DoctypeSystem));
        }
        else
        {
            sb.Append(" SYSTEM ").Append(Quote(_options.DoctypeSystem));
        }

        sb.Append('>');
        if (_options.Indent)
        {
            sb.Append('\n');
        }

        // Anything the XmlWriter already buffered (e.g. a leading comment) must land first.
        _writer.Flush();
        _text.Write(sb.ToString());
    }

#pragma warning disable CA2249 // string.Contains(char) is not available on netstandard2.0
    private static string Quote(string value) =>
        value.IndexOf('"') >= 0 ? "'" + value + "'" : "\"" + value + "\"";
#pragma warning restore CA2249

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        if (_recorded is not null)
        {
            Replay(_recorded);
        }

        EnsureHeader();
        _writer.Flush();
        _text.Flush();
    }

    private static Encoding ResolveEncoding(XmlEmitterOptions options)
    {
        Encoding encoding = options.Encoding;
        bool wide = encoding.CodePage is 1200 or 1201 or 12000 or 12001;
        bool bom = options.ByteOrderMark ?? wide;
        return encoding.CodePage switch
        {
            65001 => new UTF8Encoding(bom),
            1200 => new UnicodeEncoding(false, bom),
            1201 => new UnicodeEncoding(true, bom),
            12000 => new UTF32Encoding(false, bom),
            12001 => new UTF32Encoding(true, bom),
            _ => encoding,
        };
    }

    // ── Core writers (what actually touches the XmlWriter) ─────────────────

    private void CoreStartElement(int nameCode)
    {
        EnsureHeader();
        QName q = _pool.GetQName(nameCode);
        if (_elementDepth == 0 && !_doctypeWritten)
        {
            WriteDoctypeBeforeRoot(q);
        }

        _writer.WriteStartElement(q.Prefix, q.LocalName, q.NamespaceUri);
        _cdataStack.Push(_cdata is not null && _cdata.Contains(q));
        _elementDepth++;
    }

    private void CoreNamespace(string prefix, string uri)
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

    private void CoreAttribute(int nameCode, string value)
    {
        QName q = _pool.GetQName(nameCode);
        _writer.WriteAttributeString(q.Prefix, q.LocalName, q.NamespaceUri, value);
    }

    private void CoreEndElement()
    {
        if (_cdataStack.Count > 0)
        {
            _cdataStack.Pop();
        }

        _elementDepth--;
        _writer.WriteEndElement();
    }

    private void CoreText(string text)
    {
        EnsureHeader();
        if (_cdataStack.Count > 0 && _cdataStack.Peek())
        {
            WriteCData(text);
        }
        else
        {
            _writer.WriteString(text);
        }
    }

    // Serialization 1.0 §7.1: "]]>" inside a CDATA section is split across two sections.
    private void WriteCData(string text)
    {
        int start = 0;
        while (true)
        {
            int idx = text.IndexOf("]]>", start, StringComparison.Ordinal);
            if (idx < 0)
            {
                _writer.WriteCData(text.Substring(start));
                return;
            }

            // Close the section after "]]", reopen with the ">".
            _writer.WriteCData(text.Substring(start, idx - start + 2));
            start = idx + 2;
        }
    }

    private void CoreComment(string text)
    {
        EnsureHeader();
        _writer.WriteComment(text);
    }

    private void CoreProcessingInstruction(string target, string data)
    {
        EnsureHeader();
        _writer.WriteProcessingInstruction(target, data);
    }

    private void CoreIndent(int depth)
    {
        _writer.WriteString("\n" + new string(' ', depth * IndentUnit.Length));
    }

    // ── Indent replay ──────────────────────────────────────────────────────

    // Serialization 1.0 §7.1: whitespace is only added between the children of an element that
    // has no text-node children; elements with mixed content are written exactly as they came.
    private void Replay(List<RecordedEvent> events)
    {
        bool[] hasText = new bool[events.Count];
        Stack<int> open = new Stack<int>();
        for (int i = 0; i < events.Count; i++)
        {
            switch (events[i].Kind)
            {
                case EventKind.Start:
                    open.Push(i);
                    break;
                case EventKind.End:
                    open.Pop();
                    break;
                case EventKind.Text:
                    if (open.Count > 0)
                    {
                        hasText[open.Peek()] = true;
                    }

                    break;
            }
        }

        Stack<IndentFrame> frames = new Stack<IndentFrame>();
        int topLevelItems = 0;
        for (int i = 0; i < events.Count; i++)
        {
            RecordedEvent e = events[i];
            switch (e.Kind)
            {
                case EventKind.Start:
                    BeforeChild(frames, ref topLevelItems);
                    CoreStartElement(e.NameCode);
                    frames.Push(new IndentFrame(!hasText[i]));
                    break;

                case EventKind.Comment:
                    BeforeChild(frames, ref topLevelItems);
                    CoreComment(e.First!);
                    break;

                case EventKind.ProcessingInstruction:
                    BeforeChild(frames, ref topLevelItems);
                    CoreProcessingInstruction(e.First!, e.Second!);
                    break;

                case EventKind.Namespace:
                    CoreNamespace(e.First!, e.Second!);
                    break;

                case EventKind.Attribute:
                    CoreAttribute(e.NameCode, e.First!);
                    break;

                case EventKind.Text:
                    CoreText(e.First!);
                    break;

                case EventKind.End:
                    IndentFrame frame = frames.Pop();
                    if (frame.Indentable && frame.HadChild)
                    {
                        CoreIndent(frames.Count);
                    }

                    CoreEndElement();
                    break;
            }
        }
    }

    private void BeforeChild(Stack<IndentFrame> frames, ref int topLevelItems)
    {
        if (frames.Count == 0)
        {
            if (topLevelItems++ > 0)
            {
                _writer.WriteString("\n");
            }

            return;
        }

        IndentFrame parent = frames.Peek();
        if (parent.Indentable)
        {
            parent.HadChild = true;
            CoreIndent(frames.Count);
        }
    }

    private enum EventKind
    {
        Start,
        End,
        Namespace,
        Attribute,
        Text,
        Comment,
        ProcessingInstruction,
    }

    private readonly struct RecordedEvent
    {
        public RecordedEvent(EventKind kind, int nameCode, string? first, string? second)
        {
            Kind = kind;
            NameCode = nameCode;
            First = first;
            Second = second;
        }

        public EventKind Kind { get; }
        public int NameCode { get; }
        public string? First { get; }
        public string? Second { get; }
    }

    private sealed class IndentFrame
    {
        public IndentFrame(bool indentable)
        {
            Indentable = indentable;
        }

        public bool Indentable { get; }
        public bool HadChild { get; set; }
    }
}
