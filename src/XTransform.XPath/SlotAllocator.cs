namespace XTransform.XPath;

/// <summary>
/// Hands out local-variable slot indexes during compilation. One instance per
/// compiled expression (or XSLT template later).
/// </summary>
internal sealed class SlotAllocator
{
    private int _next;

    /// <summary>Allocates the next available slot index and returns it.</summary>
    /// <returns>A zero-based slot index unique within this allocator.</returns>
    public int Allocate() => _next++;

    /// <summary>Gets the total number of slots allocated so far.</summary>
    public int SlotCount => _next;
}
