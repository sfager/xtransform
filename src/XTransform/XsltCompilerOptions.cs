using System;
using System.Collections.Generic;

namespace XTransform;

/// <summary>Options for <see cref="XsltCompiler"/>.</summary>
public sealed class XsltCompilerOptions
{
    /// <summary>
    /// Resolves <c>xsl:include</c> / <c>xsl:import</c> targets and documents loaded at run
    /// time. Default: the local file system, no network access.
    /// </summary>
    public IResourceResolver? ResourceResolver { get; init; }

    /// <summary>
    /// .NET extension functions made available to the stylesheet. Each function is registered
    /// under its own <see cref="IExtensionFunction.Name"/> (which must be in a non-null
    /// namespace); the dictionary key is a free-form label.
    /// </summary>
    public IReadOnlyDictionary<string, IExtensionFunction>? ExtensionFunctions { get; init; }

    /// <summary>When true, any compile warning is reported as a static error.</summary>
    public bool TreatWarningsAsErrors { get; init; }

    /// <summary>Base URI for stylesheets supplied as a reader without their own base URI.</summary>
    public Uri? BaseUri { get; init; }
}
