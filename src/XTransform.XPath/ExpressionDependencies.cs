// XPath 2.0 §2.1.1 — the static context determines which dynamic context properties
// an expression may access; this enum records which properties a compiled node reads.

namespace XTransform.XPath;

/// <summary>
/// Flags that record which pieces of the dynamic context a compiled expression reads.
/// Used by the XSLT runtime to decide how much context to build before invoking an expression.
/// </summary>
[System.Flags]
internal enum ExpressionDependencies : short
{
    /// <summary>No dynamic-context dependencies.</summary>
    None = 0,

    /// <summary>Reads the focus item (fn:.</summary>
    ContextItem = 1,

    /// <summary>Reads the context position (fn:position()).</summary>
    Position = 2,

    /// <summary>Reads the context size (fn:last()).</summary>
    Last = 4,

    /// <summary>Reads one or more local variables from the frame.</summary>
    LocalVariables = 8,

    /// <summary>Reads the current group (xsl:for-each-group).</summary>
    CurrentGroup = 16,

    /// <summary>Reads the current regex match group (xsl:analyze-string).</summary>
    RegexGroup = 32,

    /// <summary>Reads the current template rule (xsl:apply-templates).</summary>
    CurrentTemplateRule = 64,

    /// <summary>Shorthand for all three focus properties: <see cref="ContextItem"/> | <see cref="Position"/> | <see cref="Last"/>.</summary>
    Focus = ContextItem | Position | Last,
}
