using XTransform.Xdm;

namespace XTransform;

/// <summary>A message raised by <c>xsl:message</c>.</summary>
public sealed class XsltMessage
{
    internal XsltMessage(string text, XdmSequence content, bool terminate, SourceSpan location)
    {
        Text = text;
        Content = content;
        Terminate = terminate;
        Location = location;
    }

    /// <summary>The message as text.</summary>
    public string Text { get; }

    /// <summary>The message content as a sequence.</summary>
    public XdmSequence Content { get; }

    /// <summary>True when the message was raised with <c>terminate="yes"</c>.</summary>
    public bool Terminate { get; }

    /// <summary>Where in the stylesheet the message was raised.</summary>
    public SourceSpan Location { get; }
}
