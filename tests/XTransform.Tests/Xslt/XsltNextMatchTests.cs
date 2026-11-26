using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §6.6, §6.7 — xsl:next-match, xsl:apply-imports and the template rules they walk.</summary>
[TestFixture]
internal sealed class XsltNextMatchTests
{
    private const string Doc = "<doc><a/><b x='1'/></doc>";

    // ── xsl:next-match ───────────────────────────────────────────────────────

    [Test]
    public void NextMatch_RunsTheNextBestRule_InPriorityOrder()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='a' priority='3'>[3<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='*' priority='2'>[2<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='node()' priority='1'>[1]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[3[2[1]]]"));
    }

    [Test]
    public void NextMatch_WhenNoRuleIsLeft_UsesTheBuiltInRule()
    {
        // The built-in rule for an element processes its children.
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc'/></xsl:template>" +
            "<xsl:template match='doc'>[doc<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='a'>(a)</xsl:template>" +
            "<xsl:template match='b'>(b)</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[doc(a)(b)]"));
    }

    [Test]
    public void NextMatch_PassesWithParam_AndNotTheCallersOwnParameters()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'><xsl:with-param name='p' select=\"'outer'\"/></xsl:apply-templates></xsl:template>" +
            "<xsl:template match='a' priority='2'><xsl:param name='p'/>[<xsl:value-of select='$p'/><xsl:next-match><xsl:with-param name='p' select=\"'next'\"/></xsl:next-match>]</xsl:template>" +
            "<xsl:template match='*' priority='1'><xsl:param name='p' select=\"'default'\"/>(<xsl:value-of select='$p'/>)</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[outer(next)]"));
    }

    [Test]
    public void NextMatch_KeepsTheModeOfTheCurrentRule()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a' mode='m'/></xsl:template>" +
            "<xsl:template match='a' mode='m' priority='2'>[m2<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='*' mode='m' priority='1'>[m1]</xsl:template>" +
            "<xsl:template match='*' priority='1'>[default]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[m2[m1]]"));
    }

    [Test]
    public void NextMatch_AlternativesWithDifferentDefaultPriorities_AreSeparateRules()
    {
        // element(a) has priority 0 and *:a has -0.25: the template runs twice before the lower rule.
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='*:a|element(a)'>[t<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='node()'>[n]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[t[t[n]]]"));
    }

    [Test]
    public void NextMatch_AlternativesWithEqualDefaultPriorities_AreStillSeparateRules()
    {
        // Both alternatives have the default priority 0.5; without a priority attribute they are separate rules.
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/b'/></xsl:template>" +
            "<xsl:template match='b[@x = 1] | b[string(@x) = \"1\"]'>[t<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='node()'>[n]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[t[t[n]]]"));
    }

    [Test]
    public void NextMatch_AlternativesWithAnExplicitPriority_AreOneRule()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='*:a|element(a)' priority='1'>[t<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='node()'>[n]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[t[n]]"));
    }

    [Test]
    public void NextMatch_WithNoCurrentRule_RaisesXtde0560()
    {
        // The run starts from a named template, so there is no template rule at all.
        const string body = "<xsl:template name='main'><xsl:next-match/></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => RunInitial(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0560));
    }

    [Test]
    public void NextMatch_InsideForEach_RaisesXtde0560()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='a'><xsl:for-each select='.'><xsl:next-match/></xsl:for-each></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0560));
    }

    [Test]
    public void NextMatch_InsideAStylesheetFunction_RaisesXtde0560()
    {
        const string body =
            "<xsl:function name='f:g' xmlns:f='urn:f'><xsl:next-match/></xsl:function>" +
            "<xsl:template match='/' xmlns:f='urn:f'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='a' xmlns:f='urn:f'><xsl:value-of select='f:g()'/></xsl:template>";

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Run(body))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0560));
    }

    [Test]
    public void NextMatch_WithAnythingButWithParamInside_RaisesXtse0010()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => Run("<xsl:template match='/'><xsl:next-match><x/></xsl:next-match></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0010));
    }

    // ── Patterns ─────────────────────────────────────────────────────────────

    [Test]
    public void NodePattern_DoesNotMatchTheDocumentNode()
    {
        // node() is child::node(): the document node has no parent, so only the built-in rule applies to it.
        const string body =
            "<xsl:template match='node()'>[<xsl:value-of select='name()'/><xsl:apply-templates/>]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[doc[a][b]]"));
    }

    [Test]
    public void AttributeKindTest_MatchesAttributes()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/b/@*'/></xsl:template>" +
            "<xsl:template match='attribute()'>[<xsl:value-of select='name()'/>]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[x]"));
    }

    [Test]
    public void TypedKindTests_HaveHigherPriority_AndOnlyMatchUntypedNodes()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template>" +
            "<xsl:template match='a'>[name]</xsl:template>" +
            "<xsl:template match='element(a, xs:untyped)'>[typed<xsl:next-match/>]</xsl:template>" +
            "<xsl:template match='element(a, xs:integer)'>[never]</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[typed[name]]"));
    }

    // ── xsl:apply-imports and import precedence ──────────────────────────────

    [Test]
    public void ApplyImports_RunsTheRuleOfTheImportedModule()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:import href='lib.xsl'/><xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template><xsl:template match='a'>[main<xsl:apply-imports/>]</xsl:template>"),
            ["lib.xsl"] = Module("<xsl:template match='a'>[lib]</xsl:template>"),
        };

        Assert.That(RunFiles(files), Is.EqualTo("[main[lib]]"));
    }

    [Test]
    public void ApplyImports_ReachesDeeperThroughAChainOfImports()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:import href='mid.xsl'/><xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template><xsl:template match='a'>[main<xsl:apply-imports/>]</xsl:template>"),
            ["mid.xsl"] = Module("<xsl:import href='low.xsl'/><xsl:template match='a'>[mid<xsl:apply-imports/>]</xsl:template>"),
            ["low.xsl"] = Module("<xsl:template match='a'>[low]</xsl:template>"),
        };

        Assert.That(RunFiles(files), Is.EqualTo("[main[mid[low]]]"));
    }

    [Test]
    public void ApplyImports_IgnoresRulesOfHigherPrecedenceAndOfModulesThatAreNotImported()
    {
        // main imports first.xsl and second.xsl (second has the higher precedence). A rule in first.xsl that calls
        // apply-imports must not reach second.xsl, which it does not import.
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:import href='first.xsl'/><xsl:import href='second.xsl'/><xsl:template match='/'><xsl:apply-templates select='/doc/b'/></xsl:template>"),
            ["first.xsl"] = Module("<xsl:template match='b'>[first<xsl:apply-imports/>]</xsl:template>"),
            ["second.xsl"] = Module("<xsl:template match='b' priority='5'>[second]</xsl:template>"),
        };

        // second wins (higher precedence); it has no apply-imports, so first is never reached.
        Assert.That(RunFiles(files), Is.EqualTo("[second]"));
    }

    [Test]
    public void ApplyImports_FromTheLowerModule_DoesNotSeeTheSiblingImportedAfterIt()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:import href='first.xsl'/><xsl:import href='second.xsl'/><xsl:template match='/'><xsl:apply-templates select='/doc/b'/></xsl:template><xsl:template match='b'>[main<xsl:apply-imports/>]</xsl:template>"),
            ["first.xsl"] = Module("<xsl:template match='b'>[first]</xsl:template>"),
            ["second.xsl"] = Module("<xsl:template match='b'>[second<xsl:apply-imports/>]</xsl:template>"),
        };

        // main -> second (the nearest imported) -> second's imports (none) -> built-in: first is a sibling, not imported by second.
        Assert.That(RunFiles(files), Is.EqualTo("[main[second]]"));
    }

    [Test]
    public void ApplyImports_WhenNothingIsImported_UsesTheBuiltInRule()
    {
        const string body =
            "<xsl:template match='/'><xsl:apply-templates select='/doc'/></xsl:template>" +
            "<xsl:template match='doc'>[doc<xsl:apply-imports/>]</xsl:template>" +
            "<xsl:template match='a'>(a)</xsl:template>";

        Assert.That(Run(body), Is.EqualTo("[doc(a)]"));
    }

    [Test]
    public void ApplyImports_WithNoCurrentRule_RaisesXtde0560()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => RunInitial("<xsl:template name='main'><xsl:apply-imports/></xsl:template>"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0560));
    }

    // ── xsl:include is a textual insert ──────────────────────────────────────

    [Test]
    public void Include_RulesTakeThePositionOfTheIncludeInTheDeclarationOrder()
    {
        // Equal precedence and priority: the rule declared last wins. The include sits between the two rules of main.
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template><xsl:template match='a'>main-first</xsl:template><xsl:include href='inc.xsl'/><xsl:template match='a'>main-last</xsl:template>"),
            ["inc.xsl"] = Module("<xsl:template match='a'>included</xsl:template>"),
        };

        Assert.That(RunFiles(files), Is.EqualTo("main-last"));
    }

    [Test]
    public void Include_AnIncludeInsideAnIncludedModule_IsExpandedToo()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template><xsl:include href='one.xsl'/>"),
            ["one.xsl"] = Module("<xsl:include href='two.xsl'/>"),
            ["two.xsl"] = Module("<xsl:template match='a'>deep</xsl:template>"),
        };

        Assert.That(RunFiles(files), Is.EqualTo("deep"));
    }

    [Test]
    public void Include_ASimplifiedStylesheetModule_ContributesItsImplicitTemplate()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:include href='simple.xsl'/>"),
            ["simple.xsl"] = "<out xsl:version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>from simplified</out>",
        };

        Assert.That(RunFiles(files), Does.Contain("from simplified"));
    }

    [Test]
    public void Include_AModuleThatIsNotAStylesheetAndHasNoXslVersion_RaisesXtse0150()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:include href='data.xml'/>"),
            ["data.xml"] = "<data><x/></data>",
        };

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => RunFiles(files))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0150));
    }

    [Test]
    public void Include_TheSameModuleIncludedTwice_IsNotACycle()
    {
        // Each include is a textual insert; only an include that leads back to itself is XTSE0210.
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:template match='/'><xsl:apply-templates select='/doc/a'/></xsl:template><xsl:include href='inc.xsl'/><xsl:include href='inc.xsl'/>"),
            ["inc.xsl"] = Module("<xsl:template match='a'>once</xsl:template>"),
        };

        Assert.That(RunFiles(files), Is.EqualTo("once"));
    }

    [Test]
    public void Include_AModuleThatIncludesItself_RaisesXtse0210()
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Module("<xsl:include href='main.xsl'/>"),
        };

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => RunFiles(files))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTSE0210));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Module(string body) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
        "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";

    private static string Run(string body) => RunFiles(new Dictionary<string, string> { ["main.xsl"] = Module(body) });

    // Starts from the named template "main" instead of applying templates to the document.
    private static string RunInitial(string body)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(Module(body)));
        XsltTransformer transformer = exe.CreateTransformer();
        transformer.InitialTemplate = new QName(string.Empty, "main");
        using MemoryStream output = new MemoryStream();
        transformer.Transform((Stream?)null, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string RunFiles(Dictionary<string, string> files)
    {
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });
        XsltExecutable exe = compiler.Compile(new Uri("file:///suite/main.xsl"));
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Doc));
        using MemoryStream output = new MemoryStream();
        exe.CreateTransformer().Transform(input, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private sealed class Resolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _files;

        public Resolver(Dictionary<string, string> files)
        {
            _files = files;
        }

        public Stream? Open(Uri uri)
        {
            const string prefix = "file:///suite/";
            string s = uri.OriginalString;
            if (!s.StartsWith(prefix, StringComparison.Ordinal) || !_files.TryGetValue(s.Substring(prefix.Length), out string? body))
            {
                return null;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(body));
        }
    }
}
