using System.Text;
using XTransform.Xdm;

namespace XTransform.Xslt.Instructions;

/// <summary>Runs an instruction body and keeps only its text: the string value of an attribute or a namespace node built from content.</summary>
internal sealed class StringCollectingReceiver : IResultReceiver
{
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly bool _collectAttributeValues;
    private bool _lastWasAtomic;

    /// <summary>The string put between two adjacent atomic values in the content (the <c>separator</c> of xsl:attribute).</summary>
    public string AtomicSeparator { get; set; } = string.Empty;

    /// <summary>Initializes a collector; with <paramref name="collectAttributeValues"/> the value of an attribute node in the content counts as text.</summary>
    public StringCollectingReceiver(bool collectAttributeValues = false)
    {
        _collectAttributeValues = collectAttributeValues;
    }

    /// <summary>The text collected so far.</summary>
    public string Result => _sb.ToString();

    /// <inheritdoc/>
    public void StartDocument() { }

    /// <inheritdoc/>
    public void EndDocument() { }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location) { }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri) { }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        if (_collectAttributeValues)
        {
            _sb.Append(value);
        }
    }

    /// <inheritdoc/>
    public void EndElement() { }

    /// <inheritdoc/>
    public void Text(string text)
    {
        _lastWasAtomic = false;
        _sb.Append(text);
    }

    /// <inheritdoc/>
    public void Comment(string text) { }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data) { }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        if (item is not null)
        {
            if (item is XdmAtomicValue && _lastWasAtomic)
            {
                _sb.Append(AtomicSeparator);
            }

            _lastWasAtomic = item is XdmAtomicValue;
            _sb.Append(item.GetStringValue());
        }
    }
}
