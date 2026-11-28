using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>P4-24 batch 3: character maps and the output method (XSLT 2.0 §20, §20.1; XTSE1570, XTSE1580, XTSE1590, XTSE1600).</summary>
[TestFixture]
public sealed class StaticErrorCharacterMapTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";
    private const string OneMap = "<xsl:output-character character='a' string='aa'/>";

    [TestCase("<xsl:output method='your::xml'/>", "XTSE1570", TestName = "MethodWithTwoColons")]
    [TestCase("<xsl:output method='xmlish'/>", "XTSE1570", TestName = "MethodThatIsNotKnownAndHasNoPrefix")]
    [TestCase("<xsl:output method='1:x'/>", "XTSE1570", TestName = "MethodThatIsNotAQName")]
    [TestCase("<xsl:character-map name='one'>" + OneMap + "</xsl:character-map><xsl:character-map name='one'>" + OneMap + "</xsl:character-map>", "XTSE1580", TestName = "TwoMapsWithOneName")]
    [TestCase("<xsl:character-map name='one' use-character-maps='two'>" + OneMap + "</xsl:character-map>", "XTSE1590", TestName = "MapUsesAnUndeclaredMap")]
    [TestCase("<xsl:output use-character-maps='nope'/>", "XTSE1590", TestName = "OutputUsesAnUndeclaredMap")]
    [TestCase("<xsl:character-map name='one' use-character-maps='one'>" + OneMap + "</xsl:character-map>", "XTSE1600", TestName = "MapUsesItself")]
    [TestCase("<xsl:character-map name='one' use-character-maps='two'>" + OneMap + "</xsl:character-map><xsl:character-map name='two' use-character-maps='three'>" + OneMap + "</xsl:character-map><xsl:character-map name='three' use-character-maps='one'>" + OneMap + "</xsl:character-map>", "XTSE1600", TestName = "ThreeMapCycle")]
    public void Compile_InvalidCharacterMapDeclaration_RaisesTheErrorCode(string body, string code)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain(code));
    }

    [Test]
    public void Compile_ThreeMapCycle_ReportsEveryMapOnTheCycleOnce()
    {
        string body =
            "<xsl:character-map name='one' use-character-maps='two'>" + OneMap + "</xsl:character-map>" +
            "<xsl:character-map name='two' use-character-maps='three'>" + OneMap + "</xsl:character-map>" +
            "<xsl:character-map name='three' use-character-maps='one'>" + OneMap + "</xsl:character-map>";

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body)))!;

        Assert.That(ex.Errors.Count(e => e.Code.Equals(XsltErrors.XTSE1600)), Is.EqualTo(3));
    }

    [TestCase("<xsl:output method='xml'/><xsl:output name='o' method='html'/><xsl:output name='p' method='text'/><xsl:output name='q' method='xhtml'/>", TestName = "KnownMethods")]
    [TestCase("<xsl:output method='my:custom' xmlns:my='urn:my'/>", TestName = "PrefixedMethod")]
    [TestCase("<xsl:character-map name='a'>" + OneMap + "</xsl:character-map><xsl:character-map name='b' use-character-maps='a'>" + OneMap + "</xsl:character-map><xsl:output use-character-maps='a b'/>", TestName = "MapsThatUseEachOther")]
    [TestCase("<xsl:character-map name='m:a' xmlns:m='urn:m'>" + OneMap + "</xsl:character-map><xsl:character-map name='m:b' xmlns:m='urn:m'>" + OneMap + "</xsl:character-map>", TestName = "NamesInANamespace")]
    [TestCase("<xsl:character-map name='p:a' xmlns:p='urn:p'>" + OneMap + "</xsl:character-map><xsl:character-map name='q:a' xmlns:q='urn:q'>" + OneMap + "</xsl:character-map>", TestName = "SameLocalNameInTwoNamespaces")]
    public void Compile_ValidCharacterMapDeclaration_Compiles(string body)
    {
        Assert.DoesNotThrow(() => Compile(Wrap(body)));
    }

    [Test]
    public void Compile_MapDeclaredInAnImportedModuleAndInTheImporter_IsNotADuplicate()
    {
        // Arrange: a map at a higher import precedence replaces one at a lower precedence (XSLT 2.0 §20.1).
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Wrap("<xsl:import href='lib.xsl'/>").Replace("<xsl:import href='lib.xsl'/>", "<xsl:import href='lib.xsl'/><xsl:character-map name='one'>" + OneMap + "</xsl:character-map>"),
            ["lib.xsl"] = Wrap("<xsl:character-map name='one'>" + OneMap + "</xsl:character-map>"),
        };
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });

        // Act / Assert
        Assert.DoesNotThrow(() => compiler.Compile(new Uri("file:///suite/main.xsl")));
    }

    [Test]
    public void Compile_SameMapInTwoIncludedModules_RaisesXtse1580()
    {
        // Arrange: an included module has the includer's import precedence.
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Wrap("<xsl:include href='lib.xsl'/><xsl:character-map name='one'>" + OneMap + "</xsl:character-map>"),
            ["lib.xsl"] = Wrap("<xsl:character-map name='one'>" + OneMap + "</xsl:character-map>"),
        };
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => compiler.Compile(new Uri("file:///suite/main.xsl")))!;

        // Assert
        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE1580));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

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
