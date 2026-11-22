// XPath 2.0 §3.8 — for expressions; §3.9 — quantified expressions; §3.10 — if expressions.

using System;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath.Ast;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-04): implement Iterate for ForNode, QuantifiedNode, IfNode

/// <summary>
/// A single variable binding in a for-expression or quantified expression.
/// Records the compile-time slot assignment and the sequence to iterate over.
/// </summary>
internal sealed record ForClauseBinding(
    QName VariableName,
    int SlotIndex,
    ExpressionNode SequenceExpr,
    SequenceType BindingType);

/// <summary>
/// A compiled for-expression: <c>for $v in seq return body</c>.
/// Static type is the return expression's item type with occurrence widened to <c>*</c>.
/// </summary>
internal sealed class ForNode : ExpressionNode
{
    /// <summary>Initializes a for-expression node.</summary>
    /// <param name="location">Source span of the full for-expression.</param>
    /// <param name="clauses">The variable bindings in order.</param>
    /// <param name="returnExpr">The compiled return expression.</param>
    internal ForNode(SourceSpan location, ImmutableArray<ForClauseBinding> clauses,
                     ExpressionNode returnExpr)
        : base(location,
               new SequenceType(returnExpr.StaticType.ItemType, Occurrence.ZeroOrMore),
               ComputeDependencies(clauses, returnExpr))
    {
        Clauses = clauses;
        Return = returnExpr;
    }

    /// <summary>Gets the variable bindings.</summary>
    internal ImmutableArray<ForClauseBinding> Clauses { get; }

    /// <summary>Gets the compiled return expression.</summary>
    internal ExpressionNode Return { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-04): implement Iterate
        throw new NotImplementedException("Evaluation for ForNode is implemented in P2-04.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("ForNode([");
        for (int i = 0; i < Clauses.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            ForClauseBinding c = Clauses[i];
            sb.Append('$').Append(FormatQName(c.VariableName)).Append("@slot").Append(c.SlotIndex);
        }

        sb.AppendLine("])");
        AppendIndented(sb, Return.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies ComputeDependencies(
        ImmutableArray<ForClauseBinding> clauses, ExpressionNode returnExpr)
    {
        ExpressionDependencies deps = returnExpr.Dependencies;
        foreach (ForClauseBinding clause in clauses)
        {
            deps |= clause.SequenceExpr.Dependencies;
        }

        return deps;
    }
}

/// <summary>
/// A compiled quantified expression: <c>some/every $v in seq satisfies test</c>.
/// Static type is always <c>xs:boolean</c>.
/// </summary>
internal sealed class QuantifiedNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes a quantified-expression node.</summary>
    /// <param name="location">Source span of the full quantified expression.</param>
    /// <param name="kind">Whether this is <c>some</c> or <c>every</c>.</param>
    /// <param name="clauses">The variable bindings in order.</param>
    /// <param name="satisfies">The compiled satisfies expression.</param>
    internal QuantifiedNode(SourceSpan location, QuantifierKind kind,
                             ImmutableArray<ForClauseBinding> clauses, ExpressionNode satisfies)
        : base(location, s_booleanType, ComputeDependencies(clauses, satisfies))
    {
        Kind = kind;
        Clauses = clauses;
        Satisfies = satisfies;
    }

    /// <summary>Gets the quantifier kind (<c>some</c> or <c>every</c>).</summary>
    internal QuantifierKind Kind { get; }

    /// <summary>Gets the variable bindings.</summary>
    internal ImmutableArray<ForClauseBinding> Clauses { get; }

    /// <summary>Gets the compiled satisfies expression.</summary>
    internal ExpressionNode Satisfies { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-04): implement Iterate
        throw new NotImplementedException("Evaluation for QuantifiedNode is implemented in P2-04.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string keyword = Kind == QuantifierKind.Some ? "some" : "every";
        StringBuilder sb = new StringBuilder();
        sb.Append("QuantifiedNode(").Append(keyword).Append(", [");
        for (int i = 0; i < Clauses.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            ForClauseBinding c = Clauses[i];
            sb.Append('$').Append(FormatQName(c.VariableName)).Append("@slot").Append(c.SlotIndex);
        }

        sb.AppendLine("])");
        AppendIndented(sb, Satisfies.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies ComputeDependencies(
        ImmutableArray<ForClauseBinding> clauses, ExpressionNode satisfies)
    {
        ExpressionDependencies deps = satisfies.Dependencies;
        foreach (ForClauseBinding clause in clauses)
        {
            deps |= clause.SequenceExpr.Dependencies;
        }

        return deps;
    }
}

/// <summary>
/// A compiled if-expression: <c>if (test) then then else else</c>.
/// Static type is <c>item()*</c> (over-approximate union of then-branch and else-branch types).
/// </summary>
internal sealed class IfNode : ExpressionNode
{
    /// <summary>Initializes an if-expression node.</summary>
    /// <param name="location">Source span of the full if-expression.</param>
    /// <param name="test">The compiled test expression.</param>
    /// <param name="then">The compiled then-branch expression.</param>
    /// <param name="else_">The compiled else-branch expression.</param>
    internal IfNode(SourceSpan location, ExpressionNode test, ExpressionNode then,
                    ExpressionNode else_)
        : base(location,
               SequenceType.AnySequence,
               test.Dependencies | then.Dependencies | else_.Dependencies)
    {
        Test = test;
        Then = then;
        Else = else_;
    }

    /// <summary>Gets the test expression.</summary>
    internal ExpressionNode Test { get; }

    /// <summary>Gets the then-branch expression.</summary>
    internal ExpressionNode Then { get; }

    /// <summary>Gets the else-branch expression.</summary>
    internal ExpressionNode Else { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-04): implement Iterate
        throw new NotImplementedException("Evaluation for IfNode is implemented in P2-04.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("IfNode");
        AppendIndented(sb, Test.ToDebugString(), 1);
        AppendIndented(sb, Then.ToDebugString(), 1);
        AppendIndented(sb, Else.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}
