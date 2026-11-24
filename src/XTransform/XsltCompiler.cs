// The public entry point for compiling stylesheets. Wraps the internal pipeline
// (reader -> processor -> linker -> compiler).

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform;

/// <summary>
/// Compiles XSLT 2.0 stylesheets into immutable <see cref="XsltExecutable"/>s. A compiler
/// instance is not thread-safe; the executables it produces are.
/// </summary>
public sealed class XsltCompiler
{
    private readonly XsltCompilerOptions _options;
    private IReadOnlyList<Diagnostic> _warnings = Array.Empty<Diagnostic>();

    /// <summary>Initializes a compiler with the given options (defaults when null).</summary>
    /// <param name="options">Compiler options, or <see langword="null"/> for defaults.</param>
    public XsltCompiler(XsltCompilerOptions? options = null)
    {
        _options = options ?? new XsltCompilerOptions();
        ValidateExtensions(_options);
    }

    /// <summary>Warnings produced by the most recent <c>Compile</c> call.</summary>
    public IReadOnlyList<Diagnostic> Warnings => _warnings;

    /// <summary>Compiles the stylesheet at <paramref name="stylesheetUri"/>.</summary>
    /// <param name="stylesheetUri">The absolute URI of the principal stylesheet module.</param>
    /// <returns>The compiled stylesheet.</returns>
    /// <exception cref="XsltStaticException">The stylesheet has static errors; all are listed.</exception>
    public XsltExecutable Compile(Uri stylesheetUri)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (stylesheetUri is null)
        {
            throw new ArgumentNullException(nameof(stylesheetUri));
        }
#pragma warning restore CA1510

        IResourceResolver resolver = ResolverOrDefault();
        return Build(() => StylesheetLinker.Link(stylesheetUri, resolver), resolver, stylesheetUri);
    }

    /// <summary>Compiles a stylesheet read from <paramref name="stylesheet"/>.</summary>
    /// <param name="stylesheet">The reader positioned at the stylesheet document.</param>
    /// <param name="baseUri">Base URI for relative includes/imports; falls back to <see cref="XsltCompilerOptions.BaseUri"/>.</param>
    /// <returns>The compiled stylesheet.</returns>
    /// <exception cref="XsltStaticException">The stylesheet has static errors; all are listed.</exception>
#pragma warning disable RS0026 // optional-parameter overloads are mandated by docs/interfaces.md
    public XsltExecutable Compile(XmlReader stylesheet, Uri? baseUri = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }
#pragma warning restore CA1510

        IResourceResolver resolver = ResolverOrDefault();
        Uri? effectiveBase = baseUri ?? _options.BaseUri;
        return Build(() => StylesheetLinker.Link(stylesheet, effectiveBase, resolver), resolver, effectiveBase);
    }

    /// <summary>Compiles a stylesheet read from <paramref name="stylesheet"/>.</summary>
    /// <param name="stylesheet">The reader positioned at the start of the stylesheet text.</param>
    /// <param name="baseUri">Base URI for relative includes/imports; falls back to <see cref="XsltCompilerOptions.BaseUri"/>.</param>
    /// <returns>The compiled stylesheet.</returns>
    /// <exception cref="XsltStaticException">The stylesheet has static errors; all are listed.</exception>
    public XsltExecutable Compile(TextReader stylesheet, Uri? baseUri = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (stylesheet is null)
        {
            throw new ArgumentNullException(nameof(stylesheet));
        }
#pragma warning restore CA1510

        Uri? effectiveBase = baseUri ?? _options.BaseUri;
        using XmlReader reader = XmlReader.Create(stylesheet, XsltInput.CreateReaderSettings(), effectiveBase?.OriginalString);
        return Compile(reader, effectiveBase);
    }

#pragma warning restore RS0026

    // ── Pipeline ───────────────────────────────────────────────────────────

    private XsltExecutable Build(Func<StylesheetAssembly> link, IResourceResolver resolver, Uri? location)
    {
        CompiledStylesheet compiled;
        try
        {
            StylesheetAssembly assembly = link();
            compiled = StylesheetCompiler.Compile(assembly, ConfigureFunctions());
        }
        catch (XmlException ex)
        {
            // A stylesheet that is not well-formed XML is a static error (XSLT 2.0 §3.1).
            throw new XsltStaticException(new[]
            {
                new Diagnostic(
                    DiagnosticSeverity.Error,
                    XsltErrors.XTSE0010,
                    $"The stylesheet is not well-formed XML: {ex.Message}",
                    new SourceSpan(location?.OriginalString, ex.LineNumber, ex.LinePosition)),
            });
        }

        _warnings = compiled.Warnings;
        if (_options.TreatWarningsAsErrors && compiled.Warnings.Count > 0)
        {
            List<Diagnostic> errors = new List<Diagnostic>(compiled.Warnings.Count);
            foreach (Diagnostic warning in compiled.Warnings)
            {
                errors.Add(new Diagnostic(DiagnosticSeverity.Error, warning.Code, warning.Message, warning.Location));
            }

            throw new XsltStaticException(errors);
        }

        return new XsltExecutable(compiled, resolver);
    }

    private IResourceResolver ResolverOrDefault() =>
        _options.ResourceResolver ?? FileSystemResourceResolver.Instance;

    private Action<XsltFunctionLibrary>? ConfigureFunctions()
    {
        IReadOnlyDictionary<string, IExtensionFunction>? extensions = _options.ExtensionFunctions;
        if (extensions is null || extensions.Count == 0)
        {
            return null;
        }

        return library =>
        {
            foreach (IExtensionFunction function in extensions.Values)
            {
                library.RegisterExtension(function.Name, function.ArgumentTypes, function.ResultType, function.Invoke);
            }
        };
    }

    private static void ValidateExtensions(XsltCompilerOptions options)
    {
        if (options.ExtensionFunctions is null)
        {
            return;
        }

        foreach (KeyValuePair<string, IExtensionFunction> entry in options.ExtensionFunctions)
        {
            if (entry.Value is null || entry.Value.Name.NamespaceUri.Length == 0)
            {
                throw new ArgumentException(
                    $"Extension function '{entry.Key}' must be non-null and have a name in a non-empty namespace",
                    nameof(options));
            }
        }
    }
}
