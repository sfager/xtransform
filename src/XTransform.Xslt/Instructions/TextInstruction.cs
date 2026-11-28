using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Emits a literal text node. Covers both <c>xsl:text</c> (whose body is fixed text) and
/// bare text nodes inside a template body that survived whitespace stripping.
/// </summary>
internal sealed class TextInstruction : XsltInstruction
{
    /// <summary>Initializes a text instruction.</summary>
    public TextInstruction(string text, SourceSpan location)
        : base(location)
    {
        Text = text;
    }

    /// <summary>The literal text to emit.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // A zero-length xsl:text is still a text node when the receiver builds a sequence.
        if (Text.Length > 0 || receiver is ISequenceReceiver)
        {
            receiver.EmitTextNode(Text);
        }
    }
}
