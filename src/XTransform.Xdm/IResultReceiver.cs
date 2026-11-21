namespace XTransform.Xdm;

/// <summary>
/// Push-based event sink for constructed content. Implementations: sequence
/// normalizer, serializer emitters, TinyTreeBuilder, test recorders. Calls
/// arrive in document order; attributes and namespaces come after
/// StartElement and before any child content.
/// </summary>
internal interface IResultReceiver
{
    /// <summary>Signals the start of a document.</summary>
    internal void StartDocument();

    /// <summary>Signals the end of a document.</summary>
    internal void EndDocument();

    /// <summary>Signals the start of an element with the given name code and location.</summary>
    internal void StartElement(int nameCode, SourceSpan location);

    /// <summary>Declares a namespace binding on the current element.</summary>
    internal void Namespace(string prefix, string uri);

    /// <summary>Adds an attribute to the current element.</summary>
    internal void Attribute(int nameCode, string value, SourceSpan location);

    /// <summary>Signals the end of the current element.</summary>
    internal void EndElement();

    /// <summary>Adds a text node with the given content.</summary>
    internal void Text(string text);

    /// <summary>Adds a comment node with the given content.</summary>
    internal void Comment(string text);

    /// <summary>Adds a processing-instruction node with the given target and data.</summary>
    internal void ProcessingInstruction(string target, string data);

    /// <summary>Appends an arbitrary XDM item to the output.</summary>
    internal void Append(XdmItem item);
}
