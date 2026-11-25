# XTransform repository instructions

## Source of truth

Read these before changing implementation or specifications:

- `docs/design.md` - architecture and pipeline stages
- `docs/decisions.md` - binding engineering decisions
- `docs/interfaces.md` - required core signatures
- `docs/phase-1-tasks.md` - current task order and acceptance criteria
- `CLAUDE.md` - repository-specific build, coding, testing, and workflow rules

When repository documents conflict, use `docs/decisions.md`, then `docs/interfaces.md`, then `docs/design.md`, then `CLAUDE.md`. If a conflict or real specification gap remains, stop and ask rather than guessing.

## Build and test

Requires the .NET 8 SDK or later.

```bash
dotnet restore
dotnet build
dotnet build --no-restore --configuration Release

# Unit tests; conformance tests are excluded
dotnet test --filter "TestCategory!=Conformance"
dotnet test --no-build --configuration Release --filter "TestCategory!=Conformance"

# Single test or fixture
dotnet test tests/XTransform.Tests --filter "Name~MethodName"
dotnet test tests/XTransform.Tests --filter "FullyQualifiedName~XPathParserTests"
dotnet test tests/XTransform.Tests --filter "Name~Range_OneToFive"

# Conformance suites
eng/fetch-test-suites.sh
# Windows:
pwsh eng/fetch-test-suites.ps1
dotnet test tests/XTransform.Conformance
```

CI runs unit tests on Ubuntu and Windows. On Windows the test project also targets `net462`; Linux primarily exercises `net8.0` and the libraries' `netstandard2.0` target. Conformance is fetched and cached in CI, defaults to parse-only mode, and writes reports under `artifacts/conformance/`. Set `XTRANSFORM_CONFORMANCE_MODE=full` for QT3 full-evaluation mode.

There is no separate lint command. Treat warnings as errors and run the build because analyzers and `.editorconfig` are enforced by MSBuild.

## Architecture

XTransform is a four-stage XSLT 2.0 / XPath 2.0 engine:

1. `XTransform.Xdm` provides XDM items and sequences, QName name codes, `TinyTree`, atomic values, cast rules, and spec error types.
2. `XTransform.XPath` lexes, parses, compiles, and evaluates XPath 2.0 expressions and patterns. Compiled expression nodes use pull iterators, while mutable focus and variable state live in `DynamicContext`/`RunState`.
3. `XTransform.Xslt` reads stylesheet modules, compiles declarations and template rules, and executes XSLT instructions. Compiled stylesheets are immutable; one transformer/run owns mutable state.
4. `XTransform.Serialization` normalizes result events and emits XML, HTML, XHTML, or text.

The public API projects sit above the engine: `XTransform` exposes the native API, `XTransform.Compat` provides the `XslCompiledTransform` compatibility shape, and `XTransform.Cli` provides the dotnet tool.

Project references are downward only:

`Xdm <- XPath <- Xslt -> Serialization -> Xdm`

`XTransform <- Xslt`; `XTransform.Compat` and `XTransform.Cli` depend on `XTransform`. Do not introduce a cycle or upward reference.

Compiled artefacts such as `CompiledStylesheet`, expression nodes, and `TinyTree` must remain immutable and shareable. Run-specific state belongs in dynamic context/run state. Names are interned through `NamePool`; nodes live in the purpose-built `TinyTree` rather than `XmlDocument` or `XDocument`.

## Specification rules

Behaviour comes from the W3C 2.0 specifications, not processor memory or XPath 3.x behaviour. XSLT 3.0-only features and schema-aware processing are out of scope for v1.

Where code follows an algorithm closely, cite the relevant specification section in a comment, for example `// XPath 2.0 §3.5.2`.

Test-suite pins live in `eng/test-suites.env`. The suites are downloaded into `.testsuites/` and unit tests must not read them. Copy fixture inputs needed by unit tests into `tests/XTransform.Tests/`.

## Code conventions

- Write library code against `netstandard2.0`; libraries target `net8.0`, `netstandard2.0`, and `net462`.
- Use `#if NET8_0_OR_GREATER` only for measured hot-path optimizations, with both branches covered.
- Polyfills come from PolySharp. Do not add hand-written polyfill types.
- Use file-scoped namespaces, explicit accessibility, braces, `_camelCase` private fields, `s_camelCase` private static fields, and `var` only when the type is obvious.
- Keep hot paths allocation-conscious: avoid LINQ, `params`, per-item closures/boxing, and string concatenation in loops for iterators, axis navigation, name tests, and comparisons.
- Count XPath strings as Unicode code points, not UTF-16 units. Use `StringComparison.Ordinal` unless a collation is involved.
- Do not use static mutable engine state, `TimeZoneInfo.Local`, or `DateTime.Now`; implicit timezone and current dateTime come from run/dynamic context.
- Public members require XML documentation and `PublicAPI.Unshipped.txt` entries. Everything is internal unless `docs/interfaces.md` explicitly defines it as public.

## Errors and tests

Follow `docs/decisions.md` D3:

- Static XPath errors and stylesheet diagnostics use the specification error model.
- Dynamic errors throw `XPathDynamicException` or `XsltDynamicException`.
- Error codes come only from `XPathErrors` or `XsltErrors`; do not add string literals at throw sites.
- Use `Try...` APIs for castable-style yes/no checks and never throw/catch internally for those paths.
- Standard .NET exceptions are for API misuse, not stylesheet or XPath input failures.

NUnit 4 tests use Arrange / Act / Assert comments and `MethodOrBehaviour_Scenario_ExpectedResult` names. Prefer `[TestCase]` / `[TestCaseSource]` and checked-in data for spec matrices. Assert exact error codes in error tests, for example `XPathErrors.XPTY0004`.

## Change boundaries

Ask before:

- adding, removing, or upgrading a package
- changing a public signature or a shipped public API entry
- changing pinned test-suite commits or gate thresholds
- adding a project or project reference
- choosing output behavior for an implementation-defined specification area

Before completing a parser/evaluator change, run the relevant unit and conformance coverage and record pass-rate changes if conformance was available. Public API changes require `PublicAPI.Unshipped.txt` updates and XML documentation.
