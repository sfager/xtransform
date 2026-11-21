namespace XTransform.Xdm;

// XPath 2.0 §2.5.4 — occurrence indicators in SequenceType

/// <summary>
/// Specifies how many items a <see cref="SequenceType"/> allows.
/// The names and indicators correspond to the XPath 2.0 SequenceType grammar.
/// </summary>
public enum Occurrence : byte
{
    /// <summary>
    /// Exactly one item (no indicator).
    /// Valid count: {1}.
    /// </summary>
    ExactlyOne = 0,

    /// <summary>
    /// Zero or one item (<c>?</c>).
    /// Valid counts: {0, 1}.
    /// </summary>
    ZeroOrOne = 1,

    /// <summary>
    /// Zero or more items (<c>*</c>).
    /// Valid counts: {0, 1, 2, …}.
    /// </summary>
    ZeroOrMore = 2,

    /// <summary>
    /// One or more items (<c>+</c>).
    /// Valid counts: {1, 2, 3, …}.
    /// </summary>
    OneOrMore = 3,

    /// <summary>
    /// The empty sequence (<c>empty-sequence()</c>).
    /// Valid count: {0}. Used as the occurrence for <see cref="SequenceType.EmptySequence"/>.
    /// </summary>
    Empty = 4,
}
