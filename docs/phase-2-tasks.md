# Phase 2 — XPath 2.0 evaluator: task list

Goal: a working XPath 2.0 engine that executes compiled expressions against a
dynamic context, with the F&O function library implemented well enough to clear
**Gate G2** — ≥ 85% of in-scope QT3 tests passing overall and ≥ 80% in each of
`fn`, `op`, `prod` ([decisions.md D1](decisions.md#d1-phase-gates-proposed)).

Phase 2 does **not** include XSLT instructions, serialization, or
backwards-compatible mode — those belong to phases 3 and 4. It also does not
implement schema-aware features (decisions.md D2 scope exclusions).

How to work through this list:

- One task = one branch (`p2-NN-short-name`) = one pull request. Do them in
  order unless the "Depends on" column allows otherwise.
- Signatures come from [interfaces.md](interfaces.md). Behaviour comes from the
  specs (links in [CLAUDE.md](../CLAUDE.md#specifications)); quote the section
  number in a code comment wherever an algorithm follows the spec closely.
- Every task's acceptance criteria include the standing ones: builds with zero
  warnings on all target frameworks, all tests pass, new public members
  recorded in `PublicAPI.Unshipped.txt`, XML doc comments on every public
  member.
- Tick the box here in the same PR that completes the task.
- Each task that changes the QT3 pass-rate measurement bumps
  `tests/XTransform.Conformance/baseline.json` in the same PR (never silently).

| ID | Task | Depends on |
| --- | --- | --- |
| P2-01 | Expression runtime foundation (`ExpressionNode`, `DynamicContext`, `RunState`) | Phase 1 complete |
| P2-02 | XPath compiler (AST → ExpressionNode) with name resolution and slot allocation | P2-01 |
| P2-03 | Literal, sequence, parenthesised and context-item evaluators | P2-02 |
| P2-04 | Variable references, `for`, quantified (`some`/`every`), `if` | P2-03 |
| P2-05 | Arithmetic, comparison and logical operators | P2-04 |
| P2-06 | Path expressions, axis steps, predicates | P2-05 |
| P2-07 | Set expressions, range, `instance of`, `treat`, `cast`, `castable` | P2-06 |
| P2-08 | Function library foundation plus accessor and context functions | P2-02 |
| P2-09 | String functions (`fn:` string group) | P2-08 |
| P2-10 | Numeric and math functions | P2-08 |
| P2-11 | Date/time and duration functions | P2-08 |
| P2-12 | Sequence functions | P2-08 |
| P2-13 | Regex functions (`fn:matches`, `fn:replace`, `fn:tokenize`) | P2-08 |
| P2-14 | QName and namespace functions | P2-08 |
| P2-15 | Gate G2 run, full-evaluation harness, baseline bump to `0.2.0-alpha.1` | P2-05 .. P2-14 |

---

## P2-01 Expression runtime foundation

- [x] Done

**Scope.** `ExpressionNode` abstract class (per [interfaces.md Evaluation](interfaces.md#evaluation))
with `Iterate` / `EvaluateItem` / `EffectiveBooleanValue`; `DynamicContext` with
focus, local-variable frame, and a `RunState` carrying globals cache, documents,
`current-dateTime`, implicit timezone, cancellation and run-wide counters; a
slot allocator helper for the compiler to reserve local-variable indexes.

**Acceptance.**
- `ExpressionNode.Iterate` is abstract; `EvaluateItem` default throws
  `XPTY0004` when the inner iterator yields more than one item;
  `EffectiveBooleanValue` default implements F&O §15.1.1 (effective boolean
  value) stopping on the first item.
- `DynamicContext.WithFocus(IXdmIterator)` returns a cheap child context whose
  `ContextItem` / `ContextPosition` move with `Next()`; `ContextSize` is
  computed lazily on first read (uses `Clone()` to count without consuming).
- `DynamicContext.WithFrame(int slotCount)` returns a context with a fresh
  `XdmSequence[]` frame; reads before writes return `XdmSequence.Empty`.
- `CheckCancellation()` throws `OperationCanceledException` on a cancelled
  run-state token; call sites are documented (loop back-edges, template
  entries later in Phase 3).
- Unit tests build a toy ExpressionNode subclass and verify
  focus/frame/cancellation plumbing with no XPath parsing involved.

## P2-02 XPath compiler

- [x] Done

**Scope.** `XPathCompiler.Compile(XPathAst, IStaticNamespaceResolver, FunctionLibrary) → ExpressionNode`.
Three passes: (1) name and scope resolution — bind each `VariableRef` to a
local-slot index (or report `XPST0008`), bind each `FunctionCall` to a
`FunctionLibrary` entry or report `XPST0017`, validate axis-step
`NodeTest` names; (2) static type inference — assign a `SequenceType` to
every node bottom-up; insert implicit atomization and function-conversion
cast operators where needed; report certain-fail cases as `XPTY0004`;
(3) code generation — emit the `ExpressionNode` tree. Compiler owns a
`SlotAllocator` that reserves local indexes per `for`/`let`/quantified
variable binding.

**Acceptance.**
- Round-trip test: compile 50+ expressions covering every AST node kind from
  Phase 1's `ParserCases.txt` and verify the resulting ExpressionNode tree
  shape via a `ToDebugString()` pretty-printer.
- Undeclared variable raises `XPST0008` with the offending span.
- Unknown function or wrong arity raises `XPST0017`.
- Static type for a literal-only expression is exact (`xs:integer` for `42`,
  `xs:string?` not needed for a string literal).
- Compile is pure — calling it twice with the same inputs produces equivalent
  trees (no shared mutable state).

## P2-03 Literal, sequence, parens, context item

- [x] Done

**Scope.** `LiteralNode`, `SequenceConcatNode` (for `(a, b, c)` with lazy
concat), `ContextItemNode`. ParenthesizedExpr is folded away by the compiler
(empty `()` → `EmptySequenceNode`). Implements F&O atomization helpers
(`fn:data` behaviour) as a shared utility.

**Acceptance.**
- `42` evaluates to a single `xs:integer`; `()` evaluates to empty.
- `(1, (2, 3), 4)` evaluates to four atomics in order; iteration is lazy —
  stopping after 2 items only consumes the first inner sequence.
- `.` evaluates to the current `ContextItem`; if context item is undefined,
  raises `XPDY0002`.
- Table-driven test of 30+ expressions comparing `EvaluateItem` and `Iterate`
  outputs against expected `XdmSequence` values.

## P2-04 Variable references, `for`, quantified, `if`

- [x] Done

**Scope.** `VariableRefNode` reads from the local frame; `ForExprNode` and
`QuantifiedExprNode` iterate their binding sequences and evaluate the return /
satisfies clause with each binding in a child context; `IfExprNode` chooses a
branch after computing the effective boolean value of the test.

**Acceptance.**
- `for $i in 1 to 5 return $i * 2` returns `(2, 4, 6, 8, 10)`.
- Lazy `for`: `(for $i in 1 to 1000000 return $i)[1]` returns `1` and only
  consumes one value from the range generator (verify via an iterator that
  tracks `Next()` calls).
- Multiple `for` clauses iterate as a cross-product in order.
- `some $x in (1,2,3) satisfies $x > 2` → true; short-circuits (iterator
  counter test).
- `every $x in (1,2,3) satisfies $x > 0` → true; `every $x in (1,2,3) satisfies $x > 1` → false.
- `if (1) then "y" else "n"` evaluates only the chosen branch (verify the
  other branch's side-effect-tracked node isn't evaluated).

## P2-05 Arithmetic, comparison, logical operators

- [x] Done

**Scope.** `ArithmeticNode` (+/-, *, div, idiv, mod with numeric type promotion
per F&O §6.2); duration arithmetic per F&O §10; `UnaryNode` (sign).
`GeneralComparisonNode` (`=`, `!=`, `<`, `<=`, `>`, `>=` with existential
semantics and type coercion per XPath 2.0 §3.5.2) and `ValueComparisonNode`
(`eq`, `ne`, `lt`, `le`, `gt`, `ge` with single-item semantics). `NodeComparisonNode`
(`is`, `<<`, `>>`) using `XdmNode.Equals` and `CompareDocumentOrder`. Logical
`and`/`or` with F&O §3.2.3 short-circuit semantics on EBV.

**Acceptance.**
- `1 + 2 * 3` → `7`; `(1 + 2) * 3` → `9`; `10 idiv 3` → `3`; `10 mod 3` → `1`.
- `xs:decimal(1.5) + 1` → `xs:decimal` `2.5` (numeric promotion).
- `xs:integer(5) div 2` → `xs:decimal` `2.5` (integer÷integer promotes).
- `1 eq 1.0` → true (both numeric, promoted); `"a" eq "a"` → true; `"a" eq 1` → `XPTY0004`.
- `(1, 2, 3) = (3, 4, 5)` → true (general comparison, existential).
- `1 < 2 and 3 < 4` → true; short-circuit: `false() and error()` → false (no error).
- `/a is /a` → true for the same node; `a << b` reflects document order.
- QT3 pass-rate for `op/add`, `op/subtract`, `op/multiply`, `op/divide`,
  `op/eq` etc. test-sets > 80% (measure and record).

## P2-06 Path expressions, axis steps, predicates

- [x] Done

**Scope.** `PathNode` evaluates a RelativePathExpr by threading iterators
through steps; each `AxisStepNode` delegates to `TinyTree.Iterate` with the
compiled `NodeTest` and then applies predicates. `PredicateNode` supports
numeric-literal predicates (`[1]` → position-equality shortcut) and general
boolean predicates. `FilterExprNode` applies the same predicate logic to
arbitrary primaries. Document-order deduplication is applied when a step's
input contains more than one node and the step output could reorder.

**Acceptance.**
- `/root/child` on a loaded TinyTree returns the correct node set in document
  order with no duplicates.
- `//*[@id='x']` finds the first element with matching attribute.
- `(1 to 100)[. mod 2 = 0][3]` returns `6` (6 is the 3rd even).
- Predicate lazy evaluation: `(1 to 1000000)[1]` consumes exactly one item.
- `position()` and `last()` inside a predicate are correctly bound to the
  predicate's current focus (not an outer one).
- QT3 pass-rate for `prod/PathExpr`, `prod/AxisStep`, `prod/Predicate` > 80%.

## P2-07 Set expressions, range, instance-of / treat / cast / castable

- [x] Done

**Scope.** `SetExprNode` (`union`, `intersect`, `except`) with document-order
dedup via node identity, returning in document order per F&O §15.3.
`RangeExprNode` for `to` (empty when left > right).
`InstanceOfNode` uses `SequenceType.Matches`.
`TreatNode` passes through on success, throws `XPTY0004` on mismatch.
`CastNode` / `CastableNode` wrap `CastTable.Cast` / `CastTable.TryCast`.

**Acceptance.**
- `(1, 2, 3) union (3, 4, 5)` → `(1, 2, 3, 4, 5)` as a sorted node-set when
  operands are nodes; for atomics `union` raises `XPTY0004` per spec.
- `//a except //b` returns document-order-sorted difference.
- `1 to 5` → `(1,2,3,4,5)`; `5 to 1` → empty sequence (not an error).
- `5 instance of xs:integer+` → true; `5 instance of xs:string` → false;
  `() instance of xs:integer` → false; `() instance of xs:integer?` → true.
- `"5" cast as xs:integer` → `5`; `"abc" cast as xs:integer` → `FORG0001`.
- `"abc" castable as xs:integer` → false (no exception).
- `.` `treat as xs:integer` passes through when focus is an integer, else
  `XPTY0004`.

## P2-08 Function library foundation plus accessor and context functions

- [x] Done

**Scope.** `FunctionLibrary` abstraction and `IExtensionFunction` extension
point. `FunctionCallNode` resolves to a function via the library at compile
time (`XPST0017` if missing). Function conversion rules (atomize sequence
inputs, promote numerics per expected type, cast `xs:untypedAtomic` to the
expected type) applied by the compiler, not the library. First tranche of
built-ins:

- Accessors: `fn:node-name`, `fn:nilled`, `fn:string`, `fn:data`, `fn:base-uri`,
  `fn:document-uri`, `fn:root`, `fn:local-name`, `fn:namespace-uri`, `fn:name`
- Booleans: `fn:true`, `fn:false`, `fn:not`, `fn:boolean`
- Context: `fn:position`, `fn:last`, `fn:current-dateTime`, `fn:current-date`,
  `fn:current-time`, `fn:implicit-timezone`, `fn:default-collation`
- Errors: `fn:error`, `fn:trace`

**Acceptance.**
- Unknown function name raises `XPST0017` at compile time with a span.
- Arity mismatch for a known function raises `XPST0017` at compile time.
- Function conversion: `string-length(42)` compiles (integer → string
  conversion) and returns `2`; `string-length(., ., .)` raises `XPST0017`.
- `fn:boolean(0)` → false; `fn:boolean("")` → false; `fn:boolean("false")` → true
  (strings are non-empty).
- `position()` and `last()` outside a focus context raise `XPDY0002`.
- `current-dateTime()` is stable per transformation (two calls in the same run
  return the same value).
- `fn:error()` always throws `XPDY0050` (default); `fn:error(xs:QName('err:MYCODE'))`
  throws with that code.

## P2-09 String functions

- [x] Done

**Scope.** F&O §7: `concat`, `string-join`, `substring`, `string-length`,
`normalize-space`, `normalize-unicode`, `upper-case`, `lower-case`, `translate`,
`encode-for-uri`, `iri-to-uri`, `escape-html-uri`, `contains`, `starts-with`,
`ends-with`, `substring-before`, `substring-after`, `string-to-codepoints`,
`codepoints-to-string`, `compare`, `codepoint-equal`.

Code-point indexing (NOT UTF-16): a 1-based index in XPath that lands between
the high and low surrogate of a non-BMP character is an error; implement
code-point iteration as a helper (`CodePointString`) shared across functions.

**Acceptance.**
- `substring("12345", 2, 3)` → "234".
- `substring("hello", 0 div 0E0)` → "" (NaN position → empty substring).
- `substring("12345", -42, 1 div 0E0)` → "12345" (negative start + infinity).
- `upper-case("ß")` → "SS" (Unicode case mapping, not ToUpperInvariant).
- `string-length("🌍")` → 1 (one code point, not 2 UTF-16 units).
- `translate("bar","abc","ABC")` → "BAr".
- QT3 `fn/concat`, `fn/substring`, `fn/normalize-space`, `fn/upper-case`
  test-sets > 80%.

## P2-10 Numeric and math functions

- [x] Done

**Scope.** F&O §6.4 and §15.4: `fn:abs`, `fn:ceiling`, `fn:floor`, `fn:round`,
`fn:round-half-to-even`, `fn:sum`, `fn:avg`, `fn:min`, `fn:max`, `fn:count`,
`fn:number`. Numeric promotion and `xs:untypedAtomic` → `xs:double`
conversion in `fn:sum` / `fn:avg` / `fn:min` / `fn:max`. Argument type for
the aggregate functions is `xs:anyAtomicType*`; empty sequence returns `0`
for `sum` (XPath 2.0 §15.4.3), empty for `avg` / `min` / `max`.

**Acceptance.**
- `abs(-3.5)` → `3.5` of the same numeric type.
- `round(2.5)` → `3` (round half towards positive infinity, F&O §4.7.4).
- `round-half-to-even(2.5)` → `2`; `round-half-to-even(3.5)` → `4`.
- `sum(())` → `0` (xs:integer); `sum((), ())` → empty (zero-arg form with
  an empty default).
- `min((3, 1, 2))` → `1`; `min(())` → empty.
- `number("foo")` → `NaN`; `number(())` → `NaN`.
- QT3 `fn/abs`, `fn/round`, `fn/sum`, `fn/avg`, `fn/min`, `fn/max` > 80%.

## P2-11 Date/time and duration functions

- [x] Done

**Scope.** F&O §10: `fn:current-dateTime` et al. (already in P2-08 for context);
component extractors (`year-from-dateTime`, `month-from-dateTime`, …,
`timezone-from-dateTime`, and equivalents for date / time / duration);
`fn:adjust-dateTime-to-timezone`, `fn:adjust-date-to-timezone`,
`fn:adjust-time-to-timezone`; `fn:dateTime` constructor (date + time);
`fn:years-from-duration`, `fn:months-from-duration`, etc. Duration arithmetic
on the operators (already in P2-05) — this task adds the component extractors
and timezone-adjustment functions.

**Acceptance.**
- `year-from-dateTime(xs:dateTime('2024-07-15T12:00:00'))` → 2024.
- `hours-from-duration(xs:duration('PT30H'))` → 6 (30 mod 24).
- `adjust-dateTime-to-timezone(xs:dateTime('2024-07-15T12:00:00Z'), xs:dayTimeDuration('PT1H'))`
  → `2024-07-15T13:00:00+01:00`.
- `dateTime(xs:date('2024-07-15'), xs:time('12:00:00'))` →
  `2024-07-15T12:00:00`; timezones must agree or `FORG0008`.
- Negative year in 5-digit range handled (`year-from-dateTime(xs:dateTime('-0001-01-01T00:00:00'))` → -1).
- QT3 `fn/year-from-dateTime` … test-sets > 80%; `fn/adjust-*` > 80%.

## P2-12 Sequence functions

- [ ] Done

**Scope.** F&O §15.1 and §15.2: `fn:empty`, `fn:exists`, `fn:index-of`,
`fn:distinct-values`, `fn:insert-before`, `fn:remove`, `fn:reverse`,
`fn:subsequence`, `fn:unordered`, `fn:zero-or-one`, `fn:one-or-more`,
`fn:exactly-one`, `fn:deep-equal`. Also `fn:id`, `fn:idref` (string-based
look-up on tree; attributes of type `xs:ID` / `xs:IDREF` only via DOCTYPE —
for untyped v1 we treat `@id` as the ID attribute by convention per
XPath 2.0 §15.5.2). Non-lazy functions use lazy iterators where safe
(`subsequence`, `reverse`, `insert-before`).

**Acceptance.**
- `empty(())` → true; `empty((1))` → false.
- `distinct-values((1, 1.0, "1"))` → `(1, "1")` (numeric 1 and 1.0 equal;
  "1" keeps as string — distinct-values uses `op:*-equal`).
- `index-of((1, 2, 3, 2), 2)` → `(2, 4)`.
- `subsequence((1,2,3,4,5), 2, 2)` → `(2, 3)`; lazy — a huge input with
  `subsequence(…, 1, 2)` reads 2 items.
- `deep-equal` recursively compares atomic values and node sub-trees per
  F&O §15.3.1 (atomization, type compare, structural equality for nodes).
- QT3 `fn/distinct-values`, `fn/subsequence`, `fn/reverse`,
  `fn/insert-before`, `fn/deep-equal` > 80%.

## P2-13 Regex functions

- [ ] Done

**Scope.** XPath 2.0 regex (F&O §7.6) with the W3C XML Schema regex dialect:
`fn:matches`, `fn:replace`, `fn:tokenize`. Implement an XPath-regex → .NET
regex translator (`XPathRegexTranslator`) handling:

- Character class subtraction (`[a-z-[aeiou]]`), including nested
- `\p{L}`, `\p{N}`, `\p{IsBasicLatin}` and other `IsX` block names
- `.` not matching `\n` by default; `.` not matching `\r` either without `s`
- Flags: `s` (dot-all), `m` (multiline), `i` (case-insensitive), `x` (comment)
- No lookaround (reject `(?=`, `(?!` → `FORX0002`)
- Backreference `\1`..`\9` supported; XPath regex allows `\0`? No — reject.

Regex engine is `System.Text.RegularExpressions.Regex` with the translated
pattern string; cache compiled regexes in `RunState` by translated pattern.

**Acceptance.**
- `matches("abc", "^[a-z]+$")` → true.
- `replace("abc", "b", "X")` → `"aXc"`.
- `tokenize("a,b,,c", ",")` → `("a", "b", "", "c")`.
- `matches("Hello", "hello", "i")` → true.
- `matches("a", "[a-z-[a]]")` → false (subtraction).
- `matches("A", "\p{Lu}")` → true (Unicode category).
- `matches("x", "(?=.)")` → raises `FORX0002` (lookahead unsupported).
- Zero-width `tokenize` pattern raises `FORX0003`.
- QT3 `fn/matches`, `fn/replace`, `fn/tokenize` > 80%.

## P2-14 QName and namespace functions

- [ ] Done

**Scope.** F&O §16: `fn:QName`, `fn:resolve-QName`, `fn:prefix-from-QName`,
`fn:local-name-from-QName`, `fn:namespace-uri-from-QName`,
`fn:namespace-uri-for-prefix`, `fn:in-scope-prefixes`, `fn:lang`.

**Acceptance.**
- `QName('http://example.com/ns', 'p:local')` → an `xs:QName` with those
  components.
- `resolve-QName('p:x', .)` resolves `p` via the element's in-scope namespaces
  (uses the node's namespace nodes; implicit `xml`).
- `prefix-from-QName(QName('http://x', 'foo'))` → empty sequence (no prefix).
- `in-scope-prefixes(.)` returns every prefix in the node's in-scope
  namespaces, including `xml`.
- `lang("en", .)` — true when the nearest ancestor-or-self with `xml:lang`
  starts with `en` (case-insensitive), per F&O §13.
- QT3 `fn/QName`, `fn/resolve-QName`, `fn/lang` > 80%.

## P2-15 Gate G2 run and baseline bump

- [ ] Done

**Scope.** Switch the conformance harness from parse-only to full evaluation
mode: for each in-scope QT3 test, build a `DynamicContext` with the test's
`<environment>` bindings and compare the result against the expected
`<assert-*>` or `<error>`. Add a result-comparison module that handles
`assert-eq`, `assert-deep-eq`, `assert-type`, `assert-string-value`,
`assert-true`, `assert-false`, `assert-empty`, `assert-count`,
`assert-permutation`, `any-of`, `all-of`, and `error code="…"`.
Update `tests/XTransform.Conformance/baseline.json` with the measured
pass-rate. Expand `known-failures/g1.txt` → `g2.txt` as needed and link both.
Bump `Directory.Build.props` to `0.2.0-alpha.1`.

**Acceptance.**
- Overall QT3 pass-rate ≥ 85% on `main`; `fn`, `op`, `prod` each ≥ 80%.
- Summary at `artifacts/conformance/qt3-summary.md` attached to the PR.
- `eng/check-conformance-baseline.sh` enforces the new baseline in CI.
- Every miss listed in `tests/XTransform.Conformance/known-failures/g2.txt`
  with a reason.
- Version bumped and reflected in any existing CLI `--version` output.
