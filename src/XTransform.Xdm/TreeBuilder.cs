using System;

namespace XTransform.Xdm;

/// <summary>
/// An <see cref="IResultReceiver"/> that captures a stream of push events into a new
/// <see cref="TinyTree"/> and exposes the resulting <see cref="XdmNode"/>.
/// Used for XSLT variable/parameter bodies that construct content — the caller sets a
/// <see cref="TreeBuilder"/> as the sink of a child <c>DynamicContext</c>, runs the
/// template body, calls <see cref="Build"/>, and uses the returned node in the parent.
/// </summary>
internal sealed class TreeBuilder : IResultReceiver
{
    private readonly TinyTreeBuilder _builder;
    private bool _documentStarted;

    /// <summary>Initializes the builder.</summary>
    public TreeBuilder(NamePool namePool, string? baseUri = null)
    {
        _builder = new TinyTreeBuilder(namePool, baseUri);
    }

    /// <inheritdoc/>
    public void StartDocument()
    {
        if (_documentStarted)
        {
            return;
        }

        _documentStarted = true;
        _builder.StartDocument();
    }

    /// <inheritdoc/>
    public void EndDocument() => _builder.EndDocument();

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        EnsureDocument();
        _builder.StartElement(nameCode, location);
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri) => _builder.Namespace(prefix, uri);

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location) =>
        _builder.Attribute(nameCode, value, location);

    /// <summary>Sets the base URI of the element that has just started.</summary>
    public void SetBaseUri(string baseUri) => _builder.SetCurrentBaseUri(baseUri);

    /// <inheritdoc/>
    public void EndElement() => _builder.EndElement();

    /// <inheritdoc/>
    public void Text(string text)
    {
        EnsureDocument();
        _builder.Text(text);
    }

    /// <summary>Adds a zero-length text node (see <see cref="TinyTreeBuilder.ZeroLengthText"/>).</summary>
    public void ZeroLengthText()
    {
        EnsureDocument();
        _builder.ZeroLengthText();
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        EnsureDocument();
        _builder.Comment(text);
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        EnsureDocument();
        _builder.ProcessingInstruction(target, data);
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        EnsureDocument();
        if (item is XdmNode node)
        {
            // Walk the node's subtree and emit its events into this builder — the receiver
            // chain treats Append on a node as a serialisation-visible deep copy.
            TinyTreeEmitter.EmitSubtree(node.Handle.Tree, node.Handle.Index, this);
            return;
        }

        if (item is XdmAtomicValue atomic)
        {
            // Atomics contribute their string value.
            _builder.Text(atomic.GetStringValue());
            return;
        }
    }

    /// <summary>
    /// Finalises the captured events into a <see cref="TinyTree"/> and returns the root
    /// document node as an <see cref="XdmNode"/>. May be called only once.
    /// </summary>
    public XdmNode Build()
    {
        if (!_documentStarted)
        {
            // An empty capture still yields a document node with no children.
            _builder.StartDocument();
            _documentStarted = true;
        }

        TinyTree tree = _builder.Build();
        return new XdmNode(NodeHandle.ForNode(tree, 0));
    }

    private void EnsureDocument()
    {
        if (!_documentStarted)
        {
            StartDocument();
        }
    }
}
