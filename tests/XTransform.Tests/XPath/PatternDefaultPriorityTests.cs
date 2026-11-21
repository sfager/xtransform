using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

// XSLT 2.0 §6.4 — default priority rules tests
namespace XTransform.Tests.XPath;

/// <summary>
/// Verifies the default priority computed by each pattern AST node against the
/// XSLT 2.0 §6.4 table.
/// </summary>
[TestFixture]
internal sealed class PatternDefaultPriorityTests
{
    private static readonly IStaticNamespaceResolver Ns = new DictionaryNamespaceResolver(
        new Dictionary<string, string>
        {
            { "ns", "http://example.com" },
        });

    private static double Priority(string pattern)
    {
        PatternAst ast = XPathParser.ParsePattern(pattern, SourceSpan.None, Ns);
        return ast.DefaultPriority;
    }

    // ── XSLT 2.0 §6.4 table: single-step, no anchor, no predicate ─────────────

    [TestCase("foo",                    0.0,   TestName = "QName_foo")]
    [TestCase("ns:foo",                 0.0,   TestName = "PrefixedQName_ns_foo")]
    [TestCase("*",                      -0.5,  TestName = "Wildcard_star")]
    [TestCase("node()",                 -0.5,  TestName = "KindTest_node")]
    [TestCase("element()",              -0.5,  TestName = "KindTest_element_empty")]
    [TestCase("element(*)",             -0.5,  TestName = "KindTest_element_wildcard")]
    [TestCase("element(foo)",           0.0,   TestName = "KindTest_element_name")]
    [TestCase("attribute()",            -0.5,  TestName = "KindTest_attribute_empty")]
    [TestCase("attribute(foo)",         0.0,   TestName = "KindTest_attribute_name")]
    [TestCase("attribute(*)",           -0.5,  TestName = "KindTest_attribute_wildcard")]
    [TestCase("text()",                 -0.5,  TestName = "KindTest_text")]
    [TestCase("comment()",              -0.5,  TestName = "KindTest_comment")]
    [TestCase("processing-instruction()",     -0.5,  TestName = "KindTest_pi_empty")]
    [TestCase("processing-instruction(\"target\")", 0.0, TestName = "KindTest_pi_target")]
    [TestCase("document-node()",        -0.5,  TestName = "KindTest_document_empty")]
    [TestCase("document-node(element(x))", 0.0, TestName = "KindTest_document_element_name")]
    [TestCase("ns:*",                   -0.25, TestName = "PrefixWildcard_ns")]
    [TestCase("*:local",               -0.25, TestName = "LocalWildcard")]
    [TestCase("@foo",                   0.0,   TestName = "Attribute_QName")]
    [TestCase("@*",                     -0.5,  TestName = "Attribute_Wildcard")]

    // ── Paths, anchors, predicates → 0.5 ──────────────────────────────────────
    [TestCase("foo/bar",                0.5,   TestName = "MultiStep_slash")]
    [TestCase("foo//bar",               0.5,   TestName = "MultiStep_doubleslash")]
    [TestCase("/foo",                   0.5,   TestName = "RootedPath")]
    [TestCase("//foo",                  0.5,   TestName = "RootDescendantPath")]
    [TestCase("foo[1]",                 0.5,   TestName = "StepWithPredicate_int")]
    [TestCase("foo[@id]",               0.5,   TestName = "StepWithPredicate_attr")]
    [TestCase("*[@id]",                 0.5,   TestName = "WildcardWithPredicate")]

    // ── id / key → 0.5 ────────────────────────────────────────────────────────
    [TestCase("id(\"x\")",              0.5,   TestName = "IdPattern_literal")]
    [TestCase("key(\"name\", \"val\")", 0.5,   TestName = "KeyPattern_literals")]
    [TestCase("id(\"x\")/foo",          0.5,   TestName = "IdKeyWithPath")]
    public void DefaultPriority_MatchesSpec(string pattern, double expected)
    {
        // Arrange / Act
        double actual = Priority(pattern);

        // Assert
        Assert.That(actual, Is.EqualTo(expected).Within(0.0001), $"Pattern: '{pattern}'");
    }

    // ── Union: each alternative has its own priority ───────────────────────────

    [Test]
    public void UnionPattern_AlternativesHaveIndependentPriorities()
    {
        // Arrange / Act
        PatternAst ast = XPathParser.ParsePattern("foo | bar | *", SourceSpan.None, Ns);

        // Assert
        Assert.That(ast, Is.InstanceOf<UnionPatternAst>());
        UnionPatternAst union = (UnionPatternAst)ast;
        Assert.That(union.Alternatives.Length, Is.EqualTo(3));
        Assert.That(union.Alternatives[0].DefaultPriority, Is.EqualTo(0.0));  // "foo"
        Assert.That(union.Alternatives[1].DefaultPriority, Is.EqualTo(0.0));  // "bar"
        Assert.That(union.Alternatives[2].DefaultPriority, Is.EqualTo(-0.5)); // "*"
        // Max priority of the union
        Assert.That(union.DefaultPriority, Is.EqualTo(0.0));
    }
}
