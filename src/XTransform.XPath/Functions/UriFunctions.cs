// F&O §8.1 — fn:resolve-uri and fn:static-base-uri.

using System;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

internal static class UriFunctions
{
    /// <summary>fn:resolve-uri($relative as xs:string?) as xs:anyURI? — against the static base URI.</summary>
    internal static IXdmIterator FnResolveUri1(DynamicContext ctx, XdmSequence[] args)
    {
        string? baseUri = ctx.Run.StaticBaseUri;
        if (baseUri is null)
        {
            if (args[0].FirstOrNull() is null)
            {
                return EmptyIterator.Instance;
            }

            throw new XPathDynamicException(
                XPathErrors.FONS0005,
                "fn:resolve-uri: the static base URI is not defined",
                SourceSpan.None);
        }

        return Resolve(args[0], baseUri);
    }

    /// <summary>fn:resolve-uri($relative as xs:string?, $base as xs:string) as xs:anyURI?.</summary>
    internal static IXdmIterator FnResolveUri2(DynamicContext ctx, XdmSequence[] args) =>
        Resolve(args[0], args[1].FirstOrNull()?.GetStringValue() ?? string.Empty);

    /// <summary>fn:static-base-uri() as xs:anyURI?.</summary>
    internal static IXdmIterator FnStaticBaseUri(DynamicContext ctx, XdmSequence[] args)
    {
        string? baseUri = ctx.Run.StaticBaseUri;
        return baseUri is null
            ? EmptyIterator.Instance
            : new SingleItemIterator(XdmAtomicValue.AnyUri(baseUri));
    }

    internal static IXdmIterator Resolve(XdmSequence relativeArg, string baseUri)
    {
        XdmItem? relativeItem = relativeArg.FirstOrNull();
        if (relativeItem is null)
        {
            return EmptyIterator.Instance;
        }

        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out Uri? baseResult))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0002,
                $"fn:resolve-uri: the base '{baseUri}' is not an absolute URI",
                SourceSpan.None);
        }

        string relative = relativeItem.GetStringValue();
        if (!Uri.TryCreate(baseResult, relative, out Uri? resolved))
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0002,
                $"fn:resolve-uri: '{relative}' is not a valid URI reference",
                SourceSpan.None);
        }

        return new SingleItemIterator(XdmAtomicValue.AnyUri(resolved.OriginalString));
    }
}
