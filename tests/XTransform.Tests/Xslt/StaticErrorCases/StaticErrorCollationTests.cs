using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt.StaticErrorCases;

/// <summary>P4-24 batch 4: collations, input-type-annotations and the PI test in a pattern (XSLT 2.0 §3.6, §5.5.2, §16.3).</summary>
[TestFixture]
public sealed class StaticErrorCollationTests
{
    private const string XslNs = "http://www.w3.org/1999/XSL/Transform";
    private const string Codepoint = "http://www.w3.org/2005/xpath-functions/collation/codepoint";

    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "' default-collation='mailto:unrecognized.collation@nowhere.com'><xsl:template match='/'/></xsl:stylesheet>", "XTSE0125", TestName = "DefaultCollationNotRecognised")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "' default-collation='http://a.example/ http://b.example/'><xsl:template match='/'/></xsl:stylesheet>", "XTSE0125", TestName = "NoneOfSeveralCollationsRecognised")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:template match='/' default-collation='urn:nope'/></xsl:stylesheet>", "XTSE0125", TestName = "DefaultCollationOnATemplate")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:key name='k' match='*' use='17' collation='http://unknown.collation.uri/'/></xsl:stylesheet>", "XTSE1210", TestName = "KeyCollationNotRecognised")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:key name='k' match='a' use='1'/><xsl:key name='k' match='b' use='1' collation='urn:other'/></xsl:stylesheet>", "XTSE1220", TestName = "TwoKeysWithDifferentCollations")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:template match='processing-instruction(proc:inst-2)' xmlns:proc='urn:proc'/></xsl:stylesheet>", "XTSE0340", TestName = "PrefixedProcessingInstructionTarget")]
    public void Compile_InvalidDeclaration_RaisesTheErrorCode(string xsl, string code)
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => new XsltCompiler().Compile(new StringReader(xsl)))!;

        Assert.That(ex.Errors.Select(e => e.Code.LocalName), Does.Contain(code));
    }

    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "' default-collation='" + Codepoint + "'><xsl:template match='/'/></xsl:stylesheet>", TestName = "CodepointDefaultCollation")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "' default-collation='http://a.example/ " + Codepoint + "'><xsl:template match='/'/></xsl:stylesheet>", TestName = "OneRecognisedCollationInAList")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:key name='k' match='*' use='1' collation='" + Codepoint + "'/></xsl:stylesheet>", TestName = "KeyWithTheCodepointCollation")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:key name='k' match='a' use='1'/><xsl:key name='k' match='b' use='1' collation='" + Codepoint + "'/></xsl:stylesheet>", TestName = "TwoKeysWithTheSameCollationWrittenTwoWays")]
    [TestCase("<xsl:stylesheet version='2.0' xmlns:xsl='" + XslNs + "'><xsl:template match='processing-instruction(inst-2)'/><xsl:template match=\"processing-instruction('inst-3')\"/><xsl:template match='processing-instruction()'/></xsl:stylesheet>", TestName = "ProcessingInstructionTargets")]
    public void Compile_ValidDeclaration_Compiles(string xsl)
    {
        Assert.DoesNotThrow(() => new XsltCompiler().Compile(new StringReader(xsl)));
    }

    [TestCase("strip", "preserve", true, TestName = "StripAndPreserve")]
    [TestCase("preserve", "strip", true, TestName = "PreserveAndStrip")]
    [TestCase("strip", "strip", false, TestName = "StripInBoth")]
    [TestCase("strip", "unspecified", false, TestName = "StripAndUnspecified")]
    [TestCase("preserve", null, false, TestName = "PreserveAndNothing")]
    public void Compile_InputTypeAnnotationsAcrossModules_RaisesXtse0265OnlyForStripAgainstPreserve(string main, string? included, bool conflict)
    {
        string mainAttribute = $" input-type-annotations='{main}'";
        string includedAttribute = included is null ? string.Empty : $" input-type-annotations='{included}'";
        Dictionary<string, string> files = new Dictionary<string, string>
        {
            ["main.xsl"] = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'{mainAttribute}><xsl:include href='lib.xsl'/></xsl:stylesheet>",
            ["lib.xsl"] = $"<xsl:stylesheet version='2.0' xmlns:xsl='{XslNs}'{includedAttribute}/>",
        };
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions { ResourceResolver = new Resolver(files) });

        if (conflict)
        {
            XsltStaticException ex = Assert.Throws<XsltStaticException>(() => compiler.Compile(new Uri("file:///suite/main.xsl")))!;
            Assert.That(ex.Errors.Select(e => e.Code), Does.Contain(XsltErrors.XTSE0265));
        }
        else
        {
            Assert.DoesNotThrow(() => compiler.Compile(new Uri("file:///suite/main.xsl")));
        }
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
