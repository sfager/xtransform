using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
internal sealed class TinyTreeLoadTests
{
    private static XmlReader ReaderFromString(string xml)
    {
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        return XmlReader.Create(new StringReader(xml), settings);
    }

    [Test]
    public void Load_SimpleElement_ProducesExpectedTree()
    {
        // Arrange
        NamePool pool = new NamePool();

        // Act
        using XmlReader reader = ReaderFromString("<root/>");
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        // Assert — document(0), root-element(1)
        Assert.That(tree.NodeCount, Is.EqualTo(2));
        Assert.That(tree.GetKind(0), Is.EqualTo(XdmNodeKind.Document));
        Assert.That(tree.GetKind(1), Is.EqualTo(XdmNodeKind.Element));
        int rootNameCode = pool.Allocate(string.Empty, "root");
        Assert.That(tree.GetNameCode(1), Is.EqualTo(rootNameCode));
    }

    [Test]
    public void Load_NestedElements_PreserveStructure()
    {
        // Arrange
        NamePool pool = new NamePool();
        const string xml = "<a><b><c/></b></a>";

        // Act
        using XmlReader reader = ReaderFromString(xml);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        // Assert — document(0), a(1), b(2), c(3)
        Assert.That(tree.NodeCount, Is.EqualTo(4));
        Assert.That(tree.GetDepth(1), Is.EqualTo(1), "a depth");
        Assert.That(tree.GetDepth(2), Is.EqualTo(2), "b depth");
        Assert.That(tree.GetDepth(3), Is.EqualTo(3), "c depth");
        Assert.That(tree.GetParent(2), Is.EqualTo(1), "b parent is a");
        Assert.That(tree.GetParent(3), Is.EqualTo(2), "c parent is b");
    }

    [Test]
    public void Load_AttributesAndNamespaces_AreCaptured()
    {
        // Arrange
        NamePool pool = new NamePool();
        const string xml = "<e xmlns:ex=\"http://example.com\" id=\"42\"/>";

        // Act
        using XmlReader reader = ReaderFromString(xml);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        // Assert — attribute
        Assert.That(tree.AttributeCount, Is.EqualTo(1));
        int firstAttr = tree.GetFirstAttribute(1);
        Assert.That(firstAttr, Is.Not.EqualTo(-1));
        Assert.That(tree.GetAttributeValue(firstAttr), Is.EqualTo("42"));

        // Assert — namespace
        Assert.That(tree.NamespaceCount, Is.GreaterThanOrEqualTo(1));
        int firstNs = tree.GetFirstNamespace(1);
        Assert.That(firstNs, Is.Not.EqualTo(-1));
        // Find the ex namespace
        bool found = false;
        for (int i = firstNs; i < tree.NamespaceCount && tree.GetNamespaceParent(i) == 1; i++)
        {
            if (tree.GetNamespacePrefix(i) == "ex" && tree.GetNamespaceUri(i) == "http://example.com")
            {
                found = true;
                break;
            }
        }
        Assert.That(found, Is.True, "namespace ex should be captured");
    }

    [Test]
    public void Load_ComplexFixture_RoundTripsStringValue()
    {
        // Arrange
        NamePool pool = new NamePool();
        const string xml = "<root><a>Hello </a><b>World</b><c>!</c></root>";
        const string expectedText = "Hello World!";

        // Act
        using XmlReader reader = ReaderFromString(xml);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        // Assert — XPath 2.0 §5.11: document string value = concat of text descendants
        Assert.That(tree.GetStringValue(0), Is.EqualTo(expectedText));
    }
}
