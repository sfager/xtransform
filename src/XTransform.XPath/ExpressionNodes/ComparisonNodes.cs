// XPath 2.0 §3.5 — comparison expressions: general, value, and node comparisons.

using System;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Operators;

namespace XTransform.XPath.ExpressionNodes;

/// <summary>Operator codes for general comparisons.</summary>
internal enum GeneralComparisonOp
{
    /// <summary><c>=</c></summary>
    Equals,
    /// <summary><c>!=</c></summary>
    NotEquals,
    /// <summary><c>&lt;</c></summary>
    LessThan,
    /// <summary><c>&lt;=</c></summary>
    LessThanOrEqual,
    /// <summary><c>&gt;</c></summary>
    GreaterThan,
    /// <summary><c>&gt;=</c></summary>
    GreaterThanOrEqual,
}

/// <summary>Operator codes for value comparisons.</summary>
internal enum ValueComparisonOp
{
    /// <summary><c>eq</c></summary>
    Eq,
    /// <summary><c>ne</c></summary>
    Ne,
    /// <summary><c>lt</c></summary>
    Lt,
    /// <summary><c>le</c></summary>
    Le,
    /// <summary><c>gt</c></summary>
    Gt,
    /// <summary><c>ge</c></summary>
    Ge,
}

/// <summary>Operator codes for node comparisons.</summary>
internal enum NodeComparisonOp
{
    /// <summary><c>is</c></summary>
    Is,
    /// <summary><c>&lt;&lt;</c></summary>
    Precedes,
    /// <summary><c>&gt;&gt;</c></summary>
    Follows,
}

/// <summary>
/// A compiled general comparison expression (<c>=</c>, <c>!=</c>, <c>&lt;</c>, etc.).
/// Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class GeneralComparisonNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes a general-comparison node.</summary>
    /// <param name="location">Source span of the full comparison expression.</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal GeneralComparisonNode(SourceSpan location, GeneralComparisonOp op,
                                    ExpressionNode left, ExpressionNode right)
        : base(location, s_booleanType, left.Dependencies | right.Dependencies)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    /// <summary>Gets the comparison operator.</summary>
    internal GeneralComparisonOp Op { get; }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // XPath 2.0 §3.5.2 — general comparison (existential semantics)
        bool result = GeneralComparison.Evaluate(Op, Left.Iterate(context), Right.Iterate(context), Location);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        bool result = GeneralComparison.Evaluate(Op, Left.Iterate(context), Right.Iterate(context), Location);
        return XdmAtomicValue.Boolean(result);
    }

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context)
    {
        return GeneralComparison.Evaluate(Op, Left.Iterate(context), Right.Iterate(context), Location);
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op switch
        {
            GeneralComparisonOp.Equals => "=",
            GeneralComparisonOp.NotEquals => "!=",
            GeneralComparisonOp.LessThan => "<",
            GeneralComparisonOp.LessThanOrEqual => "<=",
            GeneralComparisonOp.GreaterThan => ">",
            GeneralComparisonOp.GreaterThanOrEqual => ">=",
            _ => Op.ToString(),
        };

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("GeneralComparisonNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled value comparison expression (<c>eq</c>, <c>ne</c>, <c>lt</c>, etc.).
/// Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class ValueComparisonNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes a value-comparison node.</summary>
    /// <param name="location">Source span of the full comparison expression.</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal ValueComparisonNode(SourceSpan location, ValueComparisonOp op,
                                  ExpressionNode left, ExpressionNode right)
        : base(location, s_booleanType, left.Dependencies | right.Dependencies)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    /// <summary>Gets the comparison operator.</summary>
    internal ValueComparisonOp Op { get; }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // XPath 2.0 §3.5.1 — value comparison
        XdmAtomicValue? result = EvaluateValueComparison(context);
        if (result is null)
        {
            return EmptyIterator.Instance;
        }
        return new SingleItemIterator(result);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        return EvaluateValueComparison(context);
    }

    private XdmAtomicValue? EvaluateValueComparison(DynamicContext context)
    {
        // XPath 2.0 §3.5.1: each operand must yield at most one item
        XdmAtomicValue? left = Atomization.AtomizeSingle(Left.EvaluateItem(context), Location);
        XdmAtomicValue? right = Atomization.AtomizeSingle(Right.EvaluateItem(context), Location);

        // Empty operand → empty result (not false)
        if (left is null || right is null)
        {
            return null;
        }

        // xs:untypedAtomic → cast to xs:string for value comparisons
        // F&O §3.5.1: untypedAtomic operands in value comparison are cast to xs:string
        if (left.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            left = XdmAtomicValue.String(left.GetStringValue());
        }
        if (right.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            right = XdmAtomicValue.String(right.GetStringValue());
        }

        bool resultBool;

        switch (Op)
        {
            case ValueComparisonOp.Eq:
            case ValueComparisonOp.Ne:
            {
                bool? eq = ComparisonOperators.ValueEqual(left, right);
                if (!eq.HasValue)
                {
                    throw new XPathDynamicException(
                        XPathErrors.XPTY0004,
                        $"Cannot compare xs:{CastTable.GetXsLocalName(left.TypeCode)} with xs:{CastTable.GetXsLocalName(right.TypeCode)}.",
                        Location);
                }
                resultBool = Op == ValueComparisonOp.Eq ? eq.Value : !eq.Value;
                break;
            }

            default:
            {
                int? cmp = ComparisonOperators.ValueCompare(left, right);
                if (!cmp.HasValue)
                {
                    throw new XPathDynamicException(
                        XPathErrors.XPTY0004,
                        $"Cannot compare xs:{CastTable.GetXsLocalName(left.TypeCode)} with xs:{CastTable.GetXsLocalName(right.TypeCode)}.",
                        Location);
                }
                resultBool = Op switch
                {
                    ValueComparisonOp.Lt => cmp.Value < 0,
                    ValueComparisonOp.Le => cmp.Value <= 0,
                    ValueComparisonOp.Gt => cmp.Value > 0,
                    ValueComparisonOp.Ge => cmp.Value >= 0,
                    _ => throw new InvalidOperationException($"Unknown ValueComparisonOp: {Op}"),
                };
                break;
            }
        }

        return XdmAtomicValue.Boolean(resultBool);
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op switch
        {
            ValueComparisonOp.Eq => "eq",
            ValueComparisonOp.Ne => "ne",
            ValueComparisonOp.Lt => "lt",
            ValueComparisonOp.Le => "le",
            ValueComparisonOp.Gt => "gt",
            ValueComparisonOp.Ge => "ge",
            _ => Op.ToString(),
        };

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("ValueComparisonNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled node comparison expression (<c>is</c>, <c>&lt;&lt;</c>, <c>&gt;&gt;</c>).
/// Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class NodeComparisonNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes a node-comparison node.</summary>
    /// <param name="location">Source span of the full comparison expression.</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal NodeComparisonNode(SourceSpan location, NodeComparisonOp op,
                                 ExpressionNode left, ExpressionNode right)
        : base(location, s_booleanType, left.Dependencies | right.Dependencies)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    /// <summary>Gets the comparison operator.</summary>
    internal NodeComparisonOp Op { get; }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // XPath 2.0 §3.5.3 — node comparison
        XdmAtomicValue? result = EvaluateNodeComparison(context);
        if (result is null)
        {
            return EmptyIterator.Instance;
        }
        return new SingleItemIterator(result);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        return EvaluateNodeComparison(context);
    }

    private XdmAtomicValue? EvaluateNodeComparison(DynamicContext context)
    {
        // XPath 2.0 §3.5.3: each operand must yield exactly one node or empty sequence.
        // Evaluate left first; if empty, short-circuit and return empty (avoid evaluating right).
        XdmItem? leftItem = Left.EvaluateItem(context);

        if (leftItem is null)
        {
            return null;
        }

        XdmItem? rightItem = Right.EvaluateItem(context);

        // Empty right operand → empty result
        if (rightItem is null)
        {
            return null;
        }

        if (leftItem is not XdmNode leftNode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Node comparison requires node operands.",
                Location);
        }

        if (rightItem is not XdmNode rightNode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Node comparison requires node operands.",
                Location);
        }

        bool resultBool;

        switch (Op)
        {
            case NodeComparisonOp.Is:
                resultBool = leftNode.Equals(rightNode);
                break;

            case NodeComparisonOp.Precedes:
                resultBool = leftNode.CompareDocumentOrder(rightNode) < 0;
                break;

            case NodeComparisonOp.Follows:
                resultBool = leftNode.CompareDocumentOrder(rightNode) > 0;
                break;

            default:
                throw new InvalidOperationException($"Unknown NodeComparisonOp: {Op}");
        }

        return XdmAtomicValue.Boolean(resultBool);
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op switch
        {
            NodeComparisonOp.Is => "is",
            NodeComparisonOp.Precedes => "<<",
            NodeComparisonOp.Follows => ">>",
            _ => Op.ToString(),
        };

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("NodeComparisonNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}
