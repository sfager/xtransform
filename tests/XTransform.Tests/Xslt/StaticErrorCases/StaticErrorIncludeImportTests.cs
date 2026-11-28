using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>Batch 13 of P4-23: where xsl:include and xsl:import may appear (XSLT 2.0 §3.10; XTSE0170, XTSE0190, XTSE0200).</summary>
[TestFixture]
public sealed class StaticErrorIncludeImportTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template name='main'><xsl:include href='ok.xsl'/><out/></xsl:template>", TestName = "IncludeInsideATemplate")]
    [TestCase("<xsl:variable name='v'><xsl:include href='ok.xsl'/></xsl:variable>", TestName = "IncludeInsideAVariable")]
    [TestCase("<xsl:template match='/'><out><xsl:include href='ok.xsl'/></out></xsl:template>", TestName = "IncludeInsideALiteralElement")]
    public void Compile_IncludeThatIsNotTopLevel_RaisesXtse0170(string body)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body)))!;

        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0170));
    }

    [TestCase("<xsl:template name='main'><xsl:import href='ok.xsl'/><out/></xsl:template>", TestName = "ImportInsideATemplate")]
    [TestCase("<xsl:variable name='v'><xsl:import href='ok.xsl'/></xsl:variable>", TestName = "ImportInsideAVariable")]
    [TestCase("<xsl:template match='/'><xsl:choose><xsl:import href='ok.xsl'/></xsl:choose></xsl:template>", TestName = "ImportInsideAChoose")]
    public void Compile_ImportThatIsNotTopLevel_RaisesXtse0190(string body)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body)))!;

        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0190));
    }

    [TestCase("<xsl:template match='doc'/><xsl:import href='ok.xsl'/>", TestName = "ImportAfterATemplate")]
    [TestCase("<xsl:include href='ok.xsl'/><xsl:import href='ok.xsl'/>", TestName = "ImportAfterAnInclude")]
    [TestCase("<my:data xmlns:my='urn:my'/><xsl:import href='ok.xsl'/>", TestName = "ImportAfterADataElement")]
    [TestCase("<xsl:import href='ok.xsl'/><xsl:variable name='v' select='1'/><xsl:import href='ok.xsl'/>", TestName = "SecondImportAfterAVariable")]
    public void Compile_ImportThatDoesNotPrecedeTheOtherChildren_RaisesXtse0200(string body)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body), new Dictionary<string, string> { ["ok.xsl"] = Wrap(string.Empty) }))!;

        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0200));
    }

    [TestCase("<xsl:import href='ok.xsl'/><xsl:import href='ok2.xsl'/><xsl:include href='ok.xsl'/><xsl:template match='/'/>", TestName = "ImportsFirst")]
    [TestCase("<xsl:import href='ok.xsl'/><my:data xmlns:my='urn:my'/><xsl:template match='/'/>", TestName = "DataElementAfterTheImport")]
    [TestCase("<xsl:template match='/'/><xsl:include href='ok.xsl'/>", TestName = "IncludeAfterATemplate")]
    public void Compile_IncludesAndImportsInOrder_Compiles(string body)
    {
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["ok.xsl"] = Wrap(string.Empty),
            ["ok2.xsl"] = Wrap(string.Empty),
        };

        Assert.DoesNotThrow(() => Compile(Wrap(body), files));
    }

    [TestCase("<xsl:include href='ok.xsl' iref='other.xsl'/>", TestName = "IncludeWithAnUnknownAttribute")]
    [TestCase("<xsl:import href='ok.xsl' iref='other.xsl'/>", TestName = "ImportWithAnUnknownAttribute")]
    [TestCase("<xsl:include href='ok.xsl' xsl:href='ok.xsl'/>", TestName = "IncludeWithAnXsltNamespaceAttribute")]
    public void Compile_IncludeOrImportWithAnUndefinedAttribute_RaisesXtse0090(string body)
    {
        Dictionary<string, string> files = new Dictionary<string, string> { ["ok.xsl"] = Wrap(string.Empty) };

        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(Wrap(body), files))!;

        Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0090));
    }

    [TestCase("<xsl:include href='ok.xsl' use-when='true()' extension-element-prefixes=''/>", "2.0", TestName = "StandardAttributes")]
    [TestCase("<xsl:include href='ok.xsl' iref='other.xsl'/>", "3.0", TestName = "ForwardsCompatibleVersion")]
    public void Compile_IncludeWithAllowedAttributes_Compiles(string body, string version)
    {
        Dictionary<string, string> files = new Dictionary<string, string> { ["ok.xsl"] = Wrap(string.Empty) };
        string xsl = $"<xsl:stylesheet version='{version}' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

        Assert.DoesNotThrow(() => Compile(xsl, files));
    }
    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'>{body}</xsl:stylesheet>";

    private static void Compile(string xsl, Dictionary<string, string>? files = null)
    {
        Dictionary<string, string> all = files is null ? new Dictionary<string, string>() : new Dictionary<string, string>(files);
        all["main.xsl"] = xsl;
        new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(all) }).Compile(new Uri("file:///suite/main.xsl"));
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
