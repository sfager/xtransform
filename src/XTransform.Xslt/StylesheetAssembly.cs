using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// One loaded module in a stylesheet assembly, together with the import precedence the
/// linker assigned it. Higher values have higher precedence (XSLT 2.0 §3.11.3).
/// </summary>
internal sealed class StylesheetModuleEntry
{
    /// <summary>Initializes an entry.</summary>
    public StylesheetModuleEntry(StylesheetModule module, int importPrecedence, bool isPrimary, int? importSubtreeStart = null)
    {
        Module = module;
        ImportPrecedence = importPrecedence;
        IsPrimary = isPrimary;
        ImportSubtreeStart = importSubtreeStart ?? importPrecedence;
    }

    /// <summary>The processed <see cref="StylesheetModule"/>.</summary>
    public StylesheetModule Module { get; }

    /// <summary>The import precedence; higher binds tighter.</summary>
    public int ImportPrecedence { get; }

    /// <summary>True for the top-level primary module; false for everything loaded under it.</summary>
    public bool IsPrimary { get; }

    /// <summary>The lowest import precedence of the modules this module imports; equals its own precedence when it imports none.</summary>
    public int ImportSubtreeStart { get; }
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

    /// <summary>
    /// The lowest import precedence among the modules imported (directly or indirectly) by the module that has
    /// <paramref name="precedence"/>; the modules <c>xsl:apply-imports</c> may reach have precedences from this
    /// value up to but not including <paramref name="precedence"/>.
    /// </summary>
    public int GetImportSubtreeStart(int precedence)
    {
        int start = precedence;
        foreach (StylesheetModuleEntry entry in Modules)
        {
            if (entry.ImportPrecedence == precedence && entry.ImportSubtreeStart < start)
            {
                start = entry.ImportSubtreeStart;
            }
        }

        return start;
    }

    /// <summary>Every module in the assembly, in load (post-order) order.</summary>
    public IReadOnlyList<StylesheetModuleEntry> Modules { get; }
}
