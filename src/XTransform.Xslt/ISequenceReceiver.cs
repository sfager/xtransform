// A receiver that builds a sequence of items. Text produced by xsl:value-of, xsl:text and literal text is a text node of its
// own there (XSLT 2.0 §5.7.1), where an element or document receiver would merge it with its neighbours.

using XTransform.Xdm;

namespace XTransform.Xslt;

internal interface ISequenceReceiver
{
    /// <summary>Emits a text node as one item; a zero-length one is kept.</summary>
    public void TextNode(string text);
}

internal static class ReceiverExtensions
{
    /// <summary>Emits <paramref name="text"/> as a text node: its own item for a sequence receiver, ordinary text otherwise.</summary>
    public static void EmitTextNode(this IResultReceiver receiver, string text)
    {
        if (receiver is ISequenceReceiver sequence)
        {
            sequence.TextNode(text);
        }
        else if (text.Length > 0)
        {
            receiver.Text(text);
        }
    }
}
