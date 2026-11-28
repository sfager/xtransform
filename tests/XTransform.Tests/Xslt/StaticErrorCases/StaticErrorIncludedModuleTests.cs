using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>An error in an included module is reported once, although the module's declarations also appear in its includer.</summary>
[TestFixture]
public sealed class StaticErrorIncludedModuleTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    [TestCase("<xsl:template match='/' extra='1'/>", "err:XTSE0090", TestName = "UnknownAttribute")]
    [TestCase("<xsl:template match='/'><xsl:sort select='.'/></xsl:template>", "err:XTSE0010", TestName = "ContentModel")]
    [TestCase("<xsl:variable name='x/y'/>", "err:XTSE0020", TestName = "AttributeValue")]
    [TestCase("<xsl:template match='/'><xsl:element name='e' type='xs:string'/></xsl:template>", "err:XTSE1660", TestName = "SchemaAttribute")]
    [TestCase("<xsl:template name='a' mode='x y x'/>", "err:XTSE0550", TestName = "ModeList")]
    [TestCase("<xsl:variable name='q:v' select='1'/>", "err:XTSE0280", TestName = "UndeclaredPrefix")]
    [TestCase("<xsl:attribute-set name='a' use-attribute-sets='a'/>", "err:XTSE0720", TestName = "AttributeSetCycle")]
    public void Compile_ErrorInAnIncludedModule_IsReportedOnce(string partBody, string code)
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = Wrap("<xsl:include href='part.xsl'/><xsl:template match='/'/>"),
            ["part.xsl"] = Wrap(partBody),
        };
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => compiler.Compile(new Uri("file:///suite/main.xsl")))!;

        // Assert
        Assert.That(ex.Errors.Count(e => ("err:" + e.Code.LocalName) == code), Is.EqualTo(1), string.Join("; ", ex.Errors.Select(e => e.Code.LocalName + " " + e.Message)));
    }

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}' xmlns:xs='http://www.w3.org/2001/XMLSchema'>{body}</xsl:stylesheet>";

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
