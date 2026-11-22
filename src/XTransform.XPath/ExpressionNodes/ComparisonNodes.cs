// XPath 2.0 §3.5 — comparison expressions: general, value, and node comparisons.

using System;
using XTransform.Xdm;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-05): implement Iterate for GeneralComparisonNode, ValueComparisonNode, NodeComparisonNode

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
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for GeneralComparisonNode is implemented in P2-05.");
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
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for ValueComparisonNode is implemented in P2-05.");
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
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for NodeComparisonNode is implemented in P2-05.");
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
