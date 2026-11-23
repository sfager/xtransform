// XSLT 2.0 §5.6.3 — sequence normalization. The receiver placed between an XSLT body and
// the final serializer emitter: it joins adjacent atomic values with a single space, drops
// empty text nodes, and enforces attribute/namespace placement (XTDE0410, XTDE0420).
//
// P3-06 scaffolded this as a pass-through; P3-10 adds the XTDE0410 attribute-placement
// rule. The remaining rules (atomic join, attribute/namespace fix-up) land in P3-13.

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Receiver filter that forwards events to a downstream sink. In P3-10 the normalizer
/// enforces the "attributes before content" rule: if an <see cref="Attribute"/> or
/// <see cref="Namespace"/> arrives after a child has been emitted on the current element,
/// the event is dropped and its location is recorded in <see cref="Diagnostics"/> as an
/// <c>XTDE0410</c> warning.
/// </summary>
internal sealed class SequenceNormalizer : IResultReceiver
{
    private readonly IResultReceiver _downstream;
    private readonly Stack<bool> _hasContent = new Stack<bool>();
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

    /// <summary>Initializes the normalizer with the sink it forwards to.</summary>
    public SequenceNormalizer(IResultReceiver downstream)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (downstream is null)
        {
            throw new System.ArgumentNullException(nameof(downstream));
        }
#pragma warning restore CA1510

        _downstream = downstream;
    }

    /// <summary>The sink events are forwarded to.</summary>
    public IResultReceiver Downstream => _downstream;

    /// <summary>Non-fatal diagnostics collected during normalisation (XTDE0410 so far).</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <inheritdoc/>
    public void StartDocument() => _downstream.StartDocument();

    /// <inheritdoc/>
    public void EndDocument() => _downstream.EndDocument();

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        MarkParentHasContent();
        _hasContent.Push(false);
        _downstream.StartElement(nameCode, location);
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
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
        if (_hasContent.Count > 0)
        {
            _hasContent.Pop();
        }

        _downstream.EndElement();
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        MarkCurrentHasContent();
        _downstream.Text(text);
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        MarkCurrentHasContent();
        _downstream.Comment(text);
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        MarkCurrentHasContent();
        _downstream.ProcessingInstruction(target, data);
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        MarkCurrentHasContent();
        _downstream.Append(item);
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

    private void MarkParentHasContent() => MarkCurrentHasContent();
}
