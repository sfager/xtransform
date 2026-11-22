// F&O §13 (fn:lang) and §16 (QName functions).

using System;
using System.Collections.Generic;
using System.Xml;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §13 and §16 QName/namespace/lang functions:
/// fn:QName, fn:resolve-QName, fn:prefix-from-QName, fn:local-name-from-QName,
/// fn:namespace-uri-from-QName, fn:namespace-uri-for-prefix, fn:in-scope-prefixes, fn:lang.
/// </summary>
// F&O §13, §16
internal static class QNameFunctions
{
    // The implicitly-defined xml namespace binding — always in scope (XPath 2.0 §4.2).
    private const string XmlPrefix = "xml";
    private const string XmlNamespaceUri = "http://www.w3.org/XML/1998/namespace";

    // The xml:lang attribute name (in the xml namespace, local name "lang").
    private const string XmlLangLocalName = "lang";

    // =========================================================================
    // F&O §16.1.1 — fn:QName($paramURI as xs:string?, $paramQName as xs:string) as xs:QName
    // =========================================================================

    /// <summary>
    /// Implements fn:QName($paramURI as xs:string?, $paramQName as xs:string) as xs:QName.
    /// </summary>
    // F&O §16.1.1
    internal static IXdmIterator FnQName(DynamicContext ctx, XdmSequence[] args)
    {
        string uri = StringArgOrEmpty(args[0]);
        string lex = RequiredString(args[1]);

        // Parse lex as "prefix:local" or "local".
        string prefix;
        string local;
        int colon = lex.IndexOf(':');
        if (colon >= 0)
        {
            prefix = lex.Substring(0, colon);
            local = lex.Substring(colon + 1);
        }
        else
        {
            prefix = string.Empty;
            local = lex;
        }

        // Validate NCName parts — F&O §16.1.1 raises FOCA0002 for invalid lexical values.
        if (!IsValidNcName(local))
        {
            throw new XPathDynamicException(
                XPathErrors.FOCA0002,
                $"fn:QName: '{local}' is not a valid NCName.",
                default);
        }

        if (prefix.Length > 0)
        {
            if (!IsValidNcName(prefix))
            {
                throw new XPathDynamicException(
                    XPathErrors.FOCA0002,
                    $"fn:QName: prefix '{prefix}' is not a valid NCName.",
                    default);
            }

            // F&O §16.1.1: if the prefix is non-empty the URI must be non-empty.
            if (uri.Length == 0)
            {
                throw new XPathDynamicException(
                    XPathErrors.FOCA0002,
                    "fn:QName: a prefixed QName requires a non-empty namespace URI.",
                    default);
            }
        }

        return new SingleItemIterator(XdmAtomicValue.QNameValue(new QName(prefix, uri, local)));
    }

    // =========================================================================
    // F&O §16.1.2 — fn:resolve-QName($qname as xs:string?, $element as element()) as xs:QName?
    // =========================================================================

    /// <summary>
    /// Implements fn:resolve-QName($qname as xs:string?, $element as element()) as xs:QName?.
    /// </summary>
    // F&O §16.1.2
    internal static IXdmIterator FnResolveQName(DynamicContext ctx, XdmSequence[] args)
    {
        XdmItem? lexItem = args[0].FirstOrNull();
        if (lexItem is null)
        {
            // F&O §16.1.2: empty sequence → empty sequence.
            return EmptyIterator.Instance;
        }

        string lex = lexItem.GetStringValue();

        XdmNode elem = RequiredElement(args[1]);

        // Parse prefix:local or local.
        string prefix;
        string local;
        int colon = lex.IndexOf(':');
        if (colon >= 0)
        {
            prefix = lex.Substring(0, colon);
            local = lex.Substring(colon + 1);
        }
        else
        {
            prefix = string.Empty;
            local = lex;
        }

        // Validate NCName parts.
        if (!IsValidNcName(local))
        {
            throw new XPathDynamicException(
                XPathErrors.FOCA0002,
                $"fn:resolve-QName: '{local}' is not a valid NCName.",
                default);
        }

        if (prefix.Length > 0 && !IsValidNcName(prefix))
        {
            throw new XPathDynamicException(
                XPathErrors.FOCA0002,
                $"fn:resolve-QName: prefix '{prefix}' is not a valid NCName.",
                default);
        }

        // Resolve prefix against element's in-scope namespaces.
        string? uri = ResolvePrefix(prefix, elem);
        if (uri is null)
        {
            throw new XPathDynamicException(
                XPathErrors.FONS0004,
                $"fn:resolve-QName: prefix '{prefix}' is not declared in the in-scope namespaces.",
                default);
        }

        return new SingleItemIterator(XdmAtomicValue.QNameValue(new QName(prefix, uri, local)));
    }

    // =========================================================================
    // F&O §16.1.3 — fn:prefix-from-QName($arg as xs:QName?) as xs:NCName?
    // =========================================================================

    /// <summary>
    /// Implements fn:prefix-from-QName($arg as xs:QName?) as xs:NCName?.
    /// </summary>
    // F&O §16.1.3
    internal static IXdmIterator FnPrefixFromQName(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = GetOptionalQName(args[0]);
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        string prefix = av.AsQName().Prefix;
        // F&O §16.1.3: if the prefix is "" return the empty sequence.
        if (prefix.Length == 0)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(XdmAtomicValue.String(prefix));
    }

    // =========================================================================
    // F&O §16.1.4 — fn:local-name-from-QName($arg as xs:QName?) as xs:NCName?
    // =========================================================================

    /// <summary>
    /// Implements fn:local-name-from-QName($arg as xs:QName?) as xs:NCName?.
    /// </summary>
    // F&O §16.1.4
    internal static IXdmIterator FnLocalNameFromQName(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = GetOptionalQName(args[0]);
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        string local = av.AsQName().LocalName;
        return new SingleItemIterator(XdmAtomicValue.String(local));
    }

    // =========================================================================
    // F&O §16.1.5 — fn:namespace-uri-from-QName($arg as xs:QName?) as xs:anyURI?
    // =========================================================================

    /// <summary>
    /// Implements fn:namespace-uri-from-QName($arg as xs:QName?) as xs:anyURI?.
    /// </summary>
    // F&O §16.1.5
    internal static IXdmIterator FnNamespaceUriFromQName(DynamicContext ctx, XdmSequence[] args)
    {
        XdmAtomicValue? av = GetOptionalQName(args[0]);
        if (av is null)
        {
            return EmptyIterator.Instance;
        }

        string nsUri = av.AsQName().NamespaceUri;
        return new SingleItemIterator(XdmAtomicValue.AnyUri(nsUri));
    }

    // =========================================================================
    // F&O §16.1.6 — fn:namespace-uri-for-prefix($prefix as xs:string?, $element as element()) as xs:anyURI?
    // =========================================================================

    /// <summary>
    /// Implements fn:namespace-uri-for-prefix($prefix as xs:string?, $element as element()) as xs:anyURI?.
    /// </summary>
    // F&O §16.1.6
    internal static IXdmIterator FnNamespaceUriForPrefix(DynamicContext ctx, XdmSequence[] args)
    {
        XdmItem? prefixItem = args[0].FirstOrNull();
        // Empty sequence is treated as empty string (default namespace lookup).
        string prefix = prefixItem is null ? string.Empty : prefixItem.GetStringValue();

        XdmNode elem = RequiredElement(args[1]);

        string? uri = ResolvePrefix(prefix, elem);
        if (uri is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(XdmAtomicValue.AnyUri(uri));
    }

    // =========================================================================
    // F&O §16.1.7 — fn:in-scope-prefixes($element as element()) as xs:string*
    // =========================================================================

    /// <summary>
    /// Implements fn:in-scope-prefixes($element as element()) as xs:string*.
    /// </summary>
    // F&O §16.1.7
    internal static IXdmIterator FnInScopePrefixes(DynamicContext ctx, XdmSequence[] args)
    {
        XdmNode elem = RequiredElement(args[0]);

        // Collect all in-scope prefixes from the namespace axis.
        List<XdmItem> items = new List<XdmItem>();
        bool xmlSeen = false;

        foreach (XdmNode nsNode in elem.Axis(Axis.Namespace))
        {
            // Per P1-09: namespace node's NodeName.LocalName == prefix; GetStringValue() == URI.
            QName? nn = nsNode.NodeName;
            string pfx = nn?.LocalName ?? string.Empty;

            if (string.Equals(pfx, XmlPrefix, StringComparison.Ordinal))
            {
                xmlSeen = true;
            }

            items.Add(XdmAtomicValue.String(pfx));
        }

        // F&O §16.1.7: the xml prefix is always in-scope, even if not explicitly declared.
        // The NamespaceAxisIterator may omit it (see its comment), so we add it if absent.
        if (!xmlSeen)
        {
            items.Add(XdmAtomicValue.String(XmlPrefix));
        }

        return new ArrayIterator(items.ToArray());
    }

    // =========================================================================
    // F&O §13.3.2 — fn:lang($testlang as xs:string?, $node as node()) as xs:boolean
    //   1-arg variant uses context item (must be a node).
    // =========================================================================

    /// <summary>
    /// Implements fn:lang($testlang as xs:string?) as xs:boolean (1-arg, uses context item).
    /// </summary>
    // F&O §13.3.2
    internal static IXdmIterator FnLang1(DynamicContext ctx, XdmSequence[] args)
    {
        XdmItem? ci = ctx.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:lang() with one argument requires a context item.",
                default);
        }

        if (ci is not XdmNode node)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:lang() context item must be a node.",
                default);
        }

        string testLang = StringArgOrEmpty(args[0]);
        bool result = TestLang(testLang, node);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>
    /// Implements fn:lang($testlang as xs:string?, $node as node()) as xs:boolean (2-arg).
    /// </summary>
    // F&O §13.3.2
    internal static IXdmIterator FnLang2(DynamicContext ctx, XdmSequence[] args)
    {
        string testLang = StringArgOrEmpty(args[0]);

        XdmItem? nodeItem = args[1].FirstOrNull();
        if (nodeItem is null)
        {
            return new SingleItemIterator(XdmAtomicValue.Boolean(false));
        }

        if (nodeItem is not XdmNode node)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:lang: second argument must be a node().",
                default);
        }

        bool result = TestLang(testLang, node);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Tests whether <paramref name="node"/> (or an ancestor) has an xml:lang attribute whose
    /// value equals or is a sublanguage of <paramref name="testLang"/> (case-insensitive).
    /// </summary>
    // F&O §13.3.2
    private static bool TestLang(string testLang, XdmNode node)
    {
        // Walk ancestor-or-self looking for the first xml:lang attribute.
        foreach (XdmNode anc in node.Axis(Axis.AncestorOrSelf))
        {
            if (anc.NodeKind != XdmNodeKind.Element)
            {
                continue;
            }

            // Look for an attribute whose expanded name is {http://www.w3.org/XML/1998/namespace}lang.
            foreach (XdmNode attr in anc.Axis(Axis.Attribute))
            {
                QName? attrName = attr.NodeName;
                if (attrName is null)
                {
                    continue;
                }

                if (string.Equals(attrName.Value.NamespaceUri, XmlNamespaceUri, StringComparison.Ordinal) &&
                    string.Equals(attrName.Value.LocalName, XmlLangLocalName, StringComparison.Ordinal))
                {
                    // Found xml:lang — test it.
                    string lang = attr.GetStringValue();
                    return IsLangMatch(testLang, lang);
                }
            }
        }

        // No xml:lang found on any ancestor → false.
        return false;
    }

    /// <summary>
    /// Returns true if <paramref name="langValue"/> equals <paramref name="testLang"/>
    /// (case-insensitive) or starts with <paramref name="testLang"/> + "-" (case-insensitive).
    /// </summary>
    // F&O §13.3.2
    private static bool IsLangMatch(string testLang, string langValue)
    {
        if (testLang.Length == 0)
        {
            return false;
        }

        if (string.Equals(langValue, testLang, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (langValue.Length > testLang.Length &&
            langValue[testLang.Length] == '-' &&
            langValue.StartsWith(testLang, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a namespace prefix against the in-scope namespaces of an element.
    /// Returns the URI, or null if the prefix is not found.
    /// The implicit <c>xml</c> prefix is always resolved.
    /// An empty prefix resolves to the default namespace URI (may be "").
    /// </summary>
    // F&O §16.1.2, §16.1.6
    private static string? ResolvePrefix(string prefix, XdmNode elem)
    {
        // The xml prefix is always implicitly defined (XPath 2.0 §4.2).
        if (string.Equals(prefix, XmlPrefix, StringComparison.Ordinal))
        {
            return XmlNamespaceUri;
        }

        // Walk the in-scope namespace axis.
        foreach (XdmNode nsNode in elem.Axis(Axis.Namespace))
        {
            // Per P1-09: namespace node NodeName.LocalName == prefix, GetStringValue() == URI.
            QName? nn = nsNode.NodeName;
            string nsPrefix = nn?.LocalName ?? string.Empty;

            if (string.Equals(nsPrefix, prefix, StringComparison.Ordinal))
            {
                return nsNode.GetStringValue();
            }
        }

        // For the no-prefix (default namespace) case: if no default-namespace binding is
        // declared the in-scope namespace axis will have no entry with prefix == ""; the
        // default namespace URI in that case is "" (no namespace), which is a valid result.
        // Return "" rather than null so fn:namespace-uri-for-prefix returns xs:anyURI("").
        if (prefix.Length == 0)
        {
            return string.Empty;
        }

        return null;
    }

    /// <summary>
    /// Validates an NCName per XML §2.3 / XSD §3.3.7.
    /// Returns false if the string is empty or contains invalid characters.
    /// </summary>
    private static bool IsValidNcName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        // NCName start character.
        if (!XmlConvert.IsStartNCNameChar(name[0]))
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            if (!XmlConvert.IsNCNameChar(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the first item of <paramref name="seq"/> as an <see cref="XdmAtomicValue"/>
    /// whose type is <see cref="XdmTypeCode.QName"/>, or null for an empty sequence.
    /// Raises XPTY0004 if the item is not a QName atomic value.
    /// </summary>
    private static XdmAtomicValue? GetOptionalQName(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return null;
        }

        if (item is XdmAtomicValue av && av.TypeCode == XdmTypeCode.QName)
        {
            return av;
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Expected an xs:QName? argument.",
            default);
    }

    /// <summary>
    /// Returns the single element node from a sequence.
    /// Raises XPTY0004 if the item is not an element node.
    /// </summary>
    private static XdmNode RequiredElement(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is XdmNode node && node.NodeKind == XdmNodeKind.Element)
        {
            return node;
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Expected an element() argument.",
            default);
    }

    /// <summary>
    /// Returns the string value of the first item in <paramref name="seq"/>,
    /// or <see cref="string.Empty"/> if the sequence is empty.
    /// </summary>
    private static string StringArgOrEmpty(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }

    /// <summary>
    /// Returns the string value of the first item in <paramref name="seq"/>.
    /// </summary>
    private static string RequiredString(XdmSequence seq)
    {
        XdmItem? item = seq.FirstOrNull();
        return item is null ? string.Empty : item.GetStringValue();
    }
}
