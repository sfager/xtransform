using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xdm;

[TestFixture]
internal sealed class ReverseAxisPositionTests
{
    private static TinyTree LoadXml(string xml)
    {
        NamePool pool = new NamePool();
        XmlReaderSettings settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
        return TinyTreeBuilder.Load(reader, pool);
    }

    // ── Preceding-sibling (reverse axis) ─────────────────────────────────────

    [Test]
    public void PrecedingSibling_PositionIncrementsFrom1()
    {
        // Arrange — 3 siblings: s1(2), s2(3), s3(4). From s3, preceding-sibling = [s2, s1].
        // XPath 2.0 §2.4: for reverse axes, Position starts at 1 and counts items as returned.
        TinyTree tree = LoadXml("<root><s1/><s2/><s3/></root>");
        // s3 is at index 4
        NodeHandle s3 = NodeHandle.ForNode(tree, 4);
        IXdmIterator it = tree.Iterate(s3, Axis.PrecedingSibling, null);

        // Act / Assert
        XdmItem? item1 = it.Next();
        Assert.That(item1, Is.Not.Null, "should have at least one preceding sibling");
        Assert.That(it.Position, Is.EqualTo(1), "first returned item has position 1");

        XdmNode node1 = (XdmNode)item1!;
        Assert.That(node1.NodeName!.Value.LocalName, Is.EqualTo("s2"), "s2 is closest preceding sibling");

        XdmItem? item2 = it.Next();
        Assert.That(item2, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(2), "second returned item has position 2");
        Assert.That(((XdmNode)item2!).NodeName!.Value.LocalName, Is.EqualTo("s1"));

        Assert.That(it.Next(), Is.Null, "no more preceding siblings");
    }

    // ── Ancestor-or-self (reverse axis) ──────────────────────────────────────

    [Test]
    public void AncestorOrSelf_PositionIncrementsFrom1()
    {
        // Arrange — doc(0) > root(1) > a(2) > b(3).
        // ancestor-or-self of b = [b(3), a(2), root(1), doc(0)] — closest first.
        TinyTree tree = LoadXml("<root><a><b/></a></root>");
        NodeHandle b = NodeHandle.ForNode(tree, 3);
        IXdmIterator it = tree.Iterate(b, Axis.AncestorOrSelf, null);

        // Act / Assert
        XdmItem? item1 = it.Next();
        Assert.That(item1, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(1));
        Assert.That(((XdmNode)item1!).NodeName!.Value.LocalName, Is.EqualTo("b"), "self first");

        XdmItem? item2 = it.Next();
        Assert.That(item2, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(2));
        Assert.That(((XdmNode)item2!).NodeName!.Value.LocalName, Is.EqualTo("a"));

        XdmItem? item3 = it.Next();
        Assert.That(item3, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(3));
        Assert.That(((XdmNode)item3!).NodeName!.Value.LocalName, Is.EqualTo("root"));

        XdmItem? item4 = it.Next();
        Assert.That(item4, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(4));
        Assert.That(((XdmNode)item4!).NodeKind, Is.EqualTo(XdmNodeKind.Document));

        Assert.That(it.Next(), Is.Null);
    }

    // ── Ancestor (reverse axis) ───────────────────────────────────────────────

    [Test]
    public void Ancestor_PositionIncrementsFrom1_ClosestFirst()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><a><b/></a></root>");
        NodeHandle b = NodeHandle.ForNode(tree, 3);
        IXdmIterator it = tree.Iterate(b, Axis.Ancestor, null);

        // Act / Assert
        XdmItem? item1 = it.Next();
        Assert.That(it.Position, Is.EqualTo(1));
        Assert.That(((XdmNode)item1!).NodeName!.Value.LocalName, Is.EqualTo("a"), "closest ancestor first");

        XdmItem? item2 = it.Next();
        Assert.That(it.Position, Is.EqualTo(2));
        Assert.That(((XdmNode)item2!).NodeName!.Value.LocalName, Is.EqualTo("root"));

        it.Next(); // document
        Assert.That(it.Position, Is.EqualTo(3));
        Assert.That(it.Next(), Is.Null);
    }

    // ── Preceding (reverse axis) ──────────────────────────────────────────────

    [Test]
    public void Preceding_PositionIncrementsFrom1_ReverseDocumentOrder()
    {
        // Arrange — doc(0) > root(1) > a(2), b(3), c(4).
        // preceding of c (index 4) = [b(3), a(2)] in reverse doc order (b first).
        TinyTree tree = LoadXml("<root><a/><b/><c/></root>");
        NodeHandle c = NodeHandle.ForNode(tree, 4);
        IXdmIterator it = tree.Iterate(c, Axis.Preceding, null);

        // Act / Assert
        XdmItem? item1 = it.Next();
        Assert.That(item1, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(1));
        Assert.That(((XdmNode)item1!).NodeName!.Value.LocalName, Is.EqualTo("b"),
            "b immediately precedes c so it comes first in reverse doc order");

        XdmItem? item2 = it.Next();
        Assert.That(item2, Is.Not.Null);
        Assert.That(it.Position, Is.EqualTo(2));
        Assert.That(((XdmNode)item2!).NodeName!.Value.LocalName, Is.EqualTo("a"));

        // root is ancestor — excluded from preceding axis
        Assert.That(it.Next(), Is.Null, "root is an ancestor and must be excluded");
    }

    // ── Clone restarts at position 0 ─────────────────────────────────────────

    [Test]
    public void PrecedingSibling_Clone_RestartsAtPosition0()
    {
        // Arrange
        TinyTree tree = LoadXml("<root><s1/><s2/><s3/></root>");
        NodeHandle s3 = NodeHandle.ForNode(tree, 4);
        IXdmIterator it = tree.Iterate(s3, Axis.PrecedingSibling, null);

        it.Next(); // consume one item

        // Act — clone
        IXdmIterator clone = it.Clone();

        // Assert — clone starts fresh
        Assert.That(clone.Position, Is.EqualTo(0));
        XdmItem? first = clone.Next();
        Assert.That(first, Is.Not.Null);
        Assert.That(clone.Position, Is.EqualTo(1));
        Assert.That(((XdmNode)first!).NodeName!.Value.LocalName, Is.EqualTo("s2"));
    }
}
