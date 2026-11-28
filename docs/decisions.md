# Engineering decisions

Decisions that turn the [design](design.md) into rules the code must follow. Each entry
records what was decided and why. Change an entry only by editing it in a commit that
explains the change; never silently diverge from it in code.

Entries marked **Proposed** were drafted with the scaffold and still need Stephen's
sign-off; treat them as binding until changed.

---

## D1. Phase gates (Proposed)

A phase is done when its gate passes in CI on `main`. "In scope" means the test-case
filters in D2. Pass-rate = passed / in scope, counting expected-error tests as passed only
when the error code matches.

| Gate | Criteria |
| --- | --- |
| **G1 — Foundation** | 1. Every in-scope QT3 test expression either parses, or fails with `XPST0003` exactly when the test expects `XPST0003` (≥ 99% agreement; every miss listed in `tests/XTransform.Conformance/known-failures/g1.txt`). 2. The parser never throws anything other than `XPathException` on any QT3 or fuzz input. 3. Unit tests cover every XDM atomic type and every cast-table cell. |
| **G2 — XPath engine** | ≥ 85% of in-scope QT3 tests pass overall, and ≥ 80% in each of the `fn`, `op` and `prod` areas. |
| **G3 — XSLT core** | ≥ 40% of in-scope XSLT tests pass, and 5 real-world stylesheets (chosen by Stephen, stored under `tests/XTransform.Tests/Stylesheets/RealWorld/`) produce byte-identical output to a reference run. NuGet preview `0.3.0-preview.1` published. |
| **G4 — XSLT 2.0 complete** | ≥ 90% of in-scope XSLT tests pass; the XSLT 1.0 parity corpus (D6) matches `XslCompiledTransform` output or every difference is listed in `docs/compat-differences.md`. |

Reference sizes at the pinned commits (counted 2026-11-20 with the D2 filters): about
**15,300** in-scope QT3 tests (`prod` ≈ 5,900, `fn` ≈ 5,700, `op` ≈ 3,300) and about
**7,200** in-scope XSLT tests (`misc` ≈ 3,300, `insn` ≈ 940, `fn` ≈ 820, `type` ≈ 650,
`attr` ≈ 640, `expr` ≈ 580, `decl` ≈ 330). The harness prints the exact counts; use those.

## D2. W3C test suites: pins and filters

Pins live in [`eng/test-suites.env`](../eng/test-suites.env); `eng/fetch-test-suites.sh`
(or `.ps1`) clones them into `.testsuites/` (git-ignored, ~550 MB).

| Suite | Repository | Pinned commit |
| --- | --- | --- |
| QT3 (XPath/XQuery) | https://github.com/w3c/qt3tests | `201a6e466940cdfc727f4babfedcde5332b9f578` (2026-05-14) |
| XSLT | https://github.com/w3c/xslt30-test | `fddf1cf920087e791f13315d68dfbe874d97dc56` (2026-06-10) |

**QT3 filter.** A test case is in scope when its effective `spec` dependency (the test
case's own, otherwise its test set's) includes `XP20`, `XP20+` or `XP10+`, or when it has
no `spec` dependency at all. Exclude tests whose `feature` dependencies include
`schemaImport`, `schemaValidation`, `staticTyping`, `namespace-axis` or `moduleImport`.
Catalog namespace: `http://www.w3.org/2010/09/qt-fots-catalog`; dependencies are
`<dependency type="spec" value="XP20+ XQ10+"/>` (space-separated tokens).

**XSLT filter.** In scope when the effective `<dependencies><spec value="..."/>` includes
`XSLT20`, `XSLT20+` or `XSLT10+`, or there is none. Exclude `feature` values
`schema_aware`, `XSD_1.1`, `streaming`, `higher_order_functions`, `dynamic_evaluation`.
`backwards_compatibility` and `serialization` tests are **in** scope. Catalog namespace:
`http://www.w3.org/2012/10/xslt-test-catalog`.

A dependency with `satisfied="false"` inverts its meaning (the test applies only when the
feature is *absent*); honour it. This covers `year_component_values` as well as `feature`: a test whose
dependency is `year_component_values` with `satisfied="false"` for "support year above 9999" or "support negative year"
expects an overflow error from a processor that lacks that support, and XTransform supports both, so it is out of
scope.

An `on-multiple-match` dependency with value `error` is out of scope: it describes a processor that raises XTRE0540,
and XTransform recovers (docs/implementation-defined.md, XSLT 2.0 §6.4). Tests that expect recovery stay in.

Update a pin only in a dedicated commit that records the before/after pass-rates.

## D3. Error model

XPath 2.0 and XSLT 2.0 have no try/catch, so every dynamic error ends the evaluation.
Throwing is therefore the right model and costs nothing on the success path.

- One exception hierarchy, rooted at `XTransformException`, carrying an
  `XdmErrorCode` (the error QName, e.g. `err:XPTY0004`), a message, an optional
  `SourceSpan` and, at runtime, the XSLT template stack. Signatures in
  [interfaces.md](interfaces.md#errors).
- **Static errors** are collected, not thrown one at a time: the compiler records
  `Diagnostic`s in a `DiagnosticBag` and `XsltCompiler.Compile` throws one
  `XsltStaticException` listing all of them. The XPath parser stops at the first syntax
  error in an expression (`XPST0003`) but the stylesheet compiler continues with the next
  expression.
- **Dynamic errors** throw `XPathDynamicException` (XPath) or `XsltDynamicException`
  (XSLT-specific codes). `fn:error` throws `XPathDynamicException` with the user's code
  and error object.
- **Non-throwing paths** where the spec needs a yes/no answer: `castable as`,
  `fn:doc-available`, `unparsed-text-available`. These use `Try…` methods (`CastTable.TryCast`)
  and never throw-and-catch internally.
- Error codes are constants in one static class per namespace (`XPathErrors.XPTY0004`,
  `XsltErrors.XTSE0010`), never string literals at the throw site.
- Messages are English, start with a capital, name the offending value or construct, and
  do not end with a period. Example: `Cannot convert xs:string "abc" to xs:integer`.
- Never throw `InvalidOperationException`/`ArgumentException` for anything a stylesheet
  can cause; those are reserved for API misuse by .NET callers.

## D4. Versioning and public API

- **SemVer.** `0.x.y-alpha.N` until G2, `0.x.y-preview.N` from G3, `1.0.0` after G4 plus a
  stabilisation period. Minor version bumps at each gate (G1 → 0.1, G2 → 0.2, ...).
- `VersionPrefix`/`VersionSuffix` live in `Directory.Build.props`; CI appends the build
  number for pre-release packages.
- **Public API tracking.** Every src project uses `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
  New public members go in `PublicAPI.Unshipped.txt` (the build fails otherwise: RS0016).
  At each release, move entries to `PublicAPI.Shipped.txt`. Removing a shipped member
  before 1.0 is allowed but needs a line in `CHANGELOG.md`.
- Keep the public surface small: types are `internal` unless the public API section of the
  design or interfaces.md names them. `InternalsVisibleTo` already covers the test,
  conformance and benchmark projects.

## D5. Target frameworks and polyfills

- Libraries target `net8.0;netstandard2.0;net462`. Tests target `net8.0` everywhere and
  add `net462` on Windows. The CLI, conformance harness and benchmarks are `net8.0` only.
- Write code against the `netstandard2.0` surface. Use `#if NET8_0_OR_GREATER` only for
  measured hot-path optimisations, and keep both branches covered by tests.
- Compiler-feature polyfills (nullable attributes, `init`, `required`, `IsExternalInit`,
  `Index`/`Range`) come from PolySharp. Do not hand-write polyfill types.
- `string.Contains(char)`, `HashCode`, `Span<T>` APIs on strings and similar newer BCL
  members are not available on `netstandard2.0`; add small internal helpers in
  `XTransform.Xdm/Internal/Compat` when needed.

## D6. XSLT 1.0 parity corpus

- Lives in `tests/XTransform.Tests/Stylesheets/Xslt10Parity/`, one folder per case with
  `style.xsl`, `input.xml` and `expected.out` produced by `System.Xml.Xsl.XslCompiledTransform`.
- Expected outputs are regenerated only on Windows by a test marked
  `[Category("RegenerateParity")]`, run manually.
- Stephen supplies real integration stylesheets; synthetic cases cover each XSLT 1.0
  instruction and function.

## D7. Dependencies

Runtime dependencies are limited to `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`
on older targets. Adding any package — runtime, test or tooling — needs Stephen's approval
and an entry here. Versions are pinned centrally in `Directory.Packages.props`.
