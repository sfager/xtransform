// XSLT 2.0 §5.3 — the as attribute: parsing its SequenceType and converting a value to it with the function
// conversion rules of XPath 2.0 §3.1.5 (atomization, untypedAtomic cast, numeric and anyURI promotion, cardinality).

using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Operators;

namespace XTransform.Xslt;

internal static class SequenceTypes
{
    /// <summary>
    /// Parses the <c>as</c> attribute <paramref name="text"/> of <paramref name="element"/>. Returns true with a null type when
    /// there is no attribute; false (with an error reported) when it is not a valid SequenceType.
    /// </summary>
    public static bool TryParse(string? text, StylesheetElement element, DiagnosticBag bag, out SequenceType? type)
    {
        type = null;
        if (text is null)
        {
            return true;
        }

        try
        {
            NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
                element.InScopeNamespaces,
                defaultElementNamespace: element.XPathDefaultNamespace,
                defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);
            type = XPathParser.ParseSequenceType(text, element.Location, resolver);
            return true;
        }
        catch (XTransformException ex)
        {
            bag.Error(ex.ErrorCode, ex.Message, element.Location);
            return false;
        }
    }

    /// <summary>The same parse for a declaration whose element is known but that has no diagnostic bag at hand; null when invalid or absent.</summary>
    public static SequenceType? ParseOrNull(string? text, StylesheetElement element)
    {
        DiagnosticBag bag = new DiagnosticBag();
        return TryParse(text, element, bag, out SequenceType? type) ? type : null;
    }

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="type"/> by the function conversion rules and checks that the result is an
    /// instance of it. Any failure is reported as <paramref name="errorCode"/> (XTTE0570 for a variable, and so on).
    /// </summary>
    public static XdmSequence Convert(XdmSequence value, SequenceType type, QName errorCode, SourceSpan location, string what)
    {
        XdmSequence converted;
        try
        {
            converted = FunctionConversion.Apply(value.GetIterator(), type, location);
        }
        catch (XPathDynamicException ex)
        {
            throw new XsltDynamicException(errorCode, $"{what} does not match its required type: {ex.Message}", location);
        }

        if (!type.Matches(converted))
        {
            throw new XsltDynamicException(errorCode, $"{what} does not match its required type", location);
        }

        return converted;
    }
}
