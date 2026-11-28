using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 11 of P4-23: a use-attribute-sets name that no attribute set bears (XSLT 2.0 §10.2; XTSE0710).</summary>
[TestFixture]
public sealed class StaticErrorAttributeSetReferenceTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='b'/>", TestName = "AttributeSetNamesAnUndeclaredSet")]
    [TestCase("<xsl:attribute-set name='a'/><xsl:template match='/'><xsl:copy use-attribute-sets='a b'/></xsl:template>", TestName = "CopyNamesAnUndeclaredSet")]
    [TestCase("<xsl:template match='/'><xsl:element name='e' use-attribute-sets='z'/></xsl:template>", TestName = "ElementNamesAnUndeclaredSet")]
    [TestCase("<xsl:template match='/'><xsl:element name='e' use-attribute-sets='#z'/></xsl:template>", TestName = "InvalidName")]
    [TestCase("<xsl:template match='/'><out xsl:use-attribute-sets='nope'/></xsl:template>", TestName = "LiteralResultElement")]
    [TestCase("<xsl:attribute-set name='p:a'/><xsl:template match='/'><xsl:copy use-attribute-sets='q:a'/></xsl:template>", TestName = "SameLocalNameInAnotherNamespace")]
    [TestCase("<xsl:attribute-set name='a'/><xsl:template match='/'><xsl:copy use-attribute-sets='A'/></xsl:template>", TestName = "NamesAreCaseSensitive")]
    public void Compile_ReferenceToAnUndeclaredAttributeSet_RaisesXtse0710(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0710));
    }

    [Test]
    public void Compile_UndeclaredPrefixInAnAttributeSetReference_RaisesXtse0280()
    {
        // Arrange
        string xsl = Wrap("<xsl:template match='/'><xsl:copy use-attribute-sets='nope:a'/></xsl:template>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0280));
    }

    [Test]
    public void Compile_UndeclaredReferenceInsideAnIncludedModule_IsReportedOnce()
    {
        // Arrange: the included module's declarations also appear in the module that includes it.
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Wrap("<xsl:include href='part.xsl'/>"),
            ["part.xsl"] = Wrap("<xsl:template match='/'><xsl:copy use-attribute-sets='missing'/></xsl:template>"),
        };
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => compiler.Compile(new Uri("file:///suite/main.xsl")))!;

        // Assert
        Assert.That(ex.Errors.Count(e => e.Code.Equals(XsltErrors.XTSE0710)), Is.EqualTo(1));
    }

    [TestCase("<xsl:attribute-set name='a'/><xsl:attribute-set name='b' use-attribute-sets='a'/><xsl:template match='/'><xsl:copy use-attribute-sets='a b'/></xsl:template>", TestName = "DeclaredSets")]
    [TestCase("<xsl:template match='/'><xsl:copy use-attribute-sets='later'/></xsl:template><xsl:attribute-set name='later'/>", TestName = "DeclaredAfterUse")]
    [TestCase("<xsl:attribute-set name='p:a'/><xsl:template match='/'><xsl:element name='e' use-attribute-sets='p:a'/></xsl:template>", TestName = "QualifiedName")]
    [TestCase("<xsl:attribute-set name='a'/><xsl:template match='/'><out xsl:use-attribute-sets='a'/></xsl:template>", TestName = "LiteralResultElementWithADeclaredSet")]
    [TestCase("<xsl:template match='/' version='3.0'><xsl:copy use-attribute-sets='missing'/></xsl:template>", TestName = "ForwardsCompatible")]
    [TestCase("<xsl:template match='/'><xsl:copy use-attribute-sets=''/></xsl:template>", TestName = "EmptyList")]
    public void Compile_ReferenceToADeclaredAttributeSet_Compiles(string body)
    {
        // Arrange
        string xsl = Wrap(body);

        // Act / Assert
        Assert.DoesNotThrow(() => Compile(xsl));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' xmlns:p='urn:p' xmlns:q='urn:q'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl) => new XsltCompiler().Compile(new StringReader(xsl));

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
