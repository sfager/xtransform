using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

/// <summary>
/// Oracle-based axis navigation tests.  For each of the 13 XPath 2.0 axes we compare
/// the TinyTree iterator output against an independent System.Xml.Linq oracle.
/// </summary>
[TestFixture]
internal sealed class AxisNavigationTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    private static readonly string FixtureXml =
        "<?xml version=\"1.0\"?>" +
        "<root xmlns:a=\"http://example.com/a\">" +
        "<child1 id=\"x\" a:name=\"first\">" +
        "<grandchild1/>" +
        "<!-- comment -->" +
        "text content" +
        "<?pi target data?>" +
        "<grandchild2><great-grandchild/></grandchild2>" +
        "</child1>" +
        "<child2><grandchild3 a:name=\"deep\"/></child2>" +
        "<child3/>" +
        "</root>";

    private TinyTree _tree = null!;
    private XDocument _xdoc = null!;
    private NamePool _pool = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using (XmlReader reader = XmlReader.Create(new StringReader(FixtureXml), settings))
        {
            _tree = TinyTreeBuilder.Load(reader, _pool);
        }

        _xdoc = XDocument.Parse(FixtureXml, LoadOptions.None);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises an XdmNode to a stable identifier string "kind:local" (or "kind" for anonymous).
    /// </summary>
    private static string NodeId(XdmNode node)
    {
        string kind = node.NodeKind.ToString().ToLowerInvariant();
        string? local = node.NodeName?.LocalName;
        return string.IsNullOrEmpty(local) ? kind : kind + ":" + local;
    }

    /// <summary>Collect all results from an iterator as a list of NodeId strings.</summary>
    private static List<string> CollectIterator(IXdmIterator it)
    {
        List<string> result = new List<string>();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            result.Add(NodeId((XdmNode)item));
        }

        return result;
    }

    /// <summary>
    /// Serialises an XObject to a stable identifier string matching NodeId.
    /// Namespace attributes (IsNamespaceDeclaration) are treated as namespace nodes.
    /// </summary>
    private static string XObjectId(XObject obj)
    {
        if (obj is XDocument)
        {
            return "document";
        }

        if (obj is XElement el)
        {
            return "element:" + el.Name.LocalName;
        }

        if (obj is XText)
        {
            return "text";
        }

        if (obj is XComment)
        {
            return "comment";
        }

        if (obj is XProcessingInstruction pi)
        {
            return "processinginstruction:" + pi.Target;
        }

        if (obj is XAttribute attr)
        {
            if (attr.IsNamespaceDeclaration)
            {
                // Namespace nodes: local name is the prefix ("" for xmlns, "a" for xmlns:a).
                string prefix = attr.Name.LocalName;
                // xmlns (no colon) → empty prefix
                if (string.Equals(attr.Name.LocalName, "xmlns", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(attr.Name.NamespaceName))
                {
                    prefix = string.Empty;
                }

                return string.IsNullOrEmpty(prefix) ? "namespace" : "namespace:" + prefix;
            }

            return "attribute:" + attr.Name.LocalName;
        }

        return "unknown";
    }

    /// <summary>Enumerate all XObjects in document order (including attributes as separate items).</summary>
    private static IEnumerable<XObject> AllXObjects(XDocument doc)
    {
        yield return doc;
        foreach (XObject xo in AllXObjectsIn((XContainer)doc))
        {
            yield return xo;
        }
    }

    private static IEnumerable<XObject> AllXObjectsIn(XContainer container)
    {
        foreach (XNode child in container.Nodes())
        {
            if (child is XElement el)
            {
                yield return el;
                // Namespace attributes before regular attributes.
                foreach (XAttribute attr in el.Attributes())
                {
                    yield return attr;
                }

                foreach (XObject desc in AllXObjectsIn(el))
                {
                    yield return desc;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    // ── Build a map from XObject to tree index / handle ───────────────────────

    /// <summary>
    /// Returns all handles in this tree.  Regular nodes first (index order),
    /// then namespace handles, then attribute handles — to match traversal order.
    /// </summary>
    private static List<NodeHandle> AllHandles(TinyTree tree)
    {
        List<NodeHandle> handles = new List<NodeHandle>();
        for (int i = 0; i < tree.NodeCount; i++)
        {
            handles.Add(NodeHandle.ForNode(tree, i));
        }

        // Namespace and attribute handles.
        for (int i = 0; i < tree.NamespaceCount; i++)
        {
            handles.Add(NodeHandle.ForNamespace(tree, i));
        }

        for (int i = 0; i < tree.AttributeCount; i++)
        {
            handles.Add(NodeHandle.ForAttribute(tree, i));
        }

        return handles;
    }

    // ── Oracle helpers ────────────────────────────────────────────────────────

    private static List<string> OracleChild(XObject xo)
    {
        List<string> result = new List<string>();
        if (xo is XContainer c)
        {
            foreach (XNode n in c.Nodes())
            {
                result.Add(XObjectId(n));
            }
        }

        return result;
    }

    private static List<string> OracleDescendant(XObject xo, bool includeSelf)
    {
        List<string> result = new List<string>();
        if (includeSelf)
        {
            result.Add(XObjectId(xo));
        }

        if (xo is XContainer c)
        {
            foreach (XNode d in c.DescendantNodes())
            {
                result.Add(XObjectId(d));
            }
        }

        return result;
    }

    private static List<string> OracleSelf(XObject xo)
    {
        return new List<string> { XObjectId(xo) };
    }

    private static List<string> OracleAttribute(XObject xo)
    {
        List<string> result = new List<string>();
        if (xo is XElement el)
        {
            foreach (XAttribute attr in el.Attributes())
            {
                if (!attr.IsNamespaceDeclaration)
                {
                    result.Add(XObjectId(attr));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Oracle for the namespace axis: in-scope namespaces for an element.
    /// Only non-empty-URI bindings (excluding undeclarations).
    /// </summary>
    private static List<string> OracleNamespace(XObject xo)
    {
        List<string> result = new List<string>();
        if (xo is not XElement el)
        {
            return result;
        }

        // Walk up the ancestor chain collecting in-scope namespace bindings,
        // child declarations override parent ones (prefix uniqueness).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        XElement? cur = el;
        while (cur != null)
        {
            foreach (XAttribute attr in cur.Attributes())
            {
                if (!attr.IsNamespaceDeclaration)
                {
                    continue;
                }

                string prefix;
                if (string.Equals(attr.Name.LocalName, "xmlns", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(attr.Name.NamespaceName))
                {
                    prefix = string.Empty;
                }
                else
                {
                    prefix = attr.Name.LocalName;
                }

                string uri = attr.Value;

                // Skip undeclarations (xmlns="") and already-seen prefixes.
                if (!string.IsNullOrEmpty(uri) && seen.Add(prefix))
                {
                    result.Add(string.IsNullOrEmpty(prefix) ? "namespace" : "namespace:" + prefix);
                }
            }

            cur = cur.Parent;
        }

        // XDM: every element also has a namespace node for the xml prefix.
        if (seen.Add("xml"))
        {
            result.Add("namespace:xml");
        }

        return result;
    }

    private static List<string> OracleParent(XObject xo)
    {
        List<string> result = new List<string>();

        // Document node has no parent.
        if (xo is XDocument)
        {
            return result;
        }

        XObject? parent = null;

        if (xo is XNode n)
        {
            // XNode.Parent returns XElement? only. For top-level nodes whose parent is XDocument, use Document.
            parent = (XObject?)n.Parent ?? (n.Document != null && !ReferenceEquals(n.Document, n) ? (XObject?)n.Document : null);
        }
        else if (xo is XAttribute a)
        {
            parent = a.Parent;
        }

        if (parent != null)
        {
            result.Add(XObjectId(parent));
        }

        return result;
    }

    private static List<string> OracleAncestor(XObject xo, bool includeSelf)
    {
        List<string> result = new List<string>();

        // Start from self or parent.
        XObject? cur;
        if (includeSelf)
        {
            cur = xo;
        }
        else
        {
            cur = GetXParent(xo);
        }

        while (cur != null)
        {
            result.Add(XObjectId(cur));
            if (cur is XDocument)
            {
                break;
            }

            cur = GetXParent(cur);
        }

        return result;
    }

    /// <summary>
    /// Returns the logical parent of an XObject in the LINQ-to-XML tree.
    /// XNode.Parent returns XElement? only; for document-level nodes whose parent is
    /// XDocument we use the Document property instead.
    /// </summary>
    private static XObject? GetXParent(XObject xo)
    {
        if (xo is XDocument)
        {
            return null; // document has no parent
        }

        if (xo is XAttribute a)
        {
            return a.Parent;
        }

        if (xo is XNode n)
        {
            // Parent is XElement? — null for children of XDocument.
            if (n.Parent != null)
            {
                return n.Parent;
            }

            // If the node belongs to a document and is not the document itself, the parent is the document.
            if (n.Document != null && !ReferenceEquals(n.Document, n))
            {
                return n.Document;
            }
        }

        return null;
    }

    private static List<string> OracleFollowingSibling(XObject xo)
    {
        List<string> result = new List<string>();
        // Attributes have no sibling axis; XDocument has no siblings.
        if (xo is XAttribute || xo is XDocument)
        {
            return result;
        }

        if (xo is XNode node)
        {
            XNode? next = node.NextNode;
            while (next != null)
            {
                result.Add(XObjectId(next));
                next = next.NextNode;
            }
        }

        return result;
    }

    private static List<string> OraclePrecedingSibling(XObject xo)
    {
        List<string> result = new List<string>();
        // Attributes have no sibling axis; XDocument has no siblings.
        if (xo is XAttribute || xo is XDocument)
        {
            return result;
        }

        if (xo is XNode node)
        {
            XNode? prev = node.PreviousNode;
            while (prev != null)
            {
                result.Add(XObjectId(prev));
                prev = prev.PreviousNode;
            }
        }

        return result;
    }

    private static List<string> OracleFollowing(XObject xo, XDocument doc)
    {
        // All nodes that come after xo in document order, excluding descendants.
        // Attribute/namespace: following = nodes after owner element's subtree.
        // The document node has no following nodes (all others are descendants).
        if (xo is XDocument)
        {
            return new List<string>();
        }

        // Find position of xo or its owner.
        XObject target;
        if (xo is XAttribute attr)
        {
            target = attr.Parent!;
        }
        else
        {
            target = xo;
        }

        // Find the index of target's last descendant (exclusive end) in allInOrder.
        // We'll collect all nodes strictly after the subtree of target.
        List<string> allOrderedNodes = new List<string>();
        List<XObject> allNodeObjects = new List<XObject>();
        foreach (XObject obj in AllXObjects(doc))
        {
            if (obj is XAttribute)
            {
                continue;
            }

            allOrderedNodes.Add(XObjectId(obj));
            allNodeObjects.Add(obj);
        }

        // Find range: everything after the subtree rooted at target.
        int targetIdx = -1;
        for (int i = 0; i < allNodeObjects.Count; i++)
        {
            if (ReferenceEquals(allNodeObjects[i], target))
            {
                targetIdx = i;
                break;
            }
        }

        if (targetIdx < 0)
        {
            return new List<string>();
        }

        // Skip past subtree of target.
        int subtreeEnd = targetIdx + 1;
        int targetDepth = GetDepth(target);
        while (subtreeEnd < allNodeObjects.Count && GetDepth(allNodeObjects[subtreeEnd]) > targetDepth)
        {
            subtreeEnd++;
        }

        List<string> result = new List<string>();
        for (int i = subtreeEnd; i < allOrderedNodes.Count; i++)
        {
            result.Add(allOrderedNodes[i]);
        }

        return result;
    }

    private static List<string> OraclePreceding(XObject xo, XDocument doc)
    {
        // All nodes before xo in document order, excluding ancestors, reversed.
        // Document node has no preceding nodes (it is first in the document).
        if (xo is XDocument)
        {
            return new List<string>();
        }

        XObject target;
        if (xo is XAttribute attr)
        {
            target = attr.Parent!;
        }
        else
        {
            target = xo;
        }

        // Build ancestor set.
        HashSet<XObject> ancestors = new HashSet<XObject>(XObjectReferenceComparer.Instance);
        XObject? cur;
        if (xo is XNode n2)
        {
            cur = GetXParent(n2);
        }
        else if (xo is XAttribute a2)
        {
            cur = a2.Parent;
        }
        else
        {
            cur = null;
        }

        // For attribute, start including the owner element as an ancestor.
        if (xo is XAttribute)
        {
            ancestors.Add(target);
        }

        while (cur != null)
        {
            ancestors.Add(cur);
            if (cur is XDocument)
            {
                break;
            }

            cur = GetXParent(cur);
        }

        List<XObject> allNodeObjects = new List<XObject>();
        foreach (XObject obj in AllXObjects(doc))
        {
            if (obj is XAttribute)
            {
                continue;
            }

            allNodeObjects.Add(obj);
        }

        // Find target index.
        int targetIdx = -1;
        for (int i = 0; i < allNodeObjects.Count; i++)
        {
            if (ReferenceEquals(allNodeObjects[i], target))
            {
                targetIdx = i;
                break;
            }
        }

        // Collect all non-ancestor nodes before targetIdx, then reverse.
        List<string> before = new List<string>();
        for (int i = targetIdx - 1; i >= 0; i--)
        {
            XObject obj = allNodeObjects[i];
            if (!ancestors.Contains(obj))
            {
                before.Add(XObjectId(obj));
            }
        }

        // already in reverse order since we iterated backwards
        return before;
    }

    // XNode.Parent returns XElement?, which is null when the parent is XDocument.
    // Use Document property to detect document-level nodes and count the extra level.
    private static int GetDepth(XObject obj)
    {
        if (obj is XDocument)
        {
            return 0;
        }

        XNode? cur = obj as XNode;
        if (cur == null)
        {
            return 0;
        }

        // Count up through XElement parents.
        int d = 0;
        XElement? parent = cur.Parent;
        while (parent != null)
        {
            d++;
            parent = parent.Parent;
        }

        // If the node is inside an XDocument (not a free-floating XNode), add 1 for the document level.
        if (cur.Document != null)
        {
            d++;
        }

        return d;
    }

    // ── Axis test infrastructure ──────────────────────────────────────────────

    private void AssertAxisMatchesOracle(
        NodeHandle handle,
        Axis axis,
        List<string> oracle,
        string context)
    {
        IXdmIterator it = _tree.Iterate(handle, axis, null);
        List<string> actual = CollectIterator(it);
        Assert.That(actual, Is.EqualTo(oracle), $"{axis} from {context}");
    }

    // ── Node-only tests (skip attributes/namespaces for most axes) ────────────

    // We test from every node of every kind, using the fixture document.
    // For the namespace axis we only test element nodes.

    [Test]
    public void Child_AllElements_MatchOracle()
    {
        // Arrange / Act / Assert
        List<XObject> allXObjects = new List<XObject>();
        foreach (XObject xo in AllXObjects(_xdoc))
        {
            allXObjects.Add(xo);
        }

        for (int i = 0; i < _tree.NodeCount; i++)
        {
            // For Child axis, the XObject at the same document-order position is allXObjects[i].
            // We only need elements and document nodes (others have no children).
            XdmNodeKind kind = _tree.GetKind(i);
            if (kind != XdmNodeKind.Document && kind != XdmNodeKind.Element)
            {
                continue;
            }

            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleChild(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.Child, oracle, $"node[{i}]({kind})");
        }
    }

    [Test]
    public void Descendant_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleDescendant(GetXObjectByDocOrder(i), includeSelf: false);
            AssertAxisMatchesOracle(handle, Axis.Descendant, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void DescendantOrSelf_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleDescendant(GetXObjectByDocOrder(i), includeSelf: true);
            AssertAxisMatchesOracle(handle, Axis.DescendantOrSelf, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void Self_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleSelf(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.Self, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void Attribute_AllElementNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            if (_tree.GetKind(i) != XdmNodeKind.Element)
            {
                continue;
            }

            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleAttribute(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.Attribute, oracle, $"element[{i}]");
        }
    }

    [Test]
    public void Namespace_AllElementNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            if (_tree.GetKind(i) != XdmNodeKind.Element)
            {
                continue;
            }

            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleNamespace(GetXObjectByDocOrder(i));
            IXdmIterator it = _tree.Iterate(handle, Axis.Namespace, null);
            List<string> actual = CollectIterator(it);

            // Sort both, as ordering within same-element namespace bindings may vary.
            oracle.Sort(StringComparer.Ordinal);
            actual.Sort(StringComparer.Ordinal);
            Assert.That(actual, Is.EqualTo(oracle), $"namespace axis from element[{i}]");
        }
    }

    [Test]
    public void Parent_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleParent(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.Parent, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void Ancestor_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleAncestor(GetXObjectByDocOrder(i), includeSelf: false);
            AssertAxisMatchesOracle(handle, Axis.Ancestor, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void AncestorOrSelf_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleAncestor(GetXObjectByDocOrder(i), includeSelf: true);
            AssertAxisMatchesOracle(handle, Axis.AncestorOrSelf, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void FollowingSibling_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleFollowingSibling(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.FollowingSibling, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void PrecedingSibling_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OraclePrecedingSibling(GetXObjectByDocOrder(i));
            AssertAxisMatchesOracle(handle, Axis.PrecedingSibling, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void Following_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OracleFollowing(GetXObjectByDocOrder(i), _xdoc);
            AssertAxisMatchesOracle(handle, Axis.Following, oracle, $"node[{i}]");
        }
    }

    [Test]
    public void Preceding_AllNodes_MatchOracle()
    {
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            NodeHandle handle = NodeHandle.ForNode(_tree, i);
            List<string> oracle = OraclePreceding(GetXObjectByDocOrder(i), _xdoc);
            AssertAxisMatchesOracle(handle, Axis.Preceding, oracle, $"node[{i}]");
        }
    }

    // ── Attribute nodes on axes ───────────────────────────────────────────────

    [Test]
    public void Parent_AttributeHandle_ReturnsOwnerElement()
    {
        // Arrange — first attribute (index 0) of child1 (id="x")
        NodeHandle attrHandle = NodeHandle.ForAttribute(_tree, 0);
        XdmNode attrNode = new XdmNode(attrHandle);

        // Act
        XdmNode? parent = attrNode.Parent;

        // Assert
        Assert.That(parent, Is.Not.Null);
        Assert.That(parent!.NodeKind, Is.EqualTo(XdmNodeKind.Element));
    }

    [Test]
    public void Self_AttributeHandle_ReturnsSelf()
    {
        // Arrange
        NodeHandle attrHandle = NodeHandle.ForAttribute(_tree, 0);
        IXdmIterator it = _tree.Iterate(attrHandle, Axis.Self, null);

        // Act
        XdmItem? item = it.Next();

        // Assert
        Assert.That(item, Is.Not.Null);
        Assert.That(((XdmNode)item!).NodeKind, Is.EqualTo(XdmNodeKind.Attribute));
        Assert.That(it.Next(), Is.Null);
    }

    [Test]
    public void Attribute_NonElement_YieldsEmpty()
    {
        // Arrange — text node has no attributes
        // Find a text node
        int textIdx = -1;
        for (int i = 0; i < _tree.NodeCount; i++)
        {
            if (_tree.GetKind(i) == XdmNodeKind.Text)
            {
                textIdx = i;
                break;
            }
        }

        Assert.That(textIdx, Is.GreaterThan(0), "fixture must have text nodes");
        NodeHandle handle = NodeHandle.ForNode(_tree, textIdx);
        IXdmIterator it = _tree.Iterate(handle, Axis.Attribute, null);

        // Act / Assert
        Assert.That(it.Next(), Is.Null);
    }

    // ── Map from tree doc-order index to XObject ──────────────────────────────

    private List<XObject>? _docOrderXObjects;

    private XObject GetXObjectByDocOrder(int treeIndex)
    {
        if (_docOrderXObjects == null)
        {
            _docOrderXObjects = new List<XObject>();
            CollectDocOrder(_xdoc, _docOrderXObjects);
        }

        return _docOrderXObjects[treeIndex];
    }

    /// <summary>
    /// Collects XObjects in a document order that matches TinyTree's numbering scheme:
    /// document(0), then pre-order traversal of elements/text/comments/PIs.
    /// Does NOT include attributes as separate items in this traversal.
    /// </summary>
    private static void CollectDocOrder(XContainer container, List<XObject> result)
    {
        if (container is XDocument doc)
        {
            result.Add(doc);
        }

        foreach (XNode child in container.Nodes())
        {
            result.Add(child);
            if (child is XContainer c)
            {
                CollectDocOrder(c, result);
            }
        }
    }
}

/// <summary>
/// Reference-equality comparer for XObject — used in test helpers.
/// Avoids dependency on ReferenceEqualityComparer (net5.0+) for net462 compat.
/// </summary>
internal sealed class XObjectReferenceComparer : IEqualityComparer<XObject>
{
    internal static readonly XObjectReferenceComparer Instance = new XObjectReferenceComparer();
    private XObjectReferenceComparer() { }
    public bool Equals(XObject? x, XObject? y) => ReferenceEquals(x, y);
    public int GetHashCode(XObject obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
