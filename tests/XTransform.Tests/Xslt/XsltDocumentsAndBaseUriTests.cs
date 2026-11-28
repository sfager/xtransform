using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace XTransform.Tests.Xslt;

/// <summary>P4-20: document(), doc(), base URIs, DTD entities and XML 1.1 output (XSLT 2.0 §3.2, §16.1, §16.6.4; XML Base).</summary>
[TestFixture]
public sealed class XsltDocumentsAndBaseUriTests
{
    private const string Xsl = "http://www.w3.org/1999/XSL/Transform";

    // ── document() and doc() ────────────────────────────────────────────────

    [Test]
    public void Document_RelativeUri_IsResolvedAgainstTheStylesheetBaseUri()
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string> { ["http://host/dir/data.xml"] = "<d>found</d>" };

        // Act
        string result = Run("<xsl:value-of select=\"document('data.xml')/d\"/>", files: files, stylesheetBase: "http://host/dir/main.xsl");

        // Assert
        Assert.That(result, Is.EqualTo("found"));
    }

    [Test]
    public void Document_NodeArgument_IsResolvedAgainstTheBaseUriOfThatNode()
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string> { ["http://other/x/data.xml"] = "<d>found</d>" };

        // Act
        string result = Run(
            "<xsl:value-of select=\"document(/*/@href)/d\"/>",
            files: files,
            stylesheetBase: "http://host/dir/main.xsl",
            input: "<s xml:base='http://other/x/' href='data.xml'/>");

        // Assert
        Assert.That(result, Is.EqualTo("found"));
    }

    [Test]
    public void Document_SecondArgument_GivesTheBaseUriForRelativeReferences()
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string> { ["http://other/x/data.xml"] = "<d>found</d>" };

        // Act
        string result = Run(
            "<xsl:value-of select=\"document('data.xml', /*)/d\"/>",
            files: files,
            stylesheetBase: "http://host/dir/main.xsl",
            input: "<s xml:base='http://other/x/'/>");

        // Assert
        Assert.That(result, Is.EqualTo("found"));
    }

    [Test]
    public void Document_SameDocumentNamedTwice_IsReturnedOnce()
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string> { ["http://host/dir/data.xml"] = "<d/>" };

        // Act
        string result = Run("<xsl:value-of select=\"count(document(('data.xml', './data.xml')))\"/>", files: files, stylesheetBase: "http://host/dir/main.xsl");

        // Assert
        Assert.That(result, Is.EqualTo("1"));
    }

    [Test]
    public void Document_EmptyString_IsTheStylesheetItself()
    {
        // Arrange
        Dictionary<string, string> files = new Dictionary<string, string>();

        // Act
        string result = Run("<xsl:value-of select=\"name(document('')/*)\"/>", files: files, stylesheetBase: "http://host/dir/main.xsl");

        // Assert
        Assert.That(result, Is.EqualTo("xsl:stylesheet"));
    }

    [Test]
    public void Doc_EmptySequence_GivesTheEmptySequence()
    {
        Assert.That(Run("<xsl:value-of select=\"count(doc(()))\"/>"), Is.EqualTo("0"));
    }

    // ── base URIs ───────────────────────────────────────────────────────────

    [Test]
    public void StaticBaseUri_ReflectsXmlBaseOnTheStylesheetElements()
    {
        // Arrange
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/' xml:base='sub/'><xsl:value-of select='static-base-uri()'/></xsl:template>", "xml:base='http://host/dir/'");

        // Act
        string result = Transform(xsl, "<d/>", null, null);

        // Assert
        Assert.That(result, Is.EqualTo("http://host/dir/sub/"));
    }

    [Test]
    public void ResolveUri_UsesTheStaticBaseUriAtTheCall()
    {
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/' xml:base='http://one/'><xsl:value-of select=\"resolve-uri('a')\"/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Is.EqualTo("http://one/a"));
    }

    [TestCase("<d xml:base='http://a.org/'><e xml:base='sub/'/></d>", "string(base-uri(/d/e))", "http://a.org/sub/", TestName = "XmlBaseIsResolvedAgainstTheParent")]
    [TestCase("<d xml:base='http://a.org/'><e/></d>", "string(base-uri(/d/e))", "http://a.org/", TestName = "ElementInheritsTheBaseUriOfItsParent")]
    [TestCase("<d xml:base='http://a.org/' a='1'>t</d>", "string(base-uri(/d/@a))", "http://a.org/", TestName = "AttributeTakesItsParentsBaseUri")]
    [TestCase("<d xmlns:p='urn:p'/>", "count(base-uri(/d/namespace::p))", "0", TestName = "NamespaceNodeHasNoBaseUri")]
    public void BaseUri_OfSourceNodes_FollowsXmlBase(string input, string expression, string expected)
    {
        string xsl = Wrap($"<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"{expression}\"/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, input, null, null), Is.EqualTo(expected));
    }

    [Test]
    public void BaseUri_OfATemporaryTree_IsTheStaticBaseUriOfTheVariable()
    {
        string xsl = Wrap(
            "<xsl:output method='text'/><xsl:template match='/'><xsl:variable name='t' xml:base='http://v.org/'><e/></xsl:variable><xsl:value-of select='base-uri($t/e)'/></xsl:template>",
            string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Is.EqualTo("http://v.org/"));
    }

    [Test]
    public void DocumentUri_OfATemporaryTree_IsEmpty()
    {
        string xsl = Wrap(
            "<xsl:output method='text'/><xsl:template match='/'><xsl:variable name='t' xml:base='http://v.org/'><e/></xsl:variable><xsl:value-of select='count(document-uri($t))'/></xsl:template>",
            string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Is.EqualTo("0"));
    }

    [Test]
    public void InputBaseUri_IsTheBaseUriAndTheDocumentUriOfTheSource()
    {
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select='base-uri(/*), document-uri(/)' separator=' '/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, new Uri("http://src/in.xml")), Is.EqualTo("http://src/in.xml http://src/in.xml"));
    }

    // ── DTD entities ────────────────────────────────────────────────────────

    [Test]
    public void Entity_InternalGeneralEntity_IsExpandedInTheSource()
    {
        string result = Run(
            "<xsl:value-of select='/d'/>",
            input: "<!DOCTYPE d [<!ENTITY e 'expanded'>]><d>&e;</d>");

        Assert.That(result, Is.EqualTo("expanded"));
    }

    [Test]
    public void Entity_InternalGeneralEntity_IsExpandedInTheStylesheet()
    {
        string xsl = "<!DOCTYPE xsl:stylesheet [<!ENTITY e 'expanded'>]>" + Wrap("<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"'&e;'\"/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Is.EqualTo("expanded"));
    }

    [Test]
    public void Entity_ExternalEntity_IsNotResolved()
    {
        // Arrange: an external entity naming a file that exists and holds a recognizable marker.
        string secret = Path.Combine(Path.GetTempPath(), "xtransform-xxe-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(secret, "SECRET-MARKER");
        try
        {
            string input = "<!DOCTYPE d [<!ENTITY x SYSTEM '" + new Uri(secret).AbsoluteUri + "'>]><d>&x;</d>";

            // Act
            string result;
            try
            {
                result = Run("<xsl:value-of select='/d'/>", input: input);
            }
            catch (Exception)
            {
                result = string.Empty;
            }

            // Assert
            Assert.That(result, Does.Not.Contain("SECRET-MARKER"));
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Test]
    public void Entity_ExpansionBeyondTheLimit_IsRefused()
    {
        // A chain of entities that expands to far more than the cap (a "billion laughs" shape, kept small enough to build quickly).
        StringBuilder dtd = new StringBuilder("<!ENTITY a0 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'>");
        for (int i = 1; i <= 8; i++)
        {
            string previous = "&a" + (i - 1) + ";";
            dtd.Append("<!ENTITY a").Append(i).Append(" '").Append(string.Concat(System.Linq.Enumerable.Repeat(previous, 10))).Append("'>");
        }

        string input = "<!DOCTYPE d [" + dtd + "]><d>&a8;</d>";

        Assert.That(() => Run("<xsl:value-of select='string-length(/d)'/>", input: input), Throws.Exception);
    }

    [Test]
    public void UnparsedEntityUri_GivesTheSystemIdentifierResolvedAgainstTheDocument()
    {
        string input = "<!DOCTYPE d [<!NOTATION n SYSTEM 'n'><!ENTITY pic SYSTEM 'img/pic.gif' NDATA n>]><d/>";
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"unparsed-entity-uri('pic'), unparsed-entity-uri('nope')\" separator='|'/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, input, null, new Uri("http://host/dir/in.xml")), Is.EqualTo("http://host/dir/img/pic.gif|"));
    }

    [Test]
    public void UnparsedEntityPublicId_GivesThePublicIdentifier()
    {
        string input = "<!DOCTYPE d [<!NOTATION n SYSTEM 'n'><!ENTITY pic PUBLIC '-//pic//' 'pic.gif' NDATA n>]><d/>";
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/'><xsl:value-of select=\"unparsed-entity-public-id('pic')\"/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, input, null, null), Is.EqualTo("-//pic//"));
    }

    // ── XML 1.1 ─────────────────────────────────────────────────────────────

    [Test]
    public void OutputVersion11_WritesControlCharactersAsCharacterReferences()
    {
        string xsl = "<?xml version=\"1.1\"?>" + Wrap(
            "<xsl:output version='1.1' omit-xml-declaration='yes'/><xsl:template match='/'><out a='&#x82;'>&#x1;&#x85;</out></xsl:template>",
            string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Is.EqualTo("<out a=\"&#x82;\">&#x1;&#x85;</out>"));
    }

    [Test]
    public void OutputVersion11_DeclaresVersion11()
    {
        string xsl = Wrap("<xsl:output version='1.1'/><xsl:template match='/'><out/></xsl:template>", string.Empty);

        Assert.That(Transform(xsl, "<d/>", null, null), Does.StartWith("<?xml version=\"1.1\""));
    }

    [Test]
    public void Xml11Source_DeclaringVersion11_IsRead()
    {
        string result = Run("<xsl:value-of select='/d'/>", input: "<?xml version=\"1.1\"?><d>v</d>");

        Assert.That(result, Is.EqualTo("v"));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body, string attributes) =>
        "<xsl:stylesheet version='2.0' xmlns:xsl='" + Xsl + "' " + attributes + ">" + body + "</xsl:stylesheet>";

    private static string Run(
        string instruction,
        Dictionary<string, string>? files = null,
        string? stylesheetBase = null,
        string input = "<d/>")
    {
        string xsl = Wrap("<xsl:output method='text'/><xsl:template match='/'>" + instruction + "</xsl:template>", string.Empty);
        return Transform(xsl, input, files, null, stylesheetBase);
    }

    private static string Transform(string stylesheet, string input, Dictionary<string, string>? files, Uri? inputBase, string? stylesheetBase = null)
    {
        XsltCompilerOptions options = new XsltCompilerOptions { ResourceResolver = new MapResolver(files, stylesheet) };
        XsltCompiler compiler = new XsltCompiler(options);
        Uri? baseUri = stylesheetBase is null ? null : new Uri(stylesheetBase);
        XsltExecutable exe;
        if (stylesheet.StartsWith("<?xml version=\"1.1\"", StringComparison.Ordinal))
        {
            // A stylesheet that declares XML 1.1 has to come in as bytes (a text reader has no declaration to adapt).
            using MemoryStream bytes = new MemoryStream(Encoding.UTF8.GetBytes(stylesheet));
            using System.Xml.XmlReader reader = XTransform.Xslt.XsltInput.CreateReader(bytes, null);
            exe = compiler.Compile(reader, baseUri);
        }
        else
        {
            exe = compiler.Compile(new StringReader(stylesheet), baseUri);
        }

        XsltTransformer transformer = exe.CreateTransformer();
        transformer.InputBaseUri = inputBase;
        using MemoryStream inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream output = new MemoryStream();
        transformer.Transform(inputStream, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private sealed class MapResolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _files;

        public MapResolver(Dictionary<string, string>? files, string stylesheet)
        {
            _files = files ?? new Dictionary<string, string>();
            _files["http://host/dir/main.xsl"] = stylesheet;
        }

        public Stream? Open(Uri uri) =>
            _files.TryGetValue(uri.AbsoluteUri, out string? text) ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null;
    }
}
