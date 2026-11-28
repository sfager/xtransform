// XSLT 2.0 §5.7.1 — sequence normalization. The receiver placed between an XSLT body and
// the final serializer emitter:
//   * adjacent atomic values are joined with a single space (each becomes a text node);
//   * empty text nodes are dropped (and do not count as content);
//   * attribute / namespace nodes are only legal before any child content of the open
//     element — a late one is the non-recoverable error XTDE0410;
//   * an attribute / namespace node with no open element (i.e. destined for a document
//     node) raises XTDE0420.

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Receiver filter that normalises the event stream before it reaches a serializer emitter.
/// </summary>
internal sealed class SequenceNormalizer : IResultReceiver, IBaseUriReceiver
{
    private readonly IResultReceiver _downstream;
    private readonly NamePool? _namePool;
    private readonly Stack<bool> _hasContent = new Stack<bool>();

    // Attributes of each open element are held until its content starts, so that a later attribute with the same name
    // replaces an earlier one (XSLT 2.0 §5.7.1) instead of reaching the serializer twice.
    private readonly Stack<List<PendingAttribute>> _pendingAttributes = new Stack<List<PendingAttribute>>();
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

    // The namespace nodes given to each open element, and whether the element is in no namespace (XSLT 2.0 §5.7.1: XTDE0430, XTDE0440).
    private readonly Stack<Dictionary<string, string>> _elementNamespaces = new Stack<Dictionary<string, string>>();
    private readonly Stack<bool> _elementInNoNamespace = new Stack<bool>();
    private bool _lastWasAtomic;

    /// <summary>
    /// Initializes the normalizer. <paramref name="namePool"/> is only needed to route
    /// attribute / namespace node <em>items</em> (appended via <see cref="Append"/>) through
    /// the placement rules; without it such items are forwarded unchanged.
    /// </summary>
    public SequenceNormalizer(IResultReceiver downstream, NamePool? namePool = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (downstream is null)
        {
            throw new System.ArgumentNullException(nameof(downstream));
        }
#pragma warning restore CA1510

        _downstream = downstream;
        _namePool = namePool;
    }

    /// <summary>The sink events are forwarded to.</summary>
    public IResultReceiver Downstream => _downstream;

    /// <summary>Non-fatal diagnostics collected during normalisation (none at present: XTDE0410 is an error).</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc/>
    public void StartDocument()
    {
        _lastWasAtomic = false;
        _downstream.StartDocument();
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        _lastWasAtomic = false;
        _downstream.EndDocument();
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        _lastWasAtomic = false;
        MarkCurrentHasContent();
        _hasContent.Push(false);
        _pendingAttributes.Push(new List<PendingAttribute>());
        _elementNamespaces.Push(new Dictionary<string, string>(System.StringComparer.Ordinal));
        _elementInNoNamespace.Push(_namePool is not null && _namePool.GetNamespaceUri(nameCode).Length == 0);
        _downstream.StartElement(nameCode, location);
    }

    /// <inheritdoc/>
    public void BaseUri(string baseUri) => (_downstream as IBaseUriReceiver)?.BaseUri(baseUri);

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        _lastWasAtomic = false;
        if (_hasContent.Count == 0)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0420,
                $"A namespace node (prefix '{prefix}') cannot be added to a document node",
                SourceSpan.None);
        }

        if (CurrentHasContent())
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0410,
                $"A namespace node (prefix '{prefix}') follows content of the element it belongs to",
                SourceSpan.None);
        }

        // Two namespace nodes of one element with the same name must agree (XTDE0430); a namespace node with no name needs an
        // element that is in a namespace (XTDE0440). A repeated node is kept once.
        Dictionary<string, string> declared = _elementNamespaces.Peek();
        if (declared.TryGetValue(prefix ?? string.Empty, out string? existing))
        {
            if (!string.Equals(existing, uri, System.StringComparison.Ordinal))
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0430,
                    $"The namespace prefix '{prefix}' is bound to both '{existing}' and '{uri}' on one element",
                    SourceSpan.None);
            }

            return;
        }

        if (string.IsNullOrEmpty(prefix) && uri.Length > 0 && _elementInNoNamespace.Peek())
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0440,
                "An element in no namespace cannot have a namespace node for the default namespace",
                SourceSpan.None);
        }

        declared[prefix ?? string.Empty] = uri;
        _downstream.Namespace(prefix!, uri);
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        _lastWasAtomic = false;
        if (_hasContent.Count == 0)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0420,
                "An attribute node cannot be added to a document node",
                location);
        }

        if (CurrentHasContent())
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0410,
                "An attribute node follows content of the element it belongs to",
                location);
        }

        List<PendingAttribute> pending = _pendingAttributes.Peek();
        for (int i = 0; i < pending.Count; i++)
        {
            if (_namePool is null ? pending[i].NameCode == nameCode : _namePool.Canonical(pending[i].NameCode) == _namePool.Canonical(nameCode))
            {
                pending[i] = new PendingAttribute(nameCode, value, location);
                return;
            }
        }

        pending.Add(new PendingAttribute(nameCode, value, location));
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        _lastWasAtomic = false;
        FlushPendingAttributes();
        if (_pendingAttributes.Count > 0)
        {
            _pendingAttributes.Pop();
        }

        if (_hasContent.Count > 0)
        {
            _hasContent.Pop();
        }

        if (_elementNamespaces.Count > 0)
        {
            _elementNamespaces.Pop();
            _elementInNoNamespace.Pop();
        }

        _downstream.EndElement();
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        _lastWasAtomic = false;
        ForwardText(text);
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        _lastWasAtomic = false;
        MarkCurrentHasContent();
        _downstream.Comment(text);
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        _lastWasAtomic = false;
        MarkCurrentHasContent();
        _downstream.ProcessingInstruction(target, data);
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        switch (item)
        {
            case XdmAtomicValue atomic:
                // XSLT 2.0 §5.7.1: adjacent atomic values are separated by a single space.
                if (_lastWasAtomic)
                {
                    ForwardText(" ");
                }

                ForwardText(atomic.GetStringValue());
                _lastWasAtomic = true;
                return;

            case XdmNode node:
                _lastWasAtomic = false;
                AppendNode(node);
                return;

            default:
                _lastWasAtomic = false;
                MarkCurrentHasContent();
                _downstream.Append(item);
                return;
        }
    }

    private void AppendNode(XdmNode node)
    {
        switch (node.NodeKind)
        {
            case XdmNodeKind.Text:
                ForwardText(node.GetStringValue());
                return;

            case XdmNodeKind.Attribute when _namePool is not null && node.NodeName is QName attrName:
                Attribute(
                    _namePool.Allocate(attrName),
                    node.GetStringValue(),
                    SourceSpan.None);
                return;

            case XdmNodeKind.Namespace when node.NodeName is QName nsName:
                Namespace(nsName.LocalName, node.GetStringValue());
                return;

            default:
                MarkCurrentHasContent();
                _downstream.Append(node);
                return;
        }
    }

    private void ForwardText(string text)
    {
        // Empty text nodes are dropped and do not count as content.
        if (text.Length == 0)
        {
            return;
        }

        MarkCurrentHasContent();
        _downstream.Text(text);
    }

    private void FlushPendingAttributes()
    {
        if (_pendingAttributes.Count == 0)
        {
            return;
        }

        List<PendingAttribute> pending = _pendingAttributes.Peek();
        for (int i = 0; i < pending.Count; i++)
        {
            _downstream.Attribute(pending[i].NameCode, pending[i].Value, pending[i].Location);
        }

        pending.Clear();
    }

    private readonly struct PendingAttribute
    {
        public PendingAttribute(int nameCode, string value, SourceSpan location)
        {
            NameCode = nameCode;
            Value = value;
            Location = location;
        }

        public int NameCode { get; }

        public string Value { get; }

        public SourceSpan Location { get; }
    }

    private bool CurrentHasContent() => _hasContent.Count > 0 && _hasContent.Peek();

    private void MarkCurrentHasContent()
    {
        if (_hasContent.Count > 0 && !_hasContent.Peek())
        {
            FlushPendingAttributes();
            _hasContent.Pop();
            _hasContent.Push(true);
        }
    }
}
