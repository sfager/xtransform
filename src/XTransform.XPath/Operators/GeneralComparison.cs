// XPath 2.0 §3.5.2 — general comparison (existential semantics).

using XTransform.Xdm;

namespace XTransform.XPath.Operators;

using XTransform.XPath.ExpressionNodes;

/// <summary>
/// Implements XPath 2.0 §3.5.2 general comparison with existential semantics.
/// </summary>
internal static class GeneralComparison
{
    /// <summary>
    /// Evaluates a general comparison. Atomizes both sequences and returns true when
    /// any pair from left × right satisfies the operator.
    /// F&amp;O §3.5.2 — existential semantics: returns false when either sequence is empty.
    /// </summary>
    public static bool Evaluate(
        GeneralComparisonOp op,
        IXdmIterator left,
        IXdmIterator right,
        SourceSpan location)
    {
        // Atomize both operands
        IXdmIterator atomLeft = Atomization.Atomize(left);
        IXdmIterator atomRight = Atomization.Atomize(right);

        // Collect left side (we need to iterate right for each left item)
        // For short-circuit: collect left items first, then iterate right once per pair.
        // To avoid multiple scans of right, buffer the right side.
        System.Collections.Generic.List<XdmAtomicValue> rightItems = new System.Collections.Generic.List<XdmAtomicValue>();
        {
            XdmItem? ri;
            while ((ri = atomRight.Next()) is not null)
            {
                rightItems.Add((XdmAtomicValue)ri);
            }
        }

        // Empty right → false
        if (rightItems.Count == 0)
        {
            return false;
        }

        // For each left item, compare against all right items
        XdmItem? li;
        while ((li = atomLeft.Next()) is not null)
        {
            XdmAtomicValue lv = (XdmAtomicValue)li;

            for (int i = 0; i < rightItems.Count; i++)
            {
                XdmAtomicValue rv = rightItems[i];

                XdmAtomicValue coercedLeft = lv;
                XdmAtomicValue coercedRight = rv;

                // XPath 2.0 §3.5.2: untypedAtomic coercion rules
                bool leftUntyped = lv.TypeCode == XdmTypeCode.UntypedAtomic;
                bool rightUntyped = rv.TypeCode == XdmTypeCode.UntypedAtomic;

                if (leftUntyped && rightUntyped)
                {
                    // Both untypedAtomic → both to xs:string
                    coercedLeft = XdmAtomicValue.String(lv.GetStringValue());
                    coercedRight = XdmAtomicValue.String(rv.GetStringValue());
                }
                else if (leftUntyped && NumericPromotion.IsNumeric(rv.TypeCode))
                {
                    // Left untypedAtomic, right numeric → cast left to xs:double
                    coercedLeft = CastTable.Cast(lv, XdmTypeCode.Double, location);
                }
                else if (rightUntyped && NumericPromotion.IsNumeric(lv.TypeCode))
                {
                    // Right untypedAtomic, left numeric → cast right to xs:double
                    coercedRight = CastTable.Cast(rv, XdmTypeCode.Double, location);
                }
                else if (leftUntyped && !rightUntyped)
                {
                    // Left untyped, right is typed → promote left to right's type
                    if (CastTable.TryCast(lv, rv.TypeCode, out XdmAtomicValue? promoted))
                    {
                        coercedLeft = promoted!;
                    }
                    else
                    {
                        // Cast failed — skip this pair (no match)
                        continue;
                    }
                }
                else if (rightUntyped && !leftUntyped)
                {
                    // Right untyped, left is typed → promote right to left's type
                    if (CastTable.TryCast(rv, lv.TypeCode, out XdmAtomicValue? promoted))
                    {
                        coercedRight = promoted!;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (EvaluatePair(op, coercedLeft, coercedRight, location))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // XPath 2.0 §3.5.2: a pair of atomic values that cannot be compared is a type error, not a mismatch.
    private static XPathDynamicException NotComparable(XdmAtomicValue left, XdmAtomicValue right, SourceSpan location) =>
        new XPathDynamicException(
            XPathErrors.XPTY0004,
            $"Cannot compare xs:{CastTable.GetXsLocalName(left.TypeCode)} with xs:{CastTable.GetXsLocalName(right.TypeCode)}.",
            location);

    private static bool EvaluatePair(
        GeneralComparisonOp op,
        XdmAtomicValue left,
        XdmAtomicValue right,
        SourceSpan location)
    {
        switch (op)
        {
            case GeneralComparisonOp.Equals:
            {
                bool? result = ComparisonOperators.ValueEqual(left, right);
                return result ?? throw NotComparable(left, right, location);
            }

            case GeneralComparisonOp.NotEquals:
            {
                bool? result = ComparisonOperators.ValueEqual(left, right);
                return !(result ?? throw NotComparable(left, right, location));
            }

            default:
            {
                int? cmp = ComparisonOperators.ValueCompare(left, right);
                if (!cmp.HasValue)
                {
                    throw NotComparable(left, right, location);
                }
                return op switch
                {
                    GeneralComparisonOp.LessThan => cmp.Value < 0,
                    GeneralComparisonOp.LessThanOrEqual => cmp.Value <= 0,
                    GeneralComparisonOp.GreaterThan => cmp.Value > 0,
                    GeneralComparisonOp.GreaterThanOrEqual => cmp.Value >= 0,
                    _ => false,
                };
            }
        }
    }
}
