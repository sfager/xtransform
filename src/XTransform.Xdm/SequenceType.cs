using System;

namespace XTransform.Xdm;

// XPath 2.0 §2.5.4 — SequenceType: ItemType OccurrenceIndicator?
// XPath 2.0 §2.5.5 — subtype relationships for sequence types.

/// <summary>
/// An XPath 2.0 sequence type pairing an <see cref="ItemType"/> with an <see cref="Occurrence"/>.
/// Instances are immutable and thread-safe.
/// </summary>
public sealed class SequenceType
{
    /// <summary>Initializes a new <see cref="SequenceType"/>.</summary>
    /// <param name="itemType">The item type constraint.</param>
    /// <param name="occurrence">The occurrence constraint.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="itemType"/> is <see langword="null"/>.
    /// </exception>
    public SequenceType(ItemType itemType, Occurrence occurrence)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (itemType is null)
        {
            throw new ArgumentNullException(nameof(itemType));
        }
#pragma warning restore CA1510

        ItemType = itemType;
        Occurrence = occurrence;
    }

    // ── Static singletons ──────────────────────────────────────────────────────

    /// <summary>Gets the sequence type <c>empty-sequence()</c>.</summary>
    public static SequenceType EmptySequence { get; } =
        new SequenceType(ItemType.AnyItem, Occurrence.Empty);

    /// <summary>Gets the sequence type <c>item()*</c>, which matches any sequence.</summary>
    public static SequenceType AnySequence { get; } =
        new SequenceType(ItemType.AnyItem, Occurrence.ZeroOrMore);

    // ── Properties ─────────────────────────────────────────────────────────────

    /// <summary>Gets the item type constraint.</summary>
    public ItemType ItemType { get; }

    /// <summary>Gets the occurrence constraint.</summary>
    public Occurrence Occurrence { get; }

    // ── Matches ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is an instance of this sequence type.
    /// </summary>
    /// <param name="value">The XDM sequence to test.</param>
    /// <returns><see langword="true"/> if the sequence matches; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    // XPath 2.0 §2.5.4 — matching a value against a SequenceType.
    public bool Matches(XdmSequence value)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#pragma warning restore CA1510

        int count = value.Count;

        if (!OccurrenceAllows(Occurrence, count))
        {
            return false;
        }

        if (count == 0)
        {
            return true;
        }

        for (int i = 0; i < count; i++)
        {
            if (!ItemType.Matches(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    // ── IsSubtypeOf ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when every sequence that satisfies this type also
    /// satisfies <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The candidate supertype.</param>
    /// <returns><see langword="true"/> if the subtype relationship holds.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="other"/> is <see langword="null"/>.
    /// </exception>
    // XPath 2.0 §2.5.5 — subtype relationship for sequence types.
    public bool IsSubtypeOf(SequenceType other)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }
#pragma warning restore CA1510

        // empty-sequence() is a subtype of any sequence type whose occurrence allows 0 items.
        if (Occurrence == Occurrence.Empty)
        {
            return OccurrenceAllows(other.Occurrence, 0);
        }

        // Occurrence must be a subtype AND item type must be a subtype.
        if (!OccurrenceIsSubtypeOf(Occurrence, other.Occurrence))
        {
            return false;
        }

        return ItemType.IsSubtypeOf(other.ItemType);
    }

    // ── ToString ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the canonical XPath 2.0 lexical form of this sequence type.
    /// For example: <c>"xs:integer+"</c>, <c>"element(x)?"</c>, <c>"empty-sequence()"</c>.
    /// </summary>
    public override string ToString()
    {
        return Occurrence switch
        {
            Occurrence.Empty => "empty-sequence()",
            Occurrence.ExactlyOne => ItemType.ToString()!,
            Occurrence.ZeroOrOne => ItemType.ToString() + "?",
            Occurrence.ZeroOrMore => ItemType.ToString() + "*",
            Occurrence.OneOrMore => ItemType.ToString() + "+",
            _ => ItemType.ToString()!,
        };
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="count"/> items is valid for
    /// <paramref name="occurrence"/>.
    /// </summary>
    internal static bool OccurrenceAllows(Occurrence occurrence, int count)
    {
        return occurrence switch
        {
            Occurrence.Empty => count == 0,
            Occurrence.ExactlyOne => count == 1,
            Occurrence.ZeroOrOne => count <= 1,
            Occurrence.OneOrMore => count >= 1,
            Occurrence.ZeroOrMore => true,
            _ => false,
        };
    }

    /// <summary>
    /// Returns <see langword="true"/> when the set of valid counts for
    /// <paramref name="a"/> is a subset of the set for <paramref name="b"/>.
    /// </summary>
    // XPath 2.0 §2.5.5 — occurrence-subtype lattice.
    internal static bool OccurrenceIsSubtypeOf(Occurrence a, Occurrence b)
    {
        if (a == b)
        {
            return true;
        }

        // Use if-else rather than a tuple switch to avoid ValueTuple dependency on net462.
        if (a == Occurrence.Empty)
        {
            // Empty: {0} ⊆ ZeroOrOne {0,1}, ZeroOrMore {0,1,2,...}
            return b == Occurrence.ZeroOrOne || b == Occurrence.ZeroOrMore;
        }

        if (a == Occurrence.ExactlyOne)
        {
            // ExactlyOne: {1} ⊆ ZeroOrOne {0,1}, OneOrMore {1,2,...}, ZeroOrMore {0,1,2,...}
            return b == Occurrence.ZeroOrOne || b == Occurrence.OneOrMore || b == Occurrence.ZeroOrMore;
        }

        if (a == Occurrence.ZeroOrOne)
        {
            // ZeroOrOne: {0,1} ⊆ ZeroOrMore {0,1,2,...}
            return b == Occurrence.ZeroOrMore;
        }

        if (a == Occurrence.OneOrMore)
        {
            // OneOrMore: {1,2,...} ⊆ ZeroOrMore {0,1,2,...}
            return b == Occurrence.ZeroOrMore;
        }

        // ZeroOrMore: {0,1,2,...} — no proper supertypes other than itself (already handled).
        return false;
    }
}
