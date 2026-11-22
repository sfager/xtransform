# Phase 1 — Foundation: task list

Goal: the XDM layer and the XPath 2.0 front end (lexer + parser), tested and measured
against QT3. Phase 1 ends when **Gate G1** passes ([decisions.md D1](decisions.md#d1-phase-gates-proposed)).

How to work through this list:

- One task = one branch (`p1-NN-short-name`) = one pull request. Do them in order unless
  the "Depends on" column allows otherwise.
- Signatures come from [interfaces.md](interfaces.md). Behaviour comes from the specs
  (links in [CLAUDE.md](../CLAUDE.md#specifications)); quote the section number in a code
  comment wherever an algorithm follows the spec closely.
- Every task's acceptance criteria include the standing ones: builds with zero warnings on
  all target frameworks, all tests pass, new public members recorded in
  `PublicAPI.Unshipped.txt`, XML doc comments on every public member.
- Tick the box here in the same PR that completes the task.

| ID | Task | Depends on |
| --- | --- | --- |
| P1-01 | Names and source locations | — |
| P1-02 | Errors and diagnostics | P1-01 |
| P1-03 | Atomic type hierarchy | — |
| P1-04 | Payload structs: decimal, duration, date/time | P1-02 |
| P1-05 | `XdmAtomicValue` | P1-03, P1-04 |
| P1-06 | Cast table | P1-05 |
| P1-07 | Items, sequences, iterators | P1-05 |
| P1-08 | TinyTree storage and builder | P1-01, P1-07 |
| P1-09 | Axis navigation, node tests, `XdmNode` | P1-08 |
| P1-10 | Sequence types | P1-07, P1-09 |
| P1-11 | XPath lexer | P1-02 |
| P1-12 | XPath AST and expression parser | P1-11, P1-10 |
| P1-13 | XSLT pattern parser | P1-12 |
| P1-14 | Conformance harness (QT3, parse-only mode) | P1-12 |
| P1-15 | Gate G1 run and CI job | P1-13, P1-14 |

---

## P1-01 Names and source locations

- [x] Done

**Scope.** `QName`, `NamePool`, `SourceSpan` in `XTransform.Xdm`.

**Acceptance.**
- `QName` equality ignores prefix; `ParseClark` round-trips `ClarkName`; null arguments throw `ArgumentNullException`.
- `NamePool` returns the same code for the same (uri, local) from many threads at once (test with `Parallel.For`, 8 threads × 10,000 names); codes are dense from 0.
- `SourceSpan.ToString()` formats as in interfaces.md.

## P1-02 Errors and diagnostics

- [x] Done

**Scope.** Exception hierarchy, `Diagnostic`, `DiagnosticBag`, `XPathErrors`, `XsltErrors` (start with the codes listed in interfaces.md; add more as later tasks need them).

**Acceptance.**
- Every exception exposes `ErrorCode` as a QName in the `http://www.w3.org/2005/xqt-errors` namespace.
- `Diagnostic.ToString()` produces `file(line,col): error CODE: message`.
- `XsltStaticException` built from several diagnostics reports the first one's code and location.

## P1-03 Atomic type hierarchy

- [x] Done

**Scope.** `XdmTypeCode` and the derivation tree: `CastTable.PrimitiveTypeOf`, `CastTable.IsSubtypeOf`, plus a name ↔ code map for `xs:` names (used by the parser for `cast as`, `instance of`).

**Acceptance.**
- Table-driven tests cover every type's primitive and every direct parent (XSD 1.0 Part 2 §3 built-in type hierarchy).
- Unknown `xs:` names return "not found" (the parser raises `XPST0051`).

## P1-04 Payload structs: decimal, duration, date/time

- [x] Done

**Scope.** `XsdDecimal`, `XsdDuration`, `XsdDateTime` (see interfaces.md, Atomic types): lexical parsing per XSD 1.0, canonical output per F&O §17.1.2, equality and ordering per F&O §10.4 (date/time comparisons use the implicit timezone, which is a parameter, not `TimeZoneInfo.Local`).

**Acceptance.**
- Round-trip tests: parse → canonical string for every QT3 `xs:` constructor test in `prod/` and `xs/` (extract the literals into a test data file; do not read `.testsuites` from unit tests).
- Years outside 1–9999, negative years, `24:00:00`, fractional seconds to 18 digits, and timezones `±14:00` all work.
- No use of `System.DateTime` for storage.

## P1-05 `XdmAtomicValue`

- [x] Done

**Scope.** The class, all factories, `Parse`/`TryParse` for every v1 type, `GetStringValue`, typed accessors, value equality.

**Acceptance.**
- Whitespace handling per type (collapse vs preserve) matches XSD facets.
- `Boolean(true)` returns a cached instance; `Integer` chooses `long` or `BigInteger` automatically.
- Invalid lexical forms throw `XPathDynamicException` with `FORG0001`.

## P1-06 Cast table

- [x] Done

**Scope.** `CastTable.IsCastAllowed`, `Cast`, `TryCast`, `TryPromote` per F&O §17.1 (the full casting matrix).

**Acceptance.**
- One test per cell of the F&O casting table (Y, M and N cells), generated from a data file `CastMatrix.csv` checked into the test project.
- `TryCast` never throws for any input; `Cast` throws `XPTY0004` for N cells and `FORG0001` for invalid values.

## P1-07 Items, sequences, iterators

- [x] Done

**Scope.** `XdmItem`, `XdmSequence` (empty, singleton, array, lazy), `IXdmIterator` and basic iterators (array, single, empty, concatenation, mapping).

**Acceptance.**
- A lazy sequence over an infinite iterator answers `FirstOrNull()` without hanging (test with a 10-second timeout).
- `Count` on a lazy sequence reads its iterator once and memoises.
- `Clone()` on every iterator restarts from the beginning.

## P1-08 TinyTree storage and builder

- [x] Done

**Scope.** `TinyTree`, `TinyTreeBuilder` (implements `IResultReceiver`), `TinyTreeBuilder.Load(XmlReader)`. Whitespace-only text nodes are kept (stripping is the XSLT layer's job). DTD processing off.

**Acceptance.**
- Loading `.testsuites`-free fixtures (checked into `tests/XTransform.Tests/Fixtures/`) of 1, 100 and 100,000 elements; memory per element under 64 bytes excluding text (measure with `GC.GetAllocatedBytesForCurrentThread`).
- String value of the document node equals the concatenated text of the input.
- Adjacent text from the builder is merged into one node.

## P1-09 Axis navigation, node tests, `XdmNode`

- [x] Done

**Scope.** All 13 axes over `TinyTree`, `NodeTest` (kind tests, name tests, `*`, `prefix:*`, `*:local`), `XdmNode` public wrapper, document-order comparison across trees.

**Acceptance.**
- For a fixture document, every axis from every node returns the same nodes, in the same order, as an independent oracle written with `System.Xml.Linq` in the test project.
- Reverse axes return nodes in reverse document order from the iterator; the `Position` of each matches XPath's proximity position.
- `CompareDocumentOrder` puts attributes after their element and before its children.

## P1-10 Sequence types

- [ ] Done

**Scope.** `ItemType`, `Occurrence`, `SequenceType` with `Matches` and `IsSubtypeOf` (XPath 2.0 §2.5.4, without schema types).

**Acceptance.** Table-driven tests for `instance of` semantics covering every occurrence indicator and every kind test, including `element(name)`, `attribute(*)` and `document-node(element(x))`.

## P1-11 XPath lexer

- [ ] Done

**Scope.** `TokenKind`, `Token`, `XPathLexer` per XPath 2.0 Appendix A.2 (lexical structure), including operand/operator state for `*`, `div`, `-` and keyword names, nested comments, string escapes (`""` and `''`), and numeric literal forms.

**Acceptance.**
- Tests for every row of A.2.2's terminal-delimitation rules.
- `a-b` is one name; `a - b` is three tokens; `child::div div 2` lexes `div` as a name then an operator.
- Error tokens carry exact offsets; the exception's `SourceSpan.Start` points at the bad character.

## P1-12 XPath AST and expression parser

- [ ] Done

**Scope.** AST records (see interfaces.md) and `XPathParser.ParseExpression`, one method per grammar production in XPath 2.0 Appendix A.1, including the leading-`/` rule and `xgs:` constraints.

**Acceptance.**
- A pretty-printer `XPathAst.ToDebugString()` used by golden tests: 200+ expressions with expected trees in `tests/XTransform.Tests/XPath/ParserCases.txt`.
- Precedence tests for every adjacent pair of levels in the ladder in design.md.
- Undeclared prefixes raise `XPST0081`; syntax errors raise `XPST0003` with the offending span.

## P1-13 XSLT pattern parser

- [ ] Done

**Scope.** `XPathParser.ParsePattern` for the XSLT 2.0 pattern grammar (§5.5.2), including `id()` and `key()` patterns, unions, and default-priority calculation per §6.4.

**Acceptance.** Default priorities match the spec table for 30+ patterns; patterns that are valid expressions but invalid patterns (e.g. `../x`, `following::x`) raise `XTSE0340`.

## P1-14 Conformance harness (QT3, parse-only mode)

- [ ] Done

**Scope.** `XTransform.Conformance`: catalog and test-set reader for QT3 applying the D2 filter; NUnit `TestCaseSource` per in-scope test, grouped by test set; a `parse-only` mode that checks only "parses" vs "expects XPST0003"; a summary report (`artifacts/conformance/qt3-summary.md`) with counts and pass-rate per area; a known-failures file whose entries are skipped with a reason.

**Acceptance.**
- Running with no `.testsuites/` directory marks all conformance tests `Ignored` with a message pointing to `eng/fetch-test-suites.sh`, and does not fail the build.
- The reported in-scope count matches D1's reference size within 1% (investigate and update D1 if not).
- Tests are tagged `[Category("Conformance")]` so `dotnet test --filter TestCategory!=Conformance` skips them.

## P1-15 Gate G1 run and CI job

- [ ] Done

**Scope.** Fix parser gaps until G1's numbers are met; add a CI job that fetches the suites (cached by commit), runs the conformance project and uploads the summary as an artifact; fail the job if the pass-rate drops below the last recorded value in `tests/XTransform.Conformance/baseline.json`.

**Acceptance.** G1 criteria met on `main`; summary attached to the PR; version bumped to `0.1.0-alpha.1`.
