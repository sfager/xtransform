using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.Xslt;

/// <summary>XSLT 2.0 §16 and F&amp;O §8.1 — system and environment functions.</summary>
[TestFixture]
internal sealed class XsltSystemFunctionsTests
{
    private const string Doc = "<d><a/><b/></d>";

    [Test]
    public void GenerateId_OneArgument_IsStableAndDistinctPerNode()
    {
        // Arrange / Act
        string same = Eval("generate-id(/d/a) = generate-id(/d/a)");
        string different = Eval("generate-id(/d/a) = generate-id(/d/b)");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(same, Is.EqualTo("true"));
            Assert.That(different, Is.EqualTo("false"));
        });
    }

    [Test]
    public void GenerateId_NoArgument_UsesTheContextNode()
    {
        Assert.That(Eval("/d/a/generate-id() = generate-id(/d/a)"), Is.EqualTo("true"));
    }

    [Test]
    public void GenerateId_EmptySequence_IsTheEmptyString()
    {
        Assert.That(Eval("generate-id(/nothing) = ''"), Is.EqualTo("true"));
    }

    [Test]
    public void GenerateId_IsUniqueAcrossDocuments()
    {
        // The temporary tree has a node at the same index as /d/a but a different document.
        const string expr = "generate-id(/d/a) = generate-id($t/d/a)";
        string xsl = Wrap("<xsl:variable name='t'><d><a/></d></xsl:variable><xsl:template match='/'><xsl:value-of select=\"" + expr + "\"/></xsl:template>");

        Assert.That(Run(xsl, Doc), Is.EqualTo("false"));
    }

    [Test]
    public void GenerateId_AttributeAndItsParent_Differ()
    {
        Assert.That(Eval("generate-id(/d/a) = generate-id(/d/a/@x)", "<d><a x='1'/></d>"), Is.EqualTo("false"));
    }

    [Test]
    public void GenerateId_ResultIsAnNcName()
    {
        Assert.That(Eval("generate-id(/d/a) castable as xs:NCName"), Is.EqualTo("true"));
    }

    [TestCase("xsl:version", "2.0")]
    [TestCase("xsl:vendor", "XTransform")]
    [TestCase("xsl:product-name", "XTransform")]
    [TestCase("xsl:is-schema-aware", "no")]
    [TestCase("xsl:supports-serialization", "yes")]
    [TestCase("xsl:supports-backwards-compatibility", "no")]
    [TestCase("xsl:nonexistent", "")]
    public void SystemProperty_ReturnsTheDocumentedValue(string name, string expected)
    {
        Assert.That(Eval($"system-property('{name}')"), Is.EqualTo(expected));
    }

    [Test]
    public void SystemProperty_UsesTheNamespacesInScopeAtTheCall()
    {
        // The prefix 'x' is bound to the XSLT namespace here, so x:version names xsl:version.
        string xsl =
            "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:x='http://www.w3.org/1999/XSL/Transform'>" +
            "<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"system-property('x:version')\"/></xsl:template></xsl:stylesheet>";

        Assert.That(Run(xsl, Doc), Is.EqualTo("2.0"));
    }

    [Test]
    public void SystemProperty_UnboundPrefix_RaisesXtde1390()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("system-property('nope:version')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1390));
    }

    [Test]
    public void FunctionAvailable_BuiltInsAndUnknowns()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("function-available('concat')"), Is.EqualTo("true"));
            Assert.That(Eval("function-available('generate-id')"), Is.EqualTo("true"));
            Assert.That(Eval("function-available('no-such-function')"), Is.EqualTo("false"));
            Assert.That(Eval("function-available('substring', 2)"), Is.EqualTo("true"));
            Assert.That(Eval("function-available('substring', 9)"), Is.EqualTo("false"));
            Assert.That(Eval("function-available('key', 3)"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void FunctionAvailable_UnboundPrefix_RaisesXtde1400()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("function-available('nope:f')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1400));
    }

    [Test]
    public void ElementAvailable_XslInstructions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("element-available('xsl:value-of')"), Is.EqualTo("true"));
            Assert.That(Eval("element-available('xsl:nonsense')"), Is.EqualTo("false"));
            Assert.That(Eval("element-available('value-of')"), Is.EqualTo("false"));
        });
    }

    [Test]
    public void ElementAvailable_UnboundPrefix_RaisesXtde1440()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("element-available('nope:x')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1440));
    }

    [Test]
    public void TypeAvailable_OnlyTheBuiltInSchemaTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("type-available('xs:integer')"), Is.EqualTo("true"));
            Assert.That(Eval("type-available('xs:dateTime')"), Is.EqualTo("true"));
            Assert.That(Eval("type-available('xs:noSuchType')"), Is.EqualTo("false"));
            Assert.That(Eval("type-available('integer')"), Is.EqualTo("false"));
        });
    }

    [Test]
    public void TypeAvailable_UnboundPrefix_RaisesXtde1425()
    {
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval("type-available('nope:t')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE1425));
    }

    [Test]
    public void UnparsedEntityFunctions_ReturnTheEmptyStringForUndeclaredEntities()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("unparsed-entity-uri('pic') = ''"), Is.EqualTo("true"));
            Assert.That(Eval("unparsed-entity-public-id('pic') = ''"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void CurrentOutputUri_IsTheEmptySequence()
    {
        Assert.That(Eval("empty(current-output-uri())"), Is.EqualTo("true"));
    }

    [Test]
    public void ResolveUri_TwoArguments_ResolvesAgainstTheBase()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Eval("string(resolve-uri('c/d.xml', 'http://example.com/a/b'))"), Is.EqualTo("http://example.com/a/c/d.xml"));
            Assert.That(Eval("string(resolve-uri('../x', 'http://example.com/a/b/c'))"), Is.EqualTo("http://example.com/a/x"));
            Assert.That(Eval("string(resolve-uri('http://other/y', 'http://example.com/a'))"), Is.EqualTo("http://other/y"));
            Assert.That(Eval("empty(resolve-uri((), 'http://example.com/a'))"), Is.EqualTo("true"));
        });
    }

    [Test]
    public void ResolveUri_BaseThatIsNotAbsolute_RaisesForg0002()
    {
        XPathDynamicException ex = Assert.Throws<XPathDynamicException>(() => Eval("resolve-uri('x', 'relative/base')"))!;

        Assert.That(ex.ErrorCode, Is.EqualTo(XPathErrors.FORG0002));
    }

    [Test]
    public void ResolveUri_OneArgument_UsesTheStaticBaseUri()
    {
        // Arrange — the stylesheet was compiled from a file, so it has a base URI.
        string path = Path.Combine(Path.GetTempPath(), "xt-base-" + Guid.NewGuid().ToString("N") + ".xsl");
        File.WriteAllText(path, Wrap("<xsl:template match='/'><xsl:value-of select=\"resolve-uri('sibling.xml')\"/></xsl:template>"));
        try
        {
            // Act
            XsltExecutable exe = new XsltCompiler().Compile(new Uri(path));
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Doc));
            using MemoryStream output = new MemoryStream();
            exe.CreateTransformer().Transform(input, output);

            // Assert
            string expected = new Uri(new Uri(path), "sibling.xml").AbsoluteUri;
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo(expected));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void StaticBaseUri_OfAStylesheetFromAFile_IsItsUri()
    {
        string path = Path.Combine(Path.GetTempPath(), "xt-sbu-" + Guid.NewGuid().ToString("N") + ".xsl");
        File.WriteAllText(path, Wrap("<xsl:template match='/'><xsl:value-of select='static-base-uri()'/></xsl:template>"));
        try
        {
            XsltExecutable exe = new XsltCompiler().Compile(new Uri(path));
            using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(Doc));
            using MemoryStream output = new MemoryStream();
            exe.CreateTransformer().Transform(input, output);

            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo(new Uri(path).AbsoluteUri));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void UnparsedText_ReadsAFileWithAndWithoutAnEncoding()
    {
        string path = Path.Combine(Path.GetTempPath(), "xt-text-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("héllo\nworld"));
        try
        {
            string uri = new Uri(path).AbsoluteUri;
            Assert.Multiple(() =>
            {
                Assert.That(Eval($"unparsed-text('{uri}') = concat('h', codepoints-to-string(233), 'llo&#10;world')"), Is.EqualTo("true"));
                Assert.That(Eval($"unparsed-text('{uri}', 'utf-8') = unparsed-text('{uri}')"), Is.EqualTo("true"));
                Assert.That(Eval($"unparsed-text-available('{uri}')"), Is.EqualTo("true"));
                Assert.That(Eval($"unparsed-text-available('{uri}', 'utf-8')"), Is.EqualTo("true"));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void UnparsedText_MissingFile_RaisesFout1170AndIsNotAvailable()
    {
        string uri = new Uri(Path.Combine(Path.GetTempPath(), "xt-missing-" + Guid.NewGuid().ToString("N"))).AbsoluteUri;

        Assert.Multiple(() =>
        {
            Assert.That(Eval($"unparsed-text-available('{uri}')"), Is.EqualTo("false"));
            XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval($"unparsed-text('{uri}')"))!;
            Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.FOUT1170));
        });
    }

    [Test]
    public void UnparsedText_InvalidUtf8_RaisesFout1170()
    {
        string path = Path.Combine(Path.GetTempPath(), "xt-bad-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, new byte[] { 0x61, 0xFF, 0xFE, 0x62 });
        try
        {
            string uri = new Uri(path).AbsoluteUri;
            XsltDynamicException ex = Assert.Throws<XsltDynamicException>(() => Eval($"unparsed-text('{uri}')"))!;
            Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.FOUT1170));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void UnparsedText_EmptySequence_YieldsTheEmptySequence()
    {
        Assert.That(Eval("empty(unparsed-text(()))"), Is.EqualTo("true"));
    }

    private static string Wrap(string body) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:xs='http://www.w3.org/2001/XMLSchema'>" +
        "<xsl:output method='text'/>" + body + "</xsl:stylesheet>";

    private static string Eval(string xpath, string input = Doc) =>
        Run(Wrap("<xsl:template match='/'><xsl:value-of select=\"" + xpath + "\"/></xsl:template>"), input);

    private static string Run(string xsl, string input)
    {
        XsltExecutable exe = new XsltCompiler().Compile(new StringReader(xsl));
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        exe.CreateTransformer().Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
