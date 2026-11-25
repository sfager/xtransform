# Phase 4 — XSLT 2.0 complete: task list

Goal: close the gap between the G3 baseline (3,399 of 7,489 in-scope XSLT tests, 45.4%) and
**Gate G4** — ≥ 90% of in-scope XSLT tests passing, and the XSLT 1.0 parity corpus matching
`XslCompiledTransform` output or every difference listed in `docs/compat-differences.md`
([decisions.md D1](decisions.md#d1-phase-gates-proposed)).

Phase 4 finishes what Phase 3 deliberately deferred — `xsl:sort`, `xsl:number`, grouping,
`xsl:analyze-string`, `xsl:next-match` / `xsl:apply-imports`, `xsl:message`,
`xsl:result-document`, the `html` / `xhtml` emitters, picture-string formatting and
backwards-compatibility mode — plus the XSLT function library, the static and dynamic error
catalogue, and the regex dialect gaps found by the G3 run. Schema-aware features stay out of
scope (decisions.md D2).

## Read this first: G4 cannot be reached as written

G4 needs 90% of 7,489 = **6,741** passing tests. The `unicode-90` set (1,460 tests, 19.5% of
scope) cannot supply them:

- About 35 of every 38 tests in most of its category groups cannot pass in any processor: the
  catalog supplies no `charclass` parameter, so a `Cc` document is checked with the default
  `Ll` class. Its realistic ceiling is **about 650 passes** (597 today).
- Even a perfect `core` shard (6,029 tests) plus 650 gives **6,679 = 89.2%** — below 90%.

So G4 is unreachable while `unicode-90` counts in full, however good the engine becomes.
**P4-01 resolves this before any other work is sized.** The options are in that task. Everything
below is written to be useful under any of them; pass-count targets are stated for the `core`
shard.

## How to work through this list

- One task = one branch (`p4-NN-short-name`) = one pull request, in order unless "Depends on"
  allows otherwise.
- Signatures come from [interfaces.md](interfaces.md); behaviour comes from the specs (links in
  [CLAUDE.md](../CLAUDE.md#specifications)); quote the section number where an algorithm
  follows the spec closely.
- Standing acceptance for every task: builds with zero warnings on all target frameworks, all
  tests pass, new public members in `PublicAPI.Unshipped.txt` with XML docs, the task's box
  ticked in the same PR.
- **Conformance accounting.** Every task that changes the XSLT pass count (a) re-runs both
  shards and states before/after numbers in the PR, (b) regenerates
  `known-failures/xslt-g3.txt` with `eng/generate-xslt-known-failures.ps1` so fixed tests
  leave the list, and (c) raises `tests/XTransform.Conformance/xslt-baseline.json` in the same
  PR. Never lower a floor silently.
- **Do not run the full suite wrapped in `timeout`.** Use `--blame-hang-timeout` and the
  `XTRANSFORM_XSLT_SETS` / `XTRANSFORM_XSLT_SKIP_SETS` selectors; an orphaned test host holds
  gigabytes of memory.
- Numbers quoted below are **failing tests in the G3 baseline run**, by test set. They are
  upper bounds on what a task can unlock, because many tests in a set need several features.

| ID | Task | Failing tests addressed (G3 run) | Depends on |
| --- | --- | ---: | --- |
| P4-01 | Decide the G4 denominator; classify the known-failures list | — | — |
| P4-02 | Harness: stop skipping assertions the engine can now check | ~90 skipped | P4-01 |
| P4-03 | No silent no-ops: deferred instructions fail loudly until built | — | — |
| P4-04 | `xsl:sort`, `xsl:perform-sort`, sort keys | `sort` 66 | P4-03 |
| P4-05 | Collations (`collation` attributes, Unicode codepoint + default) | `collations` 37 | P4-04 |
| P4-06 | `xsl:decimal-format` and `fn:format-number` | `format-number` 70 + 178 call sites | — |
| P4-07 | `xsl:number` and picture tokens | `number` 256 | — |
| P4-08 | `fn:format-date` / `format-dateTime` / `format-time` (+ `-en`) | ~200 call sites, `format-date*` 70 | — |
| P4-09 | `xsl:for-each-group`, `current-group()`, `current-grouping-key()` | `for-each-group` 51 | P4-04 |
| P4-10 | XPath regex dialect completion (XSD validity, blocks, `\i \c \w`) | `regex-syntax-xslt20` 209, `regex-classes` 120, 177 missed `FORX0002` | — |
| P4-11 | `xsl:analyze-string`, `regex-group()` | `analyze-string` 45 | P4-10 |
| P4-12 | `xsl:next-match`, `xsl:apply-imports`, import precedence in dispatch | `next-match` 28, `import` 31 | P4-03 |
| P4-13 | System and environment functions | ~330 call sites | — |
| P4-14 | Namespaces: `xsl:namespace`, `xsl:namespace-alias`, namespace fix-up | `namespace` 82, `namespace-alias` 26 | — |
| P4-15 | `xsl:attribute-set`, `use-attribute-sets`, `xsl:copy` attributes | `attribute-set` 44, `copy` 30 | — |
| P4-16 | `xsl:message`, `xsl:result-document`, run warnings | `result-document` 60, ~26 | P4-02 |
| P4-17 | `html` and `xhtml` output methods | 88 skipped | P4-16 |
| P4-18 | Serialization parameters and `xsl:character-map` | `character-map` 27, ~50 | P4-17 |
| P4-19 | Body forms for `xsl:value-of` / `xsl:sequence`; `xsl:variable` / param typing (`as`) | `as` 73, `variable` 60, ~30 | — |
| P4-20 | Documents, base URIs and entities: `document()`, `base-uri`, DTD entities | `document` 61, `base-uri` 43, `xml-version` 26 | — |
| P4-21 | Keys, `id()`, whitespace stripping | `key` 62, `id` 28, `strip-space` 21 | — |
| P4-22 | Template rules and dispatch details (`match`, position, axes, tunnel, modes) | `match` 67, `position` 49, `axes` 51, `tunnel` 41, `apply-templates` 35 | — |
| P4-23 | Static error catalogue (XTSE*), batches by code | `error` 311 (static share) | — |
| P4-24 | Dynamic and type error catalogue (XTDE*, XTTE*, XTRE*) | `error` (dynamic share), ~100 missed codes | P4-23 |
| P4-25 | Backwards-compatibility mode (`version="1.0"`) | `backwards` 37, `xpath-compat` 16, `xslt-compat` | — |
| P4-26 | Triage sweep of the remaining sets | the long tail | P4-04 .. P4-25 |
| P4-27 | Large-input performance and memory | `unicode-90` wall time | — |
| P4-28 | XSLT 1.0 parity corpus (D6) and `docs/compat-differences.md` | — | P4-25 |
| P4-29 | Gate G4 run, baseline, NuGet preview `0.4.0-preview.1` | — | all |

Suggested order by tests unlocked per unit of effort: **P4-22 (pattern predicates first), P4-07 (done), P4-10, P4-23, P4-06, P4-04,
P4-13, P4-08, P4-09**, then the rest. Tasks with no dependencies can run in any order.

---

## P4-01 Decide the G4 denominator; classify the known-failures list

- [ ] Done

**Scope.** Resolve the arithmetic in "G4 cannot be reached as written" with a decision recorded
in [decisions.md](decisions.md), and give every entry in `known-failures/xslt-g3.txt` a *category*
instead of a truncated message.

Options for the denominator (Stephen decides; this task only records and implements it):
1. Exclude `unicode-90` from the G4 denominator, listing it as a separate stress shard with its
   own floor (denominator 6,029; 90% = 5,427 — about 2,630 more passes than today).
2. Keep it and lower the G4 threshold to what the ceiling allows (about 85%).
3. Keep both and add a `charclass`-supplying shim in the harness (rejected by default — it
   changes what the suite measures).

Categories for the known-failures list (a leading token, e.g. `# [missing-instruction]`):
`missing-instruction`, `missing-function`, `static-error-not-raised`, `dynamic-error-not-raised`,
`output-mismatch`, `regex`, `harness-limit`, `suite-defect`, `performance`.

**Acceptance.**
- The decision is in decisions.md D1/D2 and `docs/phase-4-tasks.md` is edited to match.
- `eng/generate-xslt-known-failures.ps1` emits a category per line; the file is regenerated;
  a per-category count table is added to `artifacts/conformance/xslt-summary.md`.
- `xslt-baseline.json` and `eng/check-xslt-baseline.sh` implement the chosen denominator.

## P4-02 Harness: stop skipping assertions the engine can now check

- [ ] Done

**Scope.** The G3 harness skips tests whose assertions it cannot check: `assert-serialization`
(44), `assert-serialization-error` (7), `assert-message` (26), `assert-result-document` (6),
tests with `static="yes"` parameters, and tests with no source document and no initial template
(9). Implement them as the engine grows: serialization assertions need the serialized text
(already available); message and result-document assertions need P4-16; static parameters need a
compiler option to supply them.

**Acceptance.**
- Each supported assertion kind has a unit test of the comparer itself (passing and failing case).
- Skips only remain for features still outside scope (packages, schema-aware, streaming).
- The summary reports remaining skips grouped by reason.

## P4-03 No silent no-ops: deferred instructions fail loudly until built

- [ ] Done

**Scope.** `InstructionCompiler` currently maps `xsl:analyze-string`, `xsl:for-each-group`,
`xsl:next-match`, `xsl:apply-imports`, `xsl:perform-sort` and `xsl:fallback` to `null`, so a
stylesheet using them compiles and silently produces wrong output. Replace each with a compile
error (`XTSE` "not implemented in this version") carrying the element's location, and remove each
case as its task lands. `xsl:fallback` is implemented properly here (it is only evaluated for
unknown instructions).

**Acceptance.**
- One test per element asserting the diagnostic and location.
- Conformance: tests that used these instructions move from "wrong output" to "compile error"
  in the known-failures categories; the pass count does not drop.

## P4-04 `xsl:sort`, `xsl:perform-sort`, sort keys

- [ ] Done

**Scope.** XSLT 2.0 §13: `xsl:sort` in `xsl:apply-templates` and `xsl:for-each` (and, with
P4-09, `xsl:for-each-group`), multiple keys, `select`, `data-type`, `order`, `case-order`,
`lang` as AVTs, stable sorting, `xsl:perform-sort`. Sort keys are atomised values compared with
the XPath 2.0 `lt` rules (typed) or as strings with the default collation until P4-05. Errors:
`XTTE1020` (sort key sequence of more than one item), `XTDE0030`.

**Acceptance.**
- `sort` set: ≥ 90% of in-scope tests pass.
- Stability test: equal keys keep input order for 10,000 items.
- Hot path rule applies: no LINQ in the comparison loop.

## P4-05 Collations

- [ ] Done

**Scope.** `collation` attributes on `xsl:sort`, `xsl:for-each-group`, `xsl:key`, and the default
collation (`default-collation` attribute), with the Unicode codepoint collation and a
`CompareInfo`-based collation for language tags; `fn:compare`, `fn:contains` etc. already take a
collation argument in the XPath layer — wire the dynamic context's default through.
`XTDE1035` for an unsupported collation URI.

**Acceptance.** `collations` set ≥ 80% passing; implementation-defined choices (which language
tags are supported) recorded in `docs/implementation-defined.md`.

## P4-06 `xsl:decimal-format` and `fn:format-number`

- [ ] Done

**Scope.** XSLT 2.0 §16.4: named and default decimal formats, the picture-string grammar
(`#`, `0`, `.`, `,`, `%`, `‰`, exponent not in 2.0, prefix/suffix, negative sub-picture),
rounding with `decimal-format` digits. Errors `XTSE1290`, `XTSE1295`, `XTDE1280`, `XTDE1310`.
Registered as an XSLT function (it needs the stylesheet's decimal formats).

**Acceptance.**
- Table-driven unit tests (≥ 60 cases) for picture parsing and formatting, including grouping
  separators, negative numbers, infinity and NaN.
- `format-number` set ≥ 90% passing; the 178 call-site failures elsewhere drop accordingly.

## P4-07 `xsl:number` and picture tokens

- [x] Done

**Scope.** XSLT 2.0 §12: `value`, `level` (single / multiple / any), `count`, `from`, `format`
tokens (`1`, `01`, `a`, `A`, `i`, `I`, Unicode digit sets and the enclosed-number tokens seen in
the suite), `lang`, `letter-value`, `ordinal`, `grouping-separator` / `grouping-size`.
Default `count` and `from` patterns use the pattern matcher from P1-13. `XTDE0980`, `XTTE0990`, `XTTE1000`,
`XTSE0975`. This is the largest single set (256 failing tests).

**Acceptance.**
- `number` set ≥ 85% passing.
- Alphabetic and roman numbering unit-tested at their edge values (0, range limits, carries).

**Result.** `number` set: 206 of 260 in-scope tests pass (was 4). The rest are blocked by other
tasks, not by `xsl:number`: `xsl:strip-space` (P4-21), namespace-prefix serialization (P4-14),
typed variables with `as` (P4-19), backwards-compatibility mode (P4-25), German and Italian number
words, character references for characters the output encoding cannot represent (P4-18), and
pattern predicates (see P4-22). The 85% acceptance target is reached once those land. Also fixed
here: `assert-string-value` normalizes space by default in the XSLT catalog, and `xsl:function`
bodies now run with no focus (so `xsl:number` and `current()` raise their errors there).

## P4-08 `fn:format-date` / `format-dateTime` / `format-time`

- [ ] Done

**Scope.** F&O §9.8: the picture grammar (`[Y0001]`, `[MNn]`, `[FNn]`, widths, presentation
modifiers), language and calendar arguments (the English `-en` forms first; other languages
fall back and are recorded in `implementation-defined.md`), the five-argument forms,
`FODF1280`, `FODF1310`. Exposed through the XSLT function library only if the XPath library
lacks them (check `BuiltInFunctionLibrary` first; QT3 may already cover part of this).

**Acceptance.** `format-date`, `format-date-en`, `date` sets ≥ 85% passing; 200 call-site
failures elsewhere drop accordingly.

## P4-09 `xsl:for-each-group`, `current-group()`, `current-grouping-key()`

- [ ] Done

**Scope.** XSLT 2.0 §14: `group-by`, `group-adjacent`, `group-starting-with`,
`group-ending-with`, `collation`, `composite` is **not** 2.0; `xsl:sort` inside the group
instruction (P4-04). The current group and grouping key are part of the dynamic context
(`DynamicContext` already carries a current item; add a current-group slot the same way).
`XTDE1060`, `XTDE1070`, `XTDE1080`, `XTTE1100`, `XTTE1120`.

**Acceptance.** `for-each-group` set ≥ 90% passing; a 100,000-item grouping completes in under
two seconds (hash-based grouping for `group-by`).

## P4-10 XPath regex dialect completion

- [ ] Done

**Scope.** The translator is a pass-through for most syntax. Make it validate against the XSD
regex grammar and raise `FORX0002` for invalid patterns (177 tests expect it and we accept the
pattern), support Unicode **block** escapes (`\p{IsBasicLatin}` and the full name table, including
supplementary-plane blocks), implement `\i`, `\I`, `\c`, `\C`, `\w`, `\W` exactly per XSD (the G3
work added the supplementary-plane machinery), character-class subtraction with supplementary
characters, and reject constructs .NET accepts but XSD does not (`(?:`, `\b`, lookarounds,
backreferences in character classes, `{,n}` quantifiers). Flags `x` and `q` per F&O §7.6.1.

**Acceptance.**
- `regex-syntax-xslt20` ≥ 90% and `regex-classes` ≥ 90% passing; the QT3 `fn-matches`,
  `fn-replace` and `fn-tokenize` areas do not regress.
- Table-driven validity tests (≥ 150 patterns) in `tests/XTransform.Tests/XPath/`.
- Unicode category data comes from the runtime and is newer than Unicode 9; where a conformance
  test depends on a specific version, say so in its known-failures reason.

## P4-11 `xsl:analyze-string`, `regex-group()`

- [ ] Done

**Scope.** XSLT 2.0 §15: `xsl:matching-substring`, `xsl:non-matching-substring`, `xsl:fallback`,
the `flags` attribute, `regex-group(n)` and its dynamic-context slot, zero-length-match error
`XTDE1150`, the `FORX0001` / `FORX0002` pass-through, empty-string input.

**Acceptance.** `analyze-string` set ≥ 90% passing.

## P4-12 `xsl:next-match`, `xsl:apply-imports`, import precedence in dispatch

- [ ] Done

**Scope.** XSLT 2.0 §6.7 and §6.6: the "next best" rule for the current template rule (the mode
table already orders by precedence, priority and position; record the currently running rule in
`XsltRunState`), `xsl:apply-imports` restricted to lower import precedence, `xsl:with-param`
pass-through, `XTSE0010` and `XTDE0560` cases.

**Acceptance.** `next-match`, `apply-imports` and `import` sets ≥ 85% passing.

## P4-13 System and environment functions

- [ ] Done

**Scope.** XSLT 2.0 §16: `generate-id` (stable per node, unique across documents in a run),
`system-property` (`xsl:version`, `xsl:vendor`, `xsl:vendor-url`, `xsl:product-name`,
`xsl:product-version`, `xsl:is-schema-aware`, `xsl:supports-serialization`,
`xsl:supports-backwards-compatibility`), `function-available`, `element-available`,
`type-available`, `unparsed-entity-uri`, `unparsed-entity-public-id` (needs DTD entity data
from the reader — see P4-20), `resolve-uri` (two forms), `static-base-uri`, `current-output-uri`,
`unparsed-text`, `unparsed-text-available`. Many of these must also work in `use-when` (the
`UseWhenFunctionLibrary` has its own list).

**Acceptance.** Every function has unit tests for each arity; the corresponding unknown-function
failures (≈ 330 call sites) drop to zero; values for `xsl:vendor` and friends are recorded in
`implementation-defined.md`.

## P4-14 Namespaces: `xsl:namespace`, `xsl:namespace-alias`, namespace fix-up

- [ ] Done

**Scope.** `xsl:namespace` (66 tests report it unknown), `xsl:namespace-alias` including
`#default`, `exclude-result-prefixes` and `extension-element-prefixes` on instructions,
namespace nodes on `xsl:copy` / `xsl:copy-of` (`copy-namespaces`, `inherit-namespaces`), and the
namespace fix-up of XSLT 2.0 §5.7.1 in `TreeBuilder` and the emitters so serialized output
declares the namespaces it uses. `XTDE0430`, `XTDE0440`, `XTSE0165`.

**Acceptance.** `namespace` and `namespace-alias` sets ≥ 85% passing.

## P4-15 `xsl:attribute-set`, `use-attribute-sets`, `xsl:copy` attributes

- [ ] Done

**Scope.** Attribute sets with `use-attribute-sets` on `xsl:element`, `xsl:copy`, `xsl:attribute-set`
itself and on literal result elements (`xsl:use-attribute-sets`); circularity `XTSE0720`;
evaluation order and duplicate-attribute handling. `xsl:copy`'s `copy-namespaces`,
`inherit-namespaces`, `use-attribute-sets` and `validation` (accepted, `strip` only);
`xsl:copy-of` `copy-namespaces`.

**Acceptance.** `attribute-set` ≥ 90% and `copy` ≥ 85% passing.

## P4-16 `xsl:message`, `xsl:result-document`, run warnings

- [ ] Done

**Scope.** `xsl:message` with `terminate`, body and `select`, delivered to
`XsltTransformer.MessageListener` (the property exists and is reserved), `XTMM9000` on
termination. `xsl:result-document` (`href`, `format`, the `xsl:output` parameters as attributes)
delivered through `IResultDocumentHandler` with `BaseOutputUri` resolution, `XTDE1480`,
`XTDE1490`. Surface the run diagnostics the transformer currently discards (the `XTDE0410`
warnings) through a public `Warnings` list on `XsltTransformer`.

**Acceptance.**
- `result-document` and `message` sets ≥ 85% passing.
- Public API changes agreed against [interfaces.md](interfaces.md) before implementation (ask
  first — CLAUDE.md "Ask before you…").

## P4-17 `html` and `xhtml` output methods

- [ ] Done

**Scope.** Serialization 2.0 §7 and §6: HTML 4.01 and HTML5 (`version`, `html-version`) element
and attribute rules — empty elements, boolean attributes, raw text in `script` / `style`,
`<meta http-equiv>` insertion with `include-content-type`, `doctype-public` / `doctype-system`,
`indent` for block elements, `escape-uri-attributes`; `xhtml` adds the namespace rules and
self-closing forms. Today `OutputEmitterFactory` throws `NotSupportedException` and 88 tests skip.

**Acceptance.** All 88 previously skipped tests run; ≥ 85% pass. Golden-file tests for the
element tables.

## P4-18 Serialization parameters and `xsl:character-map`

- [ ] Done

**Scope.** `xsl:character-map` / `xsl:output-character` / `use-character-maps`, the remaining
`xsl:output` parameters across all methods (`normalization-form`, `undeclare-prefixes` is 3.0
and not implemented, `byte-order-mark`, `cdata-section-elements` edge cases, `standalone`,
`omit-xml-declaration` interplay, `media-type`), `disable-output-escaping` end to end, the
`SERE*` serialization error codes, and `indent` that is stable under repeated runs.

**Acceptance.** `character-map` ≥ 90% passing; every serialization error code reachable has a
test asserting the exact code.

## P4-19 Body forms for `value-of` / `sequence`; `as` attribute and variable typing

- [ ] Done

**Scope.** `xsl:value-of` and `xsl:sequence` with a sequence-constructor body instead of
`select` (the compiler currently rejects both: "body form is deferred"), `separator`.
The `as` attribute on `xsl:variable`, `xsl:param`, `xsl:with-param`, `xsl:template`,
`xsl:function` and `xsl:element`-family results, with the function-conversion rules of XPath 2.0
§3.1.5; `XTTE0570`, `XTTE0590`, `XTTE0505`, `XTTE0520`. Fix the "function argument cardinality
violation" run failures (28 tests) that come from over-strict argument checking on
sequence-typed variables.

**Acceptance.** `as`, `variable` and `sequence` sets ≥ 85% passing.

## P4-20 Documents, base URIs and entities

- [ ] Done

**Scope.** `document()` (including the two-argument form and fragment identifiers), `doc()` /
`doc-available()` through the resource resolver, document-order across documents, `xml:base`
handling and `base-uri()` on every node kind, and **DTD entity support in the input and
stylesheet readers**: the G3 reader prohibits DTD processing, so stylesheets using internal
entities fail with "Reference to undeclared entity" (11 tests) and `unparsed-entity-*` cannot work.
Decide between `DtdProcessing.Parse` with a safe resolver and a limited internal-subset parser;
record the choice in `implementation-defined.md` (security: no external entities by default).

**Acceptance.** `document`, `base-uri` and `xml-version` sets ≥ 85% passing; an XXE test shows
external entities are not resolved by default.

## P4-21 Keys, `id()`, whitespace stripping

- [ ] Done

**Scope.** `xsl:key` with `collation`, composite and multi-valued `use`, `key()` with the
third argument and nodes from other documents, `fn:id` / `fn:idref` / `fn:element-with-id`
semantics from DTD or `xml:id`, `xsl:strip-space` / `xsl:preserve-space` with name tests and
precedence (`XTSE0270`), whitespace in the input document, and `xml:space` on the source.

**Acceptance.** `key`, `id`, `strip-space` sets ≥ 85% passing.

## P4-22 Template rules and dispatch details

- [ ] Done

**Scope.** **First: evaluate pattern predicates.** `PatternMatcher.MatchStep` does not evaluate the
predicates of a step pattern at all (it returns true, a P3-07 stub), so `match="item[@x = 1]"` matches
every `item` and `xsl:number count="note[position() mod 2 = 1]"` ignores the predicate. Compile the
predicates with the static context of the pattern, evaluate them with the focus the spec defines
(XSLT 2.0 §5.5.3: position among the candidate's siblings that satisfy the step's node test and the
earlier predicates), and allow `current()`. This is probably the largest single correctness fix in
Phase 4: do it before the other tasks that depend on matching. Then review and fix the remaining
failures in `match` (patterns with unions, `id()`,
`key()`, predicates with `last()`), `position` and `axes` (focus semantics inside patterns and
steps, reverse-axis positions), `tunnel` parameters (propagation through `apply-templates`,
`call-template`, built-in rules; `XTSE0580`, `XTDE0700`), `apply-templates` `mode="#all"` and
`#current`, built-in template rules for each mode, and `current-merge-*` is 3.0 and excluded.

**Acceptance.** `match`, `position`, `axes`, `tunnel`, `apply-templates` sets ≥ 85% passing.

**Progress (branch `p4-22-pattern-predicates`).** Pattern predicates are now compiled with the
stylesheet (`PatternPredicateTable`: template `match`, `xsl:key match`, `xsl:number count` / `from`) and
evaluated by `PatternMatcher` with the step's own focus: position and `last()` are taken among the
siblings that satisfy the node test and the earlier predicates; `current()` is the node being matched;
global variables and user functions are visible. Predicates that cannot read the position and cannot
yield a number are decided on the node alone, so flat documents stay linear. Core shard 3,013 → 3,086
(73 fixed, 0 regressed). Still open in this task: `id()` / `key()` patterns (`MatchId` ignores `id($var)`;
key patterns never match), `mode="#all"` / `#current`, tunnel parameters, and the `position` / `axes` /
`tunnel` sets. A positional predicate on a step with very many siblings is evaluated in time linear in
the sibling count per match.

## P4-23 Static error catalogue (XTSE*)

- [ ] Done

**Scope.** 311 `error` tests and many in other sets expect a compile-time error code that the
compiler does not raise because it accepts the stylesheet. Work through the missed codes in
batches (`XTSE0010`, `XTSE0020`, `XTSE0090`, `XTSE0080`, `XTSE0260`, `XTSE0340`, `XTSE0500`,
`XTSE0550`, `XTSE0680`, `XTSE0710`, `XTSE1080`, `XTSE1660`, ...), each with a validation in the
reader / processor / compiler layer that owns the rule. Cite the spec section for each.

**Acceptance.**
- One table-driven test per batch (`tests/XTransform.Tests/Xslt/StaticErrorCases/`) asserting
  the exact error code and that **all** errors in a stylesheet are reported together.
- No valid stylesheet starts failing: the pass count and the real-world corpus do not drop.

## P4-24 Dynamic and type error catalogue (XTDE*, XTTE*, XTRE*)

- [ ] Done

**Scope.** The same exercise for run-time errors the engine does not raise today
(`XTDE0820`, `XTDE0830`, `XTDE1260`, `XTDE0560`, `XTDE0410` as an error where specified,
`XTRE0540`, `XTTE0570`, `XTTE0590`, ...), plus the `FODT0001` date-overflow cases and
`XPTY0004` cases from XPath 2.0 function-conversion rules.

**Acceptance.** Table-driven tests as in P4-23; `error` set ≥ 85% passing.

## P4-25 Backwards-compatibility mode

- [ ] Done

**Scope.** XSLT 2.0 §3.8 / XPath 2.0 §3.6 and Appendix: `version="1.0"` (or any value < 2.0 on
the instruction or an ancestor) switches XPath evaluation to backwards-compatible semantics —
general comparisons convert to double, `xs:string` / `xs:double` arithmetic, first-item rules for
sequences in single-item contexts, `xsl:value-of` and AVTs using the first item. The compiler
must flag expressions with compatibility mode at compile time (add the flag to the static
context and to the expression-node dependency flags). `xsl:apply-templates` selecting
atomic values, `xsl:copy-of` and `xsl:sort` behaviour under 1.0.

**Acceptance.** `backwards`, `xpath-compat` and `xslt-compat` ≥ 85% passing; the XSLT 1.0 parity
corpus (P4-28) is runnable.

## P4-26 Triage sweep of the remaining sets

- [ ] Done

**Scope.** After P4-04 .. P4-25, re-run the suite, group the failures by cause with the same
script as P4-01, and fix the long tail by cause rather than by set: `bug` (42), `select` (40),
`use-when` (38), `core-function` (46), `function` (27), `type` (27), `accessor` (24),
`expression` (20), `math` (21), and the rest. The task ends when no single known-failures
category has more than 25 entries, or each remaining one has a written reason.

**Acceptance.** Per-category count table in the summary; the remaining list is reviewed by
hand and each cluster has a reason that says why it is not fixable (suite defect, schema-aware,
streaming, out of scope) or links to a new task.

## P4-27 Large-input performance and memory

- [ ] Done

**Scope.** The `unicode-90` shard loads documents of up to 54 MB (about 984,000 elements) and
takes about an hour with no known-failures list; the test host reaches 7 GB. Profile and fix:
`TinyTree` memory per node and attribute, reader overhead, `1 to N` range materialisation inside
`for` expressions (lazy iteration), `distinct-values` over large sequences, regex cache growth,
GC settings for the harness. Add BenchmarkDotNet cases (`bench/`) for loading a 50 MB document,
a million-item `for` loop and a million-item sort.

**Acceptance.**
- Loading a 54 MB document in under 5 seconds and under 1.5 GB working set.
- The full `unicode-90` shard (without a known-failures list) in under 15 minutes in CI.
- Benchmarks committed with a baseline; a regression threshold documented.

## P4-28 XSLT 1.0 parity corpus (D6) and `docs/compat-differences.md`

- [ ] Done

**Scope.** Build the corpus defined by [decisions.md D6](decisions.md#d6-xslt-10-parity-corpus):
XSLT 1.0 stylesheets run through `XslCompiledTransform` and through `XTransform.Compat`, outputs
compared; every difference is either fixed or listed in `docs/compat-differences.md` with the
reason (number formatting, namespace output, whitespace, sort order). Reuse the real-world
corpus runner from P3-17 and the `eng/generate-realworld-expected.ps1` approach.

**Acceptance.**
- At least 30 stylesheets with inputs; test names list the stylesheet and the aspect exercised.
- Every difference listed; none unexplained.
- `eng/check-parity.sh` wraps the test selection for CI.

## P4-29 Gate G4 run, baseline, NuGet preview

- [ ] Done

**Scope.** Run both XSLT shards in CI and record the baseline in `xslt-baseline.json`; confirm
the parity corpus passes; extend `eng/check-xslt-baseline.sh` if the denominator changed
(P4-01); bump `Directory.Build.props` to `0.4.0-preview.1` and build the packages. Publishing
remains a separate manual step.

**Acceptance.**
- Pass rate meets the G4 criterion **as amended by P4-01**.
- The five real-world stylesheets and the parity corpus are byte-identical / explained.
- `artifacts/conformance/xslt-summary.md` attached to the PR; every remaining miss in
  `known-failures/xslt-g3.txt` has a category and a reason.
