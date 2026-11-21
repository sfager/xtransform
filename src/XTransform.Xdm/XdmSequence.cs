// XPath 2.0 §2.1 — an XDM sequence is an ordered collection of zero or more items.
// Sequences are immutable from the caller's perspective; lazy sequences memoize on demand.

using System;
using System.Collections;
using System.Collections.Generic;
using XTransform.Xdm.Iterators;

namespace XTransform.Xdm;

/// <summary>
/// An immutable, ordered sequence of XDM items (item()*).
/// </summary>
/// <remarks>
/// <para>
/// Four concrete variants exist:
/// <list type="bullet">
///   <item><description><c>EmptyXdmSequence</c> — zero items.</description></item>
///   <item><description><c>SingletonXdmSequence</c> — exactly one item.</description></item>
///   <item><description><c>ArrayXdmSequence</c> — a fixed array of items.</description></item>
///   <item><description><c>LazyXdmSequence</c> — items produced on demand by an <see cref="IXdmIterator"/>
///     and memoised after first access.  <see cref="FirstOrNull"/> is cheap and does NOT
///     drain the iterator; <see cref="GetIterator"/> and index access DO drain it first.</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract class XdmSequence : IEnumerable<XdmItem>
{
    /// <summary>Prevents external derivation.</summary>
    private protected XdmSequence() { }

    // -------------------------------------------------------------------------
    // Static factory / singleton
    // -------------------------------------------------------------------------

    /// <summary>Gets the singleton empty sequence.</summary>
    public static XdmSequence Empty { get; } = new EmptyXdmSequence();

    /// <summary>Returns a sequence containing exactly <paramref name="item"/>.</summary>
    /// <param name="item">The single item.</param>
    /// <returns>A singleton sequence.</returns>
    public static XdmSequence Of(XdmItem item) => new SingletonXdmSequence(item);

    /// <summary>
    /// Returns a sequence backed by <paramref name="items"/>.
    /// A defensive copy of the array is taken.
    /// If <paramref name="items"/> is empty, <see cref="Empty"/> is returned.
    /// </summary>
    /// <param name="items">The items; must not be <see langword="null"/>.</param>
    /// <returns>An array-backed sequence, or <see cref="Empty"/> if the array is empty.</returns>
    public static XdmSequence Of(params XdmItem[] items)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(items);
#else
        if (items == null) { throw new ArgumentNullException(nameof(items)); }
#endif
        if (items.Length == 0) { return Empty; }
        if (items.Length == 1) { return new SingletonXdmSequence(items[0]); }

        XdmItem[] copy = new XdmItem[items.Length];
        Array.Copy(items, copy, items.Length);
        return new ArrayXdmSequence(copy);
    }

    /// <summary>
    /// Materialises <paramref name="items"/> into an array-backed sequence.
    /// If the enumerable is empty, <see cref="Empty"/> is returned.
    /// </summary>
    /// <param name="items">The source; must not be <see langword="null"/>.</param>
    /// <returns>An array-backed sequence, or <see cref="Empty"/>.</returns>
    public static XdmSequence From(IEnumerable<XdmItem> items)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(items);
#else
        if (items == null) { throw new ArgumentNullException(nameof(items)); }
#endif

        List<XdmItem> list = new List<XdmItem>();
        foreach (XdmItem item in items)
        {
            list.Add(item);
        }

        if (list.Count == 0) { return Empty; }
        if (list.Count == 1) { return new SingletonXdmSequence(list[0]); }
        return new ArrayXdmSequence(list.ToArray());
    }

    /// <summary>
    /// Wraps <paramref name="iterator"/> in a lazy, memoising sequence.
    /// <see cref="FirstOrNull"/> is cheap and does not drain the iterator.
    /// Any other access (<see cref="Count"/>, indexer, <see cref="GetIterator"/>,
    /// <see cref="GetEnumerator"/>) will drain the iterator completely on first access.
    /// </summary>
    /// <param name="iterator">The source iterator; ownership is transferred.</param>
    /// <returns>A lazy sequence.</returns>
    internal static XdmSequence Lazy(IXdmIterator iterator)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(iterator);
#else
        if (iterator == null) { throw new ArgumentNullException(nameof(iterator)); }
#endif
        return new LazyXdmSequence(iterator);
    }

    // -------------------------------------------------------------------------
    // Abstract / virtual members
    // -------------------------------------------------------------------------

    /// <summary>Gets the number of items in the sequence.</summary>
    public abstract int Count { get; }

    /// <summary>Gets a value indicating whether this sequence contains no items.</summary>
    public bool IsEmpty => Count == 0;

    /// <summary>Gets the item at the specified 0-based <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>The item at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="index"/> is negative or &gt;= <see cref="Count"/>.
    /// </exception>
    public abstract XdmItem this[int index] { get; }

    /// <summary>
    /// Returns the first item of the sequence without draining it, or
    /// <see langword="null"/> if the sequence is empty.
    /// For lazy sequences this is cheap: only one item is pulled from the source iterator.
    /// </summary>
    /// <returns>The first item, or <see langword="null"/>.</returns>
    public virtual XdmItem? FirstOrNull()
    {
        // Subclasses override this for the lazy case; default falls back to the iterator.
        IXdmIterator it = GetIterator();
        return it.Next();
    }

    /// <summary>Returns an iterator over this sequence.</summary>
    /// <returns>A fresh <see cref="IXdmIterator"/> starting at position 0.</returns>
    public abstract IXdmIterator GetIterator();

    /// <summary>Returns an enumerator that iterates over all items in the sequence.</summary>
    /// <returns>An <see cref="IEnumerator{T}"/> of <see cref="XdmItem"/>.</returns>
    public IEnumerator<XdmItem> GetEnumerator()
    {
        IXdmIterator it = GetIterator();
        XdmItem? item;
        while ((item = it.Next()) != null)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // =========================================================================
    // Concrete implementations (all internal)
    // =========================================================================

    // -------------------------------------------------------------------------
    // EmptyXdmSequence
    // -------------------------------------------------------------------------

    private sealed class EmptyXdmSequence : XdmSequence
    {
        internal EmptyXdmSequence() { }

        public override int Count => 0;

        public override XdmItem this[int index] =>
            throw new ArgumentOutOfRangeException(nameof(index), index, "Sequence is empty.");

        public override XdmItem? FirstOrNull() => null;

        public override IXdmIterator GetIterator() => EmptyIterator.Instance;
    }

    // -------------------------------------------------------------------------
    // SingletonXdmSequence
    // -------------------------------------------------------------------------

    private sealed class SingletonXdmSequence : XdmSequence
    {
        private readonly XdmItem _item;

        internal SingletonXdmSequence(XdmItem item) => _item = item;

        public override int Count => 1;

        public override XdmItem this[int index]
        {
            get
            {
                if (index != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be 0 for a singleton sequence.");
                }

                return _item;
            }
        }

        public override XdmItem? FirstOrNull() => _item;

        public override IXdmIterator GetIterator() => new SingleItemIterator(_item);
    }

    // -------------------------------------------------------------------------
    // ArrayXdmSequence
    // -------------------------------------------------------------------------

    private sealed class ArrayXdmSequence : XdmSequence
    {
        private readonly XdmItem[] _items;

        /// <param name="items">Already a defensive copy; ownership transferred.</param>
        internal ArrayXdmSequence(XdmItem[] items) => _items = items;

        public override int Count => _items.Length;

        public override XdmItem this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_items.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be in [0, {_items.Length}).");
                }

                return _items[index];
            }
        }

        public override XdmItem? FirstOrNull() => _items.Length > 0 ? _items[0] : null;

        public override IXdmIterator GetIterator() => new ArrayIterator(_items);
    }

    // -------------------------------------------------------------------------
    // LazyXdmSequence
    // -------------------------------------------------------------------------

    /// <summary>
    /// A lazy, memoising sequence backed by an <see cref="IXdmIterator"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Buffer states:</b> Fresh → Partial → Complete.</para>
    /// <para>
    /// <see cref="FirstOrNull"/> pulls at most one item from the source iterator and
    /// appends it to the buffer without draining the rest.  All other access paths
    /// (<see cref="Count"/>, indexer, <see cref="GetIterator"/>, <see cref="GetEnumerator"/>)
    /// fully drain the source on first call and then serve from the buffer.
    /// </para>
    /// <para>All buffer mutations are guarded by <c>_lock</c>.</para>
    /// </remarks>
    private sealed class LazyXdmSequence : XdmSequence
    {
        private readonly IXdmIterator _source;
        private readonly List<XdmItem> _buffer;
        private readonly object _lock = new object();
        private bool _complete;

        internal LazyXdmSequence(IXdmIterator source)
        {
            _source = source;
            _buffer = new List<XdmItem>();
            _complete = false;
        }

        /// <summary>
        /// Drains the source iterator completely into the buffer if not already done.
        /// Must be called under <c>_lock</c> or at a point where thread safety is guaranteed.
        /// </summary>
        private void EnsureComplete()
        {
            if (_complete) { return; }

            XdmItem? item;
            while ((item = _source.Next()) != null)
            {
                _buffer.Add(item);
            }

            _complete = true;
        }

        /// <summary>
        /// Returns the first item, pulling at most one from the source if the buffer is still empty.
        /// Does NOT drain the iterator.
        /// </summary>
        public override XdmItem? FirstOrNull()
        {
            lock (_lock)
            {
                if (_buffer.Count > 0) { return _buffer[0]; }
                if (_complete) { return null; }

                // Pull one item from source and buffer it.
                XdmItem? item = _source.Next();
                if (item == null)
                {
                    _complete = true;
                    return null;
                }

                _buffer.Add(item);
                return item;
            }
        }

        /// <summary>
        /// Drains the source (if not already done) and returns the item count.
        /// </summary>
        public override int Count
        {
            get
            {
                lock (_lock)
                {
                    EnsureComplete();
                    return _buffer.Count;
                }
            }
        }

        /// <summary>Gets the item at <paramref name="index"/>, draining source as needed.</summary>
        public override XdmItem this[int index]
        {
            get
            {
                if (index < 0) { throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative."); }

                lock (_lock)
                {
                    // Pull from source until the buffer is long enough or exhausted.
                    while (_buffer.Count <= index && !_complete)
                    {
                        XdmItem? item = _source.Next();
                        if (item == null)
                        {
                            _complete = true;
                            break;
                        }

                        _buffer.Add(item);
                    }

                    if (index >= _buffer.Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be in [0, {_buffer.Count}).");
                    }

                    return _buffer[index];
                }
            }
        }

        /// <summary>
        /// Drains the source completely and returns an array iterator over the buffer.
        /// </summary>
        public override IXdmIterator GetIterator()
        {
            lock (_lock)
            {
                EnsureComplete();
                return new ArrayIterator(_buffer.ToArray());
            }
        }
    }
}
