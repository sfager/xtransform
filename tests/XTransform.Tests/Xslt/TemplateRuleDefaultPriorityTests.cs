using System.Collections.Generic;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Golden test: 50+ patterns from the XSLT 2.0 §6.4 default-priority table paired with their
/// expected priority. This is the table-driven verification called for by P3-05's acceptance
/// criteria. Priorities here must match what <see cref="XTransform.Xslt.ModeCompiler"/> would
/// bind each rule to when no <c>priority=</c> attribute is declared.
/// </summary>
[TestFixture]
internal sealed class TemplateRuleDefaultPriorityTests
{
    private static readonly IStaticNamespaceResolver Ns = new DictionaryNamespaceResolver(
        new Dictionary<string, string>
        {
            { "ns", "http://example.com/ns" },
            { "p", "http://example.com/p" },
        });

    /// <summary>Returns the default priority for a single (non-union) pattern.</summary>
    private static double Priority(string pattern)
    {
        PatternAst ast = XPathParser.ParsePattern(pattern, SourceSpan.None, Ns);
        return ast.DefaultPriority;
    }

    public static readonly IEnumerable<TestCaseData> Cases = new[]
    {
        // ── Priority 0.5: rooted or multi-step or with predicates ────────────
        new TestCaseData("/", -0.5).SetName("root"),
        new TestCaseData("/foo", 0.5).SetName("rooted-single"),
        new TestCaseData("/foo/bar", 0.5).SetName("rooted-two-steps"),
        new TestCaseData("//foo", 0.5).SetName("descendant-foo"),
        new TestCaseData("foo/bar", 0.5).SetName("relative-two-steps"),
        new TestCaseData("foo//bar", 0.5).SetName("foo-descendant-bar"),
        new TestCaseData("foo[1]", 0.5).SetName("predicate-position"),
        new TestCaseData("foo[@id]", 0.5).SetName("predicate-attribute-exists"),
        new TestCaseData("foo[@id='x']", 0.5).SetName("predicate-attribute-equals"),
        new TestCaseData("foo[bar]", 0.5).SetName("predicate-child"),
        new TestCaseData("*[last()]", 0.5).SetName("wildcard-with-predicate"),
        new TestCaseData("@*[local-name() = 'id']", 0.5).SetName("attr-wildcard-with-predicate"),
        new TestCaseData("id('x')", 0.5).SetName("id-pattern"),
        new TestCaseData("id('x')/foo", 0.5).SetName("id-with-path"),

        // ── Priority 0.0: single-step QName / NCName / specific KindTest ─────
        new TestCaseData("foo", 0.0).SetName("ncname"),
        new TestCaseData("ns:foo", 0.0).SetName("qname-prefixed"),
        new TestCaseData("@id", 0.0).SetName("attribute-ncname"),
        new TestCaseData("@ns:id", 0.0).SetName("attribute-qname"),
        new TestCaseData("element(foo)", 0.0).SetName("element-name"),
        new TestCaseData("element(ns:foo)", 0.0).SetName("element-qname"),
        new TestCaseData("attribute(id)", 0.0).SetName("attribute-name"),
        new TestCaseData("attribute(ns:id)", 0.0).SetName("attribute-qname-kind"),
        new TestCaseData("processing-instruction('target')", 0.0).SetName("pi-with-target"),
        new TestCaseData("document-node(element(root))", 0.0).SetName("doc-with-named-element"),

        // ── Priority -0.25: prefix:* and *:local ─────────────────────────────
        new TestCaseData("ns:*", -0.25).SetName("prefix-wildcard"),
        new TestCaseData("@ns:*", -0.25).SetName("attribute-prefix-wildcard"),
        new TestCaseData("*:local", -0.25).SetName("local-wildcard"),
        new TestCaseData("@*:id", -0.25).SetName("attribute-local-wildcard"),

        // ── Priority -0.5: *, node() and un-named kind tests ─────────────────
        new TestCaseData("*", -0.5).SetName("any-element"),
        new TestCaseData("@*", -0.5).SetName("any-attribute"),
        new TestCaseData("node()", -0.5).SetName("any-node"),
        new TestCaseData("text()", -0.5).SetName("text"),
        new TestCaseData("comment()", -0.5).SetName("comment"),
        new TestCaseData("processing-instruction()", -0.5).SetName("pi-no-target"),
        new TestCaseData("document-node()", -0.5).SetName("doc-node"),
        new TestCaseData("element()", -0.5).SetName("element-anonymous"),
        new TestCaseData("element(*)", -0.5).SetName("element-wildcard-kind"),
        new TestCaseData("attribute()", -0.5).SetName("attribute-anonymous"),
        new TestCaseData("attribute(*)", -0.5).SetName("attribute-wildcard-kind"),
        new TestCaseData("document-node(element())", -0.5).SetName("doc-wildcard-element"),
        new TestCaseData("document-node(element(*))", -0.5).SetName("doc-explicit-wildcard"),

        // ── A few more rooted / descendant variations (priority 0.5) ─────────
        new TestCaseData("//*", 0.5).SetName("double-slash-any"),
        new TestCaseData("//@id", 0.5).SetName("double-slash-attribute"),
        new TestCaseData("/foo/bar/baz", 0.5).SetName("rooted-three-steps"),
        new TestCaseData("foo/bar/baz", 0.5).SetName("three-step-relative"),
        new TestCaseData("/foo[1]", 0.5).SetName("rooted-with-predicate"),
        new TestCaseData("//foo[@x]", 0.5).SetName("descendant-with-attribute"),
        new TestCaseData("foo[x][y]", 0.5).SetName("two-predicates"),
        new TestCaseData("ns:*[1]", 0.5).SetName("prefix-wildcard-with-predicate"),
        new TestCaseData("@ns:*[1]", 0.5).SetName("attr-prefix-wildcard-with-predicate"),
        new TestCaseData("node()[1]", 0.5).SetName("any-node-with-predicate"),

        // ── Key patterns (priority 0.5) ──────────────────────────────────────
        new TestCaseData("key('k', 'v')", 0.5).SetName("key-pattern"),
        new TestCaseData("key('k', 'v')/foo", 0.5).SetName("key-with-path"),
    };

    [TestCaseSource(nameof(Cases))]
    public void DefaultPriority_MatchesSpec(string pattern, double expected)
    {
        // Arrange + Act
        double actual = Priority(pattern);

        // Assert
        Assert.That(actual, Is.EqualTo(expected), $"pattern '{pattern}' expected {expected}");
    }
}
