using System;
using System.Collections.Generic;
using System.Xml;

namespace XTransform.Xdm;

/// <summary>
/// Builds a <see cref="TinyTree"/> from a stream of <see cref="IResultReceiver"/> push events.
/// After calling <see cref="EndDocument"/>, call <see cref="Build"/> to obtain the immutable tree.
/// </summary>
internal sealed class TinyTreeBuilder : IResultReceiver
{
    // ── Configuration ────────────────────────────────────────────────────────
    private readonly NamePool _namePool;
    private readonly string? _baseUri;

    // ── Node parallel lists ──────────────────────────────────────────────────
    private readonly List<byte> _kind;
    private readonly List<byte> _depth;
    private readonly List<int> _nameCode;
    private readonly List<int> _parent;
    private readonly List<int> _next;
    private readonly List<int> _textStart;
    private readonly List<int> _textLength;
    private readonly List<int> _alpha;  // first attr index per node
    private readonly List<int> _beta;   // first ns index per node

    // ── Shared text buffer ───────────────────────────────────────────────────
    private char[] _textBuffer = new char[256];
    private int _textBufferUsed;

    // ── Attribute lists ──────────────────────────────────────────────────────
    private readonly List<int> _attrParent = new List<int>(16);
    private readonly List<int> _attrNameCode = new List<int>(16);
    private readonly List<string> _attrValue = new List<string>(16);

    // ── Namespace lists ──────────────────────────────────────────────────────
    private readonly List<int> _nsParent = new List<int>(8);
    private readonly List<string> _nsPrefix = new List<string>(8);
    private readonly List<string> _nsUri = new List<string>(8);

    // ── Build state ──────────────────────────────────────────────────────────
    // Stack of open element (node) indices — used to link parent and patch next-sibling.
    private readonly Stack<int> _openElements = new Stack<int>(16);

    // Per-depth tracking of the previous sibling index so we can patch _next.
    // Key = depth, Value = node index of the most-recently-added node at that depth.
    private readonly Dictionary<int, int> _prevSiblingAtDepth = new Dictionary<int, int>(16);

    private int _currentDepth;
    private bool _built;

    // For text-merging: index (in node list) of the last node added, to detect adjacent Text events.
    private int _lastNodeIndex = -1;
    private bool _lastNodeWasText;

    /// <summary>Initializes the builder with a default node-array capacity of 64.</summary>
    internal TinyTreeBuilder(NamePool namePool, string? baseUri)
        : this(namePool, baseUri, initialNodeCapacity: 64) { }

    /// <summary>
    /// Initializes the builder. <paramref name="initialNodeCapacity"/> pre-sizes the parallel
    /// node arrays so that building N nodes with capacity &gt;= N allocates each array exactly once.
    /// This lets large loads stay within a fixed per-node byte budget.
    /// </summary>
    internal TinyTreeBuilder(NamePool namePool, string? baseUri, int initialNodeCapacity)
    {
        _namePool = namePool;
        _baseUri = baseUri;

        int c = initialNodeCapacity < 4 ? 4 : initialNodeCapacity;
        _kind = new List<byte>(c);
        _depth = new List<byte>(c);
        _nameCode = new List<int>(c);
        _parent = new List<int>(c);
        _next = new List<int>(c);
        _textStart = new List<int>(c);
        _textLength = new List<int>(c);
        _alpha = new List<int>(c);
        _beta = new List<int>(c);
    }

    // ── IResultReceiver ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void StartDocument()
    {
        ThrowIfBuilt();
        _currentDepth = 0;
        // Add the document node at index 0.
        AddNode((byte)XdmNodeKind.Document, 0, -1);
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        ThrowIfBuilt();
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        ThrowIfBuilt();
        _currentDepth++;
        int nodeIndex = AddNode((byte)XdmNodeKind.Element, (byte)_currentDepth, nameCode);
        _openElements.Push(nodeIndex);
        _lastNodeWasText = false;
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        ThrowIfBuilt();
        int parentIndex = _openElements.Count > 0 ? _openElements.Peek() : 0;

        int nsIndex = _nsParent.Count;
        _nsParent.Add(parentIndex);
        _nsPrefix.Add(prefix ?? string.Empty);
        _nsUri.Add(uri ?? string.Empty);

        // If this is the first namespace for this element, set beta.
        if (_beta[parentIndex] == -1)
        {
            _beta[parentIndex] = nsIndex;
        }
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        ThrowIfBuilt();
        int parentIndex = _openElements.Count > 0 ? _openElements.Peek() : 0;

        int attrIndex = _attrParent.Count;
        _attrParent.Add(parentIndex);
        _attrNameCode.Add(nameCode);
        _attrValue.Add(value ?? string.Empty);

        // If this is the first attribute for this element, set alpha.
        if (_alpha[parentIndex] == -1)
        {
            _alpha[parentIndex] = attrIndex;
        }
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        ThrowIfBuilt();
        if (_openElements.Count == 0)
        {
            throw new InvalidOperationException("EndElement without matching StartElement.");
        }

        _openElements.Pop();
        // Remove any "last sibling" tracking for child depth (the children of the element
        // that is now closing).  The element's own sibling slot (_currentDepth) must remain
        // so that the next sibling at the same depth can find it.
        _prevSiblingAtDepth.Remove(_currentDepth + 1);
        _currentDepth--;
        _lastNodeWasText = false;
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        ThrowIfBuilt();
        if (text is null || text.Length == 0)
        {
            return;
        }

        // Text merging: if the previous node at this depth was a text node, extend it.
        if (_lastNodeWasText && _lastNodeIndex >= 0)
        {
            // Extend the last text node's content.
            int existingStart = _textStart[_lastNodeIndex];
            int existingLen = _textLength[_lastNodeIndex];

            // The last text node's content must end exactly at _textBufferUsed
            // for us to safely extend it (adjacent Text calls only; no interleaved events).
            if (existingStart + existingLen == _textBufferUsed)
            {
                AppendToTextBuffer(text);
                _textLength[_lastNodeIndex] = _textBufferUsed - existingStart;
                return;
            }
        }

        // New text node.
        int start = _textBufferUsed;
        AppendToTextBuffer(text);
        int nodeIndex = AddNode((byte)XdmNodeKind.Text, (byte)(_currentDepth + 1), -1);
        _textStart[nodeIndex] = start;
        _textLength[nodeIndex] = _textBufferUsed - start;
        _lastNodeWasText = true;
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        ThrowIfBuilt();
        int start = _textBufferUsed;
        AppendToTextBuffer(text ?? string.Empty);
        int nodeIndex = AddNode((byte)XdmNodeKind.Comment, (byte)(_currentDepth + 1), -1);
        _textStart[nodeIndex] = start;
        _textLength[nodeIndex] = _textBufferUsed - start;
        _lastNodeWasText = false;
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        ThrowIfBuilt();
        // Store target and data as combined content; target is the nameCode.
        // For PI, we store the data in the text buffer and use nameCode for target.
        int dataStart = _textBufferUsed;
        AppendToTextBuffer(data ?? string.Empty);
        int nodeIndex = AddNode((byte)XdmNodeKind.ProcessingInstruction, (byte)(_currentDepth + 1), -1);
        _textStart[nodeIndex] = dataStart;
        _textLength[nodeIndex] = _textBufferUsed - dataStart;

        // Store the PI target in the nameCode slot using the name pool.
        _nameCode[nodeIndex] = _namePool.Allocate(string.Empty, target ?? string.Empty);
        _lastNodeWasText = false;
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        // P1-08 scope: appending non-nodes at the document level is not required.
        throw new NotSupportedException(
            "Append is not supported by TinyTreeBuilder in P1-08; use StartElement/Text/etc.");
    }

    // ── Build ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finalises the builder and returns the immutable <see cref="TinyTree"/>.
    /// Must be called after <see cref="EndDocument"/>.
    /// </summary>
    internal TinyTree Build()
    {
        ThrowIfBuilt();
        _built = true;

        int nodeCount = _kind.Count;
        int attrCount = _attrParent.Count;
        int nsCount = _nsParent.Count;

        // Trim text buffer.
        char[] textBuffer = new char[_textBufferUsed];
        Array.Copy(_textBuffer, 0, textBuffer, 0, _textBufferUsed);

        return new TinyTree(
            _namePool,
            _baseUri,
            _kind.ToArray(),
            _depth.ToArray(),
            _nameCode.ToArray(),
            _parent.ToArray(),
            _next.ToArray(),
            _textStart.ToArray(),
            _textLength.ToArray(),
            _alpha.ToArray(),
            _beta.ToArray(),
            textBuffer,
            _attrParent.ToArray(),
            _attrNameCode.ToArray(),
            _attrValue.ToArray(),
            attrCount,
            _nsParent.ToArray(),
            _nsPrefix.ToArray(),
            _nsUri.ToArray(),
            nsCount);
    }

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a complete XML document from <paramref name="reader"/> and returns the resulting tree.
    /// The caller is responsible for configuring the reader (DtdProcessing.Prohibit recommended).
    /// </summary>
    public static TinyTree Load(XmlReader reader, NamePool namePool)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (namePool is null)
        {
            throw new ArgumentNullException(nameof(namePool));
        }
#pragma warning restore CA1510

        string? baseUri = null;
        try
        {
            baseUri = reader.BaseURI;
        }
        catch (NotSupportedException)
        {
            // Some XmlReader implementations don't support BaseURI.
        }

        TinyTreeBuilder builder = new TinyTreeBuilder(namePool, baseUri);
        builder.StartDocument();

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                {
                    int nameCode = namePool.Allocate(reader.NamespaceURI ?? string.Empty, reader.LocalName);
                    builder.StartElement(nameCode, SourceSpan.None);

                    // Emit namespace declarations before attributes.
                    if (reader.MoveToFirstAttribute())
                    {
                        // First pass: namespaces.
                        do
                        {
                            if (string.Equals(reader.Prefix, "xmlns", StringComparison.Ordinal))
                            {
                                // Prefixed namespace: xmlns:foo="uri"
                                builder.Namespace(reader.LocalName, reader.Value);
                            }
                            else if (string.Equals(reader.LocalName, "xmlns", StringComparison.Ordinal)
                                     && string.IsNullOrEmpty(reader.Prefix))
                            {
                                // Default namespace: xmlns="uri"
                                builder.Namespace(string.Empty, reader.Value);
                            }
                        }
                        while (reader.MoveToNextAttribute());

                        // Second pass: regular attributes.
                        reader.MoveToFirstAttribute();
                        do
                        {
                            if (!string.Equals(reader.Prefix, "xmlns", StringComparison.Ordinal) &&
                                !(string.Equals(reader.LocalName, "xmlns", StringComparison.Ordinal)
                                  && string.IsNullOrEmpty(reader.Prefix)))
                            {
                                int attrNameCode = namePool.Allocate(reader.NamespaceURI ?? string.Empty, reader.LocalName);
                                builder.Attribute(attrNameCode, reader.Value, SourceSpan.None);
                            }
                        }
                        while (reader.MoveToNextAttribute());

                        reader.MoveToElement();
                    }

                    if (reader.IsEmptyElement)
                    {
                        builder.EndElement();
                    }

                    break;
                }

                case XmlNodeType.EndElement:
                    builder.EndElement();
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    builder.Text(reader.Value);
                    break;

                case XmlNodeType.Comment:
                    builder.Comment(reader.Value);
                    break;

                case XmlNodeType.ProcessingInstruction:
                    builder.ProcessingInstruction(reader.Name, reader.Value);
                    break;

                case XmlNodeType.XmlDeclaration:
                case XmlNodeType.DocumentType:
                case XmlNodeType.EntityReference:
                case XmlNodeType.Entity:
                case XmlNodeType.Notation:
                case XmlNodeType.EndEntity:
                    // Ignore these node types.
                    break;

                default:
                    break;
            }
        }

        builder.EndDocument();
        return builder.Build();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new node entry to all parallel lists and wires up parent/sibling links.
    /// Returns the new node index.
    /// </summary>
    private int AddNode(byte kind, byte depth, int nameCode)
    {
        int nodeIndex = _kind.Count;

        _kind.Add(kind);
        _depth.Add(depth);
        _nameCode.Add(nameCode);
        _textStart.Add(0);
        _textLength.Add(0);
        _alpha.Add(-1);
        _beta.Add(-1);
        _next.Add(-1);  // will be patched when next sibling appears

        // Set parent.
        int parentIndex = -1;
        if (_openElements.Count > 0)
        {
            parentIndex = _openElements.Peek();
        }
        else if (nodeIndex > 0)
        {
            // Top-level nodes under the document node.
            parentIndex = 0;
        }
        _parent.Add(parentIndex);

        // Patch the previous sibling at this depth to point to us.
        if (_prevSiblingAtDepth.TryGetValue(depth, out int prevSibling))
        {
            _next[prevSibling] = nodeIndex;
        }
        _prevSiblingAtDepth[depth] = nodeIndex;

        _lastNodeIndex = nodeIndex;
        return nodeIndex;
    }

    /// <summary>Appends <paramref name="text"/> to the shared character buffer, growing as needed.</summary>
    private void AppendToTextBuffer(string text)
    {
        int needed = _textBufferUsed + text.Length;
        if (needed > _textBuffer.Length)
        {
            int newSize = Math.Max(needed, _textBuffer.Length * 2);
            char[] newBuffer = new char[newSize];
            Array.Copy(_textBuffer, 0, newBuffer, 0, _textBufferUsed);
            _textBuffer = newBuffer;
        }

        text.CopyTo(0, _textBuffer, _textBufferUsed, text.Length);
        _textBufferUsed += text.Length;
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("TinyTreeBuilder.Build() has already been called.");
        }
    }
}
