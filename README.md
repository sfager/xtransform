# xtransform
An educational C#/.NET project exploring the development of a custom XSLT 2.0 and XPath 2.0 transformation engine, with future plans for AI-assisted transformation development.

> **Status:** early development (phase 1 of 4: XDM data model and XPath parser). Not usable yet.

## What it is

XTransform is an XSLT 2.0 / XPath 2.0 engine written in C#. .NET's built-in
`XslCompiledTransform` stops at XSLT 1.0; XTransform aims to fill that gap with:

- XSLT 2.0 and XPath 2.0 (basic, non-schema-aware conformance), measured against the W3C test suites
- A compile-once, run-many API that feels native to .NET
- A backwards-compatible mode for XSLT 1.0 stylesheets, plus a drop-in `XslCompiledTransform` replacement
- `net8.0`, `netstandard2.0` and `net462` builds (.NET Framework 4.6.2+)
- A `xtransform` command-line tool

## Documentation

- [Design](docs/design.md) — architecture and how each stage works
- [Decisions](docs/decisions.md) — phase gates, test suites, error model, versioning
- [Core interfaces](docs/interfaces.md)
- [Phase 1 tasks](docs/phase-1-tasks.md)

## Building

Requires the .NET 8 SDK or later.

```bash
dotnet build
dotnet test --filter "TestCategory!=Conformance"
```

Conformance tests need the W3C suites (about 550 MB, not stored in this repo):

```bash
eng/fetch-test-suites.sh          # or: pwsh eng/fetch-test-suites.ps1
dotnet test tests/XTransform.Conformance
```

## License

[MIT](LICENSE)
