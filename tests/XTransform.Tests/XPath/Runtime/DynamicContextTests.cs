using System.Threading;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath;

namespace XTransform.Tests.XPath.Runtime;

/// <summary>
/// Tests for <see cref="DynamicContext"/>: Root factory, focus management,
/// context-size lazy computation, local-variable frames and cancellation.
/// </summary>
[TestFixture]
internal sealed class DynamicContextTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RunState MakeRun() => new RunState();

    /// <summary>
    /// Wraps an <see cref="ArrayIterator"/> and records how many times
    /// <see cref="Clone"/> and <see cref="IXdmIterator.Next"/> are called.
    /// </summary>
    private sealed class TrackingIterator : IXdmIterator
    {
        private readonly XdmItem[] _items;
        private int _index;
        private int _position;

        /// <summary>Total calls to <see cref="Clone"/> on the original iterator.</summary>
        public int CloneCalls { get; private set; }

        /// <summary>Total calls to <see cref="IXdmIterator.Next"/> across both original and clones.</summary>
        public int NextCalls { get; private set; }

        public TrackingIterator(params XdmItem[] items)
        {
            _items = items;
        }

        public XdmItem? Next()
        {
            NextCalls++;
            if (_index >= _items.Length)
            {
                return null;
            }

            XdmItem item = _items[_index];
            _index++;
            _position++;
            return item;
        }

        public int Position => _position;

        public IXdmIterator Clone()
        {
            CloneCalls++;
            // Return a fresh tracking clone that shares the same counters back to the original.
            return new TrackingClone(_items, this);
        }

        private sealed class TrackingClone : IXdmIterator
        {
            private readonly XdmItem[] _items;
            private readonly TrackingIterator _owner;
            private int _index;
            private int _position;

            internal TrackingClone(XdmItem[] items, TrackingIterator owner)
            {
                _items = items;
                _owner = owner;
            }

            public XdmItem? Next()
            {
                _owner.NextCalls++;
                if (_index >= _items.Length)
                {
                    return null;
                }

                XdmItem item = _items[_index];
                _index++;
                _position++;
                return item;
            }

            public int Position => _position;

            public IXdmIterator Clone()
            {
                _owner.CloneCalls++;
                return new TrackingClone(_items, _owner);
            }
        }
    }

    private static XdmItem[] MakeItems(int count)
    {
        XdmItem[] items = new XdmItem[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = XdmAtomicValue.Integer((long)(i + 1));
        }

        return items;
    }

    // ── Root ──────────────────────────────────────────────────────────────────

    [Test]
    public void Root_HasNoContextItem_PositionIsZero_SizeIsZero()
    {
        // Arrange
        RunState run = MakeRun();

        // Act
        DynamicContext ctx = DynamicContext.Root(run);

        // Assert
        Assert.That(ctx.ContextItem, Is.Null);
        Assert.That(ctx.ContextPosition, Is.EqualTo(0));
        Assert.That(ctx.ContextSize, Is.EqualTo(0));
    }

    // ── WithFocus / AdvanceFocus ──────────────────────────────────────────────

    [Test]
    public void WithFocus_AdvanceFocus_IteratesItems()
    {
        // Arrange
        XdmItem[] items = MakeItems(3);
        ArrayIterator iter = new ArrayIterator(items);
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithFocus(iter);

        // Act + Assert — advance through all three items then get false
        Assert.That(ctx.AdvanceFocus(), Is.True);
        Assert.That(ctx.ContextItem, Is.SameAs(items[0]));

        Assert.That(ctx.AdvanceFocus(), Is.True);
        Assert.That(ctx.ContextItem, Is.SameAs(items[1]));

        Assert.That(ctx.AdvanceFocus(), Is.True);
        Assert.That(ctx.ContextItem, Is.SameAs(items[2]));

        Assert.That(ctx.AdvanceFocus(), Is.False);
    }

    [Test]
    public void ContextPosition_IncrementsPerAdvance_Resets_WhenWithFocusCalledAgain()
    {
        // Arrange
        XdmItem[] items = MakeItems(2);
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithFocus(new ArrayIterator(items));

        // Act — advance twice on the first focus context
        ctx.AdvanceFocus();
        ctx.AdvanceFocus();
        Assert.That(ctx.ContextPosition, Is.EqualTo(2));

        // Create a fresh focus context from the same root — position resets
        DynamicContext ctx2 = DynamicContext.Root(MakeRun()).WithFocus(new ArrayIterator(items));
        Assert.That(ctx2.ContextPosition, Is.EqualTo(0));
    }

    // ── ContextSize — lazy computation ────────────────────────────────────────

    [Test]
    public void ContextSize_ComputedLazilyOnFirstRead()
    {
        // Arrange — 3-item sequence; we expect Clone called once, Next called 4 times (3 items + 1 null sentinel)
        XdmItem[] items = MakeItems(3);
        TrackingIterator tracker = new TrackingIterator(items);
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithFocus(tracker);

        // Act — first read computes size
        int size1 = ctx.ContextSize;

        // Assert — clone was called once; next was called 4 times on the clone
        Assert.That(size1, Is.EqualTo(3));
        Assert.That(tracker.CloneCalls, Is.EqualTo(1));
        Assert.That(tracker.NextCalls, Is.EqualTo(4)); // 3 items + 1 null

        // Second read must not call Next again (cached)
        int nextCallsBefore = tracker.NextCalls;
        int size2 = ctx.ContextSize;
        Assert.That(size2, Is.EqualTo(3));
        Assert.That(tracker.NextCalls, Is.EqualTo(nextCallsBefore));
    }

    [Test]
    public void ContextSize_WithContextItem_UsesExplicitSize()
    {
        // Arrange — WithContextItem pins size without any iterator
        XdmAtomicValue item = XdmAtomicValue.Integer(99L);
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithContextItem(item, 2, 5);

        // Act / Assert — no iterator reads; size comes directly from the argument
        Assert.That(ctx.ContextSize, Is.EqualTo(5));
        Assert.That(ctx.ContextPosition, Is.EqualTo(2));
        Assert.That(ctx.ContextItem, Is.SameAs(item));
    }

    // ── WithFrame ─────────────────────────────────────────────────────────────

    [Test]
    public void WithFrame_InitialisesAllSlotsToEmpty()
    {
        // Arrange
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithFrame(3);

        // Act / Assert
        Assert.That(ctx.LocalFrame.Length, Is.EqualTo(3));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(ctx.LocalFrame[i], Is.SameAs(XdmSequence.Empty));
        }
    }

    [Test]
    public void WithFrame_Zero_ReturnsEmptyFrame()
    {
        // Arrange
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithFrame(0);

        // Act / Assert
        Assert.That(ctx.LocalFrame.Length, Is.EqualTo(0));
    }

    [Test]
    public void WithFrame_PreservesFocusAndContextItem()
    {
        // Arrange — advance one item into a focus then allocate a frame
        XdmItem[] items = MakeItems(2);
        DynamicContext focusCtx = DynamicContext.Root(MakeRun()).WithFocus(new ArrayIterator(items));
        focusCtx.AdvanceFocus(); // position = 1, contextItem = items[0]

        // Act
        DynamicContext frameCtx = focusCtx.WithFrame(2);

        // Assert — focus state is preserved in the new context
        Assert.That(frameCtx.ContextItem, Is.SameAs(items[0]));
        Assert.That(frameCtx.ContextPosition, Is.EqualTo(1));
        Assert.That(frameCtx.LocalFrame.Length, Is.EqualTo(2));
    }

    // ── WithContextItem ───────────────────────────────────────────────────────

    [Test]
    public void WithContextItem_PinsFocus_ReportsExactPositionAndSize()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.String("hello");

        // Act
        DynamicContext ctx = DynamicContext.Root(MakeRun()).WithContextItem(item, 3, 7);

        // Assert
        Assert.That(ctx.ContextItem, Is.SameAs(item));
        Assert.That(ctx.ContextPosition, Is.EqualTo(3));
        Assert.That(ctx.ContextSize, Is.EqualTo(7));
    }

    // ── CheckCancellation ─────────────────────────────────────────────────────

    [Test]
    public void CheckCancellation_ThrowsWhenCancelled()
    {
        // Arrange
        using CancellationTokenSource cts = new CancellationTokenSource();
        DynamicContext ctx = DynamicContext.Root(new RunState(cts.Token));
        cts.Cancel();

        // Act / Assert
        Assert.Throws<System.OperationCanceledException>(() => ctx.CheckCancellation());
    }

    [Test]
    public void CheckCancellation_NoOpWhenNotCancelled()
    {
        // Arrange
        DynamicContext ctx = DynamicContext.Root(MakeRun());

        // Act / Assert — should not throw
        Assert.DoesNotThrow(() => ctx.CheckCancellation());
    }

    // ── Functional immutability ───────────────────────────────────────────────

    [Test]
    public void WithFocus_ReturnsNewContext_LeavesOriginalUnchanged()
    {
        // Arrange
        DynamicContext original = DynamicContext.Root(MakeRun());
        XdmItem[] items = MakeItems(2);

        // Act — create a child context and advance its focus
        DynamicContext child = original.WithFocus(new ArrayIterator(items));
        child.AdvanceFocus();

        // Assert — original context is unchanged
        Assert.That(original.ContextPosition, Is.EqualTo(0));
        Assert.That(original.ContextItem, Is.Null);
    }
}
