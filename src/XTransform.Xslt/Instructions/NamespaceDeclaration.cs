namespace XTransform.Xslt.Instructions;

/// <summary>A namespace node an instruction puts on an element it constructs: a prefix (empty for the default namespace) and a URI.</summary>
internal readonly struct NamespaceDeclaration
{
    /// <summary>Initializes a declaration.</summary>
    public NamespaceDeclaration(string prefix, string uri)
    {
        Prefix = prefix;
        Uri = uri;
    }

    /// <summary>The prefix; empty for the default namespace.</summary>
    public string Prefix { get; }

    /// <summary>The namespace URI.</summary>
    public string Uri { get; }
}
