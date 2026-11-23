using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for <see cref="ModeCompiler"/> — P3-05 mode tables and template-rule compilation.
/// </summary>
[TestFixture]
internal sealed class ModeCompilerTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── Union split ──────────────────────────────────────────────────────────

    [Test]
    public void Compile_UnionPattern_SplitsIntoTwoRules()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='a | b' priority='7'/>"));

        // Assert — the default mode table has two rules both at priority 7.
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        Assert.Multiple(() =>
        {
            Assert.That(table.AllRules.Count, Is.EqualTo(2));
            Assert.That(table.AllRules.All(r => r.Priority == 7.0), Is.True);
        });
    }

    // ── Conflict detection ──────────────────────────────────────────────────

    [Test]
    public void Compile_EqualPrecedenceAndPriority_RecordsXtre0540Warning()
    {
        // Arrange — two templates matching the same element at the same priority.
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='x'/>" +
            "<xsl:template match='x'/>"));

        // Assert
        Assert.That(compiled.Warnings.Any(w => w.Code == XsltErrors.XTRE0540), Is.True);
    }

    [Test]
    public void Compile_DifferentPriority_NoWarning()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='x' priority='1'/>" +
            "<xsl:template match='x' priority='2'/>"));

        // Assert
        Assert.That(compiled.Warnings.Any(w => w.Code == XsltErrors.XTRE0540), Is.False);
    }

    // ── Indexing ─────────────────────────────────────────────────────────────

    [Test]
    public void Compile_SpecificName_GoesIntoElementBucketForThatNameOnly()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='foo'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        QName fooKey = new QName(string.Empty, string.Empty, "foo");
        Assert.Multiple(() =>
        {
            Assert.That(table.ElementsByName.ContainsKey(fooKey), Is.True);
            Assert.That(table.ElementsByName[fooKey].Count, Is.EqualTo(1));
            Assert.That(table.AnyNode, Is.Empty);
            Assert.That(table.ElementWildcard, Is.Empty);
        });
    }

    [Test]
    public void Compile_NodeKindAny_GoesIntoAnyNodeBucket()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='node()'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        Assert.Multiple(() =>
        {
            Assert.That(table.AnyNode.Count, Is.EqualTo(1));
            Assert.That(table.ElementsByName.Count, Is.EqualTo(0));
            Assert.That(table.ElementWildcard, Is.Empty);
        });
    }

    [Test]
    public void Compile_ElementStarWildcard_GoesIntoElementWildcardBucket()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='*'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        Assert.Multiple(() =>
        {
            Assert.That(table.ElementWildcard.Count, Is.EqualTo(1));
            Assert.That(table.AnyNode, Is.Empty);
        });
    }

    [Test]
    public void Compile_AttributeStarWildcard_GoesIntoAttributeWildcardBucket()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='@*'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        Assert.That(table.AttributeWildcard.Count, Is.EqualTo(1));
    }

    [Test]
    public void Compile_SpecificAttribute_GoesIntoAttributesByName()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='@id'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        QName idKey = new QName(string.Empty, string.Empty, "id");
        Assert.That(table.AttributesByName.ContainsKey(idKey), Is.True);
    }

    [Test]
    public void Compile_TextKind_GoesIntoTextBucket()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap("<xsl:template match='text()'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        Assert.That(table.ByKind.ContainsKey(XdmNodeKind.Text), Is.True);
    }

    // ── Priority sorting ─────────────────────────────────────────────────────

    [Test]
    public void Compile_HigherPriority_SortsFirstInBucket()
    {
        // Arrange — two rules on element 'x': priority 5 before priority 10 in source order.
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='x' priority='5'/>" +
            "<xsl:template match='x' priority='10'/>"));

        // Assert
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        List<TemplateRule> bucket = table.ElementsByName[new QName(string.Empty, string.Empty, "x")];
        Assert.That(bucket[0].Priority, Is.EqualTo(10.0));
    }

    [Test]
    public void Compile_SamePrecedenceSamePriority_LaterWinsOnOrdinal()
    {
        // Arrange — two rules at same precedence / priority; later should sort first.
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='x' priority='1'/>" +
            "<xsl:template match='x' priority='1'/>"));

        // Assert — the later rule (higher ordinal) sits first.
        ModeTable table = compiled.Modes[DeclarationCollectorDefault];
        List<TemplateRule> bucket = table.ElementsByName[new QName(string.Empty, string.Empty, "x")];
        Assert.That(bucket[0].DocumentOrderOrdinal, Is.GreaterThan(bucket[1].DocumentOrderOrdinal));
    }

    // ── Mode routing ─────────────────────────────────────────────────────────

    [Test]
    public void Compile_TemplateWithMode_RegistersOnlyUnderThatMode()
    {
        // Arrange
        CompiledStylesheet compiled = Compile(Wrap(
            "<xsl:template match='x' mode='summary'/>"));

        // Assert
        QName summary = new QName(string.Empty, string.Empty, "summary");
        Assert.Multiple(() =>
        {
            Assert.That(compiled.Modes.ContainsKey(summary), Is.True);
            Assert.That(compiled.Modes.ContainsKey(DeclarationCollectorDefault), Is.False);
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Replicate the internal sentinel so we don't rely on `default` QName.
    private static readonly QName DeclarationCollectorDefault =
        new QName(string.Empty, string.Empty, string.Empty);

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static CompiledStylesheet Compile(string xml)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("a.xsl", xml);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/a.xsl"), resolver);
        return XsltCompiler.Compile(asm);
    }

    private sealed class InMemoryResolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Add(string name, string xml) => _map[name] = xml;

        public Stream? Open(Uri uri)
        {
            const string prefix = "file:///suite/";
            string s = uri.OriginalString;
            if (!s.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            return _map.TryGetValue(s.Substring(prefix.Length), out string? xml)
                ? new MemoryStream(Encoding.UTF8.GetBytes(xml))
                : null;
        }
    }
}
