// XPath 2.0 §3.7 — instance of, treat as, castable as, cast as.

using System;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.ExpressionNodes;

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
        => new SingleItemIterator(XdmAtomicValue.Boolean(EvaluateBoolean(context)));

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
        => XdmAtomicValue.Boolean(EvaluateBoolean(context));

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context) => EvaluateBoolean(context);

    private bool EvaluateBoolean(DynamicContext context)
    {
        // XPath 2.0 §3.5.2: "instance of" tests the sequence against a SequenceType.
        // Materialise the operand into an XdmSequence so SequenceType.Matches can inspect
        // count + per-item type without pulling multiple times.
        return TargetType.Matches(MaterialiseOperand(Expr, context));
    }

    internal static XdmSequence MaterialiseOperand(ExpressionNode node, DynamicContext context)
    {
        IXdmIterator iter = node.Iterate(context);
        XdmItem? first = iter.Next();
        if (first is null)
        {
            return XdmSequence.Empty;
        }
        XdmItem? second = iter.Next();
        if (second is null)
        {
            return XdmSequence.Of(first);
        }
        // Multi-item sequence — read the rest.
        System.Collections.Generic.List<XdmItem> items = new() { first, second };
        while (true)
        {
            XdmItem? n = iter.Next();
            if (n is null) { break; }
            items.Add(n);
        }
        return XdmSequence.Of(items.ToArray());
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
        // XPath 2.0 §3.10.5: treat-as checks the operand's dynamic type against the target.
        // Mismatch → XPTY0004; otherwise the operand passes through unchanged.
        XdmSequence materialised = InstanceOfNode.MaterialiseOperand(Expr, context);
        if (!TargetType.Matches(materialised))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"treat as {TargetType}: value does not match the declared type",
                Location);
        }
        return materialised.GetIterator();
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
        => new SingleItemIterator(XdmAtomicValue.Boolean(EvaluateBoolean(context)));

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
        => XdmAtomicValue.Boolean(EvaluateBoolean(context));

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context) => EvaluateBoolean(context);

    private bool EvaluateBoolean(DynamicContext context)
    {
        XdmTypeCode target = CastHelpers.GetTargetTypeCode(TargetType, Location);

        IXdmIterator source = Atomization.Atomize(Expr.Iterate(context));
        XdmItem? first = source.Next();
        if (first is null)
        {
            // Empty sequence is castable iff ? was specified or target is xs:string/xs:anyURI/xs:untypedAtomic
            // Per F&O §17.1.1 strictly: castable iff AllowEmpty. For types that construct from
            // empty strings (xs:string), empty → castable only when AllowEmpty is true.
            return AllowEmpty;
        }
        XdmItem? second = source.Next();
        if (second is not null)
        {
            // Castable-as requires a single-item (possibly empty) operand; more → false.
            return false;
        }

        return CastTable.TryCast((XdmAtomicValue)first, target, out _);
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
        XdmItem? item = EvaluateItem(context);
        return item is null ? EmptyIterator.Instance : new SingleItemIterator(item);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        XdmTypeCode target = CastHelpers.GetTargetTypeCode(TargetType, Location);

        IXdmIterator source = Atomization.Atomize(Expr.Iterate(context));
        XdmItem? first = source.Next();
        if (first is null)
        {
            if (AllowEmpty)
            {
                return null;
            }
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "cast as: operand is the empty sequence but '?' was not specified",
                Location);
        }
        XdmItem? second = source.Next();
        if (second is not null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "cast as: operand must be a single atomic value",
                Location);
        }

        // CastTable.Cast throws XPathDynamicException with FORG0001 / XPTY0004 as appropriate.
        return CastTable.Cast((XdmAtomicValue)first, target, Location);
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

/// <summary>Shared helpers for cast / castable nodes.</summary>
internal static class CastHelpers
{
    /// <summary>
    /// Extracts the target atomic type code from a SingleType sequence type; throws
    /// XPTY0004 when the sequence type isn't an atomic type (e.g. a kind test).
    /// </summary>
    public static XdmTypeCode GetTargetTypeCode(SequenceType target, SourceSpan location)
    {
        if (target.ItemType is ItemType.AtomicItemType atomic)
        {
            return atomic.TypeCode;
        }
        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            $"cast / castable target must be an atomic type, not {target}",
            location);
    }
}
