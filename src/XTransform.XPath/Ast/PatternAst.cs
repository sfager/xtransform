using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;

// XSLT 2.0 §5.5.2 — pattern grammar AST nodes
// XSLT 2.0 §6.4   — default priority rules
namespace XTransform.XPath.Ast;

/// <summary>Abstract base for every XSLT pattern AST node.</summary>
internal abstract record PatternAst(SourceSpan Span)
{
    /// <summary>XSLT 2.0 §6.4 default priority for this pattern.</summary>
    public abstract double DefaultPriority { get; }

    /// <summary>Pretty-printed form used by golden tests.</summary>
    public string ToDebugString()
    {
        var sb = new StringBuilder();
        AppendDebug(sb, 0);
        return sb.ToString();
    }

    internal abstract void AppendDebug(StringBuilder sb, int indent);

    /// <summary>Writes an indentation prefix of <paramref name="depth"/> * 2 spaces.</summary>
    protected static void Indent(StringBuilder sb, int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            sb.Append("  ");
        }
    }
}

// ── Union pattern ──────────────────────────────────────────────────────────────

/// <summary>
/// Pattern ::= PathPattern ("|" PathPattern)+ — two or more alternatives.
/// When there is exactly one alternative, the parser returns the inner pattern directly.
/// </summary>
internal sealed record UnionPatternAst(
    SourceSpan Span,
    ImmutableArray<PatternAst> Alternatives
) : PatternAst(Span)
{
    /// <inheritdoc/>
    /// <remarks>
    /// Each alternative is evaluated separately per XSLT 2.0 §6.4; this Max is provided
    /// for convenience only. Consumers should inspect Alternatives individually.
    /// </remarks>
    public override double DefaultPriority
    {
        get
        {
            double max = double.NegativeInfinity;
            foreach (PatternAst alt in Alternatives)
            {
                double p = alt.DefaultPriority;
                if (p > max) { max = p; }
            }
            return max;
        }
    }

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("UnionPattern");
        foreach (PatternAst alt in Alternatives)
        {
            alt.AppendDebug(sb, indent + 1);
        }
    }
}

// ── Path pattern ───────────────────────────────────────────────────────────────

/// <summary>How a PathPatternAst is anchored at the document root.</summary>
internal enum PatternAnchor
{
    /// <summary>No leading / or // — a relative pattern.</summary>
    Relative,
    /// <summary>Anchored with a leading single /.</summary>
    Root,
    /// <summary>Anchored with a leading // (root descendant-or-self).</summary>
    RootDescendantOrSelf,
}

/// <summary>How a step connects to its predecessor.</summary>
internal enum StepCombinator
{
    /// <summary>First step of a relative pattern — no preceding separator.</summary>
    Leading,
    /// <summary>Preceded by a single /.</summary>
    Slash,
    /// <summary>Preceded by //.</summary>
    DoubleSlash,
}

/// <summary>
/// A path pattern: steps joined by "/" or "//", possibly rooted at "/" or "//".
/// XSLT 2.0 §5.5.2.
/// </summary>
internal sealed record PathPatternAst(
    SourceSpan Span,
    PatternAnchor Anchor,
    ImmutableArray<StepPatternAst> Steps   // may be empty for a bare "/"
) : PatternAst(Span)
{
    /// <inheritdoc/>
    /// <remarks>XSLT 2.0 §6.4: any path with "/" or "//" has priority 0.5.</remarks>
    public override double DefaultPriority
    {
        get
        {
            // The pattern "/" alone matches any document node and has the priority of node tests: -0.5 (conformance: conflict-resolution-1601).
            if (Anchor == PatternAnchor.Root && Steps.IsEmpty) { return -0.5; }

            // Rooted pattern → 0.5
            if (Anchor != PatternAnchor.Relative) { return 0.5; }

            // Multi-step → 0.5
            if (Steps.Length > 1) { return 0.5; }

            // Single step with DoubleSlash combinator shouldn't occur here but guard anyway
            if (Steps.Length == 1 && Steps[0].Combinator != StepCombinator.Leading) { return 0.5; }

            // Single step with predicate → 0.5
            if (Steps.Length == 1 && !Steps[0].Predicates.IsEmpty) { return 0.5; }

            // Single step, no predicate: delegate to the step
            if (Steps.Length == 1) { return Steps[0].SingleStepPriority; }

            // Bare "/" (no steps) → not normally reached; treat as 0.5
            return 0.5;
        }
    }

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string anchor = Anchor switch
        {
            PatternAnchor.Root => "Root",
            PatternAnchor.RootDescendantOrSelf => "RootDescendantOrSelf",
            _ => "Relative",
        };
        sb.Append("PathPattern(").Append(anchor).AppendLine(")");
        foreach (StepPatternAst step in Steps)
        {
            step.AppendDebug(sb, indent + 1);
        }
    }
}

/// <summary>
/// A step pattern: child or attribute axis + node test + optional predicates.
/// XSLT 2.0 §5.5.2.
/// </summary>
internal sealed record StepPatternAst(
    SourceSpan Span,
    Axis Axis,                               // Child or Attribute only
    NodeTestAst Test,
    ImmutableArray<XPathAst> Predicates,     // full XPath inside [...]
    StepCombinator Combinator                // how this step connects to the previous
) : PatternAst(Span)
{
    /// <summary>
    /// True when the step was written <c>child::</c>. Such a step stays on the child axis even with an
    /// <c>attribute()</c> test (which then never matches), whereas the abbreviated <c>attribute()</c> is on the
    /// attribute axis (XPath 2.0 §3.2.1.1); the document node is nobody's child, so it never matches either.
    /// </summary>
    public bool ExplicitChildAxis { get; init; }

    /// <inheritdoc/>
    /// <remarks>
    /// This is only the per-step priority for a single-step, no-predicate, relative pattern.
    /// PathPatternAst.DefaultPriority handles the multi-step / predicate cases.
    /// </remarks>
    public override double DefaultPriority => SingleStepPriority;

    /// <summary>
    /// Priority for this step when it is the sole step of a simple relative pattern
    /// with no predicates. XSLT 2.0 §6.4.
    /// </summary>
    internal double SingleStepPriority
    {
        get
        {
            // XSLT 2.0 §6.4
            if (Test is NameTestAst nameTest)
            {
                return nameTest.Kind switch
                {
                    NameTestKind.QualifiedName => 0.0,       // NCName or QName
                    NameTestKind.AnyName => -0.5,            // *
                    NameTestKind.PrefixWildcard => -0.25,    // ns:*
                    NameTestKind.LocalWildcard => -0.25,     // *:local
                    _ => -0.5,
                };
            }

            if (Test is KindTestAst kindTest)
            {
                return KindTestPriority(kindTest);
            }

            if (Test is AnyKindTestAst)
            {
                return -0.5; // node()
            }

            return -0.5;
        }
    }

    private static double KindTestPriority(KindTestAst kt)
    {
        // XSLT 2.0 §6.4

        // processing-instruction("target") → 0
        if (kt.Kind == XdmNodeKind.ProcessingInstruction && kt.NameArg.HasValue)
        {
            return 0.0;
        }

        // document-node(element(name)) → 0
        // document-node(element(*)) → -0.5 (the wildcard sentinel LocalName="*" means no specific name)
        if (kt.Kind == XdmNodeKind.Document && kt.ElementArg is KindTestAst innerKt
            && innerKt.NameArg.HasValue
            && !string.Equals(innerKt.NameArg.Value.LocalName, "*", System.StringComparison.Ordinal))
        {
            return 0.0;
        }

        // element(N, T) / attribute(N, T) with a type → 0.25; element(*, T) / attribute(*, T) → 0 (XSLT 2.0 §6.4)
        if ((kt.Kind == XdmNodeKind.Element || kt.Kind == XdmNodeKind.Attribute) && kt.TypeArg.HasValue)
        {
            bool named = kt.NameArg.HasValue && !string.Equals(kt.NameArg.Value.LocalName, "*", System.StringComparison.Ordinal);
            return named ? 0.25 : 0.0;
        }

        // element(name) or attribute(name) with specific QName → 0
        // Note: element(*) / attribute(*) use a sentinel QName with LocalName="*" to record
        // that the wildcard was explicit; that is treated the same as element() / attribute().
        if ((kt.Kind == XdmNodeKind.Element || kt.Kind == XdmNodeKind.Attribute) && kt.NameArg.HasValue
            && !string.Equals(kt.NameArg.Value.LocalName, "*", System.StringComparison.Ordinal))
        {
            return 0.0;
        }

        // Everything else: element(), attribute(), attribute(*), element(*), text(),
        // comment(), processing-instruction(), document-node(), node() → -0.5
        return -0.5;
    }

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string comb = Combinator switch
        {
            StepCombinator.Slash => "/",
            StepCombinator.DoubleSlash => "//",
            _ => "",
        };
        string axis = Axis == Axis.Attribute ? "attribute" : "child";
        sb.Append("StepPattern(").Append(comb).Append(axis).AppendLine("::")
            ;
        Test.AppendDebug(sb, indent + 1);
        foreach (XPathAst pred in Predicates)
        {
            Indent(sb, indent + 1);
            sb.AppendLine("[predicate]");
            pred.AppendDebug(sb, indent + 2);
        }
        Indent(sb, indent);
        sb.AppendLine(")");
    }
}

// ── id() / key() patterns ──────────────────────────────────────────────────────

/// <summary>id("string-literal") or id($var) pattern. XSLT 2.0 §5.5.2.</summary>
internal sealed record IdPatternAst(
    SourceSpan Span,
    string? IdLiteral,         // non-null when id("literal")
    QName? VariableRef         // non-null when id($var)
) : PatternAst(Span)
{
    /// <inheritdoc/>
    public override double DefaultPriority => 0.5; // XSLT 2.0 §6.4

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        if (IdLiteral != null)
        {
            sb.Append("IdPattern(\"").Append(IdLiteral).AppendLine("\")");
        }
        else
        {
            sb.Append("IdPattern($").Append(VariableRef!.Value.LocalName).AppendLine(")");
        }
    }
}

/// <summary>key("name", "value-literal") or key("name", $var) pattern. XSLT 2.0 §5.5.2.</summary>
internal sealed record KeyPatternAst(
    SourceSpan Span,
    QName KeyName,
    string? ValueLiteral,      // non-null when key("name", "literal")
    QName? VariableRef,        // non-null when key("name", $var)
    XdmAtomicValue? ValueAtomic = null   // non-null when the value is a numeric literal
) : PatternAst(Span)
{
    /// <inheritdoc/>
    public override double DefaultPriority => 0.5; // XSLT 2.0 §6.4

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        if (ValueLiteral != null)
        {
            sb.Append("KeyPattern(\"").Append(KeyName.LocalName).Append("\", \"").Append(ValueLiteral).AppendLine("\")");
        }
        else if (ValueAtomic != null)
        {
            sb.Append("KeyPattern(\"").Append(KeyName.LocalName).Append("\", ").Append(ValueAtomic.GetStringValue()).AppendLine(")");
        }
        else
        {
            sb.Append("KeyPattern(\"").Append(KeyName.LocalName).Append("\", $").Append(VariableRef!.Value.LocalName).AppendLine(")");
        }
    }
}

/// <summary>
/// An id() or key() head followed by further path steps: id("x")/foo.
/// XSLT 2.0 §5.5.2.
/// </summary>
internal sealed record IdKeyWithPathAst(
    SourceSpan Span,
    PatternAst Head,                             // IdPatternAst or KeyPatternAst
    ImmutableArray<StepPatternAst> TailSteps
) : PatternAst(Span)
{
    /// <inheritdoc/>
    public override double DefaultPriority => 0.5; // XSLT 2.0 §6.4

    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("IdKeyWithPath");
        Head.AppendDebug(sb, indent + 1);
        foreach (StepPatternAst step in TailSteps)
        {
            step.AppendDebug(sb, indent + 1);
        }
    }
}
