// XPath 2.0 §3.4 — arithmetic expressions; §3.3 — range expressions.

using System;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath.Ast;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-05): implement Iterate for ArithmeticNode, RangeNode, UnaryNode, SetOpNode

/// <summary>
/// A compiled arithmetic expression (<c>+</c>, <c>-</c>, <c>*</c>, <c>div</c>, <c>idiv</c>, <c>mod</c>).
/// Static type is over-approximate: <c>xs:integer*</c> unless a double or decimal operand is detected.
/// </summary>
internal sealed class ArithmeticNode : ExpressionNode
{
    /// <summary>Initializes an arithmetic-expression node.</summary>
    /// <param name="location">Source span of the full arithmetic expression.</param>
    /// <param name="op">The arithmetic operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal ArithmeticNode(SourceSpan location, ArithmeticOp op,
                             ExpressionNode left, ExpressionNode right)
        : base(location,
               InferType(left, right),
               left.Dependencies | right.Dependencies)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    /// <summary>Gets the arithmetic operator.</summary>
    internal ArithmeticOp Op { get; }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for ArithmeticNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op switch
        {
            ArithmeticOp.Add => "+",
            ArithmeticOp.Subtract => "-",
            ArithmeticOp.Multiply => "*",
            ArithmeticOp.Divide => "div",
            ArithmeticOp.IntegerDivide => "idiv",
            ArithmeticOp.Modulo => "mod",
            _ => Op.ToString(),
        };

        StringBuilder sb = new StringBuilder();
        sb.Append("ArithmeticNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Over-approximate type inference: xs:double* when any double operand; xs:decimal*; else xs:integer*.
    /// </summary>
    private static SequenceType InferType(ExpressionNode left, ExpressionNode right)
    {
        // XPath 2.0 §6.2 — arithmetic type promotion (over-approximate)
        XdmTypeCode leftCode = GetAtomicCode(left);
        XdmTypeCode rightCode = GetAtomicCode(right);

        if (leftCode == XdmTypeCode.Double || rightCode == XdmTypeCode.Double)
        {
            return new SequenceType(ItemType.Atomic(XdmTypeCode.Double), Occurrence.ZeroOrMore);
        }

        if (leftCode == XdmTypeCode.Decimal || rightCode == XdmTypeCode.Decimal)
        {
            return new SequenceType(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ZeroOrMore);
        }

        return new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
    }

    private static XdmTypeCode GetAtomicCode(ExpressionNode node)
    {
        if (node.StaticType.ItemType is ItemType.AtomicItemType at)
        {
            return at.TypeCode;
        }

        return XdmTypeCode.AnyAtomicType;
    }
}

/// <summary>
/// A compiled range expression <c>e to e</c>. Static type is <c>xs:integer*</c>.
/// </summary>
internal sealed class RangeNode : ExpressionNode
{
    private static readonly SequenceType s_integerStar =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);

    /// <summary>Initializes a range-expression node.</summary>
    /// <param name="location">Source span of the full range expression.</param>
    /// <param name="left">The lower-bound expression.</param>
    /// <param name="right">The upper-bound expression.</param>
    internal RangeNode(SourceSpan location, ExpressionNode left, ExpressionNode right)
        : base(location, s_integerStar, left.Dependencies | right.Dependencies)
    {
        Left = left;
        Right = right;
    }

    /// <summary>Gets the lower-bound expression.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the upper-bound expression.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for RangeNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("RangeNode(to)");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>Operator codes for set operations.</summary>
internal enum SetOp
{
    /// <summary><c>union</c> / <c>|</c></summary>
    Union,
    /// <summary><c>intersect</c></summary>
    Intersect,
    /// <summary><c>except</c></summary>
    Except,
}

/// <summary>
/// A compiled set operation (<c>union</c>, <c>intersect</c>, <c>except</c>).
/// Static type is <c>node()*</c> (over-approximate).
/// </summary>
internal sealed class SetOpNode : ExpressionNode
{
    private static readonly SequenceType s_nodesStar =
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrMore);

    /// <summary>Initializes a set-operation node.</summary>
    /// <param name="location">Source span of the full set expression.</param>
    /// <param name="op">The set operation.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal SetOpNode(SourceSpan location, SetOp op, ExpressionNode left, ExpressionNode right)
        : base(location, s_nodesStar, left.Dependencies | right.Dependencies)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    /// <summary>Gets the set operation.</summary>
    internal SetOp Op { get; }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for SetOpNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op switch
        {
            SetOp.Union => "union",
            SetOp.Intersect => "intersect",
            SetOp.Except => "except",
            _ => Op.ToString(),
        };

        StringBuilder sb = new StringBuilder();
        sb.Append("SetOpNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>Operator codes for unary expressions.</summary>
internal enum UnaryOp
{
    /// <summary>Unary minus: <c>-e</c></summary>
    Negate,
    /// <summary>Unary plus: <c>+e</c> (no-op but kept in tree)</summary>
    Plus,
}

/// <summary>
/// A compiled unary expression (<c>-e</c> or <c>+e</c>).
/// Static type is taken from the operand's static type.
/// </summary>
internal sealed class UnaryNode : ExpressionNode
{
    /// <summary>Initializes a unary-expression node.</summary>
    /// <param name="location">Source span of the full unary expression.</param>
    /// <param name="op">The unary operator.</param>
    /// <param name="operand">The operand expression.</param>
    internal UnaryNode(SourceSpan location, UnaryOp op, ExpressionNode operand)
        : base(location, operand.StaticType, operand.Dependencies)
    {
        Op = op;
        Operand = operand;
    }

    /// <summary>Gets the unary operator.</summary>
    internal UnaryOp Op { get; }

    /// <summary>Gets the operand expression.</summary>
    internal ExpressionNode Operand { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for UnaryNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string opStr = Op == UnaryOp.Negate ? "-" : "+";
        StringBuilder sb = new StringBuilder();
        sb.Append("UnaryNode(").Append(opStr).AppendLine(")");
        AppendIndented(sb, Operand.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}
