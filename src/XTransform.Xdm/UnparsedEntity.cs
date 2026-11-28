namespace XTransform.Xdm;

/// <summary>An unparsed entity declared in a document's DTD (XSLT 2.0 §16.6.4): where it is, and its public identifier.</summary>
internal sealed class UnparsedEntity
{
    /// <summary>Initializes an unparsed entity.</summary>
    public UnparsedEntity(string systemUri, string publicId)
    {
        SystemUri = systemUri;
        PublicId = publicId;
    }

    /// <summary>The system identifier, resolved against the base URI of the document that declares it.</summary>
    public string SystemUri { get; }

    /// <summary>The public identifier; empty when the declaration has none.</summary>
    public string PublicId { get; }
}
