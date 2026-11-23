using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Unit tests for <see cref="PatternMatcher"/>. Builds a small TinyTree, parses patterns via
/// <see cref="XPathParser.ParsePattern"/>, and asserts the matcher's verdict against each
/// node in the tree.
/// </summary>
[TestFixture]
internal sealed class PatternMatcherTests
{
    private static readonly NamePool s_pool = new NamePool();
    private static readonly IStaticNamespaceResolver s_ns = new DictionaryNamespaceResolver(new Dictionary<string, string>());

    private static TinyTree LoadSample()
    {
        // <doc>
        //   <a>
        //     <b id="k1"/>
        //     <b/>
        //   </a>
        //   <c>
        //     <b/>
        //   </c>
        // </doc>
        const string xml = "<doc><a><b id='k1'/><b/></a><c><b/></c></doc>";
        using StringReader sr = new StringReader(xml);
        using XmlReader reader = XmlReader.Create(sr);
        return TinyTreeBuilder.Load(reader, s_pool);
    }

    private static XdmNode Find(TinyTree tree, string localName, int occurrence = 1)
    {
        int seen = 0;
        for (int i = 0; i < tree.NodeCount; i++)
        {
            if (tree.GetKind(i) != XdmNodeKind.Element)
            {
                continue;
            }

            int nc = tree.GetNameCode(i);
            if (nc >= 0 && tree.NamePool.GetQName(nc).LocalName == localName)
            {
                seen++;
                if (seen == occurrence)
                {
                    return new XdmNode(NodeHandle.ForNode(tree, i));
                }
            }
        }

        throw new KeyNotFoundException($"no element '{localName}' #{occurrence}");
    }

    private static bool Match(string pattern, XdmNode node)
    {
        PatternAst ast = XPathParser.ParsePattern(pattern, SourceSpan.None, s_ns);
        DynamicContext ctx = DynamicContext.Root(new RunState()).WithContextItem(node, 1, 1);
        return PatternMatcher.Matches(ast, node, ctx);
    }

    [Test]
    public void Match_NameOnElement_True()
    {
        // Arrange
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.That(Match("a", Find(tree, "a")), Is.True);
    }

    [Test]
    public void Match_NameOnWrongElement_False()
    {
        // Arrange
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.That(Match("a", Find(tree, "b")), Is.False);
    }

    [Test]
    public void Match_ChildPath_OnlyMatchingChildParent()
    {
        // Arrange — "a/b" matches a <b> whose parent is <a>; doesn't match <b> under <c>.
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.Multiple(() =>
        {
            Assert.That(Match("a/b", Find(tree, "b", 1)), Is.True);   // under <a>
            Assert.That(Match("a/b", Find(tree, "b", 3)), Is.False);  // under <c>
        });
    }

    [Test]
    public void Match_DescendantPath_WalksAncestorChain()
    {
        // Arrange — "doc//b" matches any <b> inside <doc>.
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.Multiple(() =>
        {
            Assert.That(Match("doc//b", Find(tree, "b", 1)), Is.True);
            Assert.That(Match("doc//b", Find(tree, "b", 2)), Is.True);
            Assert.That(Match("doc//b", Find(tree, "b", 3)), Is.True);
        });
    }

    [Test]
    public void Match_WildcardAny_MatchesEveryElement()
    {
        // Arrange
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.That(Match("*", Find(tree, "a")), Is.True);
    }

    [Test]
    public void Match_NodeKind_MatchesEveryNode()
    {
        // Arrange — node() matches anything.
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.That(Match("node()", Find(tree, "a")), Is.True);
    }

    [Test]
    public void Match_IdLiteralPattern_MatchesByIdAttribute()
    {
        // Arrange — one <b id="k1"> under <a>.
        TinyTree tree = LoadSample();

        // Act + Assert
        Assert.Multiple(() =>
        {
            Assert.That(Match("id('k1')", Find(tree, "b", 1)), Is.True);
            Assert.That(Match("id('k1')", Find(tree, "b", 2)), Is.False);
        });
    }

    [Test]
    public void Match_RootPath_MatchesDocumentOnly()
    {
        // Arrange
        TinyTree tree = LoadSample();
        XdmNode docNode = new XdmNode(NodeHandle.ForNode(tree, 0));

        // Act + Assert
        Assert.Multiple(() =>
        {
            Assert.That(Match("/", docNode), Is.True);
            Assert.That(Match("/", Find(tree, "a")), Is.False);
        });
    }
}
