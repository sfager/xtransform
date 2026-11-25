// XPath 2.0 §2.1.2 — dynamic context: context item, context position, context size,
// variable bindings and more. All mutable state lives here; compiled nodes are stateless.

using System;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// All mutable state of one expression evaluation. Child contexts are cheap — they share
/// the <see cref="RunState"/> and typically only replace the focus or frame.
/// </summary>
internal sealed class DynamicContext
{
    private readonly IXdmIterator? _focusIterator;         // null when focus is detached
    private int _contextPosition;                           // 1-based; 0 before first Next()
    private int _contextSizeCached = -1;                    // -1 = not computed

    /// <summary>Initializes a <see cref="DynamicContext"/> with explicit component values.</summary>
    private DynamicContext(RunState run, IXdmIterator? focus, XdmItem? contextItem,
                           int position, XdmSequence[] frame, XdmItem? currentItem)
    {
        Run = run;
        _focusIterator = focus;
        ContextItem = contextItem;
        _contextPosition = position;
        LocalFrame = frame;
        CurrentItem = currentItem;
    }

    /// <summary>
    /// The XSLT current item (XSLT 2.0 §5.4.4): the item being processed by the innermost template
    /// rule or xsl:for-each, which <c>fn:current()</c> returns. Unlike <see cref="ContextItem"/> it is
    /// not changed by predicates or path steps. Null when undefined.
    /// </summary>
    public XdmItem? CurrentItem { get; }

    /// <summary>Creates a fresh root context with no focus and an empty local frame.</summary>
    /// <param name="run">The run-wide state to attach.</param>
    /// <returns>A new root context.</returns>
    public static DynamicContext Root(RunState run)
    {
        return new DynamicContext(run, focus: null, contextItem: null, position: 0, currentItem: null,
            frame: s_emptyFrame);
    }

    /// <summary>The run-wide state shared by all child contexts.</summary>
    public RunState Run { get; }

    /// <summary>The current focus item — null when focus is undefined.</summary>
    public XdmItem? ContextItem { get; private set; }

    /// <summary>1-based position of the current focus item; 0 when focus is undefined.</summary>
    public int ContextPosition => _contextPosition;

    /// <summary>
    /// Size of the current focus (fn:last). Computed lazily by cloning the focus iterator and
    /// counting; only runs on first access.
    /// </summary>
    public int ContextSize
    {
        get
        {
            if (_contextSizeCached >= 0)
            {
                return _contextSizeCached;
            }

            if (_focusIterator is null)
            {
                _contextSizeCached = 0;
                return 0;
            }

            // Clone counts without consuming the live iterator.
            IXdmIterator probe = _focusIterator.Clone();
            int count = 0;
            while (probe.Next() is not null)
            {
                count++;
            }

            _contextSizeCached = count;
            return count;
        }
    }

    /// <summary>Local-variable frame, indexed by compile-time slot.</summary>
    public XdmSequence[] LocalFrame { get; }

    /// <summary>
    /// Returns a new context bound to the given focus iterator. The caller is responsible
    /// for driving the iterator (typically in a for-each loop that re-reads <see cref="ContextItem"/>
    /// and <see cref="ContextPosition"/> after each <see cref="AdvanceFocus"/>).
    /// </summary>
    /// <param name="focus">The focus iterator to bind; ownership is transferred.</param>
    /// <returns>A new child context sharing the current <see cref="RunState"/> and <see cref="LocalFrame"/>.</returns>
    public DynamicContext WithFocus(IXdmIterator focus)
    {
        return new DynamicContext(Run, focus, contextItem: null, position: 0, frame: LocalFrame, currentItem: CurrentItem);
    }

    /// <summary>
    /// Returns a context with the focus pinned to a single item at the given position and size.
    /// No iterator is attached; <see cref="ContextSize"/> reports <paramref name="size"/> without
    /// cloning any iterator.
    /// </summary>
    /// <param name="item">The pinned focus item.</param>
    /// <param name="position">The 1-based position of the item within its sequence.</param>
    /// <param name="size">The total number of items in the sequence (for fn:last()).</param>
    /// <returns>A new child context sharing the current <see cref="RunState"/> and <see cref="LocalFrame"/>.</returns>
    public DynamicContext WithContextItem(XdmItem item, int position, int size)
    {
        DynamicContext ctx = new DynamicContext(Run, focus: null, contextItem: item,
                                                 position: position, frame: LocalFrame, currentItem: CurrentItem);
        ctx._contextSizeCached = size;
        return ctx;
    }

    /// <summary>
    /// Like <see cref="WithContextItem"/>, but also makes <paramref name="item"/> the XSLT current
    /// item. Used where XSLT changes the current item: template rules, xsl:for-each, key <c>use</c>
    /// expressions and pattern matching (XSLT 2.0 §5.4.4).
    /// </summary>
    /// <param name="item">The new context and current item.</param>
    /// <param name="position">The 1-based position of the item within its sequence.</param>
    /// <param name="size">The total number of items in the sequence.</param>
    /// <returns>A new child context sharing the current <see cref="RunState"/> and <see cref="LocalFrame"/>.</returns>
    public DynamicContext WithCurrentItem(XdmItem item, int position, int size)
    {
        DynamicContext ctx = new DynamicContext(Run, focus: null, contextItem: item,
                                                 position: position, frame: LocalFrame, currentItem: item);
        ctx._contextSizeCached = size;
        return ctx;
    }

    /// <summary>
    /// Returns a context with no focus and no XSLT current item, sharing the run state and local frame.
    /// An xsl:function body is evaluated this way: XSLT 2.0 §10.3 says the focus is undefined inside it.
    /// </summary>
    /// <returns>A new child context with an undefined focus.</returns>
    public DynamicContext WithoutFocus()
    {
        return new DynamicContext(Run, focus: null, contextItem: null, position: 0,
                                  frame: LocalFrame, currentItem: null);
    }

    /// <summary>Advances the focus; returns false at end of sequence.</summary>
    /// <returns><see langword="true"/> while items remain; <see langword="false"/> at end of sequence.</returns>
    public bool AdvanceFocus()
    {
        if (_focusIterator is null)
        {
            return false;
        }

        XdmItem? next = _focusIterator.Next();
        if (next is null)
        {
            return false;
        }

        ContextItem = next;
        _contextPosition++;
        return true;
    }

    /// <summary>
    /// Returns a new context with a fresh zero-filled local-variable frame of the given size.
    /// Reads before writes return <see cref="XdmSequence.Empty"/>.
    /// The focus and context item from this context are preserved in the child.
    /// </summary>
    /// <param name="slotCount">Number of local variable slots to allocate.</param>
    /// <returns>A new child context sharing focus and <see cref="RunState"/>.</returns>
    public DynamicContext WithFrame(int slotCount)
    {
        XdmSequence[] frame = slotCount == 0 ? s_emptyFrame : new XdmSequence[slotCount];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = XdmSequence.Empty;
        }

        return new DynamicContext(Run, _focusIterator, ContextItem, _contextPosition, frame, CurrentItem);
    }

    /// <summary>Throws <see cref="OperationCanceledException"/> if the run's token is cancelled.</summary>
    public void CheckCancellation() => Run.Cancellation.ThrowIfCancellationRequested();

    /// <summary>Shared empty frame used by all contexts with zero local slots.</summary>
    private static readonly XdmSequence[] s_emptyFrame = Array.Empty<XdmSequence>();
}
