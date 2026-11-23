// XSLT 2.0 §5.6.3 — sequence normalization. The receiver placed between an XSLT body and
// the final serializer emitter: it joins adjacent atomic values with a single space, drops
// empty text nodes, and enforces attribute/namespace placement (XTDE0410, XTDE0420).
//
// P3-06 ships a pass-through scaffold; the full rules land in P3-13.

using XTransform.Xdm;

namespace XTransform.Serialization;

/// <summary>
/// Placeholder <see cref="IResultReceiver"/> that forwards every event unchanged to a
/// downstream sink. The full XSLT 2.0 sequence-normalisation rules land in P3-13; this type
/// is introduced here so downstream code can wire the normalizer into receiver chains now.
/// </summary>
internal sealed class SequenceNormalizer : IResultReceiver
{
    private readonly IResultReceiver _downstream;

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

    /// <inheritdoc/>
    public void StartDocument() => _downstream.StartDocument();

    /// <inheritdoc/>
    public void EndDocument() => _downstream.EndDocument();

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location) =>
        _downstream.StartElement(nameCode, location);

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri) => _downstream.Namespace(prefix, uri);

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location) =>
        _downstream.Attribute(nameCode, value, location);

    /// <inheritdoc/>
    public void EndElement() => _downstream.EndElement();

    /// <inheritdoc/>
    public void Text(string text) => _downstream.Text(text);

    /// <inheritdoc/>
    public void Comment(string text) => _downstream.Comment(text);

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data) =>
        _downstream.ProcessingInstruction(target, data);

    /// <inheritdoc/>
    public void Append(XdmItem item) => _downstream.Append(item);
}
