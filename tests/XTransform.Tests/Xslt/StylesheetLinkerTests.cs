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
/// Tests for <see cref="StylesheetLinker"/> (XSLT 2.0 §3.11 — include/import resolution).
/// </summary>
[TestFixture]
internal sealed class StylesheetLinkerTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    private static Uri UriFor(string name) => new Uri("file:///suite/" + name);

    // ── Precedence shape ─────────────────────────────────────────────────────

    [Test]
    public void Link_ImportChain_PrimaryHasHighestPrecedence()
    {
        // Arrange — a imports b; b imports c.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:import href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:import href='c.xsl'/>"),
            ["c.xsl"] = Wrap(""),
        };

        // Act
        StylesheetAssembly assembly = StylesheetLinker.Link(UriFor("a.xsl"), resolver);

        // Assert — modules ordered by load; primary is last; precedences strictly increasing.
        List<int> prec = assembly.Modules.Select(m => m.ImportPrecedence).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(assembly.Modules.Count, Is.EqualTo(3));
            Assert.That(assembly.Primary.IsPrimary, Is.True);
            Assert.That(assembly.Primary.ImportPrecedence,
                Is.GreaterThan(prec[0]).And.GreaterThan(prec[1]));
            Assert.That(prec[0], Is.LessThan(prec[1]));
        });
    }

    [Test]
    public void Link_IncludedModule_SharesParentPrecedence()
    {
        // Arrange — a includes b at same precedence.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
            ["b.xsl"] = Wrap(""),
        };

        // Act
        StylesheetAssembly assembly = StylesheetLinker.Link(UriFor("a.xsl"), resolver);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(assembly.Modules.Count, Is.EqualTo(2));
            Assert.That(assembly.Modules[0].ImportPrecedence,
                Is.EqualTo(assembly.Modules[1].ImportPrecedence));
            Assert.That(assembly.Primary.ImportPrecedence,
                Is.EqualTo(assembly.Modules[0].ImportPrecedence));
        });
    }

    // ── Cycle detection ──────────────────────────────────────────────────────

    [Test]
    public void Link_CircularImport_RaisesXtse0210()
    {
        // Arrange — a imports b, b imports a.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:import href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:import href='a.xsl'/>"),
        };

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => StylesheetLinker.Link(UriFor("a.xsl"), resolver))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0210));
    }

    [Test]
    public void Link_CircularInclude_RaisesXtse0210()
    {
        // Arrange
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:include href='a.xsl'/>"),
        };

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => StylesheetLinker.Link(UriFor("a.xsl"), resolver))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0210));
    }

    // ── Missing resources ────────────────────────────────────────────────────

    [Test]
    public void Link_ResolverReturnsNull_RaisesXtse0165()
    {
        // Arrange — b.xsl is not present in the resolver.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:import href='missing.xsl'/>"),
        };

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => StylesheetLinker.Link(UriFor("a.xsl"), resolver))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0165));
    }

    // ── Base URI resolution ──────────────────────────────────────────────────

    [Test]
    public void Link_RelativeHref_ResolvedAgainstIncludingModule()
    {
        // Arrange — a (at /suite/) imports deeper/b.xsl, and deeper/b.xsl imports c.xsl
        // which must resolve to /suite/deeper/c.xsl, not /suite/c.xsl.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:import href='deeper/b.xsl'/>"),
            ["deeper/b.xsl"] = Wrap("<xsl:import href='c.xsl'/>"),
            ["deeper/c.xsl"] = Wrap(""),
            // note: no /suite/c.xsl — the test fails if b's relative ref is resolved wrong
        };

        // Act
        StylesheetAssembly assembly = StylesheetLinker.Link(UriFor("a.xsl"), resolver);

        // Assert — three modules loaded, no XTSE0165.
        Assert.That(assembly.Modules.Count, Is.EqualTo(3));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    /// <summary>Resolver backed by a name → body dictionary. Keys are relative paths under /suite/.</summary>
    private sealed class InMemoryResolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

        public string this[string key]
        {
            get => _map[key];
            set => _map[key] = value;
        }

        public Stream? Open(Uri uri)
        {
            // Strip the common /suite/ prefix.
            const string prefix = "file:///suite/";
            string s = uri.OriginalString;
            if (!s.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            string key = s.Substring(prefix.Length);
            if (!_map.TryGetValue(key, out string? body))
            {
                return null;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(body));
        }
    }
}
