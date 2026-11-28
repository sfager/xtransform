using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace XTransform.Xdm;

/// <summary>
/// Immutable, array-based representation of an XML document or document fragment.
/// Nodes are numbered in document order starting at 0 for the document node.
/// All arrays are sized exactly to their content after construction.
/// </summary>
/// <remarks>
/// Layout follows the "tiny tree" approach used by Saxon: parallel arrays avoid
/// per-node object overhead.  The document node is always at index 0.
/// </remarks>
internal sealed partial class TinyTree
{
    // ── Static tree-identity counter ───────────────────────────────────────
    private static long s_nextTreeId;

    // ── Node arrays (indexed by document-order node index) ─────────────────
    private readonly byte[] _kind;      // XdmNodeKind values
    private readonly byte[] _depth;     // 0 = document, 1 = root element, …  max 255
    private readonly int[] _nameCode;   // NamePool code; -1 for anonymous nodes
    private readonly int[] _parent;     // parent index; -1 for document node
    private readonly int[] _next;       // next-sibling index; -1 for last sibling
    private readonly int[] _textStart;  // offset into _textBuffer; 0 if unused
    private readonly int[] _textLength; // length in _textBuffer; 0 if unused
    private readonly int[] _alpha;      // first attribute index for elements; -1 if none
    private readonly int[] _beta;       // first namespace index for elements; -1 if none

    // ── Shared character buffer ─────────────────────────────────────────────
    private readonly char[] _textBuffer;

    // ── Attribute arrays (separate index space) ────────────────────────────
    private readonly int[] _attrParent;
    private readonly int[] _attrNameCode;
    private readonly string[] _attrValue;

    // ── Namespace arrays (separate index space) ────────────────────────────
    private readonly int[] _nsParent;
    private readonly string[] _nsPrefix;
    private readonly string[] _nsUri;

    // ── Constructor (called only by TinyTreeBuilder) ────────────────────────
    internal TinyTree(
        NamePool namePool,
        string? baseUri,
        byte[] kind,
        byte[] depth,
        int[] nameCode,
        int[] parent,
        int[] next,
        int[] textStart,
        int[] textLength,
        int[] alpha,
        int[] beta,
        char[] textBuffer,
        int[] attrParent,
        int[] attrNameCode,
        string[] attrValue,
        int attrCount,
        int[] nsParent,
        string[] nsPrefix,
        string[] nsUri,
        int nsCount,
        string? documentUri = null,
        Dictionary<int, string>? nodeBaseUris = null,
        Dictionary<string, UnparsedEntity>? unparsedEntities = null,
        byte[]? attributeFlags = null)
    {
        _attributeFlags = attributeFlags;
        UnparsedEntities = unparsedEntities;
        NamePool = namePool;
        BaseUri = baseUri;
        DocumentUri = documentUri;
        _nodeBaseUris = nodeBaseUris;
        TreeId = Interlocked.Increment(ref s_nextTreeId);

        NodeCount = kind.Length;
        AttributeCount = attrCount;
        NamespaceCount = nsCount;

        _kind = kind;
        _depth = depth;
        _nameCode = nameCode;
        _parent = parent;
        _next = next;
        _textStart = textStart;
        _textLength = textLength;
        _alpha = alpha;
        _beta = beta;
        _textBuffer = textBuffer;
        _attrParent = attrParent;
        _attrNameCode = attrNameCode;
        _attrValue = attrValue;
        _nsParent = nsParent;
        _nsPrefix = nsPrefix;
        _nsUri = nsUri;
    }

    // ── Tree-level properties ───────────────────────────────────────────────

    /// <summary>The name pool shared with the stylesheet/query that owns this tree.</summary>
    internal NamePool NamePool { get; }

    /// <summary>Monotonically-increasing identity assigned at construction time.</summary>
    internal long TreeId { get; }

    /// <summary>Total number of nodes (document-order indices 0 … NodeCount-1).</summary>
    internal int NodeCount { get; }

    /// <summary>Total number of attribute entries.</summary>
    internal int AttributeCount { get; }

    /// <summary>Total number of namespace entries.</summary>
    internal int NamespaceCount { get; }

    /// <summary>Base URI of the document node, or null if unknown.</summary>
    internal string? BaseUri { get; }

    // DtdInfo.IdFlag / IdRefFlag per attribute, null when no attribute is an ID or an IDREF.
    private readonly byte[]? _attributeFlags;
    private Dictionary<string, int>? _idMap;

    /// <summary>True when an attribute of this tree is an ID or an IDREF (declared in the DTD, or xml:id).</summary>
    internal bool HasIdInformation => _attributeFlags is not null;

    /// <summary>The ID / IDREF flags of the attribute at <paramref name="attrIndex"/>.</summary>
    internal byte GetAttributeFlags(int attrIndex) => _attributeFlags is not null && attrIndex < _attributeFlags.Length ? _attributeFlags[attrIndex] : (byte)0;

    /// <summary>The element that carries each ID of the tree; the first in document order when an ID is repeated.</summary>
    internal IReadOnlyDictionary<string, int> IdMap
    {
        get
        {
            Dictionary<string, int>? map = _idMap;
            if (map is null)
            {
                map = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int a = 0; a < AttributeCount; a++)
                {
                    if ((GetAttributeFlags(a) & DtdInfo.IdFlag) != 0)
                    {
                        string id = _attrValue[a].Trim();
                        if (id.Length > 0 && !map.ContainsKey(id))
                        {
                            map[id] = _attrParent[a];
                        }
                    }
                }

                _idMap = map;
            }

            return map;
        }
    }

    /// <summary>The unparsed entities its DTD declares, by name; null when there are none.</summary>
    internal IReadOnlyDictionary<string, UnparsedEntity>? UnparsedEntities { get; }

    /// <summary>The URI the document was loaded from; null for a tree that was constructed.</summary>
    internal string? DocumentUri { get; }

    // Base URIs set on elements by xml:base (resolved) or by copying; a node without one has its parent's.
    private readonly Dictionary<int, string>? _nodeBaseUris;

    /// <summary>The base URI of the node at <paramref name="index"/>: its own, else its nearest ancestor's, else the document's.</summary>
    internal string? GetBaseUri(int index)
    {
        for (int i = index; i >= 0; i = _parent[i])
        {
            if (_nodeBaseUris is not null && _nodeBaseUris.TryGetValue(i, out string? found))
            {
                return found;
            }
        }

        return BaseUri;
    }

    // ── Node accessors ──────────────────────────────────────────────────────

    /// <summary>Returns the node kind at the given document-order index.</summary>
    internal XdmNodeKind GetKind(int index) => (XdmNodeKind)_kind[index];

    /// <summary>Returns the depth at the given index (0 = document, 1 = root element, etc.).</summary>
    internal int GetDepth(int index) => _depth[index];

    /// <summary>Returns the NamePool name code, or -1 for anonymous nodes (document, text, comment).</summary>
    internal int GetNameCode(int index) => _nameCode[index];

    /// <summary>Returns the parent node index, or -1 for the document node.</summary>
    internal int GetParent(int index) => _parent[index];

    /// <summary>Returns the next-sibling index, or -1 when this is the last sibling.</summary>
    internal int GetNextSibling(int index) => _next[index];

    /// <summary>Returns the index of the first attribute for an element node, or -1 if none.</summary>
    internal int GetFirstAttribute(int elementIndex) => _alpha[elementIndex];

    /// <summary>Returns the index of the first namespace for an element node, or -1 if none.</summary>
    internal int GetFirstNamespace(int elementIndex) => _beta[elementIndex];

    // ── Attribute accessors ─────────────────────────────────────────────────

    /// <summary>Returns the element node index that owns the attribute at <paramref name="attrIndex"/>.</summary>
    internal int GetAttributeParent(int attrIndex) => _attrParent[attrIndex];

    /// <summary>Returns the NamePool code for the attribute at <paramref name="attrIndex"/>.</summary>
    internal int GetAttributeNameCode(int attrIndex) => _attrNameCode[attrIndex];

    /// <summary>Returns the string value of the attribute at <paramref name="attrIndex"/>.</summary>
    internal string GetAttributeValue(int attrIndex) => _attrValue[attrIndex];

    // ── Namespace accessors ─────────────────────────────────────────────────

    /// <summary>Returns the element node index that owns the namespace at <paramref name="nsIndex"/>.</summary>
    internal int GetNamespaceParent(int nsIndex) => _nsParent[nsIndex];

    /// <summary>Returns the prefix for the namespace at <paramref name="nsIndex"/>.</summary>
    internal string GetNamespacePrefix(int nsIndex) => _nsPrefix[nsIndex];

    /// <summary>Returns the URI for the namespace at <paramref name="nsIndex"/>.</summary>
    internal string GetNamespaceUri(int nsIndex) => _nsUri[nsIndex];

    // ── String value ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the XPath string value of the node at <paramref name="index"/>.
    /// <list type="bullet">
    ///   <item>document / element: concatenation of all descendant text nodes in document order (XPath 2.0 §5.11)</item>
    ///   <item>text / comment / PI: the content stored in the text buffer</item>
    ///   <item>attribute: the normalized attribute value</item>
    ///   <item>namespace: the namespace URI</item>
    /// </list>
    /// </summary>
    // XPath 2.0 §5.11
    internal string GetStringValue(int index)
    {
        XdmNodeKind kind = GetKind(index);

        switch (kind)
        {
            case XdmNodeKind.Text:
            case XdmNodeKind.Comment:
            case XdmNodeKind.ProcessingInstruction:
                return new string(_textBuffer, _textStart[index], _textLength[index]);

            case XdmNodeKind.Attribute:
                // Attributes use a negative encoded index into the attribute array.
                // However, attributes are stored separately; callers use attribute-array
                // indices directly via GetAttributeValue.  If this overload is called with
                // a node-array index that happens to be an attribute it means we encoded
                // it as a node — which we don't do in P1-08.  Throw to surface bugs early.
                throw new InvalidOperationException(
                    "Call GetAttributeValue for attribute nodes; attributes are not stored in the node array.");

            case XdmNodeKind.Namespace:
                throw new InvalidOperationException(
                    "Call GetNamespaceUri for namespace nodes; namespaces are not stored in the node array.");

            case XdmNodeKind.Document:
            case XdmNodeKind.Element:
                return ConcatenateDescendantText(index);

            default:
                throw new ArgumentOutOfRangeException(nameof(index), $"Unknown node kind {kind}.");
        }
    }

    /// <summary>
    /// Returns a read-only span over the raw character content of a text, comment, or PI node.
    /// </summary>
    internal ReadOnlySpan<char> GetTextSpan(int index)
    {
        int start = _textStart[index];
        int length = _textLength[index];
        return new ReadOnlySpan<char>(_textBuffer, start, length);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Walks the subtree rooted at <paramref name="rootIndex"/> in document order and
    /// concatenates the content of all text nodes.  XPath 2.0 §5.11.
    /// </summary>
    private string ConcatenateDescendantText(int rootIndex)
    {
        int rootDepth = _depth[rootIndex];
        StringBuilder? sb = null;

        for (int i = rootIndex + 1; i < NodeCount; i++)
        {
            // Stop when we leave the subtree.
            if (_depth[i] <= rootDepth)
            {
                break;
            }

            if ((XdmNodeKind)_kind[i] == XdmNodeKind.Text)
            {
                int start = _textStart[i];
                int len = _textLength[i];
                if (len > 0)
                {
                    if (sb is null)
                    {
                        sb = new StringBuilder(len);
                    }

                    sb.Append(_textBuffer, start, len);
                }
            }
        }

        return sb is null ? string.Empty : sb.ToString();
    }
}
