// F&O §3.1.5 — function conversion rules applied by the compiler at the call site.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath.Operators;

namespace XTransform.XPath;

/// <summary>
/// Applies the F&amp;O §3.1.5 function-conversion rules to a raw argument iterator,
/// producing a materialised <see cref="XdmSequence"/> ready for the function invoker.
/// Rules applied in order:
/// <list type="number">
///   <item>Atomise when the declared parameter type is atomic.</item>
///   <item>Cast xs:untypedAtomic items to the expected atomic type.</item>
///   <item>Numeric promotion (integer → decimal → float → double).</item>
///   <item>anyURI → xs:string promotion.</item>
/// </list>
/// </summary>
internal static class FunctionConversion
{
    /// <summary>
    /// Applies function-conversion rules to <paramref name="source"/> per
    /// <paramref name="expected"/> and returns the materialised argument sequence.
    /// </summary>
    /// <param name="source">Raw argument iterator (not yet consumed).</param>
    /// <param name="expected">The declared parameter sequence type.</param>
    /// <param name="location">Source span for error reporting.</param>
    /// <returns>A materialised <see cref="XdmSequence"/> after conversion.</returns>
    public static XdmSequence Apply(IXdmIterator source, SequenceType expected, SourceSpan location)
    {
        // 1. Atomise when the declared item type is atomic.
        bool atomize = expected.ItemType is ItemType.AtomicItemType;
        IXdmIterator step = atomize ? Atomization.Atomize(source) : source;

        // 2-4. Per-item conversion.
        XdmTypeCode? targetAtomicCode = (expected.ItemType as ItemType.AtomicItemType)?.TypeCode;

        List<XdmItem> buffer = new List<XdmItem>();
        while (true)
        {
            XdmItem? item = step.Next();
            if (item is null)
            {
                break;
            }

            if (targetAtomicCode.HasValue && item is XdmAtomicValue av)
            {
                item = ConvertAtomic(av, targetAtomicCode.Value, location);
            }

            buffer.Add(item);
        }

        // 5. Cardinality check.
        ValidateCardinality(buffer, expected.Occurrence, location);

        // 6. Materialise.
        if (buffer.Count == 0)
        {
            return XdmSequence.Empty;
        }

        if (buffer.Count == 1)
        {
            return XdmSequence.Of(buffer[0]);
        }

        return XdmSequence.Of(buffer.ToArray());
    }

    private static XdmAtomicValue ConvertAtomic(XdmAtomicValue value, XdmTypeCode target, SourceSpan location)
    {
        if (value.TypeCode == target)
        {
            return value;
        }

        // F&O §3.1.5: xs:anyAtomicType is abstract — any atomic value satisfies it; pass through.
        // (CastTable.Cast would throw XPTY0004 for target==AnyAtomicType.)
        if (target == XdmTypeCode.AnyAtomicType)
        {
            return value;
        }

        // Cast xs:untypedAtomic to the expected type (F&O §3.1.5 rule 2).
        if (value.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            return CastTable.Cast(value, target, location);
        }

        // Numeric promotion: widen when target is broader (F&O §3.1.5 rule 3).
        if (NumericPromotion.IsNumeric(value.TypeCode) && NumericPromotion.IsNumeric(target))
        {
            if (IsWider(target, value.TypeCode))
            {
                return NumericPromotion.PromoteNumeric(value, target, location);
            }
        }

        // anyURI → string promotion (F&O §3.1.5 rule 4).
        if (value.TypeCode == XdmTypeCode.AnyUri && target == XdmTypeCode.String)
        {
            return XdmAtomicValue.String(value.GetStringValue());
        }

        // F&O §3.1.5: if none of the above conversions apply and the value type is not a
        // subtype of the target, raise XPTY0004.
        // (Subtype values — e.g. xs:normalizedString when target is xs:string — pass through.)
        if (!CastTable.IsSubtypeOf(value.TypeCode, target))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Function argument type mismatch: cannot convert xs:{CastTable.GetXsLocalName(value.TypeCode)} to xs:{CastTable.GetXsLocalName(target)}.",
                location);
        }

        // Subtype of target — pass through.
        return value;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="wider"/> is strictly
    /// higher in the numeric promotion lattice than <paramref name="narrower"/>.
    /// F&amp;O §3.1.5 / §6.2: integer &lt; decimal &lt; float &lt; double.
    /// </summary>
    private static bool IsWider(XdmTypeCode wider, XdmTypeCode narrower)
    {
        switch (wider)
        {
            case XdmTypeCode.Double:
                return narrower == XdmTypeCode.Float
                    || narrower == XdmTypeCode.Decimal
                    || NumericPromotion.IsIntegerFamily(narrower);

            case XdmTypeCode.Float:
                return narrower == XdmTypeCode.Decimal
                    || NumericPromotion.IsIntegerFamily(narrower);

            case XdmTypeCode.Decimal:
                return NumericPromotion.IsIntegerFamily(narrower);

            default:
                return false;
        }
    }

    private static void ValidateCardinality(
        List<XdmItem> items, Occurrence occ, SourceSpan location)
    {
        int count = items.Count;
        bool ok;
        switch (occ)
        {
            case Occurrence.Empty:
                ok = count == 0;
                break;
            case Occurrence.ExactlyOne:
                ok = count == 1;
                break;
            case Occurrence.ZeroOrOne:
                ok = count <= 1;
                break;
            case Occurrence.OneOrMore:
                ok = count >= 1;
                break;
            default:
                ok = true;
                break;
        }

        if (!ok)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"Function argument cardinality violation: {count} items for occurrence {occ}.",
                location);
        }
    }
}
