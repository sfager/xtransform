// P2-14: QName and namespace function tests (F&O §13 and §16).

using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for the F&amp;O §13 (fn:lang) and §16 (QName/namespace) built-in functions.
/// </summary>
[TestFixture]
internal sealed class QNameFunctionTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fixture XML:
    /// <code>&lt;root xmlns:p="http://example" xml:lang="en-US"&gt;&lt;child/&gt;&lt;/root&gt;</code>
    /// </summary>
    private const string FixtureXml =
        "<root xmlns:p='http://example' xml:lang='en-US'><child/></root>";

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Loads the fixture and returns the document node and root element.
    /// Fixture: &lt;root xmlns:p="http://example" xml:lang="en-US"&gt;&lt;child/&gt;&lt;/root&gt;
    /// </summary>
    private static (XdmNode DocumentNode, XdmNode RootElement, XdmNode ChildElement) LoadFixture()
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(new StringReader(FixtureXml), settings);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);

        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));
        XdmNode root = GetFirstElementChild(doc);
        XdmNode child = GetFirstElementChild(root);

        return (doc, root, child);
    }

    private static XdmNode GetFirstElementChild(XdmNode parent)
    {
        foreach (XdmNode n in parent.Axis(Axis.Child))
        {
            if (n.NodeKind == XdmNodeKind.Element)
            {
                return n;
            }
        }
        throw new System.InvalidOperationException("No element child found.");
    }

    // ── fn:QName ──────────────────────────────────────────────────────────────

    [Test]
    public void QName_PrefixedLexAndUri_ReturnsExpectedQName()
    {
        // Arrange
        string expr = "fn:QName('http://example.com/ns', 'p:local')";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        XdmAtomicValue av = (XdmAtomicValue)result[0];
        Assert.That(av.TypeCode, Is.EqualTo(XdmTypeCode.QName));
        QName qn = av.AsQName();
        Assert.That(qn.Prefix, Is.EqualTo("p"));
        Assert.That(qn.LocalName, Is.EqualTo("local"));
        Assert.That(qn.NamespaceUri, Is.EqualTo("http://example.com/ns"));
    }

    [Test]
    public void QName_UnprefixedLexEmptyUri_ReturnsQNameWithEmptyComponents()
    {
        // Arrange
        string expr = "fn:QName('', 'local')";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        QName qn = ((XdmAtomicValue)result[0]).AsQName();
        Assert.That(qn.Prefix, Is.EqualTo(string.Empty));
        Assert.That(qn.LocalName, Is.EqualTo("local"));
        Assert.That(qn.NamespaceUri, Is.EqualTo(string.Empty));
    }

    [Test]
    public void QName_PrefixedLexEmptyUri_RaisesFOCA0002()
    {
        // Arrange
        string expr = "fn:QName('', 'p:local')";

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile(expr).Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOCA0002));
    }

    [Test]
    public void QName_InvalidLocalName_RaisesFOCA0002()
    {
        // Arrange — "123bad" is not a valid NCName (starts with digit).
        string expr = "fn:QName('http://x', '123bad')";

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile(expr).Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FOCA0002));
    }

    // ── fn:prefix-from-QName ─────────────────────────────────────────────────

    [Test]
    public void PrefixFromQName_QNameWithoutPrefix_ReturnsEmptySequence()
    {
        // Arrange — QName('http://x', 'foo') has no prefix.
        string expr = "fn:prefix-from-QName(fn:QName('http://x', 'foo'))";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert — empty sequence
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void PrefixFromQName_QNameWithPrefix_ReturnsPrefix()
    {
        // Arrange
        string expr = "fn:prefix-from-QName(fn:QName('http://x', 'p:foo'))";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].GetStringValue(), Is.EqualTo("p"));
    }

    [Test]
    public void PrefixFromQName_EmptySequenceArg_ReturnsEmptySequence()
    {
        // Arrange
        string expr = "fn:prefix-from-QName(())";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── fn:local-name-from-QName ─────────────────────────────────────────────

    [Test]
    public void LocalNameFromQName_ReturnsLocalPart()
    {
        // Arrange
        string expr = "fn:local-name-from-QName(fn:QName('http://x', 'p:foo'))";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].GetStringValue(), Is.EqualTo("foo"));
    }

    [Test]
    public void LocalNameFromQName_EmptySequenceArg_ReturnsEmptySequence()
    {
        // Arrange
        string expr = "fn:local-name-from-QName(())";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── fn:namespace-uri-from-QName ──────────────────────────────────────────

    [Test]
    public void NamespaceUriFromQName_ReturnsNamespaceUri()
    {
        // Arrange
        string expr = "fn:namespace-uri-from-QName(fn:QName('http://x', 'foo'))";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].GetStringValue(), Is.EqualTo("http://x"));
    }

    [Test]
    public void NamespaceUriFromQName_EmptySequenceArg_ReturnsEmptySequence()
    {
        // Arrange
        string expr = "fn:namespace-uri-from-QName(())";

        // Act
        List<XdmItem> result = Drain(Compile(expr).Root.Iterate(NewRoot()));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── fn:resolve-QName ─────────────────────────────────────────────────────

    [Test]
    public void ResolveQName_PrefixDeclaredOnElement_ReturnsQName()
    {
        // Arrange — root element has xmlns:p='http://example'
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:resolve-QName('p:x', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        XdmAtomicValue av = (XdmAtomicValue)result[0];
        Assert.That(av.TypeCode, Is.EqualTo(XdmTypeCode.QName));
        QName qn = av.AsQName();
        Assert.That(qn.Prefix, Is.EqualTo("p"));
        Assert.That(qn.LocalName, Is.EqualTo("x"));
        Assert.That(qn.NamespaceUri, Is.EqualTo("http://example"));
    }

    [Test]
    public void ResolveQName_UnknownPrefix_RaisesFONS0004()
    {
        // Arrange — prefix "unknown" is not in scope.
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:resolve-QName('unknown:x', .)").Root.Iterate(ctx)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FONS0004));
    }

    [Test]
    public void ResolveQName_EmptyLexical_ReturnsEmptySequence()
    {
        // Arrange
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:resolve-QName((), .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── fn:in-scope-prefixes ─────────────────────────────────────────────────

    [Test]
    public void InScopePrefixes_AlwaysContainsXml()
    {
        // Arrange — root has xmlns:p="http://example"; xml is always in scope.
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:in-scope-prefixes(.)").Root.Iterate(ctx));

        // Assert — must contain "xml"
        List<string> prefixes = new List<string>();
        foreach (XdmItem item in result)
        {
            prefixes.Add(item.GetStringValue());
        }

        Assert.That(prefixes, Contains.Item("xml"));
    }

    [Test]
    public void InScopePrefixes_ContainsDeclaredPrefixes()
    {
        // Arrange — root has xmlns:p="http://example"
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:in-scope-prefixes(.)").Root.Iterate(ctx));

        // Assert — must contain "p"
        List<string> prefixes = new List<string>();
        foreach (XdmItem item in result)
        {
            prefixes.Add(item.GetStringValue());
        }

        Assert.That(prefixes, Contains.Item("p"));
    }

    // ── fn:namespace-uri-for-prefix ──────────────────────────────────────────

    [Test]
    public void NamespaceUriForPrefix_KnownPrefix_ReturnsUri()
    {
        // Arrange — root has xmlns:p="http://example"
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result =
            Drain(Compile("fn:namespace-uri-for-prefix('p', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].GetStringValue(), Is.EqualTo("http://example"));
    }

    [Test]
    public void NamespaceUriForPrefix_XmlPrefix_ReturnsXmlNamespaceUri()
    {
        // Arrange
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result =
            Drain(Compile("fn:namespace-uri-for-prefix('xml', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].GetStringValue(),
            Is.EqualTo("http://www.w3.org/XML/1998/namespace"));
    }

    [Test]
    public void NamespaceUriForPrefix_UnknownPrefix_ReturnsEmptySequence()
    {
        // Arrange
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result =
            Drain(Compile("fn:namespace-uri-for-prefix('nothere', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Is.Empty);
    }

    // ── fn:lang ───────────────────────────────────────────────────────────────

    [Test]
    public void Lang2_XmlLangExactMatch_ReturnsTrue()
    {
        // Arrange — root has xml:lang="en-US"; test "en-US" (exact).
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('en-US', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.True);
    }

    [Test]
    public void Lang2_XmlLangSubLanguageMatch_ReturnsTrue()
    {
        // Arrange — root has xml:lang="en-US"; test "en" (sublanguage of "en-US").
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('en', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.True);
    }

    [Test]
    public void Lang2_XmlLangDifferentLanguage_ReturnsFalse()
    {
        // Arrange — root has xml:lang="en-US"; test "de".
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('de', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.False);
    }

    [Test]
    public void Lang2_InheritedXmlLangFromParent_ReturnsTrue()
    {
        // Arrange — child element inherits xml:lang="en-US" from root.
        (_, _, XdmNode child) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(child, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('en', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.True);
    }

    [Test]
    public void Lang2_NoXmlLangOnAnyAncestor_ReturnsFalse()
    {
        // Arrange — simple tree with no xml:lang anywhere.
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(new StringReader("<root><child/></root>"), settings);
        TinyTree tree = TinyTreeBuilder.Load(reader, pool);
        XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));
        XdmNode root = GetFirstElementChild(doc);

        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('en', .)").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.False);
    }

    [Test]
    public void Lang1_UsesContextItem_ReturnsTrue()
    {
        // Arrange — 1-arg fn:lang uses context item.
        (_, XdmNode root, _) = LoadFixture();
        DynamicContext ctx = NewRoot().WithContextItem(root, 1, 1);

        // Act
        List<XdmItem> result = Drain(Compile("fn:lang('en')").Root.Iterate(ctx));

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(((XdmAtomicValue)result[0]).AsBoolean(), Is.True);
    }

    [Test]
    public void Lang1_NoContextItem_RaisesXPDY0002()
    {
        // Arrange / Act / Assert
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(Compile("fn:lang('en')").Root.Iterate(NewRoot())))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPDY0002));
    }
}
