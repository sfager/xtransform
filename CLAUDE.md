# CLAUDE.md — XTransform

XTransform is an open-source (MIT) XSLT 2.0 / XPath 2.0 transformation engine for .NET.
Owner: Stephen Fager. Repository: https://github.com/sfager/xtransform.

## Read first

| File | What it gives you |
| --- | --- |
| [docs/design.md](docs/design.md) | Architecture, pipeline stages, what each project contains |
| [docs/decisions.md](docs/decisions.md) | Binding rules: phase gates, test-suite pins and filters, error model, versioning, target frameworks, dependencies |
| [docs/interfaces.md](docs/interfaces.md) | Signatures of the core types — implement these exactly |
| [docs/phase-1-tasks.md](docs/phase-1-tasks.md) | The current backlog, in order, with acceptance criteria |

When these documents and this file disagree, decisions.md wins, then interfaces.md, then
design.md. If you find a real conflict or a gap, stop and ask rather than guessing; if the
answer changes a document, update it in the same pull request.

## Specifications

Behaviour comes from the W3C specifications, not from memory of other processors. Cite the
section number (e.g. `// XPath 2.0 §3.5.2`) in a comment where code follows an algorithm.

- XSLT 2.0 (Second Edition): https://www.w3.org/TR/2021/REC-xslt20-20210330/
- XPath 2.0 (Second Edition): https://www.w3.org/TR/2010/REC-xpath20-20101214/
- XQuery 1.0 and XPath 2.0 Functions and Operators (Second Edition): https://www.w3.org/TR/2010/REC-xpath-functions-20101214/
- XQuery 1.0 and XPath 2.0 Data Model (Second Edition): https://www.w3.org/TR/2010/REC-xpath-datamodel-20101214/
- XSLT 2.0 and XQuery 1.0 Serialization (Second Edition): https://www.w3.org/TR/2010/REC-xslt-xquery-serialization-20101214/
- XML Schema Part 2: Datatypes (for lexical forms and the type hierarchy): https://www.w3.org/TR/xmlschema-2/

Do not implement XPath 3.x / XSLT 3.0 behaviour. Where the 2.0 and 3.0 specs differ, 2.0
wins. Schema-aware features are out of scope for v1.

## Build and test

```bash
dotnet build                                                    # all projects, all target frameworks
dotnet test --filter "TestCategory!=Conformance"                # unit tests (fast)
dotnet test tests/XTransform.Tests --filter "Name~MethodName"  # single test or class
eng/fetch-test-suites.sh                                        # once: ~550 MB into .testsuites/ (git-ignored)
dotnet test tests/XTransform.Conformance                        # conformance suites (slow)
```

On Windows use `pwsh eng/fetch-test-suites.ps1`. Tests also run on `net462` on Windows.

## Repository layout

```
src/XTransform.Xdm             XDM types, NamePool, TinyTree, cast table, errors
src/XTransform.XPath           XPath lexer, parser, compiler, evaluator, functions
src/XTransform.Serialization   receivers and emitters
src/XTransform.Xslt            stylesheet reader, XSLT compiler and runtime
src/XTransform                 public API
src/XTransform.Compat          drop-in XslCompiledTransform replacement
src/XTransform.Cli             `xtransform` dotnet tool (net8.0)
tests/XTransform.Tests         unit tests (NUnit)
tests/XTransform.Conformance   W3C QT3 / XSLT suite harness
bench/XTransform.Benchmarks    BenchmarkDotNet
eng/                           scripts and test-suite pins
docs/                          design, decisions, interfaces, task lists
```

References only point downward: Xdm ← XPath ← Xslt → Serialization → Xdm; XTransform ← Xslt;
Compat and Cli ← XTransform. Never add a reference that points back up.

Central build config: `Directory.Build.props` (version prefix/suffix, common properties),
`Directory.Packages.props` (all NuGet version pins). `InternalsVisibleTo` in each src project
already covers the test, conformance, and benchmark projects.

## Coding conventions

- **Targets.** Libraries build for `net8.0`, `netstandard2.0` and `net462`. Write against
  the `netstandard2.0` API surface; `#if NET8_0_OR_GREATER` only for measured hot-path
  optimisations, with both branches tested. Polyfills come from PolySharp — never
  hand-write `IsExternalInit`, nullable attributes and the like.
- **Warnings are errors.** Analyzers run at `latest-recommended`. Do not suppress a
  warning without a one-line justification comment next to the suppression.
- **Style.** File-scoped namespaces; explicit accessibility on every member; braces always;
  private fields `_camelCase`; `var` only when the type is obvious. `.editorconfig` is
  enforced in the build.
- **Visibility.** Everything is `internal` unless interfaces.md marks it `public`. Public
  members need XML doc comments and an entry in that project's `PublicAPI.Unshipped.txt`.
- **Immutability.** Compiled artefacts (`CompiledStylesheet`, `ExpressionNode`s, `TinyTree`)
  are immutable after construction and safe to share across threads. All run state lives
  in `DynamicContext` / `RunState`.
- **Hot paths** (iterators, axis navigation, name tests, comparisons): no LINQ, no
  `params` arrays, no boxing of structs, no closures allocated per item, no string
  concatenation in loops. Elsewhere, clarity first.
- **Errors.** Follow decisions.md D3 exactly: spec error codes from the `XPathErrors` /
  `XsltErrors` constants, never string literals; `Try…` methods for `castable as` style
  checks; standard .NET exceptions only for API misuse by callers.
- **Strings.** XPath string functions count Unicode code points, not UTF-16 units —
  handle surrogate pairs. Compare with `StringComparison.Ordinal` unless a collation is
  involved.
- **No global state.** No static mutable fields, no `TimeZoneInfo.Local` or
  `DateTime.Now` inside the engine (the implicit timezone and current dateTime come from
  the dynamic context).

## Testing rules

- NUnit 4, Arrange / Act / Assert with the three sections marked by comments.
- Test names: `MethodOrBehaviour_Scenario_ExpectedResult`.
- Prefer table-driven `[TestCase]` / `[TestCaseSource]` with data files in the test
  project for anything spec-shaped (casts, lexer rules, parser golden cases).
- Every error path asserts the exact error code (`Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004))`),
  not just that something was thrown.
- Unit tests never read `.testsuites/`; copy the few inputs you need into the test
  project. Conformance tests live only in `XTransform.Conformance`.
- New behaviour comes with tests in the same pull request. Fixing a bug starts with a
  failing test.

## Definition of done (every change)

1. `dotnet build` passes with zero warnings on all target frameworks.
2. `dotnet test --filter "TestCategory!=Conformance"` passes.
3. If parser/evaluator behaviour changed: the conformance pass-rate did not drop (run the
   relevant suite area and say the before/after numbers in the PR description).
4. Public API changes are in `PublicAPI.Unshipped.txt` and described in the PR.
5. The task's checkbox in the phase task list is ticked; any doc that changed meaning is
   updated.

## Workflow

- Work one task from the current phase task list at a time, on a branch named
  `p<phase>-<id>-<short-name>` (e.g. `p1-04-payload-structs`).
- Keep commits focused; conventional prefixes are welcome (`feat:`, `fix:`, `test:`, `docs:`, `build:`).
- Do not push to `main` or merge pull requests yourself; open the PR and stop.

## Ask before you…

- add, remove or upgrade any package (decisions.md D7);
- change a `public` signature in interfaces.md or anything already in `PublicAPI.Shipped.txt`;
- change a pinned test-suite commit or a gate threshold;
- add a project or a project reference;
- implement something the specs leave implementation-defined in a way that affects output
  (note the choice in `docs/implementation-defined.md`).
