// P2-06: PathNode evaluation tests.
// XPath 2.0 §3.2 — path expressions.

using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// End-to-end tests for path expression evaluation against a TinyTree fixture.
/// XPath 2.0 §3.2.
/// </summary>
/// <remarks>
/// Fixture XML:
/// <code>
/// &lt;root xmlns:a="http://example.com/a"&gt;
///   &lt;child1 id="x" a:name="first"&gt;
///     &lt;grandchild1/&gt;
///     &lt;grandchild2/&gt;
///   &lt;/child1&gt;
///   &lt;child2&gt;
///     &lt;grandchild3/&gt;
///   &lt;/child2&gt;
///   &lt;child3/&gt;
/// &lt;/root&gt;
/// </code>
/// </remarks>
[TestFixture]
internal sealed class PathEvaluationTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    private static List<XdmItem> Drain(IXdmIterator iter)
    {
        List<XdmItem> items = new List<XdmItem>();
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            items.Add(item);
        }
        return items;
    }

    private static string LocalName(XdmItem item) =>
        ((XdmNode)item).NodeName!.Value.LocalName;

    /// <summary>
    /// Loads the fixture XML into a TinyTree and returns the document node (index 0)
    /// and the named namespace resolver for the "a" prefix.
    /// </summary>
    private static (TinyTree tree, XdmNode docNode, IStaticNamespaceResolver ns) LoadFixture()
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        string xml =
            "<root xmlns:a=\"http://example.com/a\">" +
            "<child1 id=\"x\" a:name=\"first\"><grandchild1/><grandchild2/></child1>" +
            "<child2><grandchild3/></child2>" +
            "<child3/>" +
            "</root>";
        using XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));

        // Namespace resolver: "a" → "http://example.com/a" (extends the default bindings)
        System.Collections.Generic.Dictionary<string, string> prefixMap =
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["xs"] = "http://www.w3.org/2001/XMLSchema",
                ["fn"] = "http://www.w3.org/2005/xpath-functions",
                ["a"] = "http://example.com/a",
            };
        IStaticNamespaceResolver ns = new DictionaryNamespaceResolver(prefixMap);

        return (tree, doc, ns);
    }

    private static CompiledExpression Compile(string expr, IStaticNamespaceResolver ns) =>
        XPathCompiler.ParseAndCompile(expr, SourceSpan.None, ns, EmptyFunctionLibrary.Instance);

    // ── /root/child1 ──────────────────────────────────────────────────────────

    [Test]
    public void AbsolutePath_RootSlashChild1_ReturnsSingleNode()
    {
        // Arrange
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("/root/child1");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("child1"));
    }

    // ── /root/* ───────────────────────────────────────────────────────────────

    [Test]
    public void AbsolutePath_RootChildren_ReturnsThreeChildren()
    {
        // Arrange
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("/root/*");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(LocalName(items[0]), Is.EqualTo("child1"));
        Assert.That(LocalName(items[1]), Is.EqualTo("child2"));
        Assert.That(LocalName(items[2]), Is.EqualTo("child3"));
    }

    // ── //grandchild1 ─────────────────────────────────────────────────────────

    [Test]
    public void DoubleSlash_NamedDescendant_ReturnsOneNode()
    {
        // Arrange
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("//grandchild1");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("grandchild1"));
    }

    // ── //* ───────────────────────────────────────────────────────────────────

    [Test]
    public void DoubleSlash_Wildcard_ReturnsAllElements()
    {
        // Arrange: fixture has root + child1 + grandchild1 + grandchild2 + child2 + grandchild3 + child3 = 7 elements
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("//*");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(7));
        // All must be elements
        foreach (XdmItem item in items)
        {
            Assert.That(((XdmNode)item).NodeKind, Is.EqualTo(XdmNodeKind.Element));
        }
    }

    // ── ACCEPTANCE: //*[@id='x'] ─────────────────────────────────────────────

    [Test]
    public void AbsolutePath_DescendantsWithAttributePredicate_ReturnsMatchingElement()
    {
        // Arrange: //*[@id='x'] finds child1 (the only element with id="x")
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("//*[@id='x']");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("child1"));
    }

    // ── /root/child1/grandchild2 ──────────────────────────────────────────────

    [Test]
    public void AbsolutePath_ThreeLevels_ReturnsDeepElement()
    {
        // Arrange
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("/root/child1/grandchild2");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("grandchild2"));
    }

    // ── Relative path: child1 from root element ───────────────────────────────

    [Test]
    public void RelativePath_Child_ReturnsMatchingChild()
    {
        // Arrange: context is the root element
        (TinyTree tree, _, _) = LoadFixture();
        XdmNode rootElement = new XdmNode(NodeHandle.ForNode(tree, 1));
        CompiledExpression ce = Compile("child1");
        DynamicContext ctx = NewRoot().WithContextItem(rootElement, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("child1"));
    }

    // ── .. (parent) ───────────────────────────────────────────────────────────

    [Test]
    public void ParentStep_FromChild_ReturnsParent()
    {
        // Arrange: context is child1; ".." should give the root element
        (TinyTree tree, _, _) = LoadFixture();
        // Find child1
        XdmNode? child1 = null;
        for (int i = 0; i < tree.NodeCount; i++)
        {
            if (tree.GetKind(i) == XdmNodeKind.Element)
            {
                int nc = tree.GetNameCode(i);
                if (nc >= 0 && tree.NamePool.GetLocalName(nc) == "child1")
                {
                    child1 = new XdmNode(NodeHandle.ForNode(tree, i));
                    break;
                }
            }
        }
        Assert.That(child1, Is.Not.Null, "child1 not found in fixture");

        CompiledExpression ce = Compile("..");
        DynamicContext ctx = NewRoot().WithContextItem(child1!, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("root"));
    }

    // ── Document-order deduplication: child1/grandchild1/.. ──────────────────

    [Test]
    public void PathWithParentStep_MultipleInputNodes_DeduplicatesInDocumentOrder()
    {
        // Arrange: child1/grandchild1/.. — should return child1 (deduplicated, not doubled)
        // In relative context from root element:
        (TinyTree tree, _, _) = LoadFixture();
        XdmNode rootElement = new XdmNode(NodeHandle.ForNode(tree, 1));
        CompiledExpression ce = Compile("child1/grandchild1/..");
        DynamicContext ctx = NewRoot().WithContextItem(rootElement, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert: child1 appears exactly once
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(LocalName(items[0]), Is.EqualTo("child1"));
    }

    // ── @id from child1 via path ──────────────────────────────────────────────

    [Test]
    public void AttributeStep_ViaPath_ReturnsAttribute()
    {
        // Arrange: /root/child1/@id — should yield the id attribute node
        (_, XdmNode doc, _) = LoadFixture();
        CompiledExpression ce = Compile("/root/child1/@id");
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        XdmNode attr = (XdmNode)items[0];
        Assert.That(attr.NodeKind, Is.EqualTo(XdmNodeKind.Attribute));
        Assert.That(attr.GetStringValue(), Is.EqualTo("x"));
    }
}
