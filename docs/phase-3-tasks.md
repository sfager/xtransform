# Phase 3 — XSLT core: task list

Goal: a working XSLT 2.0 engine that can compile a stylesheet and execute it
against an input document to produce serialized XML or text output, enough to
clear **Gate G3** — ≥ 40% of in-scope XSLT tests passing and five real-world
stylesheets producing byte-identical output to a reference run
([decisions.md D1](decisions.md#d1-phase-gates-proposed)).

Phase 3 covers the stylesheet reader, the XSLT compiler, the push-based
instruction runtime, template dispatch, global variables/params/functions/keys,
and the `xml` and `text` serializers. It does **not** include grouping,
`xsl:analyze-string`, the `html` / `xhtml` emitters, `xsl:result-document`,
`xsl:number`, picture-string formatting, `xsl:sort` or backwards-compatibility
mode — those are Phase 4. It also does not implement schema-aware features
(decisions.md D2 scope exclusions).

How to work through this list:

- One task = one branch (`p3-NN-short-name`) = one pull request. Do them in
  order unless the "Depends on" column allows otherwise.
- Signatures come from [interfaces.md](interfaces.md). Behaviour comes from the
  specs (links in [CLAUDE.md](../CLAUDE.md#specifications)); quote the section
  number in a code comment wherever an algorithm follows the spec closely.
- Every task's acceptance criteria include the standing ones: builds with zero
  warnings on all target frameworks, all tests pass, new public members
  recorded in `PublicAPI.Unshipped.txt`, XML doc comments on every public
  member.
- Tick the box here in the same PR that completes the task.
- Each task that changes the XSLT pass-rate measurement bumps
  `tests/XTransform.Conformance/xslt-baseline.json` in the same PR (never silently).

| ID | Task | Depends on |
| --- | --- | --- |
| P3-01 | Stylesheet reader — XML → `StylesheetAst`, namespaces, line info | Phase 2 complete |
| P3-02 | Attribute value templates, `use-when`, `xml:space` | P3-01 |
| P3-03 | Import / include resolution, import precedence | P3-01 |
| P3-04 | XSLT compiler — declaration collection and name binding | P3-03, P3-02 |
| P3-05 | Mode table and template-rule compilation (uses P1-13 patterns) | P3-04 |
| P3-06 | `IResultReceiver` chain and `TreeBuilder` for temporary trees | Phase 2 complete |
| P3-07 | Core control-flow instructions: `xsl:template`, `xsl:apply-templates`, `xsl:call-template`, `xsl:if`, `xsl:choose`, `xsl:for-each` | P3-05, P3-06 |
| P3-08 | Variables and params: `xsl:variable`, `xsl:param`, global variables with cycle detection | P3-04, P3-07 |
| P3-09 | Content-producing instructions: `xsl:value-of`, `xsl:text`, `xsl:sequence`, `xsl:copy`, `xsl:copy-of` | P3-07 |
| P3-10 | Constructor instructions: `xsl:element`, `xsl:attribute`, `xsl:comment`, `xsl:processing-instruction`, `xsl:document`, literal result elements | P3-07 |
| P3-11 | `xsl:function`, `xsl:key` (with lazy per-document index), `fn:key` / `fn:id` / `fn:document` | P3-08 |
| P3-12 | `CancellationToken` plumbing — poll on iterator loop back-edges and template entry; retire the P2-15 watchdog | Phase 2 complete |
| P3-13 | `SequenceNormalizer` and `XmlEmitter` (xml output method) | P3-06 |
| P3-14 | `TextEmitter` (text output method) and `xsl:output` wiring | P3-13 |
| P3-15 | Public API: `XsltCompiler` / `XsltExecutable` / `XsltTransformer` | P3-04 .. P3-14 |
| P3-16 | XSLT conformance harness (full-eval, xml + text methods) with D2 filter | P3-15 |
| P3-17 | Real-world stylesheet corpus (5 stylesheets, byte-identical check) | P3-15 |
| P3-18 | Gate G3 run, baseline, NuGet preview `0.3.0-preview.1` | P3-16, P3-17 |

---

## P3-01 Stylesheet reader

- [x] Done

**Scope.** `StylesheetReader.Read(XmlReader, Uri) → StylesheetModule`:
load a stylesheet into a lightweight AST that keeps line/column info and the
set of in-scope namespaces per element. Recognise XSLT elements by their
namespace URI (`http://www.w3.org/1999/XSL/Transform`); every other element is
either a literal result element or an extension element. The reader does **not**
resolve `xsl:include` / `xsl:import` (P3-03), compile AVTs (P3-02), or
evaluate `use-when` (P3-02).

**Acceptance.**
- DTD processing off, line info on; every `StylesheetNode` carries a
  `SourceSpan` with `file(line,col)` → `file(line,col)` resolution.
- `xsl:stylesheet` / `xsl:transform` with any other `version` than `1.0` /
  `2.0` records a warning (`XTSE0150` deferred until compiler) but reads.
- Only `xsl:` instructions declared for XSLT 2.0 are recognised; unknown
  `xsl:*` elements raise `XTSE0805` (forwards-compatible processing) only if
  `version="1.0"` is set, otherwise `XTSE0805` straight away.
- Foreign-namespace children inside `xsl:stylesheet` are tolerated when they
  bear a namespace URI; those without cause `XTSE0130`.
- Table-driven test over 20+ small stylesheet fixtures in
  `tests/XTransform.Tests/Xslt/ReaderCases/` verifies the resulting AST shape.

## P3-02 Attribute value templates, `use-when`, `xml:space`

- [x] Done

**Scope.** Split every attribute value on an XSLT instruction (and every
attribute on a literal result element that is not a namespace declaration) into
a list of literal and expression parts honouring `{{` / `}}` escapes; raise
`XTSE0370` on an unmatched brace. Evaluate `[xsl:]use-when` against a static
context that exposes `system-property`, `function-available` and
`element-available` only — no user variables (XSLT 2.0 §3.12). Apply
`xml:space="preserve"` and whitespace stripping per XSLT 2.0 §4.4 (strip
whitespace-only text nodes unless the parent's effective strip-space rule
preserves it).

**Acceptance.**
- `href="{$base}/{$path}"` splits into three parts (expr, literal, expr).
- `{{literal}}` becomes the literal text `{literal}`; `}}` without a matching
  `{{` raises `XTSE0370`.
- `use-when="system-property('xsl:version') = '2.0'"` keeps the element;
  `use-when="false()"` removes it from the AST before compilation.
- A user variable inside `use-when` raises `XPST0008` (no dynamic context).
- Whitespace-only text between instructions is stripped; whitespace inside
  `xsl:text` is preserved.

## P3-03 Import / include resolution

- [x] Done

**Scope.** Recursively resolve `xsl:include` (same precedence as parent) and
`xsl:import` (new, lower precedence) through `IResourceResolver`. Detect
cycles (`XTSE0210` for include, `XTSE0210` for import). Assign each loaded
module a precedence number used in P3-04.

**Acceptance.**
- A three-module chain (`a imports b, b imports c`) produces precedences
  `a > b > c`; a module included at each level shares its parent's precedence.
- Circular import raises `XTSE0210` naming both ends of the cycle.
- `IResourceResolver.Open` returning `null` raises `XTSE0165`.
- Base URIs resolve relative to the including module's URI, not the top one.

## P3-04 Declaration collection and name binding

- [x] Done

**Scope.** First compiler pass over the merged module tree: collect every
top-level declaration (templates, named templates, modes, global variables and
params, `xsl:function`s, keys, attribute sets, decimal formats, output
definitions, character maps, namespace aliases). Resolve import precedence and
detect duplicates per spec (`XTSE0630` global variables, `XTSE0660` templates,
`XTSE0770` keys, `XTSE1040` decimal formats, `XTSE1580` character maps).
Bind every `QName` reference to a declaration or raise the appropriate
`XPST*` / `XTSE*` code.

**Acceptance.**
- Two global `xsl:variable`s with the same name at the same precedence raise
  `XTSE0630`; at different precedences the higher wins silently.
- A reference to an undeclared global variable raises `XPST0008`; to an
  undeclared mode raises `XTSE0680`.
- An `xsl:function` with a non-prefixed name raises `XTSE0740`.
- The compiler emits every static error (`DiagnosticBag`) and throws one
  `XsltStaticException` listing them all (per [D3](decisions.md#d3-error-model)).

## P3-05 Mode table and template-rule compilation

- [x] Done

**Scope.** For each mode, compile every matching `xsl:template` into a
`TemplateRule` (pattern, body, params, priority, precedence). Split union
patterns into one rule each (XSLT 2.0 §6). Compute default priority from the
pattern (reuses P1-13 output). Build a per-mode `ModeTable` indexed by node
kind and `NamePool` code, each bucket sorted by `(precedence desc, priority
desc, document-order desc)` so the first match wins. Patterns are matched
bottom-up (test the node, then its parent) per design.md.

**Acceptance.**
- `<xsl:template match="a | b">` produces two rules, both with the specified
  priority (`XSLT 2.0 §6.4`).
- Two rules of equal precedence and priority: the later one wins, and a
  `XTRE0540` diagnostic is recorded (off by default, on with
  `TreatWarningsAsErrors`).
- Pattern index: `match="foo"` lands in the element bucket for the `foo`
  name code only; `match="node()"` lands in a catch-all bucket for every kind.
- 50+ patterns from XSLT 2.0 §6.4's default-priority table verified in a
  golden test.

## P3-06 `IResultReceiver` chain and temporary trees

- [x] Done

**Scope.** Finalise the `IResultReceiver` interface already used by
`TinyTreeBuilder` (P1-08). Add `SequenceNormalizer` scaffolding (full rules in
P3-13), a `MultiplexReceiver` that fans out to several sinks, and a
`TreeBuilder` receiver that produces a `TinyTree` from events (used for
variable bodies and parameter values of type `node()`).

**Acceptance.**
- Round-trip: build a `TinyTree` via `TreeBuilder` events, then re-emit it into
  another `TreeBuilder` — the two trees are `deep-equal`.
- `MultiplexReceiver` forwards every event to every sink in registration
  order; closing it closes each sink exactly once.
- A `TreeBuilder` used as the sink of a child context captures the
  constructed sub-tree; the parent context sees `XdmNode` wrappers over that
  tree, not events.

## P3-07 Core control-flow instructions

- [x] Done

**Scope.** `xsl:template`, `xsl:apply-templates`, `xsl:call-template` (with
`<xsl:with-param>`), `xsl:if`, `xsl:choose` / `xsl:when` / `xsl:otherwise`,
`xsl:for-each`. Each instruction is an `XsltInstruction` subclass with
`Execute(DynamicContext, IResultReceiver)`. Template dispatch uses the
`ModeTable` from P3-05 and the pattern matcher. Built-in rules apply when no
match is found (XSLT 2.0 §6.6): element → recurse into children; text /
attribute → copy string value; comment / PI / document → recurse.

**Acceptance.**
- `apply-templates select="child::foo"` matches the right template; with no
  match, the built-in rule fires.
- `call-template` passes `with-param` values to matching `xsl:param`
  declarations; missing required params raise `XTDE0700`.
- Recursion depth limit (`MaxRecursionDepth` on `XsltTransformer`) raises
  `XTDE0810` with the template stack.
- `xsl:for-each` sets the focus (context item, position, size) correctly for
  each iteration; `position()` and `last()` inside work.
- `xsl:choose` evaluates branches lazily — only the taken branch's body runs.
- Golden tests: 20+ small transformations covering every instruction,
  comparing serialized output byte-for-byte against pre-rendered expectations.

## P3-08 Variables, params, globals

- [x] Done

**Scope.** `xsl:variable` and `xsl:param` at every level: when `select` is
present, evaluate lazily; when a body is present, build a temporary tree
(P3-06). Global variables / params evaluate once per run on first use, cached
in `RunState`, with cycle detection (`XTDE0640`). External parameter values
set via `XsltTransformer.SetParameter` override the default.

**Acceptance.**
- `<xsl:variable name="x" select="1 + 2"/>` binds `$x` to `3`; shadowing
  within a nested block works per scope rules.
- A variable whose body contains elements produces an `XdmNode` wrapping a
  document node whose children are the constructed content.
- `XsltTransformer.SetParameter` on a non-existent global parameter raises
  `ArgumentException` (API misuse, not an XSLT error).
- Global-variable cycle `$a → $b → $a` raises `XTDE0640` with both names.
- Required global param with no external value raises `XTDE0050`.

## P3-09 Content-producing instructions

- [x] Done

**Scope.** `xsl:value-of` (string value of sequence, separated by
`@separator` default single space), `xsl:text` (literal with `@disable-output-escaping`
tracked but **not honoured** in v1 — raise a warning), `xsl:sequence`
(push atomic values and existing nodes without copying), `xsl:copy`
(copy the context node shallowly), `xsl:copy-of` (deep copy with namespace
fixup, F&O §15.1.6).

**Acceptance.**
- `value-of select="(1, 2, 3)" separator=","` emits `1,2,3` to the
  receiver as a single text event.
- `sequence select="."` passes the existing node along as an `Append(XdmItem)`
  event (no copy); `copy-of select="."` emits a start/end-element pair.
- `xsl:copy` on an attribute node emits an `Attribute` event, not a text one.
- Namespace fixup on `copy-of` adds every in-scope namespace that is used by
  the copied subtree but not already in scope at the destination.

## P3-10 Constructor instructions

- [x] Done

**Scope.** `xsl:element` (name is an AVT; namespace resolved per XSLT 2.0
§11.2), `xsl:attribute` (ditto), `xsl:comment`, `xsl:processing-instruction`,
`xsl:document` (pushes `StartDocument` / `EndDocument`), and literal result
elements (every non-XSLT element in the template body, with its literal
attributes and expression AVTs). Attribute-set references (`[xsl:]use-attribute-sets`)
emit their contents before the element's own attributes.

**Acceptance.**
- `<xsl:element name="{$tag}" namespace="{$ns}">` produces an element in the
  resolved namespace.
- `<p xsl:use-attribute-sets="s">` emits the attributes declared in
  attribute set `s` before any attribute explicitly on `<p>`; later wins.
- An attribute emitted after a child element raises `XTDE0410` and the
  attribute is dropped (don't abort the whole transformation).
- `xsl:comment` body containing `--` or ending in `-` raises `XTDE0450`.
- Golden test: literal result element with AVT attributes and nested
  `xsl:value-of` matches pre-rendered XML.

## P3-11 `xsl:function`, `xsl:key`, resource functions

- [x] Done

**Scope.** `xsl:function` compiles into a `UserDefinedFunction` registered in
the compiled stylesheet's `FunctionLibrary` (reuses the P2-08 abstraction).
`xsl:key` definitions produce a per-key spec; the index for each
`(document, key name)` pair is built lazily on first `fn:key` call, stored on
the `TinyTree` instance. Implement `fn:key`, `fn:id` / `fn:idref` against the
current document, `fn:document` through `IResourceResolver` with the per-run
document cache, `fn:doc` and `fn:doc-available`.

**Acceptance.**
- `<xsl:function name="my:double"><xsl:param name="x"/><xsl:sequence
  select="$x * 2"/></xsl:function>` is callable as `my:double(21)` → `42`.
- `key('by-id', 'x')` returns the node(s) whose key value is `'x'`; repeated
  calls on the same document reuse the index.
- `fn:document('other.xml')` goes through `IResourceResolver`; the default
  resolver denies non-`file://` URIs.
- `fn:doc-available` never throws — missing / unreadable → `false`.

## P3-12 Cancellation on loop back-edges and template entry

- [x] Done

**Scope.** Call `DynamicContext.CheckCancellation()` at every iterator
loop back-edge (`for`, `xsl:for-each`, path step, filter) and at the top of
every template / `xsl:function` invocation, per
[interfaces.md](interfaces.md#evaluation) and the P2-01 promise. Retire the
5-second watchdog in the conformance runner (added in P2-15) once the
cancellation token actually stops a runaway stylesheet.

**Acceptance.**
- A stylesheet that spins in a `for` loop and a `CancellationToken` that
  cancels after 100 ms raises `OperationCanceledException` within 150 ms.
- Every one of the 30 watchdog-timeout entries in
  `tests/XTransform.Conformance/known-failures/g2.txt` either passes or
  moves its reason away from "watchdog".
- Benchmarks (`XTransform.Benchmarks`): adding `CheckCancellation` to hot
  iterators costs < 3% on the "evaluate 100,000 integers" micro-benchmark.

## P3-13 `SequenceNormalizer` and `XmlEmitter`

- [x] Done

**Scope.** `SequenceNormalizer` implements XSLT 2.0 §5.6.3: adjacent atomic
values join with a single space, text nodes with empty string values are
dropped, attributes and namespaces placed before any child emits raise
`XTDE0410` / `XTDE0420`. `XmlEmitter` writes to a `TextWriter` honouring
`encoding`, `indent`, `omit-xml-declaration`, `standalone`,
`doctype-public`, `doctype-system`, `cdata-section-elements`,
`byte-order-mark`.

**Acceptance.**
- `<xsl:sequence select="(1, 2, 'x')"/>` as the entire result produces the
  text `1 2 x` (normalizer joined atomics).
- `cdata-section-elements="code"` wraps the text content of every emitted
  `code` element in `<![CDATA[…]]>`, splitting on `]]>` per spec.
- `indent="yes"` indents element content only where it contains no
  significant text (mixed content is left alone).
- `omit-xml-declaration="yes"` and `method="xml"` produces no XML
  declaration; with `omit-xml-declaration="no"` and `encoding="utf-16"`,
  emit `<?xml version="1.0" encoding="UTF-16"?>` and a BOM.

## P3-14 `TextEmitter` and `xsl:output` wiring

- [x] Done

**Scope.** `TextEmitter` writes only the string values of text nodes to a
`TextWriter` (no markup). `xsl:output` instructions merge by name (unnamed =
principal); the compiler builds one `XsltOutputSettings` per name. The public
API exposes these through `XsltExecutable.OutputSettings` and the chosen
`method` picks the emitter in the runtime.

**Acceptance.**
- `method="text"` emits only text; attributes, comments, PIs are dropped.
- `<xsl:output method="text" encoding="utf-8"/>` honoured; no BOM for
  utf-8 unless `byte-order-mark="yes"` set.
- Multiple `<xsl:output>` declarations merge per XSLT 2.0 §20 (attributes
  with higher precedence win; equal precedence is an error only for
  conflicting values).

## P3-15 Public API wiring

- [x] Done

**Scope.** Implement `XsltCompiler`, `XsltExecutable`, `XsltTransformer`
exactly as signed in [interfaces.md Public API](interfaces.md#public-api).
`XsltCompiler.Compile` runs the full pipeline (reader → compiler → runtime
tree); `XsltExecutable.CreateTransformer` is cheap; `XsltTransformer.Transform`
accepts `Stream` / `XmlReader` / `XNode` input and `Stream` / `TextWriter` /
`XmlWriter` output. All new public members land in
`PublicAPI.Unshipped.txt`. XML doc comments on every public member.

**Acceptance.**
- End-to-end test: compile a 10-line stylesheet, transform a tiny input, and
  compare the output to a canonical string.
- `XsltCompiler.Compile` with a stylesheet containing three static errors
  throws one `XsltStaticException` listing all three.
- Thread-safety: 8 threads reusing one `XsltExecutable` run 1,000
  transformations each without crosstalk; outputs are identical.
- `XsltTransformer.TransformAsync` honours a `CancellationToken` that trips
  within 250 ms of a cancel (relies on P3-12).

## P3-16 XSLT conformance harness

- [ ] Done

**Scope.** Parallel to P2-15's QT3 harness, add an NUnit fixture that walks
the xslt30-test catalog (pinned by [decisions.md D2](decisions.md#d2-w3c-test-suites-pins-and-filters)),
applies the D2 XSLT filter, runs each test through `XsltCompiler` +
`XsltTransformer` and compares the serialized result to the expected output
using the shared `Qt3ResultComparer` (XSLT uses the same assertion elements).
Known-failures file `known-failures/xslt-g3.txt`. Summary at
`artifacts/conformance/xslt-summary.md`.

**Acceptance.**
- Catalog namespace handling for `http://www.w3.org/2012/10/xslt-test-catalog`.
- Spec filter includes `XSLT20`, `XSLT20+`, `XSLT10+` or absent; exclusions
  match D2.
- Known-failures list skips entries with reasons; summary separates XSLT
  misses from parse-only ones.
- Per-area breakdown (`insn`, `fn`, `attr`, `expr`, `type`, `decl`, `misc`).

## P3-17 Real-world stylesheet corpus

- [ ] Done

**Scope.** Five real stylesheets chosen by Stephen (e.g. DocBook-xsl
subsection, DITA Open Toolkit snippet, an internal integration stylesheet),
each with a tiny input XML and a pre-rendered expected output produced by a
reference processor. Lives under
`tests/XTransform.Tests/Stylesheets/RealWorld/<name>/{style.xsl,input.xml,expected.out}`.
Tests compare byte-for-byte; a `[Category("RegenerateRealWorld")]` test
regenerates expected outputs manually.

**Acceptance.**
- Five directories, each with its three files; test names list the stylesheet
  and the aspect being exercised.
- All five produce byte-identical output against the pre-rendered expectation
  (pass at G3 — until then the test is marked `[Explicit]` and excluded).
- `eng/check-realworld.sh` wraps the test selection for CI.

## P3-18 Gate G3 run and NuGet preview

- [ ] Done

**Scope.** Run the XSLT conformance harness in CI; record the baseline at
`tests/XTransform.Conformance/xslt-baseline.json`. Verify the five real-world
stylesheets pass. Extend `eng/check-conformance-baseline.sh` to also check the
XSLT summary. Bump `Directory.Build.props` to `0.3.0-preview.1`. Publish the
NuGet preview package.

**Acceptance.**
- Overall XSLT pass-rate ≥ 40% on `main`.
- Five real-world stylesheets produce byte-identical output on `main`.
- Baseline file enforced in CI; summary at
  `artifacts/conformance/xslt-summary.md` attached to the PR.
- `0.3.0-preview.1` package built; publish is a separate manual step
  (noted in the PR description, not automated here).
- Every miss listed in `tests/XTransform.Conformance/known-failures/xslt-g3.txt`
  with a reason.
