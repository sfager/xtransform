namespace XTransform.Xdm;

/// <summary>
/// Implemented by a receiver that keeps base URIs. A copy tells it the base URI of the node it was copied from, right after the
/// element starts; a receiver that does not keep them (a serializer) simply does not implement this.
/// </summary>
internal interface IBaseUriReceiver
{
    /// <summary>Sets the base URI of the element that has just started.</summary>
    public void BaseUri(string baseUri);
}
