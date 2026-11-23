// Top-level declaration model produced by the XSLT compiler's declaration-collection pass.
// XSLT 2.0 §3.6, §6.2, §9.3, §10.3 — stylesheet declarations.

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Base class for a top-level XSLT declaration.</summary>
internal abstract class TopLevelDecl
{
    /// <summary>Initializes the declaration with its source element and precedence.</summary>
    protected TopLevelDecl(StylesheetElement element, int importPrecedence)
    {
        Element = element;
        ImportPrecedence = importPrecedence;
    }

    /// <summary>The source element this declaration was built from.</summary>
    public StylesheetElement Element { get; }

    /// <summary>Import precedence of the containing module (higher binds tighter).</summary>
    public int ImportPrecedence { get; }

    /// <summary>Source location of the declaration.</summary>
    public SourceSpan Location => Element.Location;
}

/// <summary>A top-level <c>xsl:variable</c> or <c>xsl:param</c>.</summary>
internal sealed class GlobalVariableDecl : TopLevelDecl
{
    /// <summary>Initializes a global variable / param declaration.</summary>
    public GlobalVariableDecl(
        StylesheetElement element,
        int importPrecedence,
        QName name,
        string? selectExpression,
        bool isParam,
        bool isRequired,
        string? asType)
        : base(element, importPrecedence)
    {
        Name = name;
        SelectExpression = selectExpression;
        IsParam = isParam;
        IsRequired = isRequired;
        AsType = asType;
    }

    /// <summary>The expanded variable name.</summary>
    public QName Name { get; }

    /// <summary>The <c>select</c> attribute text (may be null when the value is provided by element content).</summary>
    public string? SelectExpression { get; }

    /// <summary>True for <c>xsl:param</c>, false for <c>xsl:variable</c>.</summary>
    public bool IsParam { get; }

    /// <summary>True when <c>required="yes"</c> was declared.</summary>
    public bool IsRequired { get; }

    /// <summary>The <c>as</c> attribute's SequenceType text, when declared.</summary>
    public string? AsType { get; }
}

/// <summary>An <c>xsl:template</c> declaration (named, matching, or both).</summary>
internal sealed class TemplateDecl : TopLevelDecl
{
    /// <summary>Initializes a template declaration.</summary>
    public TemplateDecl(
        StylesheetElement element,
        int importPrecedence,
        QName? name,
        string? matchPattern,
        IReadOnlyList<QName> modes,
        string? priority)
        : base(element, importPrecedence)
    {
        Name = name;
        MatchPattern = matchPattern;
        Modes = modes;
        Priority = priority;
    }

    /// <summary>Expanded template name, or <see langword="null"/> when the template has only <c>@match</c>.</summary>
    public QName? Name { get; }

    /// <summary>The <c>match</c> pattern text, or <see langword="null"/> for a purely named template.</summary>
    public string? MatchPattern { get; }

    /// <summary>The modes declared via <c>@mode</c>; empty when the default mode applies.</summary>
    public IReadOnlyList<QName> Modes { get; }

    /// <summary>The <c>priority</c> attribute, when declared.</summary>
    public string? Priority { get; }
}

/// <summary>Key for a function declaration lookup — the (name, arity) pair.</summary>
internal readonly struct FunctionKey : System.IEquatable<FunctionKey>
{
    /// <summary>Initializes a key.</summary>
    public FunctionKey(QName name, int arity)
    {
        Name = name;
        Arity = arity;
    }

    /// <summary>The expanded function name.</summary>
    public QName Name { get; }

    /// <summary>The arity.</summary>
    public int Arity { get; }

    /// <inheritdoc/>
    public bool Equals(FunctionKey other) => Name.Equals(other.Name) && Arity == other.Arity;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FunctionKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (Name.GetHashCode() * 397) ^ Arity;
        }
    }
}

/// <summary>An <c>xsl:function</c> declaration.</summary>
internal sealed class FunctionDecl : TopLevelDecl
{
    /// <summary>Initializes an xsl:function declaration.</summary>
    public FunctionDecl(
        StylesheetElement element,
        int importPrecedence,
        QName name,
        int arity,
        string? asType)
        : base(element, importPrecedence)
    {
        Name = name;
        Arity = arity;
        AsType = asType;
    }

    /// <summary>The expanded function name.</summary>
    public QName Name { get; }

    /// <summary>Number of <c>xsl:param</c> children.</summary>
    public int Arity { get; }

    /// <summary>The declared result type, when <c>as</c> is present.</summary>
    public string? AsType { get; }
}

/// <summary>Aggregated top-level declarations for a linked stylesheet assembly.</summary>
internal sealed class DeclarationTable
{
    /// <summary>Initializes a declaration table.</summary>
    public DeclarationTable(
        IReadOnlyDictionary<QName, GlobalVariableDecl> globalVariables,
        IReadOnlyList<TemplateDecl> templates,
        IReadOnlyDictionary<QName, TemplateDecl> namedTemplates,
        IReadOnlyDictionary<FunctionKey, FunctionDecl> functions,
        IReadOnlyCollection<QName> declaredModes)
    {
        GlobalVariables = globalVariables;
        Templates = templates;
        NamedTemplates = namedTemplates;
        Functions = functions;
        DeclaredModes = declaredModes;
    }

    /// <summary>Global variables and parameters, keyed by expanded name — higher-precedence wins.</summary>
    public IReadOnlyDictionary<QName, GlobalVariableDecl> GlobalVariables { get; }

    /// <summary>Every template declaration in the assembly (named and match templates).</summary>
    public IReadOnlyList<TemplateDecl> Templates { get; }

    /// <summary>Named templates keyed by expanded name; higher-precedence wins.</summary>
    public IReadOnlyDictionary<QName, TemplateDecl> NamedTemplates { get; }

    /// <summary>User-defined functions keyed by (name, arity).</summary>
    public IReadOnlyDictionary<FunctionKey, FunctionDecl> Functions { get; }

    /// <summary>Set of modes that have at least one matching template rule.</summary>
    public IReadOnlyCollection<QName> DeclaredModes { get; }
}
