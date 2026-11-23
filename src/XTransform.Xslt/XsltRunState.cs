// XSLT-specific run state layered over the XPath RunState: template call stack,
// recursion-depth counter, link to the CompiledStylesheet.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

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
