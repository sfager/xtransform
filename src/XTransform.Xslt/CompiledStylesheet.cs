using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// The output of <see cref="XsltCompiler"/>. Currently exposes the declaration table only;
/// runtime instruction trees land in P3-07+ and get attached here.
/// </summary>
/// <remarks>
/// The type will be wrapped by the public <c>XsltExecutable</c> in P3-15; it stays
/// <c>internal</c> until then.
/// </remarks>
internal sealed class CompiledStylesheet
{
    /// <summary>Initializes with its declaration table and any warnings.</summary>
    public CompiledStylesheet(
        StylesheetAssembly assembly,
        DeclarationTable declarations,
        IReadOnlyDictionary<QName, ModeTable> modes,
        IReadOnlyList<Diagnostic> warnings)
    {
        Assembly = assembly;
        Declarations = declarations;
        Modes = modes;
        Warnings = warnings;
    }

    /// <summary>The linked stylesheet assembly this was compiled from.</summary>
    public StylesheetAssembly Assembly { get; }

    /// <summary>The collected top-level declarations.</summary>
    public DeclarationTable Declarations { get; }

    /// <summary>Per-mode template-rule tables. Empty <see cref="QName"/> is the default mode.</summary>
    public IReadOnlyDictionary<QName, ModeTable> Modes { get; }

    /// <summary>Non-fatal warnings produced during compilation.</summary>
    public IReadOnlyList<Diagnostic> Warnings { get; }
}
