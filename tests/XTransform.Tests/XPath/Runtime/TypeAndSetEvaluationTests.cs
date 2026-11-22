using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// P2-07 evaluation coverage for instance-of, treat, cast, castable, and set
/// operations (union / intersect / except). XPath 2.0 §3.3.3, §3.7, F&amp;O §15.3.
/// </summary>
[TestFixture]
internal sealed class TypeAndSetEvaluationTests
{
    private const string Fixture = @"<root><a id='1'/><b id='2'/><a id='3'/><c id='4'/></root>";

    private static CompiledExpression Compile(string expr) =>
        XPathCompiler.ParseAndCompile(
            expr, SourceSpan.None,
            DictionaryNamespaceResolver.Default,
            EmptyFunctionLibrary.Instance);

    private static TinyTree Load(string xml)
    {
        NamePool pool = new();
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
        return TinyTreeBuilder.Load(reader, pool);
    }

    private static DynamicContext RootCtx(CompiledExpression ce, TinyTree tree)
    {
        XdmNode doc = new(NodeHandle.ForNode(tree, 0));
        return DynamicContext.Root(new RunState())
            .WithFrame(ce.SlotCount)
            .WithContextItem(doc, 1, 1);
    }

    private static DynamicContext RootCtx(CompiledExpression ce)
        => DynamicContext.Root(new RunState()).WithFrame(ce.SlotCount);

    private static List<XdmItem> Drain(IXdmIterator iter)
    {
        List<XdmItem> result = new();
        while (true)
        {
            XdmItem? n = iter.Next();
            if (n is null) { break; }
            result.Add(n);
        }
        return result;
    }

    // ── instance of ─────────────────────────────────────────────────────────

    [Test]
    public void InstanceOf_IntegerSingleton_MatchesXsIntegerPlus()
    {
        CompiledExpression ce = Compile("5 instance of xs:integer+");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    [Test]
    public void InstanceOf_Integer_DoesNotMatchXsString()
    {
        CompiledExpression ce = Compile("5 instance of xs:string");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.False);
    }

    [Test]
    public void InstanceOf_Empty_MatchesOptional()
    {
        CompiledExpression ce = Compile("() instance of xs:integer?");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    [Test]
    public void InstanceOf_Empty_DoesNotMatchExactlyOne()
    {
        CompiledExpression ce = Compile("() instance of xs:integer");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.False);
    }

    [Test]
    public void InstanceOf_Many_MatchesStar()
    {
        CompiledExpression ce = Compile("(1, 2, 3) instance of xs:integer*");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    [Test]
    public void InstanceOf_Many_DoesNotMatchExactlyOne()
    {
        CompiledExpression ce = Compile("(1, 2, 3) instance of xs:integer");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.False);
    }

    [Test]
    public void InstanceOf_Decimal_MatchesXsDecimal()
    {
        CompiledExpression ce = Compile("1.5 instance of xs:decimal");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    // ── cast as ─────────────────────────────────────────────────────────────

    [Test]
    public void Cast_StringToInteger_Succeeds()
    {
        CompiledExpression ce = Compile("'5' cast as xs:integer");
        XdmItem? item = ce.Root.EvaluateItem(RootCtx(ce));
        Assert.That(item, Is.Not.Null);
        Assert.That(((XdmAtomicValue)item!).AsInt64(), Is.EqualTo(5L));
    }

    [Test]
    public void Cast_InvalidStringToInteger_ThrowsForg0001()
    {
        CompiledExpression ce = Compile("'abc' cast as xs:integer");
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => ce.Root.EvaluateItem(RootCtx(ce)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0001));
    }

    [Test]
    public void Cast_EmptyWithoutQuestion_ThrowsXpty0004()
    {
        CompiledExpression ce = Compile("() cast as xs:integer");
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => ce.Root.EvaluateItem(RootCtx(ce)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void Cast_EmptyWithQuestion_ReturnsEmpty()
    {
        CompiledExpression ce = Compile("() cast as xs:integer?");
        Assert.That(Drain(ce.Root.Iterate(RootCtx(ce))), Is.Empty);
    }

    [Test]
    public void Cast_IntegerToString_ProducesCanonicalForm()
    {
        CompiledExpression ce = Compile("42 cast as xs:string");
        XdmItem? item = ce.Root.EvaluateItem(RootCtx(ce));
        Assert.That(((XdmAtomicValue)item!).GetStringValue(), Is.EqualTo("42"));
    }

    // ── castable as ─────────────────────────────────────────────────────────

    [Test]
    public void Castable_ValidString_IsTrue()
    {
        CompiledExpression ce = Compile("'5' castable as xs:integer");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    [Test]
    public void Castable_InvalidString_IsFalse_NoException()
    {
        CompiledExpression ce = Compile("'abc' castable as xs:integer");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.False);
    }

    [Test]
    public void Castable_EmptyWithoutQuestion_IsFalse()
    {
        CompiledExpression ce = Compile("() castable as xs:integer");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.False);
    }

    [Test]
    public void Castable_EmptyWithQuestion_IsTrue()
    {
        CompiledExpression ce = Compile("() castable as xs:integer?");
        Assert.That(ce.Root.EffectiveBooleanValue(RootCtx(ce)), Is.True);
    }

    // ── treat as ────────────────────────────────────────────────────────────

    [Test]
    public void Treat_Match_ReturnsOperandUnchanged()
    {
        CompiledExpression ce = Compile("5 treat as xs:integer");
        XdmItem? item = ce.Root.EvaluateItem(RootCtx(ce));
        Assert.That(((XdmAtomicValue)item!).AsInt64(), Is.EqualTo(5L));
    }

    [Test]
    public void Treat_Mismatch_ThrowsXpty0004()
    {
        CompiledExpression ce = Compile("5 treat as xs:string");
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => ce.Root.EvaluateItem(RootCtx(ce)))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void Treat_IntegerAsDecimal_PassesThrough()
    {
        // Integer is a subtype of Decimal (via CastTable.IsSubtypeOf)
        CompiledExpression ce = Compile("5 treat as xs:decimal");
        XdmItem? item = ce.Root.EvaluateItem(RootCtx(ce));
        Assert.That(((XdmAtomicValue)item!).AsInt64(), Is.EqualTo(5L));  // still xs:integer
    }

    // ── set operators (nodes) ───────────────────────────────────────────────

    [Test]
    public void Union_TwoNodeSets_YieldsDocOrderDeduped()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//a | //c");
        List<XdmItem> items = Drain(ce.Root.Iterate(RootCtx(ce, tree)));
        // Two <a> + one <c> = 3 elements in doc order
        Assert.That(items, Has.Count.EqualTo(3));
        Assert.That(((XdmNode)items[0]).NodeName!.Value.LocalName, Is.EqualTo("a"));
        Assert.That(((XdmNode)items[1]).NodeName!.Value.LocalName, Is.EqualTo("a"));
        Assert.That(((XdmNode)items[2]).NodeName!.Value.LocalName, Is.EqualTo("c"));
    }

    [Test]
    public void Union_OverlappingSets_Deduplicates()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//a | //a");
        List<XdmItem> items = Drain(ce.Root.Iterate(RootCtx(ce, tree)));
        Assert.That(items, Has.Count.EqualTo(2));  // two <a> elements, not four
    }

    [Test]
    public void Intersect_OverlappingSets_YieldsCommon()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//*[@id='1'] intersect //a");
        List<XdmItem> items = Drain(ce.Root.Iterate(RootCtx(ce, tree)));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(((XdmNode)items[0]).NodeName!.Value.LocalName, Is.EqualTo("a"));
    }

    [Test]
    public void Intersect_Disjoint_YieldsEmpty()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//a intersect //c");
        Assert.That(Drain(ce.Root.Iterate(RootCtx(ce, tree))), Is.Empty);
    }

    [Test]
    public void Except_RemovesRightSide()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//*[@id] except //a");
        // All four have @id; remove the two <a>s → 2 remain: <b> and <c>
        List<XdmItem> items = Drain(ce.Root.Iterate(RootCtx(ce, tree)));
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(((XdmNode)items[0]).NodeName!.Value.LocalName, Is.EqualTo("b"));
        Assert.That(((XdmNode)items[1]).NodeName!.Value.LocalName, Is.EqualTo("c"));
    }

    [Test]
    public void Union_WithAtomicOperand_ThrowsXpty0004()
    {
        CompiledExpression ce = Compile("(1, 2, 3) union (4, 5, 6)");
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(
            () => Drain(ce.Root.Iterate(RootCtx(ce))))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.XPTY0004));
    }

    [Test]
    public void Union_EmptyWithNonEmpty_ReturnsNonEmpty()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("() | //a");
        List<XdmItem> items = Drain(ce.Root.Iterate(RootCtx(ce, tree)));
        Assert.That(items, Has.Count.EqualTo(2));
    }

    [Test]
    public void Except_EmptyRight_ReturnsLeft()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("//a except ()");
        Assert.That(Drain(ce.Root.Iterate(RootCtx(ce, tree))), Has.Count.EqualTo(2));
    }

    [Test]
    public void Intersect_EmptyLeft_ReturnsEmpty()
    {
        TinyTree tree = Load(Fixture);
        CompiledExpression ce = Compile("() intersect //a");
        Assert.That(Drain(ce.Root.Iterate(RootCtx(ce, tree))), Is.Empty);
    }
}
