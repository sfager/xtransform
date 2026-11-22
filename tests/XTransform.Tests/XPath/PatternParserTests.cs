using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

// XSLT 2.0 §5.5.2 — pattern grammar parsing tests
namespace XTransform.Tests.XPath;

/// <summary>
/// Round-trip tests: each basic pattern shape parses to the expected AST node type,
/// axis, and node test.
/// </summary>
[TestFixture]
internal sealed class PatternParserTests
{
    private static readonly IStaticNamespaceResolver Ns = DictionaryNamespaceResolver.Default;
    private static readonly IStaticNamespaceResolver NsWithPrefix = new DictionaryNamespaceResolver(
        new Dictionary<string, string> { { "ns", "http://example.com" } });

    private static PatternAst Parse(string pattern, IStaticNamespaceResolver? ns = null)
    {
        return XPathParser.ParsePattern(pattern, SourceSpan.None, ns ?? Ns);
    }

    // ── single child step ─────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_SimpleName_SingleChildStep()
    {
        // Arrange / Act
        PatternAst ast = Parse("foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Anchor, Is.EqualTo(PatternAnchor.Relative));
        Assert.That(path.Steps.Length, Is.EqualTo(1));
        StepPatternAst step = path.Steps[0];
        Assert.That(step.Axis, Is.EqualTo(Axis.Child));
        Assert.That(step.Test, Is.InstanceOf<NameTestAst>());
        NameTestAst nt = (NameTestAst)step.Test;
        Assert.That(nt.Kind, Is.EqualTo(NameTestKind.QualifiedName));
        Assert.That(nt.Name!.Value.LocalName, Is.EqualTo("foo"));
    }

    [Test]
    public void ParsePattern_AtFoo_SingleAttributeStep()
    {
        // Arrange / Act
        PatternAst ast = Parse("@foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        StepPatternAst step = path.Steps[0];
        Assert.That(step.Axis, Is.EqualTo(Axis.Attribute));
        Assert.That(step.Test, Is.InstanceOf<NameTestAst>());
        NameTestAst nt = (NameTestAst)step.Test;
        Assert.That(nt.Name!.Value.LocalName, Is.EqualTo("foo"));
    }

    [Test]
    public void ParsePattern_Star_WildcardChildStep()
    {
        // Arrange / Act
        PatternAst ast = Parse("*");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        StepPatternAst step = path.Steps[0];
        Assert.That(step.Axis, Is.EqualTo(Axis.Child));
        Assert.That(step.Test, Is.InstanceOf<NameTestAst>());
        NameTestAst nt = (NameTestAst)step.Test;
        Assert.That(nt.Kind, Is.EqualTo(NameTestKind.AnyName));
    }

    // ── two-step paths ────────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_FooSlashBar_TwoSteps()
    {
        // Arrange / Act
        PatternAst ast = Parse("foo/bar");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps.Length, Is.EqualTo(2));
        NameTestAst nt1 = (NameTestAst)path.Steps[0].Test;
        Assert.That(nt1.Name!.Value.LocalName, Is.EqualTo("foo"));
        Assert.That(path.Steps[1].Combinator, Is.EqualTo(StepCombinator.Slash));
        NameTestAst nt2 = (NameTestAst)path.Steps[1].Test;
        Assert.That(nt2.Name!.Value.LocalName, Is.EqualTo("bar"));
    }

    [Test]
    public void ParsePattern_FooDoubleSlashBar_TwoStepsDoubleSlash()
    {
        // Arrange / Act
        PatternAst ast = Parse("foo//bar");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps.Length, Is.EqualTo(2));
        Assert.That(path.Steps[1].Combinator, Is.EqualTo(StepCombinator.DoubleSlash));
    }

    // ── anchored patterns ─────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_SlashFoo_RootedPath()
    {
        // Arrange / Act
        PatternAst ast = Parse("/foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Anchor, Is.EqualTo(PatternAnchor.Root));
        Assert.That(path.Steps.Length, Is.EqualTo(1));
    }

    [Test]
    public void ParsePattern_DoubleSlashFoo_RootDescendant()
    {
        // Arrange / Act
        PatternAst ast = Parse("//foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Anchor, Is.EqualTo(PatternAnchor.RootDescendantOrSelf));
        Assert.That(path.Steps.Length, Is.EqualTo(1));
    }

    // ── union patterns ────────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_FooBarUnion_UnionWithTwoAlternatives()
    {
        // Arrange / Act
        PatternAst ast = Parse("foo | bar");

        // Assert
        Assert.That(ast, Is.InstanceOf<UnionPatternAst>());
        UnionPatternAst union = (UnionPatternAst)ast;
        Assert.That(union.Alternatives.Length, Is.EqualTo(2));
    }

    // ── id() / key() patterns ─────────────────────────────────────────────────

    [Test]
    public void ParsePattern_IdLiteral_IdPatternAst()
    {
        // Arrange / Act
        PatternAst ast = Parse("id(\"x\")");

        // Assert
        Assert.That(ast, Is.InstanceOf<IdPatternAst>());
        IdPatternAst id = (IdPatternAst)ast;
        Assert.That(id.IdLiteral, Is.EqualTo("x"));
        Assert.That(id.VariableRef, Is.Null);
    }

    [Test]
    public void ParsePattern_IdVariable_IdPatternAst()
    {
        // Arrange / Act
        PatternAst ast = Parse("id($v)");

        // Assert
        Assert.That(ast, Is.InstanceOf<IdPatternAst>());
        IdPatternAst id = (IdPatternAst)ast;
        Assert.That(id.VariableRef!.Value.LocalName, Is.EqualTo("v"));
        Assert.That(id.IdLiteral, Is.Null);
    }

    [Test]
    public void ParsePattern_KeyLiteralLiteral_KeyPatternAst()
    {
        // Arrange / Act
        PatternAst ast = Parse("key(\"mykey\", \"myval\")");

        // Assert
        Assert.That(ast, Is.InstanceOf<KeyPatternAst>());
        KeyPatternAst key = (KeyPatternAst)ast;
        Assert.That(key.KeyName.LocalName, Is.EqualTo("mykey"));
        Assert.That(key.ValueLiteral, Is.EqualTo("myval"));
        Assert.That(key.VariableRef, Is.Null);
    }

    [Test]
    public void ParsePattern_KeyLiteralVariable_KeyPatternAst()
    {
        // Arrange / Act
        PatternAst ast = Parse("key(\"mykey\", $v)");

        // Assert
        Assert.That(ast, Is.InstanceOf<KeyPatternAst>());
        KeyPatternAst key = (KeyPatternAst)ast;
        Assert.That(key.VariableRef!.Value.LocalName, Is.EqualTo("v"));
        Assert.That(key.ValueLiteral, Is.Null);
    }

    [Test]
    public void ParsePattern_IdWithPath_IdKeyWithPathAst()
    {
        // Arrange / Act
        PatternAst ast = Parse("id(\"x\")/foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<IdKeyWithPathAst>());
        IdKeyWithPathAst idPath = (IdKeyWithPathAst)ast;
        Assert.That(idPath.Head, Is.InstanceOf<IdPatternAst>());
        Assert.That(idPath.TailSteps.Length, Is.EqualTo(1));
        NameTestAst nt = (NameTestAst)idPath.TailSteps[0].Test;
        Assert.That(nt.Name!.Value.LocalName, Is.EqualTo("foo"));
    }

    // ── predicates ────────────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_FooWithPredicate_StepHasPredicate()
    {
        // Arrange / Act
        PatternAst ast = Parse("foo[1]");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        StepPatternAst step = path.Steps[0];
        Assert.That(step.Predicates.Length, Is.EqualTo(1));
    }

    // ── kind tests ────────────────────────────────────────────────────────────

    [Test]
    public void ParsePattern_ProcessingInstructionWithTarget_KindTest()
    {
        // Arrange / Act
        PatternAst ast = Parse("processing-instruction(\"target\")");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Test, Is.InstanceOf<KindTestAst>());
        KindTestAst kt = (KindTestAst)path.Steps[0].Test;
        Assert.That(kt.Kind, Is.EqualTo(XdmNodeKind.ProcessingInstruction));
        Assert.That(kt.NameArg!.Value.LocalName, Is.EqualTo("target"));
    }

    [Test]
    public void ParsePattern_DocumentNodeWithElement_KindTest()
    {
        // Arrange / Act
        PatternAst ast = Parse("document-node(element(foo))");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Test, Is.InstanceOf<KindTestAst>());
        KindTestAst kt = (KindTestAst)path.Steps[0].Test;
        Assert.That(kt.Kind, Is.EqualTo(XdmNodeKind.Document));
        Assert.That(kt.ElementArg, Is.InstanceOf<KindTestAst>());
        KindTestAst inner = (KindTestAst)kt.ElementArg!;
        Assert.That(inner.NameArg!.Value.LocalName, Is.EqualTo("foo"));
    }

    // ── explicit axis specifiers ──────────────────────────────────────────────

    [Test]
    public void ParsePattern_ChildAxisExplicit_ChildAxisStep()
    {
        // Arrange / Act
        PatternAst ast = Parse("child::foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Axis, Is.EqualTo(Axis.Child));
    }

    [Test]
    public void ParsePattern_AttributeAxisExplicit_AttributeAxisStep()
    {
        // Arrange / Act
        PatternAst ast = Parse("attribute::foo");

        // Assert
        Assert.That(ast, Is.InstanceOf<PathPatternAst>());
        PathPatternAst path = (PathPatternAst)ast;
        Assert.That(path.Steps[0].Axis, Is.EqualTo(Axis.Attribute));
    }
}
