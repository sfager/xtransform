// F&O §2 — Accessor functions.

using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §2 node accessor functions:
/// node-name, nilled, string, data, base-uri, document-uri, root,
/// local-name, namespace-uri, name.
/// </summary>
internal static class AccessorFunctions
{
    // F&O §2.1 — fn:node-name() as xs:QName? (0-arg: uses context item)
    /// <summary>Implements fn:node-name() with no argument (uses context item).</summary>
    internal static IXdmIterator FnNodeName0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:node-name() with no argument requires a context item.",
                default);
        }

        return FnNodeNameFromItem(ci);
    }

    // F&O §2.1 — fn:node-name($arg as node()?) as xs:QName?
    /// <summary>Implements fn:node-name($arg as node()?) as xs:QName?.</summary>
    internal static IXdmIterator FnNodeName1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        if (node is null)
        {
            return EmptyIterator.Instance;
        }

        return FnNodeNameFromItem(node);
    }

    private static IXdmIterator FnNodeNameFromItem(XdmItem item)
    {
        if (item is not XdmNode node)
        {
            return EmptyIterator.Instance;
        }

        QName? name = node.NodeName;
        if (name is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(XdmAtomicValue.QNameValue(name.Value));
    }

    // F&O §2.2 — fn:nilled() as xs:boolean? (0-arg: uses context item)
    /// <summary>Implements fn:nilled() with no argument (uses context item).</summary>
    internal static IXdmIterator FnNilled0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:nilled() with no argument requires a context item.",
                default);
        }

        if (ci is not XdmNode)
        {
            return EmptyIterator.Instance;
        }

        // F&O §2.2: nilled property is false for untyped nodes.
        return new SingleItemIterator(XdmAtomicValue.Boolean(false));
    }

    // F&O §2.2 — fn:nilled($arg as node()?) as xs:boolean?
    // Always returns false for v1 (no schema-aware typing).
    /// <summary>Implements fn:nilled (always false for unschema'd trees).</summary>
    internal static IXdmIterator FnNilled1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        if (node is null)
        {
            return EmptyIterator.Instance;
        }

        // F&O §2.2: nilled property is false for untyped nodes.
        return new SingleItemIterator(XdmAtomicValue.Boolean(false));
    }

    // F&O §2.3 — fn:string($arg as item()?) as xs:string
    /// <summary>Implements fn:string (0-arg uses context item).</summary>
    internal static IXdmIterator FnString0(DynamicContext context, XdmSequence[] args)
    {
        // 0-arg: apply to context item (raises XPDY0002 if no focus)
        XdmItem? item = context.ContextItem;
        if (item is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:string() with no argument requires a context item.",
                default);
        }

        return new SingleItemIterator(XdmAtomicValue.String(item.GetStringValue()));
    }

    /// <summary>Implements fn:string($arg as item()?) as xs:string.</summary>
    internal static IXdmIterator FnString1(DynamicContext context, XdmSequence[] args)
    {
        // F&O §2.3: empty sequence → ""
        XdmItem? item = args[0].FirstOrNull();
        string result = item is null ? string.Empty : item.GetStringValue();
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    // F&O §2.4 — fn:data($arg as item()*) as xs:anyAtomicType*
    /// <summary>Implements fn:data via atomization.</summary>
    internal static IXdmIterator FnData(DynamicContext context, XdmSequence[] args)
    {
        // fn:data parameter type is item()*, so function-conversion did not atomize.
        // We atomize here explicitly.
        return Atomization.Atomize(args[0].GetIterator());
    }

    // F&O §2.5 — fn:base-uri() as xs:anyURI? (0-arg: uses context item)
    /// <summary>Implements fn:base-uri() with no argument (uses context item).</summary>
    internal static IXdmIterator FnBaseUri0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:base-uri() with no argument requires a context item.",
                default);
        }

        if (ci is not XdmNode node)
        {
            return EmptyIterator.Instance;
        }

        string? baseUri = node.BaseUri;
        return baseUri is null ? EmptyIterator.Instance
            : new SingleItemIterator(XdmAtomicValue.AnyUri(baseUri));
    }

    // F&O §2.5 — fn:base-uri($arg as node()?) as xs:anyURI?
    /// <summary>Implements fn:base-uri($arg as node()?) as xs:anyURI?.</summary>
    internal static IXdmIterator FnBaseUri1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        if (node is null)
        {
            return EmptyIterator.Instance;
        }

        string? baseUri = node.BaseUri;
        if (baseUri is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(XdmAtomicValue.AnyUri(baseUri));
    }

    // F&O §2.6 — fn:document-uri($arg as node()?) as xs:anyURI?
    /// <summary>Implements fn:document-uri.</summary>
    internal static IXdmIterator FnDocumentUri(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        if (node is null)
        {
            return EmptyIterator.Instance;
        }

        // F&O §2.6: returns () for non-document nodes; for documents returns the document URI.
        if (node.NodeKind != XdmNodeKind.Document)
        {
            return EmptyIterator.Instance;
        }

        string? docUri = node.DocumentUri;
        if (docUri is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(XdmAtomicValue.AnyUri(docUri));
    }

    // F&O §15.5.3 — fn:root($arg as node()?) as node()?
    /// <summary>Implements fn:root by walking the parent chain.</summary>
    internal static IXdmIterator FnRoot0(DynamicContext context, XdmSequence[] args)
    {
        // 0-arg: use context item
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:root() with no argument requires a context item.",
                default);
        }

        if (ci is not XdmNode n)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:root() requires a node context item.",
                default);
        }

        return new SingleItemIterator(WalkToRoot(n));
    }

    /// <summary>Implements fn:root($arg as node()?) as node()?.</summary>
    internal static IXdmIterator FnRoot1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        if (node is null)
        {
            return EmptyIterator.Instance;
        }

        return new SingleItemIterator(WalkToRoot(node));
    }

    private static XdmNode WalkToRoot(XdmNode node)
    {
        XdmNode current = node;
        while (true)
        {
            XdmNode? parent = current.Parent;
            if (parent is null)
            {
                return current;
            }

            current = parent;
        }
    }

    // F&O §2.7 — fn:local-name($arg as node()?) as xs:string
    /// <summary>Implements fn:local-name (0-arg uses context item).</summary>
    internal static IXdmIterator FnLocalName0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:local-name() with no argument requires a context item.",
                default);
        }

        string result = ci is XdmNode n ? (n.NodeName?.LocalName ?? string.Empty) : string.Empty;
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    /// <summary>Implements fn:local-name($arg as node()?) as xs:string.</summary>
    internal static IXdmIterator FnLocalName1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        string result = node?.NodeName?.LocalName ?? string.Empty;
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    // F&O §2.8 — fn:namespace-uri($arg as node()?) as xs:anyURI
    /// <summary>Implements fn:namespace-uri (0-arg uses context item).</summary>
    internal static IXdmIterator FnNamespaceUri0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:namespace-uri() with no argument requires a context item.",
                default);
        }

        string result = ci is XdmNode n ? (n.NodeName?.NamespaceUri ?? string.Empty) : string.Empty;
        return new SingleItemIterator(XdmAtomicValue.AnyUri(result));
    }

    /// <summary>Implements fn:namespace-uri($arg as node()?) as xs:anyURI.</summary>
    internal static IXdmIterator FnNamespaceUri1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        string result = node?.NodeName?.NamespaceUri ?? string.Empty;
        return new SingleItemIterator(XdmAtomicValue.AnyUri(result));
    }

    // F&O §2.9 — fn:name($arg as node()?) as xs:string
    /// <summary>Implements fn:name (0-arg uses context item).</summary>
    internal static IXdmIterator FnName0(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? ci = context.ContextItem;
        if (ci is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:name() with no argument requires a context item.",
                default);
        }

        string result = ci is XdmNode n ? GetNodeName(n) : string.Empty;
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    /// <summary>Implements fn:name($arg as node()?) as xs:string.</summary>
    internal static IXdmIterator FnName1(DynamicContext context, XdmSequence[] args)
    {
        XdmNode? node = GetOptionalNode(args[0], context);
        string result = node is null ? string.Empty : GetNodeName(node);
        return new SingleItemIterator(XdmAtomicValue.String(result));
    }

    private static string GetNodeName(XdmNode node)
    {
        QName? qn = node.NodeName;
        if (qn is null)
        {
            return string.Empty;
        }

        // For PI the target is in LocalName; for element/attribute use prefix:local or local.
        if (qn.Value.Prefix.Length > 0)
        {
            return qn.Value.Prefix + ":" + qn.Value.LocalName;
        }

        return qn.Value.LocalName;
    }

    // ── Helper: get optional node from a sequence (or context) ─────────────────

    /// <summary>
    /// Returns the single node from a 0-or-1 node sequence, or null for the empty sequence.
    /// Raises XPTY0004 if the item is not a node.
    /// </summary>
    private static XdmNode? GetOptionalNode(XdmSequence seq, DynamicContext context)
    {
        XdmItem? item = seq.FirstOrNull();
        if (item is null)
        {
            return null;
        }

        if (item is XdmNode node)
        {
            return node;
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            "Expected a node() argument.",
            default);
    }
}
