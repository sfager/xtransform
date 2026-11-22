// XPath 2.0 §3.7 — instance of, treat as, castable as, cast as.

using System;
using XTransform.Xdm;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-05): implement Iterate for InstanceOfNode, TreatNode, CastableNode, CastNode

/// <summary>
/// A compiled <c>instance of</c> expression. Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class InstanceOfNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes an instance-of node.</summary>
    /// <param name="location">Source span of the full expression.</param>
    /// <param name="expr">The compiled expression to test.</param>
    /// <param name="targetType">The sequence type to test against.</param>
    internal InstanceOfNode(SourceSpan location, ExpressionNode expr, SequenceType targetType)
        : base(location, s_booleanType, expr.Dependencies)
    {
        Expr = expr;
        TargetType = targetType;
    }

    /// <summary>Gets the expression being tested.</summary>
    internal ExpressionNode Expr { get; }

    /// <summary>Gets the target sequence type.</summary>
    internal SequenceType TargetType { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for InstanceOfNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("InstanceOfNode(").Append(TargetType.ToString()).AppendLine(")");
        AppendIndented(sb, Expr.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled <c>treat as</c> expression. Static type is the target type.
/// </summary>
internal sealed class TreatNode : ExpressionNode
{
    /// <summary>Initializes a treat-as node.</summary>
    /// <param name="location">Source span of the full expression.</param>
    /// <param name="expr">The compiled expression to treat.</param>
    /// <param name="targetType">The declared sequence type.</param>
    internal TreatNode(SourceSpan location, ExpressionNode expr, SequenceType targetType)
        : base(location, targetType, expr.Dependencies)
    {
        Expr = expr;
        TargetType = targetType;
    }

    /// <summary>Gets the expression being treated.</summary>
    internal ExpressionNode Expr { get; }

    /// <summary>Gets the target sequence type.</summary>
    internal SequenceType TargetType { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for TreatNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("TreatNode(").Append(TargetType.ToString()).AppendLine(")");
        AppendIndented(sb, Expr.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled <c>castable as</c> expression. Static type is <c>xs:boolean</c>.
/// </summary>
internal sealed class CastableNode : ExpressionNode
{
    private static readonly SequenceType s_booleanType =
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    /// <summary>Initializes a castable-as node.</summary>
    /// <param name="location">Source span of the full expression.</param>
    /// <param name="expr">The compiled expression to test.</param>
    /// <param name="targetType">The target atomic type.</param>
    /// <param name="allowEmpty">Whether <c>?</c> was present (empty sequence allowed).</param>
    internal CastableNode(SourceSpan location, ExpressionNode expr, SequenceType targetType,
                           bool allowEmpty)
        : base(location, s_booleanType, expr.Dependencies)
    {
        Expr = expr;
        TargetType = targetType;
        AllowEmpty = allowEmpty;
    }

    /// <summary>Gets the expression being tested.</summary>
    internal ExpressionNode Expr { get; }

    /// <summary>Gets the target atomic type.</summary>
    internal SequenceType TargetType { get; }

    /// <summary>Gets a value indicating whether the empty sequence is allowed.</summary>
    internal bool AllowEmpty { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for CastableNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("CastableNode(").Append(TargetType.ToString()).AppendLine(")");
        AppendIndented(sb, Expr.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// A compiled <c>cast as</c> expression. Static type is the target type.
/// </summary>
internal sealed class CastNode : ExpressionNode
{
    /// <summary>Initializes a cast-as node.</summary>
    /// <param name="location">Source span of the full expression.</param>
    /// <param name="expr">The compiled expression to cast.</param>
    /// <param name="targetType">The target atomic type (possibly with <c>?</c>).</param>
    /// <param name="allowEmpty">Whether <c>?</c> was present (empty sequence allowed).</param>
    internal CastNode(SourceSpan location, ExpressionNode expr, SequenceType targetType,
                      bool allowEmpty)
        : base(location, targetType, expr.Dependencies)
    {
        Expr = expr;
        TargetType = targetType;
        AllowEmpty = allowEmpty;
    }

    /// <summary>Gets the expression being cast.</summary>
    internal ExpressionNode Expr { get; }

    /// <summary>Gets the target atomic type.</summary>
    internal SequenceType TargetType { get; }

    /// <summary>Gets a value indicating whether the empty sequence is allowed.</summary>
    internal bool AllowEmpty { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-05): implement Iterate
        throw new NotImplementedException("Evaluation for CastNode is implemented in P2-05.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("CastNode(").Append(TargetType.ToString()).AppendLine(")");
        AppendIndented(sb, Expr.ToDebugString(), 1);
        return sb.ToString().TrimEnd();
    }
}
