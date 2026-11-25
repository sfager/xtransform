// Non-decimal numbering sequences selected by a single-character format token (XSLT 2.0 §12.3:
// "Any other format token ... denotes a numbering sequence in which that token is the number 1";
// which sequences exist is implementation-defined). Each sequence is a list of runs of consecutive
// code points, optionally with a separate element for zero. A number outside the sequence's range
// is formatted with the token "1" by the caller (§12.3). The documented list is in
// docs/implementation-defined.md.

namespace XTransform.Xslt;

internal static class SpecialSequences
{
    // A run of consecutive code points covering numbers [FirstNumber, FirstNumber + Count).
    private readonly struct Segment
    {
        public Segment(int firstNumber, int firstCodePoint, int count)
        {
            FirstNumber = firstNumber;
            FirstCodePoint = firstCodePoint;
            Count = count;
        }

        public int FirstNumber { get; }

        public int FirstCodePoint { get; }

        public int Count { get; }
    }

    private sealed class Sequence
    {
        public Sequence(int token, int zero, params Segment[] segments)
        {
            Token = token;
            Zero = zero;
            Segments = segments;
        }

        /// <summary>The code point for the number 1 (the format token).</summary>
        public int Token { get; }

        /// <summary>The code point for 0, or -1 when the sequence has none.</summary>
        public int Zero { get; }

        public Segment[] Segments { get; }
    }

    private static readonly Sequence[] s_sequences =
    {
        new Sequence(0x2460, 0x24EA, new Segment(1, 0x2460, 20), new Segment(21, 0x3251, 15), new Segment(36, 0x32B1, 15)),   // ① circled digit one; 0 = ⓪
        new Sequence(0x2474, -1, new Segment(1, 0x2474, 20)),                                                                  // ⑴ parenthesized digit one
        new Sequence(0x2488, 0x1F100, new Segment(1, 0x2488, 20)),                                                             // ⒈ digit one full stop; 0 = 🄀
        new Sequence(0x1F102, 0x1F101, new Segment(1, 0x1F102, 9)),                                                            // 🄂 digit one comma; 0 = 🄁
        new Sequence(0x2776, 0x24FF, new Segment(1, 0x2776, 10), new Segment(11, 0x24EB, 10)),                                 // ❶ dingbat negative circled digit one; 0 = ⓿
        new Sequence(0x2780, 0x1F10B, new Segment(1, 0x2780, 10)),                                                             // ➀ dingbat circled sans-serif digit one; 0 = 🄋
        new Sequence(0x278A, 0x1F10C, new Segment(1, 0x278A, 10)),                                                             // ➊ dingbat negative circled sans-serif digit one; 0 = 🄌
        new Sequence(0x24F5, -1, new Segment(1, 0x24F5, 10)),                                                                  // ⓵ double circled digit one
        new Sequence(0x3220, -1, new Segment(1, 0x3220, 10)),                                                                  // ㈠ parenthesized ideograph one
        new Sequence(0x3280, -1, new Segment(1, 0x3280, 10)),                                                                  // ㊀ circled ideograph one
        new Sequence(0x10107, -1, new Segment(1, 0x10107, 10)),                                                                // 𐄇 aegean number one
        new Sequence(0x111E1, -1, new Segment(1, 0x111E1, 10)),                                                                // 𑇡 sinhala archaic digit one
        new Sequence(0x10E60, -1, new Segment(1, 0x10E60, 10)),                                                                // 𐹠 rumi digit one
        new Sequence(0x1E8C7, -1, new Segment(1, 0x1E8C7, 9)),                                                                 // 𞣇 mende kikakui digit one
        new Sequence(0x1D360, -1, new Segment(1, 0x1D360, 9)),                                                                 // 𝍠 counting rod unit digit one
        new Sequence(0x102E1, -1, new Segment(1, 0x102E1, 10)),                                                                // 𐋡 coptic epact digit one
        new Sequence(0x11052, -1, new Segment(1, 0x11052, 10)),                                                                // 𑁒 brahmi number one
    };

    /// <summary>Returns the formatted number, or null when the token or number is not covered.</summary>
    public static string? TryFormat(string token, long n)
    {
        if (token.Length == 0)
        {
            return null;
        }

        int cp = char.ConvertToUtf32(token, 0);
        int width = char.IsSurrogatePair(token, 0) ? 2 : 1;
        if (token.Length != width)
        {
            return null;
        }

        foreach (Sequence sequence in s_sequences)
        {
            if (sequence.Token != cp)
            {
                continue;
            }

            if (n == 0)
            {
                return sequence.Zero >= 0 ? char.ConvertFromUtf32(sequence.Zero) : null;
            }

            foreach (Segment segment in sequence.Segments)
            {
                if (n >= segment.FirstNumber && n < segment.FirstNumber + segment.Count)
                {
                    return char.ConvertFromUtf32(segment.FirstCodePoint + (int)(n - segment.FirstNumber));
                }
            }

            return null;
        }

        return null;
    }
}
