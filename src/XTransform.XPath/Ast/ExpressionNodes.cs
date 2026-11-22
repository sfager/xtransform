using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using XTransform.Xdm;

// XPath 2.0 §A.1 productions: all expression AST node types
namespace XTransform.XPath.Ast;

// ── Top-level sequence ──────────────────────────────────────────────────────

/// <summary>XPath 2.0 §A.1 prod 1: Expr — a comma-separated list of ExprSingle.</summary>
internal sealed record SequenceExpr(SourceSpan Span, ImmutableArray<XPathAst> Items) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("SequenceExpr");
        foreach (XPathAst item in Items)
        {
            item.AppendDebug(sb, indent + 1);
        }
    }
}

// ── Control flow ────────────────────────────────────────────────────────────

/// <summary>A single binding in a ForExpr or QuantifiedExpr: <c>$var in expr</c>.</summary>
internal sealed record ForClause(QName Variable, XPathAst SequenceExpr, SourceSpan Span);

/// <summary>Quantifier kind for QuantifiedExpr.</summary>
internal enum QuantifierKind
{
    /// <summary><c>some $x in … satisfies …</c></summary>
    Some,
    /// <summary><c>every $x in … satisfies …</c></summary>
    Every,
}

/// <summary>XPath 2.0 §A.1: ForExpr — <c>for $x in e, … return e</c>.</summary>
internal sealed record ForExpr(SourceSpan Span, ImmutableArray<ForClause> Clauses, XPathAst ReturnExpr) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("ForExpr");
        foreach (ForClause clause in Clauses)
        {
            Indent(sb, indent + 1);
            sb.Append("ForClause(").Append(clause.Variable.ToString()).AppendLine(")");
            clause.SequenceExpr.AppendDebug(sb, indent + 2);
        }
        Indent(sb, indent + 1);
        sb.AppendLine("return");
        ReturnExpr.AppendDebug(sb, indent + 2);
    }
}

/// <summary>XPath 2.0 §A.1: QuantifiedExpr — <c>some/every $x in e satisfies e</c>.</summary>
internal sealed record QuantifiedExpr(SourceSpan Span, QuantifierKind Kind, ImmutableArray<ForClause> Clauses, XPathAst Satisfies) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string keyword = Kind == QuantifierKind.Some ? "some" : "every";
        sb.Append("QuantifiedExpr(").Append(keyword).AppendLine(")");
        foreach (ForClause clause in Clauses)
        {
            Indent(sb, indent + 1);
            sb.Append("ForClause(").Append(clause.Variable.ToString()).AppendLine(")");
            clause.SequenceExpr.AppendDebug(sb, indent + 2);
        }
        Indent(sb, indent + 1);
        sb.AppendLine("satisfies");
        Satisfies.AppendDebug(sb, indent + 2);
    }
}

/// <summary>XPath 2.0 §A.1: IfExpr — <c>if (test) then then else else</c>.</summary>
internal sealed record IfExpr(SourceSpan Span, XPathAst Test, XPathAst Then, XPathAst Else) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("IfExpr");
        Indent(sb, indent + 1);
        sb.AppendLine("if");
        Test.AppendDebug(sb, indent + 2);
        Indent(sb, indent + 1);
        sb.AppendLine("then");
        Then.AppendDebug(sb, indent + 2);
        Indent(sb, indent + 1);
        sb.AppendLine("else");
        Else.AppendDebug(sb, indent + 2);
    }
}

// ── Logical / comparison / arithmetic ──────────────────────────────────────

/// <summary>XPath 2.0 §A.1: OrExpr.</summary>
internal sealed record OrExpr(SourceSpan Span, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("OrExpr");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }
}

/// <summary>XPath 2.0 §A.1: AndExpr.</summary>
internal sealed record AndExpr(SourceSpan Span, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("AndExpr");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }
}

/// <summary>Comparison operator codes.</summary>
internal enum ComparisonOp
{
    // General comparisons
    Equals, NotEquals, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual,
    // Value comparisons
    Eq, Ne, Lt, Le, Gt, Ge,
    // Node identity / order
    Is, Precedes, Follows,
}

/// <summary>XPath 2.0 §A.1: ComparisonExpr.</summary>
internal sealed record ComparisonExpr(SourceSpan Span, ComparisonOp Op, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string opStr = OpToString(Op);
        sb.Append("ComparisonExpr(").Append(opStr).AppendLine(")");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }

    private static string OpToString(ComparisonOp op)
    {
        return op switch
        {
            ComparisonOp.Equals => "=",
            ComparisonOp.NotEquals => "!=",
            ComparisonOp.LessThan => "<",
            ComparisonOp.LessThanOrEqual => "<=",
            ComparisonOp.GreaterThan => ">",
            ComparisonOp.GreaterThanOrEqual => ">=",
            ComparisonOp.Eq => "eq",
            ComparisonOp.Ne => "ne",
            ComparisonOp.Lt => "lt",
            ComparisonOp.Le => "le",
            ComparisonOp.Gt => "gt",
            ComparisonOp.Ge => "ge",
            ComparisonOp.Is => "is",
            ComparisonOp.Precedes => "<<",
            ComparisonOp.Follows => ">>",
            _ => op.ToString(),
        };
    }
}

/// <summary>XPath 2.0 §A.1: RangeExpr — <c>e to e</c>.</summary>
internal sealed record RangeExpr(SourceSpan Span, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("RangeExpr(to)");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }
}

/// <summary>Arithmetic operator codes.</summary>
internal enum ArithmeticOp
{
    Add, Subtract, Multiply, Divide, IntegerDivide, Modulo,
}

/// <summary>XPath 2.0 §A.1: AdditiveExpr / MultiplicativeExpr.</summary>
internal sealed record ArithmeticExpr(SourceSpan Span, ArithmeticOp Op, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string opStr = OpToString(Op);
        sb.Append("ArithmeticExpr(").Append(opStr).AppendLine(")");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }

    private static string OpToString(ArithmeticOp op)
    {
        return op switch
        {
            ArithmeticOp.Add => "+",
            ArithmeticOp.Subtract => "-",
            ArithmeticOp.Multiply => "*",
            ArithmeticOp.Divide => "div",
            ArithmeticOp.IntegerDivide => "idiv",
            ArithmeticOp.Modulo => "mod",
            _ => op.ToString(),
        };
    }
}

/// <summary>Set operator codes.</summary>
internal enum SetOp
{
    Union, Intersect, Except,
}

/// <summary>XPath 2.0 §A.1: UnionExpr / IntersectExceptExpr.</summary>
internal sealed record SetExpr(SourceSpan Span, SetOp Op, XPathAst Left, XPathAst Right) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string opStr = Op switch
        {
            SetOp.Union => "union",
            SetOp.Intersect => "intersect",
            SetOp.Except => "except",
            _ => Op.ToString(),
        };
        sb.Append("SetExpr(").Append(opStr).AppendLine(")");
        Left.AppendDebug(sb, indent + 1);
        Right.AppendDebug(sb, indent + 1);
    }
}

/// <summary>XPath 2.0 §A.1: InstanceofExpr — <c>e instance of T</c>.</summary>
internal sealed record InstanceOfExpr(SourceSpan Span, XPathAst Expr, SequenceType TargetType) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("InstanceOfExpr(").Append(TargetType.ToString()).AppendLine(")");
        Expr.AppendDebug(sb, indent + 1);
    }
}

/// <summary>XPath 2.0 §A.1: TreatExpr — <c>e treat as T</c>.</summary>
internal sealed record TreatExpr(SourceSpan Span, XPathAst Expr, SequenceType TargetType) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("TreatExpr(").Append(TargetType.ToString()).AppendLine(")");
        Expr.AppendDebug(sb, indent + 1);
    }
}

/// <summary>XPath 2.0 §A.1: CastableExpr — <c>e castable as T</c>.</summary>
internal sealed record CastableExpr(SourceSpan Span, XPathAst Expr, SequenceType TargetType) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("CastableExpr(").Append(TargetType.ToString()).AppendLine(")");
        Expr.AppendDebug(sb, indent + 1);
    }
}

/// <summary>XPath 2.0 §A.1: CastExpr — <c>e cast as T</c>.</summary>
internal sealed record CastExpr(SourceSpan Span, XPathAst Expr, SequenceType TargetType) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("CastExpr(").Append(TargetType.ToString()).AppendLine(")");
        Expr.AppendDebug(sb, indent + 1);
    }
}

// ── Unary ───────────────────────────────────────────────────────────────────

/// <summary>Unary operator codes.</summary>
internal enum UnaryOp
{
    /// <summary>Unary minus: <c>-e</c>.</summary>
    Negate,
    /// <summary>Unary plus: <c>+e</c> (no-op but kept in tree).</summary>
    Plus,
}

/// <summary>XPath 2.0 §A.1: UnaryExpr.</summary>
internal sealed record UnaryExpr(SourceSpan Span, UnaryOp Op, XPathAst Operand) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string opStr = Op == UnaryOp.Negate ? "-" : "+";
        sb.Append("UnaryExpr(").Append(opStr).AppendLine(")");
        Operand.AppendDebug(sb, indent + 1);
    }
}

// ── Path ────────────────────────────────────────────────────────────────────

/// <summary>Path anchor: how the path expression starts.</summary>
internal enum PathAnchor
{
    /// <summary>Relative path (no leading slash).</summary>
    Relative,
    /// <summary>Absolute path starting with <c>/</c>.</summary>
    AbsoluteSlash,
    /// <summary>Absolute path starting with <c>//</c>.</summary>
    AbsoluteDoubleSlash,
}

/// <summary>XPath 2.0 §A.1: PathExpr — an anchored sequence of steps.</summary>
internal sealed record PathExpr(SourceSpan Span, PathAnchor Anchor, ImmutableArray<XPathAst> Steps) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string anchorStr = Anchor switch
        {
            PathAnchor.AbsoluteSlash => "/",
            PathAnchor.AbsoluteDoubleSlash => "//",
            _ => "relative",
        };
        sb.Append("PathExpr(").Append(anchorStr).AppendLine(")");
        foreach (XPathAst step in Steps)
        {
            step.AppendDebug(sb, indent + 1);
        }
    }
}

/// <summary>XPath 2.0 §A.1: AxisStep — an axis + node test + predicates.</summary>
internal sealed record AxisStep(SourceSpan Span, Axis Axis, NodeTestAst Test, ImmutableArray<XPathAst> Predicates) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string axisName = AxisToName(Axis);
        sb.Append("AxisStep(").Append(axisName).AppendLine(")");
        Test.AppendDebug(sb, indent + 1);
        foreach (XPathAst pred in Predicates)
        {
            Indent(sb, indent + 1);
            sb.AppendLine("Predicate");
            pred.AppendDebug(sb, indent + 2);
        }
    }

    private static string AxisToName(Axis axis)
    {
        return axis switch
        {
            Axis.Child => "child",
            Axis.Descendant => "descendant",
            Axis.DescendantOrSelf => "descendant-or-self",
            Axis.Self => "self",
            Axis.Attribute => "attribute",
            Axis.Parent => "parent",
            Axis.Ancestor => "ancestor",
            Axis.AncestorOrSelf => "ancestor-or-self",
            Axis.FollowingSibling => "following-sibling",
            Axis.PrecedingSibling => "preceding-sibling",
            Axis.Following => "following",
            Axis.Preceding => "preceding",
            Axis.Namespace => "namespace",
            _ => axis.ToString().ToLowerInvariant(),
        };
    }
}

// ── Primaries ───────────────────────────────────────────────────────────────

/// <summary>XPath 2.0 §A.1: Literal — string, integer, decimal, or double.</summary>
internal sealed record Literal(SourceSpan Span, XdmAtomicValue Value) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string typeName = "xs:" + CastTable.GetXsLocalName(Value.TypeCode);
        string valStr = Value.GetStringValue();
        sb.Append("Literal(").Append(typeName).Append(", ").Append(valStr).AppendLine(")");
    }
}

/// <summary>XPath 2.0 §A.1: VarRef — <c>$name</c>.</summary>
internal sealed record VariableRef(SourceSpan Span, QName Name) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("VariableRef(").Append(Name.ToString()).AppendLine(")");
    }
}

/// <summary>XPath 2.0 §A.1: FunctionCall — <c>name(args...)</c>.</summary>
internal sealed record FunctionCall(SourceSpan Span, QName Name, ImmutableArray<XPathAst> Arguments) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.Append("FunctionCall(").Append(Name.ToString()).AppendLine(")");
        foreach (XPathAst arg in Arguments)
        {
            arg.AppendDebug(sb, indent + 1);
        }
    }
}

/// <summary>XPath 2.0 §A.1: ContextItemExpr — <c>.</c> in primary position.</summary>
internal sealed record ContextItemExpr(SourceSpan Span) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("ContextItemExpr");
    }
}

/// <summary>XPath 2.0 §A.1: ParenthesizedExpr — <c>(expr)</c> or <c>()</c>.</summary>
internal sealed record ParenthesizedExpr(SourceSpan Span, XPathAst? Inner) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        if (Inner == null)
        {
            sb.AppendLine("ParenthesizedExpr(empty)");
        }
        else
        {
            sb.AppendLine("ParenthesizedExpr");
            Inner.AppendDebug(sb, indent + 1);
        }
    }
}

/// <summary>XPath 2.0 §A.1: FilterExpr — a primary expression with predicates.</summary>
internal sealed record FilterExpr(SourceSpan Span, XPathAst Primary, ImmutableArray<XPathAst> Predicates) : XPathAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("FilterExpr");
        Primary.AppendDebug(sb, indent + 1);
        foreach (XPathAst pred in Predicates)
        {
            Indent(sb, indent + 1);
            sb.AppendLine("Predicate");
            pred.AppendDebug(sb, indent + 2);
        }
    }
}

#pragma warning disable CA1801 // unused parameter - CultureInfo kept for future use
// CA1305 suppression: CultureInfo.InvariantCulture is not needed for simple string concatenation;
// format strings here contain no culture-sensitive formatting.
#pragma warning restore CA1801
