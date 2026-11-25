// XSLT 2.0 §5.7.1 — sequence normalization. The receiver placed between an XSLT body and
// the final serializer emitter:
//   * adjacent atomic values are joined with a single space (each becomes a text node);
//   * empty text nodes are dropped (and do not count as content);
//   * attribute / namespace nodes are only legal before any child content of the open
//     element — late ones are dropped with an XTDE0410 warning;
//   * an attribute / namespace node with no open element (i.e. destined for a document
//     node) raises XTDE0420.

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Receiver filter that normalises the event stream before it reaches a serializer emitter.
/// </summary>
internal sealed class SequenceNormalizer : IResultReceiver
{
    private readonly IResultReceiver _downstream;
    private readonly NamePool? _namePool;
    private readonly Stack<bool> _hasContent = new Stack<bool>();
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();
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

    /// <summary>Non-fatal diagnostics collected during normalisation (XTDE0410).</summary>
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
        _downstream.StartElement(nameCode, location);
    }

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
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                XsltErrors.XTDE0410,
                $"xsl:namespace emitted after content on current element — dropped (prefix '{prefix}')",
                SourceSpan.None));
            return;
        }

        _downstream.Namespace(prefix, uri);
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
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Warning,
                XsltErrors.XTDE0410,
                "Attribute emitted after content on current element — dropped",
                location));
            return;
        }

        _downstream.Attribute(nameCode, value, location);
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        _lastWasAtomic = false;
        if (_hasContent.Count > 0)
        {
            _hasContent.Pop();
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
                    _namePool.Allocate(attrName.NamespaceUri, attrName.LocalName),
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

    private bool CurrentHasContent() => _hasContent.Count > 0 && _hasContent.Peek();

    private void MarkCurrentHasContent()
    {
        if (_hasContent.Count > 0 && !_hasContent.Peek())
        {
            _hasContent.Pop();
            _hasContent.Push(true);
        }
    }
}
