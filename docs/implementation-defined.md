# Implementation-defined behaviour

Where the XSLT 2.0, XPath 2.0 or F&O specifications leave behaviour implementation-defined,
record XTransform's choice here: the spec reference, the choice, and why.

| Spec reference | Item | XTransform's choice | Reason |
| --- | --- | --- | --- |
| XSLT 2.0 §12.3 `lang` | Default and supported languages for `xsl:number` | English only. An absent or unsupported `lang` is treated as `en`, which the spec allows ("the language it would use with lang omitted"). | Number words and ordinals for other languages need per-language tables; tracked as a follow-up to P4-07. |
| XSLT 2.0 §12.3 `w`, `W`, `Ww` | English cardinal words | `zero`, `twenty-one`, `one hundred and five` (British "and" after hundreds and before a final group below one hundred); supported up to the `long` range. Title case capitalises each word and each hyphenated part (`Twenty-One`). | The spec does not prescribe the wording; this matches common English usage. |
| XSLT 2.0 §12.3 `ordinal` | English ordinals | Any non-empty `ordinal` value requests English ordinals: `1st 2nd 3rd 4th 11th 12th 13th`, words `first … twelfth … twenty-first`. Ordinals are ignored for alphabetic and roman tokens. | Unsupported combinations produce cardinals (spec permits). |
| XSLT 2.0 §12.3 `i` / `I` | Range of roman numerals | 1 to 3999, subtractive form (`IV`, not `IIII`). Numbers outside the range use the token `1`. | Spec requires an upper bound of at least 1000. |
| XSLT 2.0 §12.3 `a` / `A` | Alphabetic sequences | The 26-letter Latin sequence `a … z aa ab …`, lower bound 1. `letter-value` is accepted and ignored. | `traditional` sequences exist only for other alphabets. |
| XSLT 2.0 §12.3 other tokens | Supported numbering sequences | Every token that is a decimal digit family (any Unicode `Nd` digit whose value is 1, with zero padding), plus the single-character sequences in `SpecialSequences.cs`: circled, parenthesised and full-stop digits (with the zero elements ⓪ ⓿ 🄀 🄁 🄋 🄌 where Unicode defines them), dingbat digits, double-circled, ideograph, Aegean, Sinhala archaic, Rumi, Mende Kikakui, counting-rod, Coptic Epact and Brahmi numbers. Any other token, or a number outside a sequence's range, uses the token `1`. | The spec leaves the repertoire open and requires the fallback. Each table entry is checked against the runtime's Unicode numeric values by a unit test. |
| XSLT 2.0 §12.1 | Values beyond 64 bits | `xsl:number value` converts to `long`; a value that does not fit (such as `1E100`) raises `XTDE0980`. | The spec allows arbitrary-precision integers; revisit if a use case appears. |
