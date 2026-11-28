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

    // The namespace declarations written for the open elements, innermost last, and where each open element's start in the lists.
    // A declaration that an ancestor already made is not written again (XSLT 2.0 §5.7.1 namespace fix-up).
    private readonly List<string> _declaredPrefixes = new List<string>();
    private readonly List<string> _declaredUris = new List<string>();
    private readonly Stack<int> _declarationMarks = new Stack<int>();

    // The start of an element is held until its namespace nodes and attributes are known, so that a prefix that clashes with a
    // namespace node of the same element can be replaced before anything is written (XSLT 2.0 §5.7.1 namespace fix-up).
    private bool _hasPendingStart;
    private int _pendingNameCode;
    private readonly List<string> _pendingPrefixes = new List<string>();
    private readonly List<string> _pendingUris = new List<string>();
    private readonly List<int> _pendingAttributeNames = new List<int>();
    private readonly List<string> _pendingAttributeValues = new List<string>();
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
                Attribute(_pool.Allocate(attrName), node.GetStringValue(), SourceSpan.None);
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

        FlushPendingStart();
        EnsureHeader();
        _writer.Flush();
        _text.Flush();
    }

    private static Encoding ResolveEncoding(XmlEmitterOptions options) =>
        EncodingResolver.Resolve(options.Encoding, options.ByteOrderMark, characterReferences: true);

    // ── Core writers (what actually touches the XmlWriter) ─────────────────

    private void CoreStartElement(int nameCode)
    {
        FlushPendingStart();
        EnsureHeader();
        QName q = _pool.GetQName(nameCode);
        if (_elementDepth == 0 && !_doctypeWritten)
        {
            WriteDoctypeBeforeRoot(q);
        }

        _declarationMarks.Push(_declaredPrefixes.Count);
        _hasPendingStart = true;
        _pendingNameCode = nameCode;
        _cdataStack.Push(_cdata is not null && _cdata.Contains(q));
        _elementDepth++;
    }

    private void CoreNamespace(string prefix, string uri)
    {
        prefix ??= string.Empty;

        // The xml prefix is bound to its namespace everywhere and is never declared (Namespaces in XML 1.0 §3).
        if (prefix == "xml")
        {
            return;
        }

        if (_hasPendingStart)
        {
            // A later node for the same prefix replaces an earlier one.
            int existing = _pendingPrefixes.IndexOf(prefix);
            if (existing >= 0)
            {
                _pendingUris[existing] = uri;
                return;
            }

            _pendingPrefixes.Add(prefix);
            _pendingUris.Add(uri);
            return;
        }

        WriteNamespace(prefix, uri);
    }

    private void CoreAttribute(int nameCode, string value)
    {
        if (_hasPendingStart)
        {
            _pendingAttributeNames.Add(nameCode);
            _pendingAttributeValues.Add(value);
            return;
        }

        QName q = _pool.GetQName(nameCode);
        _writer.WriteAttributeString(q.Prefix, q.LocalName, q.NamespaceUri, value);
    }

    // Writes the held start tag: the namespace nodes the ancestors have not already made, the element name and the attributes,
    // each with a prefix that agrees with the namespace nodes of this element.
    private void FlushPendingStart()
    {
        if (!_hasPendingStart)
        {
            return;
        }

        _hasPendingStart = false;
        QName element = _pool.GetQName(_pendingNameCode);

        // The nodes this element really adds: those an ancestor has not already declared.
        List<string> prefixes = new List<string>();
        List<string> uris = new List<string>();
        for (int i = 0; i < _pendingPrefixes.Count; i++)
        {
            if (!DeclaredInScope(_pendingPrefixes[i], _pendingUris[i]))
            {
                prefixes.Add(_pendingPrefixes[i]);
                uris.Add(_pendingUris[i]);
            }
        }

        // A name whose prefix is bound to another namespace by this element's own nodes takes a prefix that is free.
        string elementPrefix = element.Prefix;
        if (element.NamespaceUri.Length > 0 && ClashesWithOwnNode(prefixes, uris, elementPrefix, element.NamespaceUri))
        {
            elementPrefix = FreePrefix(prefixes, uris);
        }

        _writer.WriteStartElement(elementPrefix, element.LocalName, element.NamespaceUri);
        for (int i = 0; i < prefixes.Count; i++)
        {
            WriteNamespace(prefixes[i], uris[i]);
        }

        // The namespace of the element's own name is declared before the attributes, as written by hand.
        if (element.NamespaceUri.Length > 0)
        {
            WriteNamespace(elementPrefix, element.NamespaceUri);
        }

        Dictionary<string, string>? renamed = null;
        for (int i = 0; i < _pendingAttributeNames.Count; i++)
        {
            QName attribute = _pool.GetQName(_pendingAttributeNames[i]);
            string attributePrefix = attribute.Prefix;
            if (attribute.NamespaceUri.Length > 0 && ClashesWithOwnNode(prefixes, uris, attributePrefix, attribute.NamespaceUri))
            {
                renamed ??= new Dictionary<string, string>(StringComparer.Ordinal);
                if (!renamed.TryGetValue(attribute.NamespaceUri, out string? fresh))
                {
                    fresh = FreePrefix(prefixes, uris);
                    prefixes.Add(fresh);
                    uris.Add(attribute.NamespaceUri);
                    renamed[attribute.NamespaceUri] = fresh;
                }

                attributePrefix = fresh;
            }

            if (attribute.NamespaceUri.Length > 0 && attributePrefix.Length > 0)
            {
                WriteNamespace(attributePrefix, attribute.NamespaceUri);
            }

            _writer.WriteAttributeString(attributePrefix, attribute.LocalName, attribute.NamespaceUri, _pendingAttributeValues[i]);
        }

        _pendingPrefixes.Clear();
        _pendingUris.Clear();
        _pendingAttributeNames.Clear();
        _pendingAttributeValues.Clear();
    }

    // True when an ancestor already declared this prefix for this namespace.
    private bool DeclaredInScope(string prefix, string uri)
    {
        for (int i = _declaredPrefixes.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_declaredPrefixes[i], prefix, StringComparison.Ordinal))
            {
                return string.Equals(_declaredUris[i], uri, StringComparison.Ordinal);
            }
        }

        return false;
    }

    // True when one of this element's own namespace nodes binds the prefix to a different namespace.
    private static bool ClashesWithOwnNode(List<string> prefixes, List<string> uris, string prefix, string uri)
    {
        int index = prefixes.IndexOf(prefix);
        return index >= 0 && !string.Equals(uris[index], uri, StringComparison.Ordinal);
    }

    // ns0, ns1, ...: the first prefix neither this element nor an ancestor binds.
    private string FreePrefix(List<string> prefixes, List<string> uris)
    {
        for (int i = 0; ; i++)
        {
            string candidate = "ns" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (prefixes.IndexOf(candidate) >= 0)
            {
                continue;
            }

            bool inScope = false;
            for (int j = 0; j < _declaredPrefixes.Count; j++)
            {
                inScope |= string.Equals(_declaredPrefixes[j], candidate, StringComparison.Ordinal);
            }

            if (!inScope)
            {
                return candidate;
            }
        }
    }

    private void WriteNamespace(string prefix, string uri)
    {
        // The xml prefix is bound everywhere and never declared.
        if (prefix == "xml")
        {
            return;
        }

        for (int i = _declaredPrefixes.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_declaredPrefixes[i], prefix, StringComparison.Ordinal))
            {
                if (string.Equals(_declaredUris[i], uri, StringComparison.Ordinal))
                {
                    return;
                }

                break;
            }
        }

        _declaredPrefixes.Add(prefix);
        _declaredUris.Add(uri);

        if (prefix.Length == 0)
        {
            _writer.WriteAttributeString("xmlns", uri);
        }
        else
        {
            _writer.WriteAttributeString("xmlns", prefix, "http://www.w3.org/2000/xmlns/", uri);
        }
    }

    private void CoreEndElement()
    {
        FlushPendingStart();
        if (_cdataStack.Count > 0)
        {
            _cdataStack.Pop();
        }

        _elementDepth--;
        if (_declarationMarks.Count > 0)
        {
            int mark = _declarationMarks.Pop();
            _declaredPrefixes.RemoveRange(mark, _declaredPrefixes.Count - mark);
            _declaredUris.RemoveRange(mark, _declaredUris.Count - mark);
        }

        _writer.WriteEndElement();
    }

    private void CoreText(string text)
    {
        FlushPendingStart();
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
        FlushPendingStart();
        EnsureHeader();
        _writer.WriteComment(text);
    }

    private void CoreProcessingInstruction(string target, string data)
    {
        FlushPendingStart();
        EnsureHeader();
        _writer.WriteProcessingInstruction(target, data);
    }

    private void CoreIndent(int depth)
    {
        FlushPendingStart();
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
