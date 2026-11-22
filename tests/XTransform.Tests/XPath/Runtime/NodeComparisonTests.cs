// P2-05: Node comparison operator evaluation tests.
// XPath 2.0 §3.5.3 — node comparisons: is, <<, >>.

using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

[TestFixture]
internal sealed class NodeComparisonTests
{
    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static DynamicContext NewRoot() => DynamicContext.Root(new RunState());

    // Load a small XML tree: <root><a/><b/></root>
    // Indices: document=0, root=1, a=2, b=3
    private static TinyTree LoadTree()
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(
            new StringReader("<root><a/><b/></root>"), settings);
        return TinyTreeBuilder.Load(reader, pool);
    }

    private static XdmNode Node(TinyTree tree, int index)
        => new XdmNode(NodeHandle.ForNode(tree, index));

    // ── is (node identity) ────────────────────────────────────────────────────

    [Test]
    public void Is_SameNode_ReturnsTrue()
    {
        // Arrange: context item = <a>; evaluate `. is .`
        TinyTree tree = LoadTree();
        XdmNode a = Node(tree, 2);
        CompiledExpression ce = Compile(". is .");
        DynamicContext ctx = NewRoot().WithContextItem(a, 1, 1);

        // Act
        bool result = ce.Root.EffectiveBooleanValue(ctx);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Is_DifferentNodes_ReturnsFalse()
    {
        // Arrange
        TinyTree tree = LoadTree();
        XdmNode a = Node(tree, 2);
        XdmNode b = Node(tree, 3);

        // Act / Assert
        Assert.That(a.Equals(b), Is.False);
        Assert.That(a.Equals(a), Is.True);
    }

    // ── document-order comparisons ────────────────────────────────────────────

    [Test]
    public void CompareDocumentOrder_ABeforeB_NegativeResult()
    {
        // Arrange
        TinyTree tree = LoadTree();
        XdmNode a = Node(tree, 2);
        XdmNode b = Node(tree, 3);

        // Act / Assert: a comes before b in document order
        Assert.That(a.CompareDocumentOrder(b), Is.LessThan(0));
        Assert.That(b.CompareDocumentOrder(a), Is.GreaterThan(0));
        Assert.That(a.CompareDocumentOrder(a), Is.EqualTo(0));
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Test]
    public void NodeComparison_NonNodeOperand_ThrowsXPTY0004()
    {
        // Arrange: `1 is 1` — operands are not nodes
        CompiledExpression ce = Compile("1 is 1");

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => ce.Root.EvaluateItem(NewRoot()))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void NodeComparison_EmptyOperand_ReturnsEmpty()
    {
        // Arrange: `() is .` — empty left operand
        CompiledExpression ce = Compile("() is .");

        // Act
        XdmItem? result = ce.Root.EvaluateItem(NewRoot());

        // Assert
        Assert.That(result, Is.Null);
    }
}
