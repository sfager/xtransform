// F&O §6.2 — numeric type promotion lattice.
// xs:integer < xs:decimal < xs:float < xs:double

using System;
using XTransform.Xdm;

namespace XTransform.XPath.Operators;

/// <summary>
/// Helpers for F&amp;O §6.2 numeric type promotion in arithmetic and comparison operations.
/// </summary>
internal static class NumericPromotion
{
    /// <summary>
    /// Returns the common numeric type for a binary arithmetic or comparison operation,
    /// or throws XPTY0004 when either operand is not numeric.
    /// </summary>
    /// <param name="a">Left operand (after untypedAtomic → double pre-processing).</param>
    /// <param name="b">Right operand (after untypedAtomic → double pre-processing).</param>
    /// <param name="location">Source span for error reporting.</param>
    /// <returns>The common <see cref="XdmTypeCode"/> to which both operands should be promoted.</returns>
    public static XdmTypeCode CommonNumericType(XdmAtomicValue a, XdmAtomicValue b, SourceSpan location)
    {
        XdmTypeCode at = a.TypeCode;
        XdmTypeCode bt = b.TypeCode;

        if (!IsNumeric(at))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Arithmetic requires a numeric operand; got xs:{CastTable.GetXsLocalName(at)}.",
                location);
        }

        if (!IsNumeric(bt))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Arithmetic requires a numeric operand; got xs:{CastTable.GetXsLocalName(bt)}.",
                location);
        }

        // F&O §6.2 promotion lattice: integer < decimal < float < double
        if (at == XdmTypeCode.Double || bt == XdmTypeCode.Double)
        {
            return XdmTypeCode.Double;
        }

        if (at == XdmTypeCode.Float || bt == XdmTypeCode.Float)
        {
            return XdmTypeCode.Float;
        }

        // Both are in the decimal/integer family.
        bool aBothInDecimal = at == XdmTypeCode.Decimal || IsIntegerFamily(at);
        bool bBothInDecimal = bt == XdmTypeCode.Decimal || IsIntegerFamily(bt);

        if (at == XdmTypeCode.Decimal || bt == XdmTypeCode.Decimal)
        {
            return XdmTypeCode.Decimal;
        }

        // Both integer-family
        return XdmTypeCode.Integer;
    }

    /// <summary>
    /// Promotes <paramref name="value"/> to the given numeric target type.
    /// Uses <see cref="CastTable.Cast"/> for the actual numeric widening.
    /// </summary>
    public static XdmAtomicValue PromoteNumeric(XdmAtomicValue value, XdmTypeCode target, SourceSpan location)
    {
        if (value.TypeCode == target)
        {
            return value;
        }

        // For integer family → target, the Cast handles all cases.
        return CastTable.Cast(value, target, location);
    }

    /// <summary>Returns true when <paramref name="type"/> is in the numeric hierarchy.</summary>
    public static bool IsNumeric(XdmTypeCode type)
    {
        return type == XdmTypeCode.Double
            || type == XdmTypeCode.Float
            || type == XdmTypeCode.Decimal
            || IsIntegerFamily(type);
    }

    /// <summary>Returns true when <paramref name="type"/> is in the xs:integer family.</summary>
    public static bool IsIntegerFamily(XdmTypeCode type)
    {
        return CastTable.IsSubtypeOf(type, XdmTypeCode.Integer);
    }
}
