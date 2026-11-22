namespace XTransform.Xdm.Iterators;

/// <summary>
/// An iterator that immediately signals end-of-sequence. Singleton: <see cref="Instance"/>.
/// </summary>
internal sealed class EmptyIterator : IXdmIterator
{
    private EmptyIterator() { }

    /// <summary>The single shared instance; allocation-free.</summary>
    internal static readonly EmptyIterator Instance = new EmptyIterator();

    /// <inheritdoc/>
    public XdmItem? Next() => null;

    /// <inheritdoc/>
    public int Position => 0;

    /// <inheritdoc/>
    public IXdmIterator Clone() => Instance;
}
