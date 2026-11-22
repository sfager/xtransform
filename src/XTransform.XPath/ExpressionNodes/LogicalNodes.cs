// XPath 2.0 §3.6 — logical expressions (or, and).

using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.ExpressionNodes;

/// <summary>
/// A compiled <c>or</c> expression. Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class OrNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes an or-expression node.</summary>
    /// <param name="location">Source span of the full or-expression.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal OrNode(SourceSpan location, ExpressionNode left, ExpressionNode right)
        : base(location, s_booleanType, left.Dependencies | right.Dependencies)
    {
        Left = left;
        Right = right;
    }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context)
    {
        // XPath 2.0 §3.6: short-circuit on left true
        return Left.EffectiveBooleanValue(context) || Right.EffectiveBooleanValue(context);
    }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        return new SingleItemIterator(XdmAtomicValue.Boolean(EffectiveBooleanValue(context)));
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        return XdmAtomicValue.Boolean(EffectiveBooleanValue(context));
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("OrNode");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled <c>and</c> expression. Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class AndNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes an and-expression node.</summary>
    /// <param name="location">Source span of the full and-expression.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    internal AndNode(SourceSpan location, ExpressionNode left, ExpressionNode right)
        : base(location, s_booleanType, left.Dependencies | right.Dependencies)
    {
        Left = left;
        Right = right;
    }

    /// <summary>Gets the left operand.</summary>
    internal ExpressionNode Left { get; }

    /// <summary>Gets the right operand.</summary>
    internal ExpressionNode Right { get; }

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context)
    {
        // XPath 2.0 §3.6: short-circuit on left false
        return Left.EffectiveBooleanValue(context) && Right.EffectiveBooleanValue(context);
    }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        return new SingleItemIterator(XdmAtomicValue.Boolean(EffectiveBooleanValue(context)));
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        return XdmAtomicValue.Boolean(EffectiveBooleanValue(context));
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("AndNode");
        AppendIndented(sb, Left.ToDebugString(), 1);
        AppendIndented(sb, Right.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}
