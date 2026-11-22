// XPath 2.0 §3.8 — for expressions; §3.9 — quantified expressions; §3.10 — if expressions.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Ast;

namespace XTransform.XPath.ExpressionNodes;

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
        return new EnumeratorIterator(() => EnumerateBindings(context, 0).GetEnumerator());
    }

    // XPath 2.0 §3.8: each clause iterates its sequence; the return expression is evaluated
    // once per nested-tuple binding, and its items are yielded in order. Nested clauses
    // form a lexicographic cross-product.
    private IEnumerable<XdmItem> EnumerateBindings(DynamicContext context, int clauseIndex)
    {
        if (clauseIndex == Clauses.Length)
        {
            IXdmIterator inner = Return.Iterate(context);
            while (true)
            {
                XdmItem? n = inner.Next();
                if (n is null)
                {
                    yield break;
                }
                yield return n;
            }
        }
        else
        {
            ForClauseBinding clause = Clauses[clauseIndex];
            IXdmIterator source = clause.SequenceExpr.Iterate(context);
            XdmSequence[] frame = context.LocalFrame;
            while (true)
            {
                XdmItem? item = source.Next();
                if (item is null)
                {
                    yield break;
                }
                // $v in XPath 2.0 is bound to a single item. Store it as a one-item sequence
                // so VariableRefNode.Iterate can return a fresh iterator each time.
                frame[clause.SlotIndex] = XdmSequence.Of(item);
                foreach (XdmItem inner in EnumerateBindings(context, clauseIndex + 1))
                {
                    yield return inner;
                }
            }
        }
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
        bool result = EvaluateBoolean(context);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
        => XdmAtomicValue.Boolean(EvaluateBoolean(context));

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context)
        => EvaluateBoolean(context);

    // XPath 2.0 §3.9: 'some' is true iff at least one tuple makes satisfies true; 'every'
    // is true iff every tuple makes it true. Both short-circuit; both vacuously match
    // the empty source (some → false, every → true).
    private bool EvaluateBoolean(DynamicContext context) => EvaluateAtClause(context, 0);

    private bool EvaluateAtClause(DynamicContext context, int clauseIndex)
    {
        if (clauseIndex == Clauses.Length)
        {
            return Satisfies.EffectiveBooleanValue(context);
        }

        ForClauseBinding clause = Clauses[clauseIndex];
        IXdmIterator source = clause.SequenceExpr.Iterate(context);
        XdmSequence[] frame = context.LocalFrame;
        while (true)
        {
            XdmItem? item = source.Next();
            if (item is null)
            {
                break;
            }
            frame[clause.SlotIndex] = XdmSequence.Of(item);
            bool inner = EvaluateAtClause(context, clauseIndex + 1);
            if (Kind == QuantifierKind.Some && inner)
            {
                return true;
            }
            if (Kind == QuantifierKind.Every && !inner)
            {
                return false;
            }
        }

        // No tuple produced the early-exit result.
        //   some: nothing was true → false
        //   every: nothing was false (incl. the empty case) → true
        return Kind == QuantifierKind.Every;
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
        // XPath 2.0 §3.10: evaluate the test's effective boolean value, then pick ONE branch.
        // The untaken branch is never evaluated, even if it would have thrown.
        return Test.EffectiveBooleanValue(context)
            ? Then.Iterate(context)
            : Else.Iterate(context);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
        => Test.EffectiveBooleanValue(context)
            ? Then.EvaluateItem(context)
            : Else.EvaluateItem(context);

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context)
        => Test.EffectiveBooleanValue(context)
            ? Then.EffectiveBooleanValue(context)
            : Else.EffectiveBooleanValue(context);

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
