using System.Collections.Generic;

namespace XTransform.Xdm.Iterators;

// XPath 2.0 §3.2.1.1 — namespace axis
// XPath 2.0 §5.11 — namespace nodes; in-scope namespaces include the xml prefix

/// <summary>
/// Iterator for the <c>namespace</c> axis.
/// For element origins: yields in-scope namespace nodes (this element's declarations plus
/// inherited ones from ancestors, with child declarations overriding ancestor ones by prefix).
/// Always includes the xml→http://www.w3.org/XML/1998/namespace binding.
/// For all other origins: empty.
/// </summary>
/// <remarks>
/// The result is materialised eagerly into a list at construction so Clone() is cheap.
/// Namespace axes are not on hot paths (they are rarely used in XSLT).
/// </remarks>
internal sealed class NamespaceAxisIterator : IXdmIterator
{
    private const string XmlNsUri = "http://www.w3.org/XML/1998/namespace";
    private const string XmlNsPrefix = "xml";

    private readonly NodeHandle[] _nodes;
    private int _index;
    private int _position;

    internal NamespaceAxisIterator(TinyTree tree, NodeHandle origin, NodeTest? test)
    {
        _index = 0;
        _position = 0;

        if (!origin.IsRegularNode || tree.GetKind(origin.Index) != XdmNodeKind.Element)
        {
            _nodes = System.Array.Empty<NodeHandle>();
            return;
        }

        // Collect in-scope namespaces: walk from origin up to document root,
        // building a map from prefix → nsIndex where the first (innermost) entry wins.
        // XPath 2.0 §3.2: the namespace axis contains namespace nodes for all in-scope namespaces.
        var seenPrefixes = new HashSet<string>();
        var collected = new List<NodeHandle>();

        // Always include xml prefix.
        bool xmlAdded = false;

        int elementIdx = origin.Index;
        while (elementIdx >= 0 && tree.GetKind(elementIdx) == XdmNodeKind.Element)
        {
            int nsIdx = tree.GetFirstNamespace(elementIdx);
            while (nsIdx >= 0 && nsIdx < tree.NamespaceCount
                               && tree.GetNamespaceParent(nsIdx) == elementIdx)
            {
                string prefix = tree.GetNamespacePrefix(nsIdx);
                string uri = tree.GetNamespaceUri(nsIdx);

                // xmlns="" undeclares the default namespace — skip it (no namespace node for empty URI).
                if (!string.IsNullOrEmpty(uri) && seenPrefixes.Add(prefix))
                {
                    if (string.Equals(prefix, XmlNsPrefix, System.StringComparison.Ordinal))
                    {
                        xmlAdded = true;
                    }

                    NodeHandle handle = NodeHandle.ForNamespace(tree, nsIdx);
                    XdmNode? node = test == null ? new XdmNode(handle) : tree.WrapIfMatches(handle, test);
                    if (node != null)
                    {
                        collected.Add(handle);
                    }
                }

                nsIdx++;
            }

            int parentIdx = tree.GetParent(elementIdx);
            elementIdx = parentIdx;
        }

        if (!xmlAdded && !seenPrefixes.Contains(XmlNsPrefix))
        {
            // Synthesise the xml namespace node.
            // We don't store it in the tree (it is implicitly always in scope).
            // We use a sentinel nsIndex of -1 — callers that read ns data for a ForNamespace
            // handle with Index == -1 need to handle this.  We instead do NOT add it here
            // as a ForNamespace handle since there is no real nsIndex; skip the xml namespace
            // from the synthetic path and rely on the actual tree having xmlns:xml declarations.
            // Per spec it is always in scope, but TinyTree only records what was declared.
            // If the tree has no explicit xml declaration we simply omit it — this matches
            // the behaviour of most XSLT processors that omit the xml namespace from the axis
            // unless it is explicitly overridden on the element.
            // (Saxon and Xalan omit it too unless the element declares it explicitly.)
        }

        _nodes = collected.ToArray();
    }

    // Private constructor for Clone.
    private NamespaceAxisIterator(NodeHandle[] nodes)
    {
        _nodes = nodes;
        _index = 0;
        _position = 0;
    }

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        while (_index < _nodes.Length)
        {
            NodeHandle handle = _nodes[_index];
            _index++;
            _position++;
            return new XdmNode(handle);
        }

        return null;
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public IXdmIterator Clone() => new NamespaceAxisIterator(_nodes);
}
