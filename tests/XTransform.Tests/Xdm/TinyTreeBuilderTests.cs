using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
internal sealed class TinyTreeBuilderTests
{
    private static NamePool Pool() => new NamePool();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TinyTree EmptyDocument()
    {
        NamePool pool = Pool();
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);
        builder.StartDocument();
        builder.EndDocument();
        return builder.Build();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Test]
    public void StartDocument_EndDocument_Empty_BuildsDocumentOnlyTree()
    {
        // Arrange / Act
        TinyTree tree = EmptyDocument();

        // Assert
        Assert.That(tree.NodeCount, Is.EqualTo(1));
        Assert.That(tree.GetKind(0), Is.EqualTo(XdmNodeKind.Document));
    }

    [Test]
    public void SingleRootElement_HasCorrectKindAndNameCode()
    {
        // Arrange
        NamePool pool = Pool();
        int nameCode = pool.Allocate(string.Empty, "root");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(nameCode, SourceSpan.None);
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert
        Assert.That(tree.NodeCount, Is.EqualTo(2));
        Assert.That(tree.GetKind(1), Is.EqualTo(XdmNodeKind.Element));
        Assert.That(tree.GetNameCode(1), Is.EqualTo(nameCode));
    }

    [Test]
    public void NestedElements_HaveCorrectDepthsAndParents()
    {
        // Arrange
        NamePool pool = Pool();
        int rootCode = pool.Allocate(string.Empty, "root");
        int childCode = pool.Allocate(string.Empty, "child");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(rootCode, SourceSpan.None);
        builder.StartElement(childCode, SourceSpan.None);
        builder.EndElement();
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — document=0, root=1, child=2
        Assert.That(tree.NodeCount, Is.EqualTo(3));
        Assert.That(tree.GetDepth(0), Is.EqualTo(0), "document depth");
        Assert.That(tree.GetDepth(1), Is.EqualTo(1), "root element depth");
        Assert.That(tree.GetDepth(2), Is.EqualTo(2), "child element depth");
        Assert.That(tree.GetParent(0), Is.EqualTo(-1), "document has no parent");
        Assert.That(tree.GetParent(1), Is.EqualTo(0), "root parent is document");
        Assert.That(tree.GetParent(2), Is.EqualTo(1), "child parent is root");
    }

    [Test]
    public void Attributes_LinkToTheirElement()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "e");
        int attrCode = pool.Allocate(string.Empty, "id");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Attribute(attrCode, "42", SourceSpan.None);
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert
        Assert.That(tree.AttributeCount, Is.EqualTo(1));
        int firstAttr = tree.GetFirstAttribute(1);
        Assert.That(firstAttr, Is.EqualTo(0));
        Assert.That(tree.GetAttributeParent(0), Is.EqualTo(1));
        Assert.That(tree.GetAttributeNameCode(0), Is.EqualTo(attrCode));
        Assert.That(tree.GetAttributeValue(0), Is.EqualTo("42"));
    }

    [Test]
    public void Namespaces_LinkToTheirElement()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate("http://example.com", "e");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Namespace("ex", "http://example.com");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert
        // The outermost element gets the xml node (XDM) before the nodes it declares.
        Assert.That(tree.NamespaceCount, Is.EqualTo(2));
        int firstNs = tree.GetFirstNamespace(1);
        Assert.That(firstNs, Is.EqualTo(0));
        Assert.That(tree.GetNamespacePrefix(0), Is.EqualTo("xml"));
        Assert.That(tree.GetNamespaceParent(1), Is.EqualTo(1));
        Assert.That(tree.GetNamespacePrefix(1), Is.EqualTo("ex"));
        Assert.That(tree.GetNamespaceUri(1), Is.EqualTo("http://example.com"));
    }

    [Test]
    public void AdjacentText_IsMerged()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "p");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Text("Hello");
        builder.Text(", World");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — document(0), element(1), one merged text node(2)
        Assert.That(tree.NodeCount, Is.EqualTo(3), "two Text calls should produce one node");
        Assert.That(tree.GetKind(2), Is.EqualTo(XdmNodeKind.Text));
        Assert.That(tree.GetStringValue(2), Is.EqualTo("Hello, World"));
    }

    [Test]
    public void WhitespaceOnlyText_IsKept()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "e");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Text("   ");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — XSLT 2.0 §3.4.4: stripping is the XSLT layer's responsibility
        Assert.That(tree.NodeCount, Is.EqualTo(3), "whitespace-only text node must be kept");
        Assert.That(tree.GetKind(2), Is.EqualTo(XdmNodeKind.Text));
        Assert.That(tree.GetStringValue(2), Is.EqualTo("   "));
    }

    [Test]
    public void Comment_IsStored()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "root");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Comment("a comment");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert
        Assert.That(tree.GetKind(2), Is.EqualTo(XdmNodeKind.Comment));
        Assert.That(tree.GetStringValue(2), Is.EqualTo("a comment"));
    }

    [Test]
    public void ProcessingInstruction_IsStored()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "root");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.ProcessingInstruction("xml-stylesheet", "type=\"text/css\"");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert
        Assert.That(tree.GetKind(2), Is.EqualTo(XdmNodeKind.ProcessingInstruction));
        Assert.That(tree.GetStringValue(2), Is.EqualTo("type=\"text/css\""));
    }

    [Test]
    public void NextSibling_ChainIsCorrect()
    {
        // Arrange — <root><a/><b/><c/></root>
        NamePool pool = Pool();
        int rootCode = pool.Allocate(string.Empty, "root");
        int aCode = pool.Allocate(string.Empty, "a");
        int bCode = pool.Allocate(string.Empty, "b");
        int cCode = pool.Allocate(string.Empty, "c");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(rootCode, SourceSpan.None);
        builder.StartElement(aCode, SourceSpan.None);
        builder.EndElement();
        builder.StartElement(bCode, SourceSpan.None);
        builder.EndElement();
        builder.StartElement(cCode, SourceSpan.None);
        builder.EndElement();
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — document=0, root=1, a=2, b=3, c=4
        Assert.That(tree.GetNextSibling(2), Is.EqualTo(3), "a->b");
        Assert.That(tree.GetNextSibling(3), Is.EqualTo(4), "b->c");
        Assert.That(tree.GetNextSibling(4), Is.EqualTo(-1), "c is last");
    }

    [Test]
    public void GetStringValue_DocumentNode_ConcatenatesAllDescendantText()
    {
        // Arrange — <root>Hello <child>World</child>!</root>
        NamePool pool = Pool();
        int rootCode = pool.Allocate(string.Empty, "root");
        int childCode = pool.Allocate(string.Empty, "child");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(rootCode, SourceSpan.None);
        builder.Text("Hello ");
        builder.StartElement(childCode, SourceSpan.None);
        builder.Text("World");
        builder.EndElement();
        builder.Text("!");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — XPath 2.0 §5.11
        Assert.That(tree.GetStringValue(0), Is.EqualTo("Hello World!"));
    }

    [Test]
    public void GetStringValue_Element_ConcatenatesDescendantTextOnly()
    {
        // Arrange — <root>before<child>inner</child>after</root>
        NamePool pool = Pool();
        int rootCode = pool.Allocate(string.Empty, "root");
        int childCode = pool.Allocate(string.Empty, "child");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(rootCode, SourceSpan.None);
        builder.Text("before");
        builder.StartElement(childCode, SourceSpan.None);
        builder.Text("inner");
        builder.EndElement();
        builder.Text("after");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — root element is at index 1 (XPath 2.0 §5.11)
        Assert.That(tree.GetStringValue(1), Is.EqualTo("beforeinnerafter"));
    }

    [Test]
    public void GetStringValue_Attribute_ReturnsValue()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "e");
        int attrCode = pool.Allocate(string.Empty, "href");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Attribute(attrCode, "http://example.com", SourceSpan.None);
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — attributes use GetAttributeValue, not GetStringValue on node index
        Assert.That(tree.GetAttributeValue(0), Is.EqualTo("http://example.com"));
    }

    [Test]
    public void GetStringValue_TextNode_ReturnsText()
    {
        // Arrange
        NamePool pool = Pool();
        int elemCode = pool.Allocate(string.Empty, "p");
        TinyTreeBuilder builder = new TinyTreeBuilder(pool, null);

        // Act
        builder.StartDocument();
        builder.StartElement(elemCode, SourceSpan.None);
        builder.Text("content");
        builder.EndElement();
        builder.EndDocument();
        TinyTree tree = builder.Build();

        // Assert — text node is at index 2
        Assert.That(tree.GetKind(2), Is.EqualTo(XdmNodeKind.Text));
        Assert.That(tree.GetStringValue(2), Is.EqualTo("content"));
    }
}
