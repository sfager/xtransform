using XTransform.Xdm;
using XTransform.XPath.Ast;

namespace XTransform.Xslt;

/// <summary>
/// One pattern rule for one mode, split out from an <c>xsl:template</c>. A single template
/// with <c>match="a | b"</c> produces two rules per mode it declares.
/// </summary>
internal sealed class TemplateRule
{
    /// <summary>Initializes a template rule.</summary>
    public TemplateRule(
        TemplateDecl source,
        PatternAst pattern,
        double priority,
        int importPrecedence,
        int documentOrderOrdinal,
        QName mode)
    {
        Source = source;
        Pattern = pattern;
        Priority = priority;
        ImportPrecedence = importPrecedence;
        DocumentOrderOrdinal = documentOrderOrdinal;
        Mode = mode;
    }

    /// <summary>The source <c>xsl:template</c> declaration this rule came from.</summary>
    public TemplateDecl Source { get; }

    /// <summary>Single-alternative pattern (never a <see cref="UnionPatternAst"/>).</summary>
    public PatternAst Pattern { get; }

    /// <summary>Priority used for conflict resolution.</summary>
    public double Priority { get; }

    /// <summary>Import precedence inherited from the containing module.</summary>
    public int ImportPrecedence { get; }

    /// <summary>
    /// Ordinal tie-breaker: higher means later in (module-precedence, document) order; the
    /// later rule wins when precedence and priority are equal (XSLT 2.0 §6.4).
    /// </summary>
    public int DocumentOrderOrdinal { get; }

    /// <summary>Mode this rule is registered under (the empty-string QName means the default mode).</summary>
    public QName Mode { get; }

    /// <summary>Location of the originating <c>xsl:template</c>.</summary>
    public SourceSpan Location => Source.Location;
}
