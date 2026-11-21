using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>Tests the full 5×5 subtype matrix for <see cref="Occurrence"/>.</summary>
[TestFixture]
internal sealed class OccurrenceTests
{
    // ── Subtype matrix ─────────────────────────────────────────────────────────
    //
    // Count sets:
    //   Empty        = {0}
    //   ExactlyOne   = {1}
    //   ZeroOrOne    = {0,1}
    //   OneOrMore    = {1,2,…}
    //   ZeroOrMore   = {0,1,2,…}
    //
    // A is subtype of B ⟺ count-set(A) ⊆ count-set(B).

    private static readonly object[][] s_data =
    [
        // Every occurrence is subtype of itself (5 cases).
        [Occurrence.Empty,      Occurrence.Empty,      true],
        [Occurrence.ExactlyOne, Occurrence.ExactlyOne, true],
        [Occurrence.ZeroOrOne,  Occurrence.ZeroOrOne,  true],
        [Occurrence.OneOrMore,  Occurrence.OneOrMore,  true],
        [Occurrence.ZeroOrMore, Occurrence.ZeroOrMore, true],

        // Empty {0} ⊆ others that allow 0
        [Occurrence.Empty, Occurrence.ZeroOrOne,  true],
        [Occurrence.Empty, Occurrence.ZeroOrMore, true],
        // Empty {0} ⊄ things that don't allow 0
        [Occurrence.Empty, Occurrence.ExactlyOne, false],
        [Occurrence.Empty, Occurrence.OneOrMore,  false],

        // ExactlyOne {1} ⊆ ZeroOrOne, OneOrMore, ZeroOrMore
        [Occurrence.ExactlyOne, Occurrence.ZeroOrOne,  true],
        [Occurrence.ExactlyOne, Occurrence.OneOrMore,  true],
        [Occurrence.ExactlyOne, Occurrence.ZeroOrMore, true],
        // ExactlyOne {1} ⊄ Empty
        [Occurrence.ExactlyOne, Occurrence.Empty, false],

        // ZeroOrOne {0,1} ⊆ ZeroOrMore {0,1,2,...}
        [Occurrence.ZeroOrOne, Occurrence.ZeroOrMore, true],
        // ZeroOrOne {0,1} ⊄ ExactlyOne {1}, OneOrMore {1,2,...}, Empty {0}
        [Occurrence.ZeroOrOne, Occurrence.ExactlyOne, false],
        [Occurrence.ZeroOrOne, Occurrence.OneOrMore,  false],
        [Occurrence.ZeroOrOne, Occurrence.Empty,      false],

        // OneOrMore {1,2,...} ⊆ ZeroOrMore {0,1,2,...}
        [Occurrence.OneOrMore, Occurrence.ZeroOrMore, true],
        // OneOrMore {1,2,...} ⊄ ExactlyOne {1}, ZeroOrOne {0,1}, Empty {0}
        [Occurrence.OneOrMore, Occurrence.ExactlyOne, false],
        [Occurrence.OneOrMore, Occurrence.ZeroOrOne,  false],
        [Occurrence.OneOrMore, Occurrence.Empty,      false],

        // ZeroOrMore {0,1,2,...} ⊄ any strict subset
        [Occurrence.ZeroOrMore, Occurrence.Empty,      false],
        [Occurrence.ZeroOrMore, Occurrence.ExactlyOne, false],
        [Occurrence.ZeroOrMore, Occurrence.ZeroOrOne,  false],
        [Occurrence.ZeroOrMore, Occurrence.OneOrMore,  false],
    ];

    public static IEnumerable<object[]> SubtypeMatrix => s_data;

    [Test, TestCaseSource(nameof(SubtypeMatrix))]
    public void OccurrenceIsSubtypeOf_Matrix_ExpectedResult(
        Occurrence a, Occurrence b, bool expected)
    {
        // Arrange / Act
        bool actual = SequenceType.OccurrenceIsSubtypeOf(a, b);

        // Assert
        Assert.That(actual, Is.EqualTo(expected),
            $"OccurrenceIsSubtypeOf({a}, {b}) should be {expected}");
    }
}
