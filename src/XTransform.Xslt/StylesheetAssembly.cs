using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// One loaded module in a stylesheet assembly, together with the import precedence the
/// linker assigned it. Higher values have higher precedence (XSLT 2.0 §3.11.3).
/// </summary>
internal sealed class StylesheetModuleEntry
{
    /// <summary>Initializes an entry.</summary>
    public StylesheetModuleEntry(StylesheetModule module, int importPrecedence, bool isPrimary)
    {
        Module = module;
        ImportPrecedence = importPrecedence;
        IsPrimary = isPrimary;
    }

    /// <summary>The processed <see cref="StylesheetModule"/>.</summary>
    public StylesheetModule Module { get; }

    /// <summary>The import precedence; higher binds tighter.</summary>
    public int ImportPrecedence { get; }

    /// <summary>True for the top-level primary module; false for everything loaded under it.</summary>
    public bool IsPrimary { get; }
}

/// <summary>
/// A primary stylesheet module together with every module reached through its
/// <c>xsl:include</c> / <c>xsl:import</c> graph, flattened into a list ordered by
/// ascending import precedence.
/// </summary>
internal sealed class StylesheetAssembly
{
    /// <summary>Initializes an assembly.</summary>
    public StylesheetAssembly(
        StylesheetModuleEntry primary,
        IReadOnlyList<StylesheetModuleEntry> modules)
    {
        Primary = primary;
        Modules = modules;
    }

    /// <summary>The primary (top-level) module.</summary>
    public StylesheetModuleEntry Primary { get; }

    /// <summary>Every module in the assembly, in load (post-order) order.</summary>
    public IReadOnlyList<StylesheetModuleEntry> Modules { get; }
}
