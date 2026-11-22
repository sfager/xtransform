// F&O §3.1.1 — fn:error; F&O §17.1.5 — fn:trace.

using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of fn:error and fn:trace.
/// F&amp;O §3.1.1 / §17.1.5.
/// </summary>
internal static class ErrorFunctions
{
    // F&O §3.1.1 — fn:error() — FOER0000 (no arg)
    /// <summary>Implements fn:error() with no arguments.</summary>
    internal static IXdmIterator FnError0(DynamicContext context, XdmSequence[] args)
    {
        throw new XPathDynamicException(
            XPathErrors.FOER0000,
            "fn:error() called with no argument.",
            default);
    }

    // F&O §3.1.1 — fn:error($code as xs:QName)
    /// <summary>Implements fn:error($code as xs:QName) — throws with the caller-specified code.</summary>
    internal static IXdmIterator FnError1(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? item = args[0].FirstOrNull();
        if (item is null)
        {
            // Empty arg → same as 0-arg form.
            throw new XPathDynamicException(
                XPathErrors.FOER0000,
                "fn:error($code) called with empty QName argument.",
                default);
        }

        if (item is not XdmAtomicValue av || av.TypeCode != XdmTypeCode.QName)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:error: argument must be an xs:QName.",
                default);
        }

        QName code = av.AsQName();
        throw new XPathDynamicException(code, $"fn:error({code})", default);
    }

    // F&O §17.1.5 — fn:trace($value as item()*, $label as xs:string) as item()*
    // Sink integration is a later task; for P2-08 just return the input unchanged.
    /// <summary>Implements fn:trace — returns input sequence unchanged (sink deferred).</summary>
    internal static IXdmIterator FnTrace(DynamicContext context, XdmSequence[] args)
    {
        // P2-08: diagnostic sink integration deferred.
        // The label is args[1] (xs:string); we ignore it for now.
        return args[0].GetIterator();
    }
}
