// XSLT-specific run state layered over the XPath RunState: template call stack,
// recursion-depth counter, link to the CompiledStylesheet.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt;

/// <summary>
/// Per-transformation mutable state: template call stack for diagnostics, recursion-depth
/// counter, and a back-reference to the <see cref="CompiledStylesheet"/>. Not thread-safe —
/// one instance per transformation.
/// </summary>
internal sealed class XsltRunState
{
    /// <summary>Default stack-depth ceiling; mirrors <c>XsltTransformer.MaxRecursionDepth</c>.</summary>
    public const int DefaultMaxRecursionDepth = 10_000;

    private int _depth;
    private QName _currentMode = DeclarationCollector.DefaultMode;
    private readonly List<KeyValuePair<QName, XdmSequence>> _variableStack = new List<KeyValuePair<QName, XdmSequence>>();

    /// <summary>Initializes a run state.</summary>
    public XsltRunState(
        CompiledStylesheet stylesheet,
        RunState xpathRun,
        NamePool namePool,
        int maxRecursionDepth = DefaultMaxRecursionDepth)
    {
        Stylesheet = stylesheet;
        XPathRun = xpathRun;
        NamePool = namePool;
        MaxRecursionDepth = maxRecursionDepth;
    }

    /// <summary>The compiled stylesheet being executed.</summary>
    public CompiledStylesheet Stylesheet { get; }

    /// <summary>Name pool shared with the receiver chain; used by output instructions to allocate name codes.</summary>
    public NamePool NamePool { get; }

    /// <summary>The match whose groups <c>regex-group()</c> reads: set while an xsl:matching-substring body runs, null elsewhere.</summary>
    public System.Text.RegularExpressions.Match? CurrentRegexMatch { get; set; }

    /// <summary>The underlying XPath run state (current-dateTime, cancellation, timezone).</summary>
    public RunState XPathRun { get; }

    /// <summary>The ceiling at which <see cref="EnterTemplate"/> throws <c>XTDE0810</c>.</summary>
    public int MaxRecursionDepth { get; }

    /// <summary>Current depth of the template / function call stack.</summary>
    public int Depth => _depth;

    /// <summary>The template call stack (bottom = first entered, top = currently executing).</summary>
    public List<TemplateFrame> CallStack { get; } = new List<TemplateFrame>();

    /// <summary>The mode currently being executed (used by <c>xsl:apply-templates mode="#current"</c>).</summary>
    public QName CurrentMode
    {
        get => _currentMode;
        set => _currentMode = value;
    }

    /// <summary>
    /// Pushes a template frame. Raises <c>XTDE0810</c> when the depth would exceed the ceiling.
    /// </summary>
    public void EnterTemplate(TemplateDecl template, SourceSpan callSite)
    {
        if (_depth >= MaxRecursionDepth)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0810,
                $"Template recursion depth exceeded {MaxRecursionDepth} frames (template '{template.Element.Name.LocalName}')",
                callSite);
        }

        CallStack.Add(new TemplateFrame(template, callSite));
        _depth++;
    }

    /// <summary>Pops the top template frame.</summary>
    public void LeaveTemplate()
    {
        if (CallStack.Count > 0)
        {
            CallStack.RemoveAt(CallStack.Count - 1);
        }

        if (_depth > 0)
        {
            _depth--;
        }
    }

    // ── Variable scope ─────────────────────────────────────────────────────

    /// <summary>Marker used by <see cref="PushVariable"/>/<see cref="RestoreVariablesTo"/> to bound a scope.</summary>
    public int VariableScopeMarker => _variableStack.Count;

    /// <summary>Pushes a variable binding onto the stack. Later lookups see this value until it is popped.</summary>
    public void PushVariable(QName name, XdmSequence value)
    {
        _variableStack.Add(new KeyValuePair<QName, XdmSequence>(name, value));
    }

    /// <summary>Pops every binding added after <paramref name="marker"/>.</summary>
    public void RestoreVariablesTo(int marker)
    {
        while (_variableStack.Count > marker)
        {
            _variableStack.RemoveAt(_variableStack.Count - 1);
        }
    }

    /// <summary>Lazily-cached global variable values, keyed by expanded name.</summary>
    public Dictionary<QName, XdmSequence> Globals { get; } = new Dictionary<QName, XdmSequence>();

    /// <summary>Global variables currently being evaluated; used for XTDE0640 cycle detection.</summary>
    public HashSet<QName> GlobalsInProgress { get; } = new HashSet<QName>();

    /// <summary>External parameter overrides supplied by the caller (consumed by the global evaluator).</summary>
    public Dictionary<QName, XdmSequence> ExternalParameters { get; } = new Dictionary<QName, XdmSequence>();

    // ── Documents and resolver ─────────────────────────────────────────────

    /// <summary>Resource resolver used by fn:document / fn:doc. Nullable — falls back to the default file-system resolver.</summary>
    public IResourceResolver? Resolver { get; set; }

    /// <summary>Document cache keyed by resolved absolute URI string.</summary>
    public Dictionary<string, XdmNode> DocumentCache { get; } =
        new Dictionary<string, XdmNode>(System.StringComparer.Ordinal);

    /// <summary>
    /// Loads the document at <paramref name="uriString"/> via <see cref="Resolver"/>, caching
    /// the result. Returns <see langword="null"/> when the resource cannot be retrieved —
    /// callers must decide whether that's an error (fn:document / fn:doc) or a false result
    /// (fn:doc-available).
    /// </summary>
    public XdmNode? LoadDocument(string uriString)
    {
        if (string.IsNullOrEmpty(uriString))
        {
            return null;
        }

        if (DocumentCache.TryGetValue(uriString, out XdmNode? cached))
        {
            return cached;
        }

        Uri uri;
        try
        {
            uri = new Uri(uriString, UriKind.Absolute);
        }
        catch
        {
            return null;
        }

        IResourceResolver resolver = Resolver ?? FileSystemResourceResolver.Instance;
        Stream? stream;
        try
        {
            stream = resolver.Open(uri);
        }
        catch
        {
            return null;
        }

        if (stream is null)
        {
            return null;
        }

        try
        {
            using (stream)
            using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
            }))
            {
                // Documents loaded by doc() / document() are source documents: xsl:strip-space applies (§4.3).
                WhitespaceTable whitespace = Stylesheet.Declarations.Whitespace;
                TinyTree tree = TinyTreeBuilder.Load(reader, NamePool, whitespace.HasStripRules ? whitespace.ShouldStrip : null);
                XdmNode doc = new XdmNode(NodeHandle.ForNode(tree, 0));
                DocumentCache[uriString] = doc;
                return doc;
            }
        }
        catch
        {
            return null;
        }
    }

    // ── Keys ────────────────────────────────────────────────────────────────

    private readonly Dictionary<KeyIndexKey, KeyIndex> _keyIndexes =
        new Dictionary<KeyIndexKey, KeyIndex>();

    private readonly HashSet<KeyIndexKey> _keysInProgress = new HashSet<KeyIndexKey>();

    /// <summary>
    /// Returns the per-document key index for <paramref name="keyName"/>, building it on first
    /// access. The index is scoped to the document that owns <paramref name="tree"/>.
    /// </summary>
    public KeyIndex GetOrBuildKeyIndex(TinyTree tree, QName keyName, IReadOnlyList<KeyDecl> decls, DynamicContext context)
    {
        KeyIndexKey key = new KeyIndexKey(tree.TreeId, keyName);
        if (_keyIndexes.TryGetValue(key, out KeyIndex? existing))
        {
            return existing;
        }

        // XSLT 2.0 §16.3.3: a key whose definition depends on itself is a circularity (XTDE0640).
        if (!_keysInProgress.Add(key))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0640,
                $"Circular reference: the definition of key '{keyName.ClarkName}' depends on itself",
                decls[0].Location);
        }

        try
        {
            KeyIndex index = new KeyIndex();
            foreach (KeyDecl decl in decls)
            {
                AddToKeyIndex(index, tree, decl, context);
            }

            _keyIndexes[key] = index;
            return index;
        }
        finally
        {
            _keysInProgress.Remove(key);
        }
    }

    // Adds the nodes selected by one xsl:key declaration; several declarations of a name combine (§16.3).
    private static void AddToKeyIndex(KeyIndex index, TinyTree tree, KeyDecl decl, DynamicContext context)
    {
        NamespaceBindingsResolver resolver = new NamespaceBindingsResolver(
            decl.Element.InScopeNamespaces,
            defaultElementNamespace: string.Empty,
            defaultFunctionNamespace: BuiltInFunctionLibrary.FnNamespace);

        // The pattern was parsed and its predicates compiled when the stylesheet was compiled.
        PatternAst pattern =
            ((XsltRunState)context.Run.Tag!).Stylesheet.PatternPredicates.GetKeyPattern(decl)
            ?? XPathParser.ParsePattern(decl.MatchPattern, decl.Location, resolver);
        CompiledExpression useExpr = XPathCompiler.ParseAndCompile(
            decl.UseExpression, decl.Location, resolver, ((XsltRunState)context.Run.Tag!).Stylesheet.Functions);

        // Walk every node in the tree; nodes matching the pattern contribute to the index.
        for (int i = 0; i < tree.NodeCount; i++)
        {
            XdmNode node = new XdmNode(NodeHandle.ForNode(tree, i));
            if (!PatternMatcher.Matches(pattern, node, context))
            {
                continue;
            }

            DynamicContext itemCtx = context.WithCurrentItem(node, 1, 1).WithFrame(useExpr.SlotCount);
            IXdmIterator iter = useExpr.Root.Iterate(itemCtx);
            XdmItem? value;
            while ((value = iter.Next()) is not null)
            {
                index.Add(value.GetStringValue(), node);
            }
        }
    }

    // ── Variable lookup ────────────────────────────────────────────────────

    /// <summary>
    /// Looks up the current value of a variable. Searches the local scope stack top-down
    /// first, then falls back to <see cref="Globals"/>. Returns <see cref="XdmSequence.Empty"/>
    /// when nothing is bound — the compiler would have caught undeclared references via
    /// XPST0008 in P3-04, so the empty fallback is only hit during bootstrap (initial globals).
    /// </summary>
    public XdmSequence LookupVariable(QName name)
    {
        for (int i = _variableStack.Count - 1; i >= 0; i--)
        {
            if (_variableStack[i].Key.Equals(name))
            {
                return _variableStack[i].Value;
            }
        }

        if (Globals.TryGetValue(name, out XdmSequence? value))
        {
            return value;
        }

        return XdmSequence.Empty;
    }
}

/// <summary>Composite key for the per-(document, name) key-index cache.</summary>
internal readonly struct KeyIndexKey : System.IEquatable<KeyIndexKey>
{
    /// <summary>Initializes the key.</summary>
    public KeyIndexKey(long treeId, QName name)
    {
        TreeId = treeId;
        Name = name;
    }

    /// <summary>Document identity.</summary>
    public long TreeId { get; }

    /// <summary>Key name.</summary>
    public QName Name { get; }

    /// <inheritdoc/>
    public bool Equals(KeyIndexKey other) => TreeId == other.TreeId && Name.Equals(other.Name);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeyIndexKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            return (TreeId.GetHashCode() * 397) ^ Name.GetHashCode();
        }
    }
}

/// <summary>One frame on the template call stack.</summary>
internal readonly struct TemplateFrame
{
    /// <summary>Initializes a frame.</summary>
    public TemplateFrame(TemplateDecl template, SourceSpan callSite)
    {
        Template = template;
        CallSite = callSite;
    }

    /// <summary>The template whose body is executing.</summary>
    public TemplateDecl Template { get; }

    /// <summary>Source location of the <c>apply-templates</c> / <c>call-template</c> that invoked it.</summary>
    public SourceSpan CallSite { get; }
}
