// XPath 2.0 §2.1 — the XDM data model defines sequences as ordered collections of items.
// An iterator provides a streaming, position-aware traversal of such a sequence.

using System.Diagnostics.CodeAnalysis;

namespace XTransform.Xdm;

/// <summary>
/// A forward-only, position-aware iterator over a sequence of XDM items (item()*).
/// Cloning an iterator restarts it from the beginning.
/// </summary>
public interface IXdmIterator
{
    /// <summary>
    /// Advances the iterator and returns the next item, or <see langword="null"/> when the
    /// sequence is exhausted. Once <see langword="null"/> is returned, all further calls also
    /// return <see langword="null"/>.
    /// </summary>
    /// <returns>The next <see cref="XdmItem"/>, or <see langword="null"/> at end-of-sequence.</returns>
    // CA1716: "Next" conflicts with the VB.NET reserved keyword. The XDM/XPath streaming
    // iterator pattern uses this exact name throughout the spec; the conflict is unavoidable
    // and the target audience is C# / F# code, not VB.NET consumers.
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "XDM iterator pattern mandates the name 'Next'; VB.NET compatibility is not a goal of this library.")]
    public XdmItem? Next();

    /// <summary>
    /// Gets the 1-based position of the last item returned by <see cref="Next"/>.
    /// Zero before the first call to <see cref="Next"/>.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Returns a fresh iterator that will re-iterate the same sequence from the beginning,
    /// independent of the current iterator's state.
    /// </summary>
    /// <returns>A new <see cref="IXdmIterator"/> starting at position 0.</returns>
    public IXdmIterator Clone();
}
