using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;

namespace XTransform.Tests.PublicApi;

/// <summary>P3-15: the public XsltCompiler / XsltExecutable / XsltTransformer surface.</summary>
[TestFixture]
internal sealed class XsltPublicApiTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";
    private static readonly string[] s_titleOnly = { "title" };

    private const string ListStylesheet =
        "<xsl:stylesheet version='2.0' xmlns:xsl='" + XsltNs + "'>" +
        "<xsl:output omit-xml-declaration='yes'/>" +
        "<xsl:param name='title' select=\"'none'\"/>" +
        "<xsl:template match='/'>" +
        "<list title='{$title}'>" +
        "<xsl:for-each select='doc/item'><li><xsl:value-of select='.'/></li></xsl:for-each>" +
        "</list>" +
        "</xsl:template>" +
        "</xsl:stylesheet>";

    // ── End to end ──────────────────────────────────────────────────────────

    [Test]
    public void Transform_StreamToStream_ProducesCanonicalOutput()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        XsltTransformer transformer = exe.CreateTransformer();

        // Act
        string output = RunToString(transformer, "<doc><item>a</item><item>b</item></doc>");

        // Assert
        Assert.That(output, Is.EqualTo("<list title=\"none\"><li>a</li><li>b</li></list>"));
    }

    [Test]
    public void Transform_WithParameter_UsesSuppliedValue()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        XsltTransformer transformer = exe.CreateTransformer();
        transformer.SetParameter(new QName("title"), XdmAtomicValue.String("T"));

        // Act
        string output = RunToString(transformer, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("<list title=\"T\" />"));
    }

    [Test]
    public void Transform_XmlReaderToTextWriter_UsesOutputSettings()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        using StringReader sr = new StringReader("<doc><item>x</item></doc>");
        using XmlReader reader = XmlReader.Create(sr);
        using StringWriter sw = new StringWriter();

        // Act
        exe.CreateTransformer().Transform(reader, sw);

        // Assert
        Assert.That(sw.ToString(), Is.EqualTo("<list title=\"none\"><li>x</li></list>"));
    }

    [Test]
    public void Transform_XmlReaderToXmlWriter_WritesThroughCallerWriter()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        using StringReader sr = new StringReader("<doc><item>x</item></doc>");
        using XmlReader reader = XmlReader.Create(sr);
        StringBuilder sb = new StringBuilder();
        using (XmlWriter writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        }))
        {
            // Act
            exe.CreateTransformer().Transform(reader, writer);
        }

        // Assert
        Assert.That(sb.ToString(), Is.EqualTo("<list title=\"none\"><li>x</li></list>"));
    }

    [Test]
    public void TransformToSequence_ReturnsConstructedNodes()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        using StringReader sr = new StringReader("<doc><item>a</item></doc>");
        using XmlReader reader = XmlReader.Create(sr);

        // Act
        XdmSequence result = exe.CreateTransformer().TransformToSequence(reader);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Count, Is.EqualTo(1));
            XdmNode node = (XdmNode)result[0];
            Assert.That(node.NodeKind, Is.EqualTo(XdmNodeKind.Element));
            Assert.That(node.NodeName!.Value.LocalName, Is.EqualTo("list"));
            Assert.That(node.GetStringValue(), Is.EqualTo("a"));
        });
    }

    // ── Entry points ────────────────────────────────────────────────────────

    [Test]
    public void InitialTemplate_WithNoInput_RunsNamedTemplate()
    {
        // Arrange
        XsltExecutable exe = Compile(Wrap(
            "<xsl:template name='start'><xsl:sequence select=\"'hello'\"/></xsl:template>"));
        XsltTransformer transformer = exe.CreateTransformer();
        transformer.InitialTemplate = new QName("start");

        // Act
        XdmSequence result = transformer.TransformToSequence(null);

        // Assert
        Assert.That(result.Count, Is.EqualTo(1));
    }

    [Test]
    public void NoInputAndNoInitialTemplate_Throws()
    {
        XsltTransformer transformer = Compile(ListStylesheet).CreateTransformer();

        Assert.That(() => transformer.TransformToSequence(null), Throws.InvalidOperationException);
    }

    [Test]
    public void InitialTemplate_Unknown_RaisesXtde0040()
    {
        XsltTransformer transformer = Compile(ListStylesheet).CreateTransformer();
        transformer.InitialTemplate = new QName("nope");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => transformer.TransformToSequence(null))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0040));
    }

    [Test]
    public void InitialMode_SelectsTemplatesInThatMode()
    {
        // Arrange
        XsltExecutable exe = Compile(Wrap(
            "<xsl:output omit-xml-declaration='yes'/>" +
            "<xsl:template match='/' mode='m'>in-m</xsl:template>" +
            "<xsl:template match='/'>default</xsl:template>"));
        XsltTransformer transformer = exe.CreateTransformer();
        transformer.InitialMode = new QName("m");

        // Act
        string output = RunToString(transformer, "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("in-m"));
    }

    [Test]
    public void InitialMode_Unknown_RaisesXtde0045()
    {
        XsltTransformer transformer = Compile(ListStylesheet).CreateTransformer();
        transformer.InitialMode = new QName("nope");

        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => RunToString(transformer, "<doc/>"))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0045));
    }

    [Test]
    public void SetParameter_UnknownName_ThrowsArgumentException()
    {
        XsltTransformer transformer = Compile(ListStylesheet).CreateTransformer();

        Assert.That(
            () => transformer.SetParameter(new QName("missing"), XdmAtomicValue.String("x")),
            Throws.ArgumentException);
    }

    [Test]
    public void GlobalParameters_ListsDeclaredParams()
    {
        XsltExecutable exe = Compile(ListStylesheet);

        Assert.That(exe.GlobalParameters.Select(q => q.LocalName), Is.EqualTo(s_titleOnly));
    }

    [Test]
    public void OutputSettings_ReflectsPrincipalXslOutput()
    {
        XsltExecutable exe = Compile(ListStylesheet);

        Assert.That(exe.OutputSettings.OmitXmlDeclaration, Is.True);
    }

    // ── Static errors ───────────────────────────────────────────────────────

    [Test]
    public void Compile_WithThreeStaticErrors_ThrowsOneExceptionListingAll()
    {
        // Arrange — duplicate variable, duplicate named template, unprefixed function name.
        string xsl = Wrap(
            "<xsl:variable name='v' select='1'/><xsl:variable name='v' select='2'/>" +
            "<xsl:template name='t'/><xsl:template name='t'/>" +
            "<xsl:function name='noprefix'/>");

        // Act
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile(xsl))!;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors.Count, Is.EqualTo(3));
            Assert.That(ex.Errors.Select(e => e.Code), Is.EquivalentTo(new[]
            {
                XsltErrors.XTSE0630,
                XsltErrors.XTSE0660,
                XsltErrors.XTSE0740,
            }));
        });
    }

    [Test]
    public void Compile_NotWellFormedXml_RaisesXtse0010()
    {
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => Compile("<xsl:stylesheet"))!;

        Assert.That(ex.Errors.Single().Code, Is.EqualTo(XsltErrors.XTSE0010));
    }

    [Test]
    public void Compile_TreatWarningsAsErrors_PromotesWarnings()
    {
        // Arrange — disable-output-escaping produces a compile warning.
        string xsl = Wrap(
            "<xsl:template match='/'><xsl:text disable-output-escaping='yes'>x</xsl:text></xsl:template>");
        XsltCompiler lenient = new XsltCompiler();
        XsltCompiler strict = new XsltCompiler(new XsltCompilerOptions { TreatWarningsAsErrors = true });

        // Act
        lenient.Compile(new StringReader(xsl));
        XsltStaticException ex = Assert.Throws<XsltStaticException>(() => strict.Compile(new StringReader(xsl)))!;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(lenient.Warnings, Is.Not.Empty);
            Assert.That(ex.Errors.All(e => e.Severity == DiagnosticSeverity.Error), Is.True);
        });
    }

    // ── Extension functions ─────────────────────────────────────────────────

    [Test]
    public void ExtensionFunction_IsCallableFromStylesheet()
    {
        // Arrange
        Triple triple = new Triple();
        XsltCompiler compiler = new XsltCompiler(new XsltCompilerOptions
        {
            ExtensionFunctions = new Dictionary<string, IExtensionFunction> { ["triple"] = triple },
        });
        XsltExecutable exe = compiler.Compile(new StringReader(Wrap(
            "<xsl:output omit-xml-declaration='yes'/>" +
            "<xsl:template match='/' xmlns:x='urn:ext'><xsl:value-of select='x:triple(14)'/></xsl:template>")));

        // Act
        string output = RunToString(exe.CreateTransformer(), "<doc/>");

        // Assert
        Assert.That(output, Is.EqualTo("42"));
    }

    [Test]
    public void ExtensionFunction_InNoNamespace_IsRejected()
    {
        Assert.That(
            () => new XsltCompiler(new XsltCompilerOptions
            {
                ExtensionFunctions = new Dictionary<string, IExtensionFunction> { ["bad"] = new NoNamespace() },
            }),
            Throws.ArgumentException);
    }

    // ── Threading and cancellation ──────────────────────────────────────────

    [Test]
    public void SharedExecutable_EightThreadsThousandRunsEach_NoCrosstalk()
    {
        // Arrange
        XsltExecutable exe = Compile(ListStylesheet);
        const string input = "<doc><item>a</item><item>b</item></doc>";
        const string expected = "<list title=\"none\"><li>a</li><li>b</li></list>";
        List<string> wrong = new List<string>();

        // Act
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ =>
        {
            for (int i = 0; i < 1000; i++)
            {
                string output = RunToString(exe.CreateTransformer(), input);
                if (!string.Equals(output, expected, StringComparison.Ordinal))
                {
                    lock (wrong)
                    {
                        wrong.Add(output);
                    }
                }
            }
        });

        // Assert
        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public async Task TransformAsync_CancelledToken_StopsWithin250ms()
    {
        // Arrange — a stylesheet that would spin for a very long time.
        XsltExecutable exe = Compile(Wrap(
            "<xsl:template match='/'><xsl:for-each select='1 to 1000000000'><x/></xsl:for-each></xsl:template>"));
        using CancellationTokenSource cts = new CancellationTokenSource();
        using MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes("<doc/>"));
        using MemoryStream output = new MemoryStream();

        // Act
        Task run = exe.CreateTransformer().TransformAsync(input, output, cts.Token);
        await Task.Delay(100);
        Stopwatch sw = Stopwatch.StartNew();
        cts.Cancel();
        Exception? caught = null;
        try
        {
            await run;
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }

        sw.Stop();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(caught, Is.Not.Null);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(250));
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static XsltExecutable Compile(string xsl) =>
        new XsltCompiler().Compile(new StringReader(xsl));

    private static string RunToString(XsltTransformer transformer, string input)
    {
        using MemoryStream inStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using MemoryStream outStream = new MemoryStream();
        transformer.Transform(inStream, outStream);
        return Encoding.UTF8.GetString(outStream.ToArray());
    }

    private sealed class Triple : IExtensionFunction
    {
        private static readonly SequenceType s_integer =
            new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

        public QName Name { get; } = new QName("urn:ext", "triple");

        public IReadOnlyList<SequenceType> ArgumentTypes { get; } = new[] { s_integer };

        public SequenceType ResultType => s_integer;

        public XdmSequence Invoke(IReadOnlyList<XdmSequence> arguments)
        {
            long n = ((XdmAtomicValue)arguments[0][0]).AsInt64();
            return XdmSequence.Of(XdmAtomicValue.Integer(n * 3));
        }
    }

    private sealed class NoNamespace : IExtensionFunction
    {
        public QName Name { get; } = new QName("local");

        public IReadOnlyList<SequenceType> ArgumentTypes { get; } = Array.Empty<SequenceType>();

        public SequenceType ResultType => SequenceType.AnySequence;

        public XdmSequence Invoke(IReadOnlyList<XdmSequence> arguments) => XdmSequence.Empty;
    }
}
