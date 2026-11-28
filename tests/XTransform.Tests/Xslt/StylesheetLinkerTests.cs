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
    public void Link_IncludedModule_IsSplicedIntoTheIncluderAtTheIncludePosition()
    {
        // Arrange — a includes b between two templates; b has a template of its own.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:template match='x'/><xsl:include href='b.xsl'/><xsl:template match='z'/>"),
            ["b.xsl"] = Wrap("<xsl:template match='y'/>"),
        };

        // Act
        StylesheetAssembly assembly = StylesheetLinker.Link(UriFor("a.xsl"), resolver);

        // Assert — an include is a textual insert: one module, with b's template between a's two.
        System.Collections.Generic.List<string> matches = new System.Collections.Generic.List<string>();
        foreach (StylesheetNode child in assembly.Primary.Module.Root.Children)
        {
            if (child is StylesheetElement e && e.IsXsltElement && e.Name.LocalName == "template")
            {
                foreach (StylesheetAttribute attr in e.Attributes)
                {
                    if (attr.Name.LocalName == "match")
                    {
                        matches.Add(attr.Value);
                    }
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(assembly.Modules.Count, Is.EqualTo(1));
            Assert.That(string.Join(",", matches), Is.EqualTo("x,y,z"));
        });
    }

    [Test]
    public void Link_IncludeInsideAnIncludedModule_IsAlsoSpliced()
    {
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:include href='c.xsl'/><xsl:template match='b'/>"),
            ["c.xsl"] = Wrap("<xsl:template match='c'/>"),
        };

        StylesheetAssembly assembly = StylesheetLinker.Link(UriFor("a.xsl"), resolver);

        int templates = 0;
        foreach (StylesheetNode child in assembly.Primary.Module.Root.Children)
        {
            if (child is StylesheetElement e && e.IsXsltElement && e.Name.LocalName == "template")
            {
                templates++;
            }
        }

        Assert.That(templates, Is.EqualTo(2));
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
    public void Link_CircularInclude_RaisesXtse0180()
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
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0180));
    }

    [Test]
    public void Link_ModuleThatIncludesItself_RaisesXtse0180()
    {
        // Arrange
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='a.xsl'/>"),
        };

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => StylesheetLinker.Link(UriFor("a.xsl"), resolver))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0180));
    }

    [Test]
    public void Link_IndirectCircularInclude_RaisesXtse0180()
    {
        // Arrange — a includes b, b includes c, c includes b.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:include href='c.xsl'/>"),
            ["c.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
        };

        // Act + Assert
        XsltStaticException ex = Assert.Throws<XsltStaticException>(
            () => StylesheetLinker.Link(UriFor("a.xsl"), resolver))!;
        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0180));
    }

    [Test]
    public void Link_CycleClosedByAnImport_RaisesXtse0210()
    {
        // Arrange — a includes b, and b imports a: the request that closes the cycle is an import.
        InMemoryResolver resolver = new InMemoryResolver
        {
            ["a.xsl"] = Wrap("<xsl:include href='b.xsl'/>"),
            ["b.xsl"] = Wrap("<xsl:import href='a.xsl'/>"),
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
