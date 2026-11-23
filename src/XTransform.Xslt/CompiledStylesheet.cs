using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

/// <summary>
/// The output of <see cref="XsltCompiler"/>. The type will be wrapped by the public
/// <c>XsltExecutable</c> in P3-15; it stays <c>internal</c> until then.
/// </summary>
internal sealed class CompiledStylesheet
{
    /// <summary>Initializes a compiled stylesheet.</summary>
    public CompiledStylesheet(
        StylesheetAssembly assembly,
        DeclarationTable declarations,
        IReadOnlyDictionary<QName, ModeTable> modes,
        IReadOnlyDictionary<TemplateDecl, CompiledTemplate> compiledTemplates,
        IReadOnlyDictionary<QName, KeyDecl> keys,
        XsltFunctionLibrary functions,
        IReadOnlyList<Diagnostic> warnings)
    {
        Assembly = assembly;
        Declarations = declarations;
        Modes = modes;
        CompiledTemplates = compiledTemplates;
        Keys = keys;
        Functions = functions;
        Warnings = warnings;
    }

    /// <summary>The linked stylesheet assembly this was compiled from.</summary>
    public StylesheetAssembly Assembly { get; }

    /// <summary>The collected top-level declarations.</summary>
    public DeclarationTable Declarations { get; }

    /// <summary>Per-mode template-rule tables. Empty <see cref="QName"/> is the default mode.</summary>
    public IReadOnlyDictionary<QName, ModeTable> Modes { get; }

    /// <summary>Compiled template bodies keyed by their source <see cref="TemplateDecl"/>.</summary>
    public IReadOnlyDictionary<TemplateDecl, CompiledTemplate> CompiledTemplates { get; }

    /// <summary>Key declarations keyed by expanded name (multiple xsl:key with the same name combine at lookup).</summary>
    public IReadOnlyDictionary<QName, KeyDecl> Keys { get; }

    /// <summary>The composite function library used at compile and run time.</summary>
    public XsltFunctionLibrary Functions { get; }

    /// <summary>Non-fatal warnings produced during compilation.</summary>
    public IReadOnlyList<Diagnostic> Warnings { get; }
}
