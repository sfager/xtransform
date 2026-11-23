using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// One parsed stylesheet module — the output of <see cref="StylesheetReader"/>.
/// A module is not yet linked against its <c>xsl:include</c> / <c>xsl:import</c> dependencies
/// (that is the job of P3-03). It carries non-fatal diagnostics collected during reading;
/// fatal problems throw <see cref="XsltStaticException"/> at read time.
/// </summary>
internal sealed class StylesheetModule
{
    /// <summary>Initializes a module.</summary>
    public StylesheetModule(
        string? moduleUri,
        StylesheetElement root,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        ModuleUri = moduleUri;
        Root = root;
        Diagnostics = diagnostics;
    }

    /// <summary>The module's URI, or <see langword="null"/> when unknown.</summary>
    public string? ModuleUri { get; }

    /// <summary>The root element — typically <c>xsl:stylesheet</c> or <c>xsl:transform</c>, but a simplified stylesheet uses a literal result element as root (XSLT 2.0 §3.7).</summary>
    public StylesheetElement Root { get; }

    /// <summary>True when the root is <c>xsl:stylesheet</c> or <c>xsl:transform</c>.</summary>
    public bool IsFullStylesheet =>
        Root.IsXsltElement &&
        (string.Equals(Root.Name.LocalName, "stylesheet", System.StringComparison.Ordinal) ||
         string.Equals(Root.Name.LocalName, "transform", System.StringComparison.Ordinal));

    /// <summary>The declared version from the root element's <c>version</c> attribute, or empty when none.</summary>
    public string DeclaredVersion => Root.DeclaredVersion;

    /// <summary>Warnings and recoverable errors collected during reading.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
