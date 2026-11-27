// Collects the result of a transformation as a raw XDM sequence (XsltTransformer.TransformToSequence).
// Top-level atomic values and nodes are kept as items; top-level constructed content is built
// into nodes, one item per element / comment / processing-instruction / run of text.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Receiver that gathers the result sequence rather than serialising it.</summary>
internal sealed class SequenceResultReceiver : IResultReceiver, ISequenceReceiver
{
    private readonly NamePool _pool;
    private readonly List<XdmItem> _items = new List<XdmItem>();
    private readonly StringBuilder _pendingText = new StringBuilder();
    private TreeBuilder? _current;
    private int _depth;
    private bool _ownsDocument;
    private readonly bool _ignoreOutermostDocument;
    private int _documentNesting;
    private bool _outermostIgnored;

    /// <summary>Initializes a receiver whose constructed nodes use <paramref name="pool"/>.</summary>
    public SequenceResultReceiver(NamePool pool, bool principalResult = false)
    {
        _pool = pool;
        _ignoreOutermostDocument = principalResult;
    }

    /// <summary>Returns the collected sequence. Call once the transformation has finished.</summary>
    public XdmSequence ToSequence()
    {
        FlushText();
        return XdmSequence.From(_items);
    }

    /// <inheritdoc/>
    public void StartDocument()
    {
        // The principal result of a transformation is wrapped in one document event that is not itself an item.
        _documentNesting++;
        if (_ignoreOutermostDocument && _documentNesting == 1)
        {
            _outermostIgnored = true;
            return;
        }

        // A document node constructed at the top level (xsl:document) is an item of the sequence; its content is
        // built into it. Inside an element the document wrapper is transparent.
        if (_depth == 0 && _current is null)
        {
            FlushText();
            _current = new TreeBuilder(_pool);
            _current.StartDocument();
            _ownsDocument = true;
        }

        if (_ownsDocument)
        {
            _depth++;
        }
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        _documentNesting--;
        if (_outermostIgnored && _documentNesting == 0)
        {
            _outermostIgnored = false;
            return;
        }

        if (!_ownsDocument)
        {
            return;
        }

        _depth--;
        if (_depth == 0)
        {
            _current!.EndDocument();
            _items.Add(_current.Build());
            _current = null;
            _ownsDocument = false;
        }
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        FlushText();
        _current ??= new TreeBuilder(_pool);
        _current.StartElement(nameCode, location);
        _depth++;
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        if (_current is not null && _depth > 0)
        {
            _current.Namespace(prefix, uri);
        }
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        if (_current is not null && _depth > 0)
        {
            _current.Attribute(nameCode, value, location);
            return;
        }

        // An attribute outside any element is an item of its own (xsl:attribute in a sequence constructor).
        FlushText();
        TreeBuilder holder = new TreeBuilder(_pool);
        holder.StartElement(nameCode, location);
        holder.Attribute(nameCode, value, location);
        holder.EndElement();
        foreach (XdmNode element in holder.Build().Axis(Axis.Child))
        {
            foreach (XdmNode attribute in element.Axis(Axis.Attribute))
            {
                _items.Add(attribute);
                return;
            }
        }
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        if (_current is null)
        {
            return;
        }

        _current.EndElement();
        _depth--;
        if (_depth == 0)
        {
            _items.Add(FirstChild(_current.Build()));
            _current = null;
        }
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        if (_depth > 0)
        {
            _current!.Text(text);
        }
        else
        {
            _pendingText.Append(text);
        }
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        if (_depth > 0)
        {
            _current!.Comment(text);
            return;
        }

        FlushText();
        TreeBuilder builder = new TreeBuilder(_pool);
        builder.Comment(text);
        _items.Add(FirstChild(builder.Build()));
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        if (_depth > 0)
        {
            _current!.ProcessingInstruction(target, data);
            return;
        }

        FlushText();
        TreeBuilder builder = new TreeBuilder(_pool);
        builder.ProcessingInstruction(target, data);
        _items.Add(FirstChild(builder.Build()));
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        if (_depth > 0)
        {
            _current!.Append(item);
            return;
        }

        FlushText();
        _items.Add(item);
    }

    /// <summary>A text node that is an item of the sequence in its own right: separate from the text around it, and kept even when empty.</summary>
    public void TextNode(string text)
    {
        if (_depth > 0)
        {
            _current!.Text(text);
            return;
        }

        FlushText();
        TreeBuilder builder = new TreeBuilder(_pool);
        if (text.Length == 0)
        {
            builder.ZeroLengthText();
        }
        else
        {
            builder.Text(text);
        }

        _items.Add(FirstChild(builder.Build()));
    }

    private void FlushText()
    {
        if (_pendingText.Length == 0)
        {
            return;
        }

        TreeBuilder builder = new TreeBuilder(_pool);
        builder.Text(_pendingText.ToString());
        _pendingText.Clear();
        _items.Add(FirstChild(builder.Build()));
    }

    private static XdmNode FirstChild(XdmNode document)
    {
        foreach (XdmNode child in document.Axis(Axis.Child))
        {
            return child;
        }

        // Cannot happen: every builder above received at least one child event.
        return document;
    }
}
