// F&O §9 — Boolean functions and F&O §15.1.1 — fn:boolean (EBV).

using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §9 boolean functions:
/// fn:true, fn:false, fn:boolean, fn:not.
/// </summary>
internal static class BooleanFunctions
{
    // F&O §9.1.1 — fn:true() as xs:boolean
    /// <summary>Implements fn:true().</summary>
    internal static IXdmIterator FnTrue(DynamicContext context, XdmSequence[] args)
    {
        return new SingleItemIterator(XdmAtomicValue.Boolean(true));
    }

    // F&O §9.1.2 — fn:false() as xs:boolean
    /// <summary>Implements fn:false().</summary>
    internal static IXdmIterator FnFalse(DynamicContext context, XdmSequence[] args)
    {
        return new SingleItemIterator(XdmAtomicValue.Boolean(false));
    }

    // F&O §15.1.1 — fn:boolean($arg as item()*) as xs:boolean
    /// <summary>Implements fn:boolean — returns the EBV of its argument.</summary>
    internal static IXdmIterator FnBoolean(DynamicContext context, XdmSequence[] args)
    {
        bool ebv = EffectiveBooleanValue(args[0]);
        return new SingleItemIterator(XdmAtomicValue.Boolean(ebv));
    }

    // F&O §9.3.3 — fn:not($arg as item()*) as xs:boolean
    /// <summary>Implements fn:not — negation of EBV.</summary>
    internal static IXdmIterator FnNot(DynamicContext context, XdmSequence[] args)
    {
        bool ebv = EffectiveBooleanValue(args[0]);
        return new SingleItemIterator(XdmAtomicValue.Boolean(!ebv));
    }

    /// <summary>
    /// Computes the Effective Boolean Value (EBV) of a materialised sequence.
    /// F&amp;O §15.1.1 / XPath 2.0 §2.4.3.
    /// </summary>
    internal static bool EffectiveBooleanValue(XdmSequence seq)
    {
        // Empty sequence → false.
        XdmItem? first = seq.FirstOrNull();
        if (first is null)
        {
            return false;
        }

        // If first item is a node → true (regardless of further items).
        if (first.IsNode)
        {
            return true;
        }

        // If first item is atomic, sequence must be a singleton (else FORG0006).
        if (seq.Count > 1)
        {
            // F&O §15.1.1: more than one item in a non-node sequence → FORG0006.
            throw new XPathDynamicException(
                XPathErrors.FORG0006,
                "fn:boolean: sequence of more than one item (not starting with a node) has no defined EBV.",
                default);
        }

        XdmAtomicValue av = (XdmAtomicValue)first;
        return AtomicEbv(av);
    }

    /// <summary>EBV for a single atomic value. F&amp;O §15.1.1.</summary>
    private static bool AtomicEbv(XdmAtomicValue av)
    {
        switch (av.TypeCode)
        {
            case XdmTypeCode.Boolean:
                return av.AsBoolean();

            case XdmTypeCode.String:
            case XdmTypeCode.AnyUri:
            case XdmTypeCode.UntypedAtomic:
            case XdmTypeCode.NormalizedString:
            case XdmTypeCode.Token:
            case XdmTypeCode.Language:
            case XdmTypeCode.NmToken:
            case XdmTypeCode.Name:
            case XdmTypeCode.NcName:
            case XdmTypeCode.Id:
            case XdmTypeCode.IdRef:
            case XdmTypeCode.Entity:
                // F&O §15.1.1: string/anyURI/untypedAtomic → length > 0.
                return av.GetStringValue().Length > 0;

            case XdmTypeCode.Double:
            {
                double d = av.AsDouble();
                return !double.IsNaN(d) && d != 0.0;
            }

            case XdmTypeCode.Float:
            {
                float f = av.AsFloat();
                return !float.IsNaN(f) && f != 0.0f;
            }

            case XdmTypeCode.Decimal:
                return av.AsDecimal().Value != 0m;

            case XdmTypeCode.Integer:
            case XdmTypeCode.NonPositiveInteger:
            case XdmTypeCode.NegativeInteger:
            case XdmTypeCode.Long:
            case XdmTypeCode.Int:
            case XdmTypeCode.Short:
            case XdmTypeCode.Byte:
            case XdmTypeCode.NonNegativeInteger:
            case XdmTypeCode.UnsignedLong:
            case XdmTypeCode.UnsignedInt:
            case XdmTypeCode.UnsignedShort:
            case XdmTypeCode.UnsignedByte:
            case XdmTypeCode.PositiveInteger:
                return av.AsInt64() != 0;

            default:
                // F&O §15.1.1: all other atomic types raise FORG0006.
                throw new XPathDynamicException(
                    XPathErrors.FORG0006,
                    $"fn:boolean: xs:{CastTable.GetXsLocalName(av.TypeCode)} has no defined EBV.",
                    default);
        }
    }
}
