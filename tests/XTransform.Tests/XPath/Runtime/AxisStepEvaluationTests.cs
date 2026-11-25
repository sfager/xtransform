// P2-06: AxisStepNode evaluation tests.
// XPath 2.0 §3.2.1 — axis steps.

using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <c>AxisStepNode.Iterate</c>.
/// XPath 2.0 §3.2.1 — axis steps with node tests.
/// </summary>
[TestFixture]
internal sealed class AxisStepEvaluationTests
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

    /// <summary>
    /// Loads the test fixture:
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
    /// </summary>
    private static TinyTree LoadFixture()
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
        return TinyTreeBuilder.Load(reader, pool);
    }

    // ── child::child1 from root ────────────────────────────────────────────────

    [Test]
    public void ChildAxis_NamedElement_ReturnsMatchingChild()
    {
        // Arrange
        TinyTree tree = LoadFixture();
        // Document=0, root=1
        XdmNode rootElement = new XdmNode(NodeHandle.ForNode(tree, 1));
        CompiledExpression ce = Compile("child::child1");
        DynamicContext ctx = NewRoot().WithContextItem(rootElement, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        XdmNode child1 = (XdmNode)items[0];
        Assert.That(child1.NodeKind, Is.EqualTo(XdmNodeKind.Element));
        Assert.That(child1.NodeName!.Value.LocalName, Is.EqualTo("child1"));
    }

    // ── descendant::grandchild1 from root ──────────────────────────────────────

    [Test]
    public void DescendantAxis_NamedElement_ReturnsDeepChild()
    {
        // Arrange
        TinyTree tree = LoadFixture();
        XdmNode rootElement = new XdmNode(NodeHandle.ForNode(tree, 1));
        CompiledExpression ce = Compile("descendant::grandchild1");
        DynamicContext ctx = NewRoot().WithContextItem(rootElement, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(((XdmNode)items[0]).NodeName!.Value.LocalName, Is.EqualTo("grandchild1"));
    }

    // ── self::node() from an element ──────────────────────────────────────────

    [Test]
    public void SelfAxis_NodeTest_ReturnsTheElementItself()
    {
        // Arrange
        TinyTree tree = LoadFixture();
        XdmNode rootElement = new XdmNode(NodeHandle.ForNode(tree, 1));
        CompiledExpression ce = Compile("self::node()");
        DynamicContext ctx = NewRoot().WithContextItem(rootElement, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(((XdmNode)items[0]).Equals(rootElement), Is.True);
    }

    // ── parent::* from a grandchild ───────────────────────────────────────────

    [Test]
    public void ParentAxis_WildcardNameTest_ReturnsParentElement()
    {
        // Arrange: navigate to grandchild1 (child1 is at index 2 in fixture, grandchild1 is 3)
        TinyTree tree = LoadFixture();
        // Find grandchild1 by name
        XdmNode? grandchild1 = null;
        for (int i = 0; i < tree.NodeCount; i++)
        {
            if (tree.GetKind(i) == XdmNodeKind.Element)
            {
                int nc = tree.GetNameCode(i);
                if (nc >= 0 && tree.NamePool.GetLocalName(nc) == "grandchild1")
                {
                    grandchild1 = new XdmNode(NodeHandle.ForNode(tree, i));
                    break;
                }
            }
        }
        Assert.That(grandchild1, Is.Not.Null, "grandchild1 not found");

        CompiledExpression ce = Compile("parent::*");
        DynamicContext ctx = NewRoot().WithContextItem(grandchild1!, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(((XdmNode)items[0]).NodeName!.Value.LocalName, Is.EqualTo("child1"));
    }

    // ── @id from child1 ───────────────────────────────────────────────────────

    [Test]
    public void AttributeAxis_NamedAttribute_ReturnsIdAttribute()
    {
        // Arrange: navigate to child1
        TinyTree tree = LoadFixture();
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
        Assert.That(child1, Is.Not.Null, "child1 not found");

        CompiledExpression ce = Compile("@id");
        DynamicContext ctx = NewRoot().WithContextItem(child1!, 1, 1);

        // Act
        List<XdmItem> items = Drain(ce.Root.Iterate(ctx));

        // Assert
        Assert.That(items, Has.Count.EqualTo(1));
        XdmNode attr = (XdmNode)items[0];
        Assert.That(attr.NodeKind, Is.EqualTo(XdmNodeKind.Attribute));
        Assert.That(attr.GetStringValue(), Is.EqualTo("x"));
    }

    // ── Context item not a node → XPTY0004 ────────────────────────────────────

    [Test]
    public void Iterate_ContextItemIsAtomic_RaisesXPTY0020()
    {
        // Arrange
        CompiledExpression ce = Compile("child::*");
        DynamicContext ctx = NewRoot().WithContextItem(XdmAtomicValue.Integer(42L), 1, 1);

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() =>
            Drain(ce.Root.Iterate(ctx)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0020));
    }

    // ── Context item undefined → XPDY0002 ─────────────────────────────────────

    [Test]
    public void Iterate_ContextItemUndefined_RaisesXPDY0002()
    {
        // Arrange: no context item set
        CompiledExpression ce = Compile("child::*");
        DynamicContext ctx = NewRoot();

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() =>
            Drain(ce.Root.Iterate(ctx)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }
}
