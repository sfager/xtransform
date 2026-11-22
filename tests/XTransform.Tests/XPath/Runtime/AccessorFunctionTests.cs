// P2-08: Accessor function tests (F&O §2).

using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §2 accessor built-in functions.
/// </summary>
[TestFixture]
internal sealed class AccessorFunctionTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr,
            SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            BuiltInFunctionLibrary.Instance);

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

    private static string StringOf(XdmItem item) => item.GetStringValue();

    /// <summary>
    /// Loads a small fixture:
    /// <code>&lt;root id="r"&gt;&lt;child/&gt;&lt;/root&gt;</code>
    /// </summary>
    private static (TinyTree Tree, XdmNode DocumentNode, XdmNode RootElement) LoadFixture()
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        string xml = "<root id=\"r\"><child/></root>";
        using XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        // Document node is at index 0.
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));
        // First (and only) element child of document is <root>.
        XdmNode root = doc.Axis(Axis.Child).GetEnumerator() is var en && en.MoveNext()
            ? en.Current
            : throw new System.InvalidOperationException("Fixture has no root element.");

        return (tree, doc, root);
    }

    // ── fn:string ────────────────────────────────────────────────────────────

    [Test]
    public void FnString_Integer_ReturnsDecimalRepresentation()
    {
        // Arrange / Act
        List<XdmItem> result = Drain(Compile("fn:string(42)").Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo("42"));
    }

    [Test]
    public void FnString_EmptySequence_ReturnsEmptyString()
    {
        // Arrange / Act
        List<XdmItem> result = Drain(Compile("fn:string(())").Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo(string.Empty));
    }

    [Test]
    public void FnString_ZeroArg_UsesContextItem()
    {
        // Arrange
        DynamicContext ctx = NewRoot().WithContextItem(XdmAtomicValue.Integer(42), 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:string()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo("42"));
    }

    [Test]
    public void FnString_ZeroArg_NoContextItem_ThrowsXPDY0002()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:string()").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }

    // ── fn:data ───────────────────────────────────────────────────────────────

    [Test]
    public void FnData_IntegerAndString_PassesThrough()
    {
        // Arrange / Act
        List<XdmItem> result = Drain(Compile("fn:data((1, 'a'))").Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(((XdmAtomicValue)result[0]).AsInt64(), Is.EqualTo(1L));
        Assert.That(result[1].GetStringValue(), Is.EqualTo("a"));
    }

    // ── fn:local-name ──────────────────────────────────────────────────────────

    [Test]
    public void FnLocalName_Element_ReturnsLocalName()
    {
        // Arrange
        (_, _, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act — fn:local-name() with context item
        List<XdmItem> result = Drain(Compile("fn:local-name()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo("root"));
    }

    [Test]
    public void FnLocalName_EmptyArg_ReturnsEmptyString()
    {
        // Arrange / Act
        List<XdmItem> result = Drain(Compile("fn:local-name(())").Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo(string.Empty));
    }

    // ── fn:name ────────────────────────────────────────────────────────────────

    [Test]
    public void FnName_Element_ReturnsQNameString()
    {
        // Arrange
        (_, _, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:name()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(StringOf(result[0]), Is.EqualTo("root"));
    }

    // ── fn:namespace-uri ───────────────────────────────────────────────────────

    [Test]
    public void FnNamespaceUri_NoNamespace_ReturnsEmptyAnyUri()
    {
        // Arrange
        (_, _, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:namespace-uri()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        XdmAtomicValue av = (XdmAtomicValue)result[0];
        Assert.That(av.TypeCode, Is.EqualTo(XdmTypeCode.AnyUri));
        Assert.That(av.GetStringValue(), Is.EqualTo(string.Empty));
    }

    // ── fn:root ────────────────────────────────────────────────────────────────

    [Test]
    public void FnRoot_ChildElement_ReturnsDocumentNode()
    {
        // Arrange
        (_, XdmNode doc, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act — fn:root() with context item = root element should return document
        List<XdmItem> result = Drain(Compile("fn:root()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        XdmNode resultNode = (XdmNode)result[0];
        Assert.That(resultNode.NodeKind, Is.EqualTo(XdmNodeKind.Document));
    }

    // ── fn:node-name ───────────────────────────────────────────────────────────

    [Test]
    public void FnNodeName_Element_ReturnsQName()
    {
        // Arrange
        (_, _, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:node-name()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        XdmAtomicValue av = (XdmAtomicValue)result[0];
        Assert.That(av.TypeCode, Is.EqualTo(XdmTypeCode.QName));
        Assert.That(av.AsQName().LocalName, Is.EqualTo("root"));
    }

    [Test]
    public void FnNodeName_DocumentNode_ReturnsEmpty()
    {
        // Arrange
        (_, XdmNode doc, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(doc, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:node-name()").Root.Iterate(ctx));

        // Assert — document nodes have no name
        Assert.That(result, Is.Empty);
    }

    // ── fn:nilled ──────────────────────────────────────────────────────────────

    [Test]
    public void FnNilled_UnschemaNode_ReturnsFalse()
    {
        // Arrange
        (_, _, XdmNode root) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:nilled()").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.False);
    }
}
