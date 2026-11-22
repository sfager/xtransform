// XPath 2.0 §3.4 — arithmetic expressions; §3.3 — range expressions.

using System;
using System.Globalization;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Ast;
using XTransform.XPath.Operators;

namespace XTransform.XPath.ExpressionNodes;

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
        // XPath 2.0 §3.4 — arithmetic expression evaluation
        XdmAtomicValue? left = Atomization.AtomizeSingle(Left.EvaluateItem(context), Location);
        XdmAtomicValue? right = Atomization.AtomizeSingle(Right.EvaluateItem(context), Location);

        // F&O §6.2: empty operand → empty result
        if (left is null || right is null)
        {
            return EmptyIterator.Instance;
        }

        XdmAtomicValue result = Op switch
        {
            ArithmeticOp.Add          => ArithmeticOperators.Add(left, right, Location),
            ArithmeticOp.Subtract     => ArithmeticOperators.Subtract(left, right, Location),
            ArithmeticOp.Multiply     => ArithmeticOperators.Multiply(left, right, Location),
            ArithmeticOp.Divide       => ArithmeticOperators.Divide(left, right, Location),
            ArithmeticOp.IntegerDivide => ArithmeticOperators.IntegerDivide(left, right, Location),
            ArithmeticOp.Modulo       => ArithmeticOperators.Modulo(left, right, Location),
            _ => throw new InvalidOperationException($"Unknown ArithmeticOp: {Op}"),
        };

        return new SingleItemIterator(result);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        // Fast path: avoid wrapping in iterator when only one item needed.
        XdmAtomicValue? left = Atomization.AtomizeSingle(Left.EvaluateItem(context), Location);
        XdmAtomicValue? right = Atomization.AtomizeSingle(Right.EvaluateItem(context), Location);

        if (left is null || right is null)
        {
            return null;
        }

        return Op switch
        {
            ArithmeticOp.Add          => ArithmeticOperators.Add(left, right, Location),
            ArithmeticOp.Subtract     => ArithmeticOperators.Subtract(left, right, Location),
            ArithmeticOp.Multiply     => ArithmeticOperators.Multiply(left, right, Location),
            ArithmeticOp.Divide       => ArithmeticOperators.Divide(left, right, Location),
            ArithmeticOp.IntegerDivide => ArithmeticOperators.IntegerDivide(left, right, Location),
            ArithmeticOp.Modulo       => ArithmeticOperators.Modulo(left, right, Location),
            _ => throw new InvalidOperationException($"Unknown ArithmeticOp: {Op}"),
        };
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
        // XPath 2.0 §3.3 — range expression
        XdmAtomicValue? lhs = Atomization.AtomizeSingle(Left.EvaluateItem(context), Location);
        XdmAtomicValue? rhs = Atomization.AtomizeSingle(Right.EvaluateItem(context), Location);

        if (lhs is null || rhs is null)
        {
            return EmptyIterator.Instance;
        }

        long from;
        long to;
        try
        {
            from = lhs.TypeCode == XdmTypeCode.UntypedAtomic
                ? long.Parse(lhs.GetStringValue(), CultureInfo.InvariantCulture)
                : lhs.AsInt64();
            to = rhs.TypeCode == XdmTypeCode.UntypedAtomic
                ? long.Parse(rhs.GetStringValue(), CultureInfo.InvariantCulture)
                : rhs.AsInt64();
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is InvalidCastException)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Range expression endpoints must be xs:integer: {ex.Message}",
                Location);
        }

        if (from > to)
        {
            return EmptyIterator.Instance;
        }

        return new RangeIterator(from, to);
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
        // XPath 2.0 §3.3.3 / F&O §15.3 — set operations on sequences of NODES.
        // Collect both sides, verify all items are nodes (else XPTY0004), and perform the
        // operation using node identity. Output is in document order with duplicates removed.
        System.Collections.Generic.List<XdmNode> leftNodes = CollectNodes(Left, context);
        System.Collections.Generic.List<XdmNode> rightNodes = CollectNodes(Right, context);

        System.Collections.Generic.List<XdmNode> result = Op switch
        {
            SetOp.Union => Union(leftNodes, rightNodes),
            SetOp.Intersect => Intersect(leftNodes, rightNodes),
            SetOp.Except => Except(leftNodes, rightNodes),
            _ => throw new InvalidOperationException($"Unknown set op {Op}"),
        };

        XdmItem[] asItems = new XdmItem[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            asItems[i] = result[i];
        }
        return new ArrayIterator(asItems);
    }

    private System.Collections.Generic.List<XdmNode> CollectNodes(ExpressionNode expr, DynamicContext context)
    {
        System.Collections.Generic.List<XdmNode> nodes = new();
        IXdmIterator iter = expr.Iterate(context);
        while (true)
        {
            XdmItem? n = iter.Next();
            if (n is null) { break; }
            if (n is not XdmNode node)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    $"{OpName(Op)} requires sequences of nodes; an atomic value was found",
                    Location);
            }
            nodes.Add(node);
        }
        return nodes;
    }

    private static System.Collections.Generic.List<XdmNode> Union(
        System.Collections.Generic.List<XdmNode> a,
        System.Collections.Generic.List<XdmNode> b)
    {
        System.Collections.Generic.List<XdmNode> all = new(a.Count + b.Count);
        all.AddRange(a);
        all.AddRange(b);
        return SortAndDeduplicate(all);
    }

    private static System.Collections.Generic.List<XdmNode> Intersect(
        System.Collections.Generic.List<XdmNode> a,
        System.Collections.Generic.List<XdmNode> b)
    {
        System.Collections.Generic.HashSet<XdmNode> bSet = new(b, XdmNodeIdentityComparer.Instance);
        System.Collections.Generic.List<XdmNode> result = new();
        foreach (XdmNode n in a)
        {
            if (bSet.Contains(n))
            {
                result.Add(n);
            }
        }
        return SortAndDeduplicate(result);
    }

    private static System.Collections.Generic.List<XdmNode> Except(
        System.Collections.Generic.List<XdmNode> a,
        System.Collections.Generic.List<XdmNode> b)
    {
        System.Collections.Generic.HashSet<XdmNode> bSet = new(b, XdmNodeIdentityComparer.Instance);
        System.Collections.Generic.List<XdmNode> result = new();
        foreach (XdmNode n in a)
        {
            if (!bSet.Contains(n))
            {
                result.Add(n);
            }
        }
        return SortAndDeduplicate(result);
    }

    private static System.Collections.Generic.List<XdmNode> SortAndDeduplicate(System.Collections.Generic.List<XdmNode> nodes)
    {
        if (nodes.Count <= 1)
        {
            return nodes;
        }
        nodes.Sort((x, y) => x.CompareDocumentOrder(y));
        System.Collections.Generic.List<XdmNode> dedup = new(nodes.Count);
        dedup.Add(nodes[0]);
        for (int i = 1; i < nodes.Count; i++)
        {
            if (!nodes[i].Equals(nodes[i - 1]))
            {
                dedup.Add(nodes[i]);
            }
        }
        return dedup;
    }

    private static string OpName(SetOp op) => op switch
    {
        SetOp.Union => "union",
        SetOp.Intersect => "intersect",
        SetOp.Except => "except",
        _ => op.ToString(),
    };

    private sealed class XdmNodeIdentityComparer : System.Collections.Generic.IEqualityComparer<XdmNode>
    {
        internal static readonly XdmNodeIdentityComparer Instance = new();
        public bool Equals(XdmNode? x, XdmNode? y) => x is null ? y is null : x.Equals(y);
        public int GetHashCode(XdmNode obj) => obj.GetHashCode();
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
        // XPath 2.0 §3.4 — unary arithmetic
        XdmItem? item = Operand.EvaluateItem(context);
        XdmAtomicValue? atom = Atomization.AtomizeSingle(item, Location);

        if (atom is null)
        {
            return EmptyIterator.Instance;
        }

        XdmAtomicValue result;
        if (Op == UnaryOp.Negate)
        {
            result = ArithmeticOperators.Negate(atom, Location);
        }
        else
        {
            // UnaryOp.Plus is identity but xs:untypedAtomic → cast to xs:double
            if (atom.TypeCode == XdmTypeCode.UntypedAtomic)
            {
                result = CastTable.Cast(atom, XdmTypeCode.Double, Location);
            }
            else
            {
                result = atom;
            }
        }

        return new SingleItemIterator(result);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        XdmItem? item = Operand.EvaluateItem(context);
        XdmAtomicValue? atom = Atomization.AtomizeSingle(item, Location);

        if (atom is null)
        {
            return null;
        }

        if (Op == UnaryOp.Negate)
        {
            return ArithmeticOperators.Negate(atom, Location);
        }

        if (atom.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            return CastTable.Cast(atom, XdmTypeCode.Double, Location);
        }

        return atom;
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

/// <summary>
/// A lazy iterator over an integer range [from..to] inclusive.
/// XPath 2.0 §3.3 — range expression.
/// </summary>
internal sealed class RangeIterator : IXdmIterator
{
    private readonly long _from;
    private readonly long _to;
    private long _next;
    private int _position;

    internal RangeIterator(long from, long to)
    {
        _from = from;
        _to = to;
        _next = from;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_next > _to)
        {
            return null;
        }
        _position++;
        return XdmAtomicValue.Integer(_next++);
    }

    /// <inheritdoc/>
    public IXdmIterator Clone() => new RangeIterator(_from, _to);
}
