// F&O §15.2 / §16 — Context functions.

using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §15.2 / §16 context functions:
/// fn:position, fn:last, fn:current-dateTime, fn:current-date, fn:current-time,
/// fn:implicit-timezone, fn:default-collation.
/// </summary>
internal static class ContextFunctions
{
    /// <summary>Codepoint collation URI, the default for v1.</summary>
    internal const string CodepointCollationUri =
        "http://www.w3.org/2005/xpath-functions/collation/codepoint";

    // F&O §15.2.1 — fn:position() as xs:integer
    /// <summary>Implements fn:position().</summary>
    internal static IXdmIterator FnPosition(DynamicContext context, XdmSequence[] args)
    {
        int pos = context.ContextPosition;
        if (pos == 0)
        {
            // XPath 2.0 §2.1.2: context position is undefined when there is no focus.
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:position() called with no focus in scope.",
                default);
        }

        return new SingleItemIterator(XdmAtomicValue.Integer((long)pos));
    }

    // F&O §15.2.2 — fn:last() as xs:integer
    /// <summary>Implements fn:last().</summary>
    internal static IXdmIterator FnLast(DynamicContext context, XdmSequence[] args)
    {
        int pos = context.ContextPosition;
        if (pos == 0)
        {
            // XPath 2.0 §2.1.2: context size is undefined when there is no focus.
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:last() called with no focus in scope.",
                default);
        }

        return new SingleItemIterator(XdmAtomicValue.Integer((long)context.ContextSize));
    }

    // F&O §16.2 — fn:current-dateTime() as xs:dateTime
    /// <summary>Implements fn:current-dateTime().</summary>
    internal static IXdmIterator FnCurrentDateTime(DynamicContext context, XdmSequence[] args)
    {
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(context.Run.CurrentDateTime));
    }

    // F&O §16.3 — fn:current-date() as xs:date
    /// <summary>Implements fn:current-date().</summary>
    internal static IXdmIterator FnCurrentDate(DynamicContext context, XdmSequence[] args)
    {
        XsdDateTime dt = context.Run.CurrentDateTime;
        XsdDateTime dateOnly = new XsdDateTime(
            XsdDateTimeKind.Date,
            dt.Year, dt.Month, dt.Day,
            0, 0, default,
            dt.TimezoneMinutes);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(dateOnly));
    }

    // F&O §16.4 — fn:current-time() as xs:time
    /// <summary>Implements fn:current-time().</summary>
    internal static IXdmIterator FnCurrentTime(DynamicContext context, XdmSequence[] args)
    {
        XsdDateTime dt = context.Run.CurrentDateTime;
        XsdDateTime timeOnly = new XsdDateTime(
            XsdDateTimeKind.Time,
            0, 0, 0,
            dt.Hour, dt.Minute, dt.Second,
            dt.TimezoneMinutes);
        return new SingleItemIterator(XdmAtomicValue.DateTimeValue(timeOnly));
    }

    // F&O §16.6 — fn:implicit-timezone() as xs:dayTimeDuration
    /// <summary>Implements fn:implicit-timezone().</summary>
    internal static IXdmIterator FnImplicitTimezone(DynamicContext context, XdmSequence[] args)
    {
        int minutes = context.Run.ImplicitTimezoneMinutes;
        // xs:dayTimeDuration: months = 0, seconds = minutes * 60.
        XsdDecimal seconds = new XsdDecimal(minutes * 60m);
        XsdDuration duration = new XsdDuration(0, seconds);
        return new SingleItemIterator(XdmAtomicValue.DayTimeDuration(duration));
    }

    // F&O §17.1 — fn:default-collation() as xs:string
    /// <summary>Implements fn:default-collation().</summary>
    internal static IXdmIterator FnDefaultCollation(DynamicContext context, XdmSequence[] args)
    {
        return new SingleItemIterator(XdmAtomicValue.String(CodepointCollationUri));
    }
}
