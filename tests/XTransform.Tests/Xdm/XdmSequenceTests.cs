using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.Tests.Xdm;

[TestFixture]
public sealed class XdmSequenceTests
{
    // =========================================================================
    // XdmSequence.Empty
    // =========================================================================

    [Test]
    public void Empty_IsReferenceIdenticalAcrossCalls()
    {
        // Arrange / Act
        XdmSequence first = XdmSequence.Empty;
        XdmSequence second = XdmSequence.Empty;

        // Assert
        Assert.That(ReferenceEquals(first, second), Is.True, "Empty must be a singleton.");
    }

    [Test]
    public void Empty_Count_IsZero()
    {
        // Arrange / Act / Assert
        Assert.That(XdmSequence.Empty.Count, Is.EqualTo(0));
    }

    [Test]
    public void Empty_IsEmpty_IsTrue()
    {
        // Arrange / Act / Assert
        Assert.That(XdmSequence.Empty.IsEmpty, Is.True);
    }

    [Test]
    public void Empty_Iterator_YieldsNothing()
    {
        // Arrange
        IXdmIterator it = XdmSequence.Empty.GetIterator();

        // Act / Assert
        Assert.That(it.Next(), Is.Null);
    }

    [Test]
    public void Empty_Indexer_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => { XdmItem _ = XdmSequence.Empty[0]; });
    }

    [Test]
    public void Empty_FirstOrNull_ReturnsNull()
    {
        // Arrange / Act / Assert
        Assert.That(XdmSequence.Empty.FirstOrNull(), Is.Null);
    }

    [Test]
    public void Empty_GetEnumerator_YieldsNoItems()
    {
        // Arrange
        List<XdmItem> items = new List<XdmItem>();

        // Act
        foreach (XdmItem item in XdmSequence.Empty)
        {
            items.Add(item);
        }

        // Assert
        Assert.That(items, Is.Empty);
    }

    // =========================================================================
    // XdmSequence.Of(XdmItem) — singleton sequence
    // =========================================================================

    [Test]
    public void Of_SingleItem_CountIsOne()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(1L);

        // Act
        XdmSequence seq = XdmSequence.Of(item);

        // Assert
        Assert.That(seq.Count, Is.EqualTo(1));
    }

    [Test]
    public void Of_SingleItem_IndexZeroReturnsItem()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Boolean(true);
        XdmSequence seq = XdmSequence.Of(item);

        // Act / Assert
        Assert.That(seq[0], Is.SameAs(item));
    }

    [Test]
    public void Of_SingleItem_IndexOneThrows()
    {
        // Arrange
        XdmSequence seq = XdmSequence.Of(XdmAtomicValue.Boolean(false));

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => { XdmItem _ = seq[1]; });
    }

    [Test]
    public void Of_SingleItem_IteratorYieldsItemOnce()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.String("hello");
        XdmSequence seq = XdmSequence.Of(item);
        IXdmIterator it = seq.GetIterator();

        // Act
        XdmItem? first = it.Next();
        XdmItem? second = it.Next();

        // Assert
        Assert.That(first, Is.SameAs(item));
        Assert.That(second, Is.Null);
    }

    [Test]
    public void Of_SingleItem_FirstOrNull_ReturnsItem()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(42L);

        // Act / Assert
        Assert.That(XdmSequence.Of(item).FirstOrNull(), Is.SameAs(item));
    }

    // =========================================================================
    // XdmSequence.Of(params XdmItem[]) — array-backed sequence
    // =========================================================================

    [Test]
    public void Of_EmptyArray_ReturnsEmptySingleton()
    {
        // Arrange / Act
        XdmSequence seq = XdmSequence.Of();

        // Assert
        Assert.That(ReferenceEquals(seq, XdmSequence.Empty), Is.True);
    }

    [Test]
    public void Of_SingleElementArray_CountIsOne()
    {
        // Arrange
        XdmAtomicValue item = XdmAtomicValue.Integer(99L);

        // Act
        XdmSequence seq = XdmSequence.Of(item);

        // Assert
        Assert.That(seq.Count, Is.EqualTo(1));
    }

    [Test]
    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(2L)]
    public void Of_MultipleItems_IndexerReturnsCorrectItem(long value)
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(0L);
        XdmAtomicValue b = XdmAtomicValue.Integer(1L);
        XdmAtomicValue c = XdmAtomicValue.Integer(2L);
        XdmSequence seq = XdmSequence.Of(a, b, c);
        int index = (int)value;

        // Act / Assert
        Assert.That(((XdmAtomicValue)seq[index]).AsInt64(), Is.EqualTo(value));
    }

    [Test]
    public void Of_MultipleItems_GetEnumeratorYieldsAllInOrder()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.String("a");
        XdmAtomicValue b = XdmAtomicValue.String("b");
        XdmAtomicValue c = XdmAtomicValue.String("c");
        XdmSequence seq = XdmSequence.Of(a, b, c);
        List<XdmItem> collected = new List<XdmItem>();

        // Act
        foreach (XdmItem item in seq)
        {
            collected.Add(item);
        }

        // Assert
        Assert.That(collected, Is.EqualTo(new[] { a, b, c }));
    }

    [Test]
    public void Of_MultipleItems_FirstOrNull_ReturnsFirst()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(100L);
        XdmAtomicValue b = XdmAtomicValue.Integer(200L);
        XdmSequence seq = XdmSequence.Of(a, b);

        // Act / Assert
        Assert.That(seq.FirstOrNull(), Is.SameAs(a));
    }

    // =========================================================================
    // XdmSequence.From(IEnumerable<XdmItem>)
    // =========================================================================

    [Test]
    public void From_Empty_ReturnsEmptySingleton()
    {
        // Arrange
        List<XdmItem> source = new List<XdmItem>();

        // Act
        XdmSequence seq = XdmSequence.From(source);

        // Assert
        Assert.That(ReferenceEquals(seq, XdmSequence.Empty), Is.True);
    }

    [Test]
    public void From_ThreeItems_PreservesOrder()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);
        List<XdmItem> source = new List<XdmItem> { a, b, c };

        // Act
        XdmSequence seq = XdmSequence.From(source);

        // Assert
        Assert.That(seq.Count, Is.EqualTo(3));
        Assert.That(seq[0], Is.SameAs(a));
        Assert.That(seq[1], Is.SameAs(b));
        Assert.That(seq[2], Is.SameAs(c));
    }

    [Test]
    public void From_LinqOpsWork()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmSequence seq = XdmSequence.From(new XdmItem[] { a, b });
        List<XdmItem> collected = new List<XdmItem>();

        // Act — use IEnumerable<XdmItem> foreach (IEnumerable works via GetEnumerator)
        foreach (XdmItem item in seq)
        {
            collected.Add(item);
        }

        // Assert
        Assert.That(collected.Count, Is.EqualTo(2));
    }

    // =========================================================================
    // Lazy sequence (XdmSequence.Lazy) — normal cases
    // =========================================================================

    [Test]
    public void Lazy_FirstOrNull_ReturnsFirstItemWithoutDrainingIterator()
    {
        // Arrange — 3 items; we only want the first
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);
        IXdmIterator source = new ArrayIterator(new XdmItem[] { a, b, c });
        XdmSequence seq = XdmSequence.Lazy(source);

        // Act
        XdmItem? first = seq.FirstOrNull();

        // Assert
        Assert.That(first, Is.SameAs(a));
    }

    [Test]
    public void Lazy_GetEnumerator_ReturnsAllItems()
    {
        // Arrange
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);
        XdmSequence seq = XdmSequence.Lazy(new ArrayIterator(new XdmItem[] { a, b, c }));
        List<XdmItem> collected = new List<XdmItem>();

        // Act
        foreach (XdmItem item in seq)
        {
            collected.Add(item);
        }

        // Assert
        Assert.That(collected, Is.EqualTo(new[] { a, b, c }));
    }

    [Test]
    public void Lazy_AfterFirstOrNull_GetEnumeratorStillReturnsAllItems()
    {
        // Arrange — FirstOrNull pulls one item into the buffer.
        // GetEnumerator (which calls GetIterator and drains) must still return all items.
        XdmAtomicValue a = XdmAtomicValue.Integer(10L);
        XdmAtomicValue b = XdmAtomicValue.Integer(20L);
        XdmAtomicValue c = XdmAtomicValue.Integer(30L);
        XdmSequence seq = XdmSequence.Lazy(new ArrayIterator(new XdmItem[] { a, b, c }));

        // Act
        XdmItem? first = seq.FirstOrNull(); // pulls one into buffer
        List<XdmItem> collected = new List<XdmItem>();
        foreach (XdmItem item in seq)
        {
            collected.Add(item);
        }

        // Assert
        Assert.That(first, Is.SameAs(a));
        Assert.That(collected, Is.EqualTo(new[] { a, b, c }), "GetEnumerator must return ALL items including the first.");
    }

    // =========================================================================
    // Acceptance test: lazy over infinite iterator — must not hang
    // =========================================================================

    [Test]
    [CancelAfter(10000)]
    public void Lazy_FirstOrNull_InfiniteIterator_ReturnsFirstItemWithoutHanging(CancellationToken _)
    {
        // Arrange — an iterator that never returns null
        IXdmIterator infinite = new InfiniteCountingIterator();
        XdmSequence seq = XdmSequence.Lazy(infinite);

        // Act
        XdmItem? first = seq.FirstOrNull();

        // Assert — first item is Integer(1)
        Assert.That(first, Is.Not.Null);
        Assert.That(((XdmAtomicValue)first!).AsInt64(), Is.EqualTo(1L));
    }

    // =========================================================================
    // Acceptance test: lazy Count memoised — iterator is drained only once
    // =========================================================================

    [Test]
    public void Lazy_Count_IsMemoisedAndIteratorDrainedOnce()
    {
        // Arrange
        CountingIterator source = new CountingIterator(5);
        XdmSequence seq = XdmSequence.Lazy(source);

        // Act — call Count twice
        int countFirst = seq.Count;
        int countSecond = seq.Count;

        // Assert
        Assert.That(countFirst, Is.EqualTo(5), "Count must equal the number of items.");
        Assert.That(countSecond, Is.EqualTo(5), "Second Count call must return the same value.");
        // 5 items + 1 null sentinel = 6 calls total, NOT 12 (which would indicate double drain)
        Assert.That(source.NextCallCount, Is.EqualTo(6),
            $"Next() must have been called exactly 6 times (5 items + 1 null); was {source.NextCallCount}.");
    }

    // =========================================================================
    // Acceptance test: Clone() restarts iteration for all iterator types
    // =========================================================================

    [Test]
    [TestCaseSource(nameof(CloneTestCases))]
    public void Clone_RestartsFromBeginning(IXdmIterator iterator, XdmItem[] expectedItems)
    {
        // Arrange — advance iterator partway
        iterator.Next();

        // Act
        IXdmIterator clone = iterator.Clone();
        List<XdmItem> cloneResults = new List<XdmItem>();
        XdmItem? item;
        while ((item = clone.Next()) != null)
        {
            cloneResults.Add(item);
        }

        // Assert
        Assert.That(cloneResults, Is.EqualTo(expectedItems),
            "Clone must produce all items from the beginning.");
        Assert.That(clone.Position, Is.EqualTo(expectedItems.Length));
    }

    private static IEnumerable<TestCaseData> CloneTestCases()
    {
        XdmAtomicValue a = XdmAtomicValue.Integer(1L);
        XdmAtomicValue b = XdmAtomicValue.Integer(2L);
        XdmAtomicValue c = XdmAtomicValue.Integer(3L);

        yield return new TestCaseData(EmptyIterator.Instance, Array.Empty<XdmItem>())
            .SetName("Clone_EmptyIterator_RestartsFromBeginning");

        yield return new TestCaseData(new SingleItemIterator(a), new XdmItem[] { a })
            .SetName("Clone_SingleItemIterator_RestartsFromBeginning");

        yield return new TestCaseData(new ArrayIterator(new XdmItem[] { a, b, c }), new XdmItem[] { a, b, c })
            .SetName("Clone_ArrayIterator_RestartsFromBeginning");

        yield return new TestCaseData(
            new ConcatIterator(new IXdmIterator[] { new SingleItemIterator(a), new SingleItemIterator(b) }),
            new XdmItem[] { a, b })
            .SetName("Clone_ConcatIterator_RestartsFromBeginning");

        yield return new TestCaseData(
            new MappingIterator(new ArrayIterator(new XdmItem[] { a, b }), item => new SingleItemIterator(item)),
            new XdmItem[] { a, b })
            .SetName("Clone_MappingIterator_RestartsFromBeginning");
    }

    // =========================================================================
    // Helper iterator: infinite counter
    // =========================================================================

    /// <summary>An iterator that yields <c>XdmAtomicValue.Integer(n)</c> for n = 1, 2, 3, ... forever.</summary>
    private sealed class InfiniteCountingIterator : IXdmIterator
    {
        private long _n;

        internal InfiniteCountingIterator(long start = 1) => _n = start;

        public XdmItem? Next() => XdmAtomicValue.Integer(_n++);

        public int Position => (int)(_n - 1);

        public IXdmIterator Clone() => new InfiniteCountingIterator();
    }

    // =========================================================================
    // Helper iterator: limited counter that tracks Next() calls
    // =========================================================================

    /// <summary>Produces <paramref name="count"/> integer items and tracks <see cref="NextCallCount"/>.</summary>
    private sealed class CountingIterator : IXdmIterator
    {
        private readonly int _count;
        private int _yielded;
        private int _nextCallCount;

        internal CountingIterator(int count) => _count = count;

        /// <summary>Total number of times <see cref="Next"/> has been called.</summary>
        internal int NextCallCount => _nextCallCount;

        public XdmItem? Next()
        {
            _nextCallCount++;
            if (_yielded < _count)
            {
                return XdmAtomicValue.Integer((long)(++_yielded));
            }

            return null;
        }

        public int Position => _yielded;

        public IXdmIterator Clone() => new CountingIterator(_count);
    }
}
