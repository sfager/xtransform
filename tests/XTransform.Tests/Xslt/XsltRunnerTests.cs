// Test file: inline string[] tag lists are intentionally per-case; a shared static table
// would just obscure which tags each test cares about.
#pragma warning disable CA1861 // Avoid constant arrays as arguments

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.Xslt;
using XTransform.Xslt.Instructions;

namespace XTransform.Tests.Xslt;

/// <summary>
/// Tests for <see cref="XsltRunner"/> and the P3-07 control-flow / dispatch runtime. We
/// observe dispatch by instrumenting template bodies with a <see cref="RecorderInstruction"/>
/// that emits a Comment event — every entry in <see cref="RecordingReceiver.Comments"/> marks
/// which template fired, in order.
/// </summary>
[TestFixture]
internal sealed class XsltRunnerTests
{
    private const string XsltNs = "http://www.w3.org/1999/XSL/Transform";

    // ── Dispatch ─────────────────────────────────────────────────────────────

    [Test]
    public void Transform_ApplyTemplates_InvokesMatchingRule()
    {
        // Arrange — one template matching <b>.
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:apply-templates select='doc/a/b'/>" +
            "</xsl:template>" +
            "<xsl:template match='b'><!--placeholder--></xsl:template>"),
            recorderOn: new[] { "b" });

        XdmNode doc = LoadInput("<doc><a><b/><b/></a></doc>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert — the <b> template ran twice (two <b> under /doc/a).
        Assert.That(sink.CountTag("b"), Is.EqualTo(2));
    }

    [Test]
    public void Transform_ApplyTemplates_NoMatch_FiresBuiltInRule()
    {
        // Arrange — no explicit rule; built-in rule for element recurses into children.
        // We install a recorder on the text rule; the only text node is inside <b>.
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='text()'><!--text--></xsl:template>"),
            recorderOn: new[] { "text()" });

        XdmNode doc = LoadInput("<doc><a>hello</a></doc>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert — the text node matched our template, reached via the default element
        // built-in rule recursing from doc → a → text.
        Assert.That(sink.CountTag("text()"), Is.EqualTo(1));
    }

    [Test]
    public void Transform_HigherPriorityWins()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='b' priority='1'><!--lo--></xsl:template>" +
            "<xsl:template match='b' priority='2'><!--hi--></xsl:template>" +
            "<xsl:template match='/'><xsl:apply-templates select='doc/b'/></xsl:template>"),
            recorderOn: new[] { "b@1", "b@2" });

        XdmNode doc = LoadInput("<doc><b/></doc>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert — only the priority-2 rule fired.
        Assert.Multiple(() =>
        {
            Assert.That(sink.CountTag("b@2"), Is.EqualTo(1));
            Assert.That(sink.CountTag("b@1"), Is.EqualTo(0));
        });
    }

    // ── Call-template + params ───────────────────────────────────────────────

    [Test]
    public void Transform_CallTemplate_RunsNamedTemplate()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'><xsl:call-template name='n'/></xsl:template>" +
            "<xsl:template name='n'><!--n--></xsl:template>"),
            recorderOn: new[] { "n" });

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert
        Assert.That(sink.CountTag("n"), Is.EqualTo(1));
    }

    [Test]
    public void Transform_RequiredParamWithoutValue_RaisesXtde0700()
    {
        // Arrange — called template declares a required param; caller omits it.
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'><xsl:call-template name='n'/></xsl:template>" +
            "<xsl:template name='n'>" +
            "  <xsl:param name='p' required='yes'/>" +
            "  <!--body-->" +
            "</xsl:template>"),
            recorderOn: System.Array.Empty<string>());

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act + Assert
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => XsltRunner.Transform(compiled, doc, sink))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0700));
    }

    // ── Control flow ────────────────────────────────────────────────────────

    [Test]
    public void Transform_IfTrue_RunsBody()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:if test='true()'><xsl:call-template name='t'/></xsl:if>" +
            "</xsl:template>" +
            "<xsl:template name='t'><!--t--></xsl:template>"),
            recorderOn: new[] { "t" });

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert
        Assert.That(sink.CountTag("t"), Is.EqualTo(1));
    }

    [Test]
    public void Transform_IfFalse_SkipsBody()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:if test='false()'><xsl:call-template name='t'/></xsl:if>" +
            "</xsl:template>" +
            "<xsl:template name='t'><!--t--></xsl:template>"),
            recorderOn: new[] { "t" });

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert
        Assert.That(sink.CountTag("t"), Is.EqualTo(0));
    }

    [Test]
    public void Transform_Choose_FirstMatchingWhenWins()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:choose>" +
            "    <xsl:when test='false()'><xsl:call-template name='a'/></xsl:when>" +
            "    <xsl:when test='true()'><xsl:call-template name='b'/></xsl:when>" +
            "    <xsl:otherwise><xsl:call-template name='c'/></xsl:otherwise>" +
            "  </xsl:choose>" +
            "</xsl:template>" +
            "<xsl:template name='a'><!--a--></xsl:template>" +
            "<xsl:template name='b'><!--b--></xsl:template>" +
            "<xsl:template name='c'><!--c--></xsl:template>"),
            recorderOn: new[] { "a", "b", "c" });

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert — only the second when fires.
        Assert.Multiple(() =>
        {
            Assert.That(sink.CountTag("a"), Is.EqualTo(0));
            Assert.That(sink.CountTag("b"), Is.EqualTo(1));
            Assert.That(sink.CountTag("c"), Is.EqualTo(0));
        });
    }

    [Test]
    public void Transform_ChooseOtherwise_WhenNoWhenMatches()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:choose>" +
            "    <xsl:when test='false()'><xsl:call-template name='a'/></xsl:when>" +
            "    <xsl:otherwise><xsl:call-template name='o'/></xsl:otherwise>" +
            "  </xsl:choose>" +
            "</xsl:template>" +
            "<xsl:template name='a'><!--a--></xsl:template>" +
            "<xsl:template name='o'><!--o--></xsl:template>"),
            recorderOn: new[] { "a", "o" });

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(sink.CountTag("a"), Is.EqualTo(0));
            Assert.That(sink.CountTag("o"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Transform_ForEach_IteratesSequence()
    {
        // Arrange
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'>" +
            "  <xsl:for-each select='doc/a'><xsl:call-template name='a'/></xsl:for-each>" +
            "</xsl:template>" +
            "<xsl:template name='a'><!--a--></xsl:template>"),
            recorderOn: new[] { "a" });

        XdmNode doc = LoadInput("<doc><a/><a/><a/></doc>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act
        XsltRunner.Transform(compiled, doc, sink);

        // Assert
        Assert.That(sink.CountTag("a"), Is.EqualTo(3));
    }

    // ── Recursion depth ─────────────────────────────────────────────────────

    [Test]
    public void Transform_InfiniteRecursion_RaisesXtde0810()
    {
        // Arrange — a self-calling named template with no base case.
        CompiledStylesheet compiled = CompileWithRecorder(Wrap(
            "<xsl:template match='/'><xsl:call-template name='r'/></xsl:template>" +
            "<xsl:template name='r'><xsl:call-template name='r'/></xsl:template>"),
            recorderOn: System.Array.Empty<string>());

        XdmNode doc = LoadInput("<doc/>");
        RecordingReceiver sink = new RecordingReceiver();

        // Act + Assert — capped at a small depth for a fast test.
        XsltDynamicException ex = Assert.Throws<XsltDynamicException>(
            () => XsltRunner.Transform(compiled, doc, sink, maxRecursionDepth: 32))!;
        Assert.That(ex.ErrorCode, Is.EqualTo(XsltErrors.XTDE0810));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Wrap(string body) =>
        $"<xsl:stylesheet version='2.0' xmlns:xsl='{XsltNs}'>{body}</xsl:stylesheet>";

    private static XdmNode LoadInput(string xml)
    {
        using StringReader sr = new StringReader(xml);
        using XmlReader reader = XmlReader.Create(sr);
        TinyTree tree = TinyTreeBuilder.Load(reader, new NamePool());
        return new XdmNode(NodeHandle.ForNode(tree, 0));
    }

    /// <summary>
    /// Compiles <paramref name="xml"/>, then replaces the body of every template whose match
    /// or name matches one of the <paramref name="recorderOn"/> tags with a
    /// <see cref="RecorderInstruction"/>. Tag semantics:
    ///   "name"   — matches <c>xsl:template name="name"</c>
    ///   "match"  — matches <c>xsl:template match="match"</c>
    ///   "match@priority" — a specific priority (e.g. "b@2").
    /// </summary>
    private static CompiledStylesheet CompileWithRecorder(string xml, string[] recorderOn)
    {
        InMemoryResolver resolver = new InMemoryResolver();
        resolver.Add("a.xsl", xml);
        StylesheetAssembly asm = StylesheetLinker.Link(new Uri("file:///suite/a.xsl"), resolver);
        CompiledStylesheet compiled = StylesheetCompiler.Compile(asm);

        Dictionary<TemplateDecl, CompiledTemplate> replaced =
            new Dictionary<TemplateDecl, CompiledTemplate>();
        foreach (KeyValuePair<TemplateDecl, CompiledTemplate> kv in compiled.CompiledTemplates)
        {
            if (ShouldRecord(kv.Key, recorderOn, out string? tag))
            {
                CompiledTemplate withRecorder = new CompiledTemplate(
                    kv.Value.Source,
                    kv.Value.Parameters,
                    new SequenceInstruction(new XsltInstruction[]
                    {
                        new RecorderInstruction(tag!),
                        kv.Value.Body,
                    }, kv.Value.Source.Location));
                replaced[kv.Key] = withRecorder;
            }
            else
            {
                replaced[kv.Key] = kv.Value;
            }
        }

        return new CompiledStylesheet(
            compiled.Assembly,
            compiled.Declarations,
            compiled.Modes,
            replaced,
            compiled.Keys,
            compiled.Functions,
            compiled.Warnings);
    }

    private static bool ShouldRecord(TemplateDecl decl, string[] tags, out string? tag)
    {
        foreach (string t in tags)
        {
            int at = t.IndexOf('@');
            string matchOrName = at >= 0 ? t.Substring(0, at) : t;
            string? priority = at >= 0 ? t.Substring(at + 1) : null;

            // Match by @name first.
            if (decl.Name.HasValue && decl.Name.Value.LocalName == matchOrName)
            {
                tag = t;
                return true;
            }

            // Match by @match (plus optional priority filter).
            if (decl.MatchPattern == matchOrName)
            {
                if (priority is null || decl.Priority == priority)
                {
                    tag = t;
                    return true;
                }
            }
        }

        tag = null;
        return false;
    }

    private sealed class RecorderInstruction : XsltInstruction
    {
        public RecorderInstruction(string tag) : base(SourceSpan.None) { Tag = tag; }
        public string Tag { get; }

        public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
        {
            receiver.Comment(Tag);
        }
    }

    private sealed class RecordingReceiver : IResultReceiver
    {
        public List<string> Comments { get; } = new List<string>();

        public int CountTag(string tag)
        {
            int n = 0;
            for (int i = 0; i < Comments.Count; i++)
            {
                if (Comments[i] == tag) { n++; }
            }

            return n;
        }

        public void StartDocument() { }
        public void EndDocument() { }
        public void StartElement(int nameCode, SourceSpan location) { }
        public void Namespace(string prefix, string uri) { }
        public void Attribute(int nameCode, string value, SourceSpan location) { }
        public void EndElement() { }
        public void Text(string text) { }
        public void Comment(string text) => Comments.Add(text);
        public void ProcessingInstruction(string target, string data) { }
        public void Append(XdmItem item) { }
    }

    private sealed class InMemoryResolver : IResourceResolver
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Add(string name, string xml) => _map[name] = xml;

        public Stream? Open(Uri uri)
        {
            const string prefix = "file:///suite/";
            string s = uri.OriginalString;
            if (!s.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            return _map.TryGetValue(s.Substring(prefix.Length), out string? xml)
                ? new MemoryStream(Encoding.UTF8.GetBytes(xml))
                : null;
        }
    }
}
