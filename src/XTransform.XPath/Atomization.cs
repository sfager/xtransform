// F&O §2.4.2 — atomization (the fn:data operation).
//
// Atomization is applied wherever XPath needs atomic values from a sequence that may
// contain nodes: arithmetic operands, comparison operands, function-conversion args,
// cast / castable target expressions, and the fn:data function itself.
//
// For an untyped tree the typed value of an element / attribute / document / text /
// namespace is xs:untypedAtomic with the node's string value; the typed value of a
// comment or processing-instruction is xs:string with the node's string value. These
// rules are implemented on XdmNode.GetTypedValue in Phase 1.

using System;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath;

/// <summary>
/// Helpers that apply F&amp;O §2.4.2 atomization to items and sequences.
/// </summary>
internal static class Atomization
{
    /// <summary>
    /// Returns a lazy iterator that atomizes each item of <paramref name="source"/>:
    /// atomic items pass through; nodes are replaced by their typed value (one or more
    /// atomic values per node).
    /// </summary>
    public static IXdmIterator Atomize(IXdmIterator source)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull isn't available on netstandard2.0 / net462 without a polyfill.
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#pragma warning restore CA1510
        return new AtomizingIterator(source);
    }

    /// <summary>
    /// Atomizes <paramref name="item"/>. Returns <see langword="null"/> when the item is
    /// a node whose typed value is the empty sequence; throws <see cref="XPathDynamicException"/>
    /// with <c>XPTY0004</c> when the typed value has more than one item (callers that need
    /// the general case should use <see cref="Atomize"/>).
    /// </summary>
    public static XdmAtomicValue? AtomizeSingle(XdmItem? item, SourceSpan location)
    {
        if (item is null)
        {
            return null;
        }
        if (item is XdmAtomicValue atomic)
        {
            return atomic;
        }
        if (item is XdmNode node)
        {
            XdmSequence typed = node.GetTypedValue();
            IXdmIterator iter = typed.GetIterator();
            XdmItem? first = iter.Next();
            if (first is null)
            {
                return null;
            }
            if (iter.Next() is not null)
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    "Atomized node produced more than one atomic value",
                    location);
            }
            return (XdmAtomicValue)first;
        }
        throw new InvalidOperationException($"Unknown XdmItem subtype: {item.GetType().FullName}");
    }

    private sealed class AtomizingIterator : IXdmIterator
    {
        private readonly IXdmIterator _source;
        private IXdmIterator? _currentTyped;  // typed-value sub-iterator for a node
        private int _position;

        public AtomizingIterator(IXdmIterator source)
        {
            _source = source;
        }

        public int Position => _position;

        public XdmItem? Next()
        {
            while (true)
            {
                // Drain the current node's typed-value sub-iterator first.
                if (_currentTyped is not null)
                {
                    XdmItem? sub = _currentTyped.Next();
                    if (sub is not null)
                    {
                        _position++;
                        return sub;
                    }
                    _currentTyped = null;
                }

                XdmItem? next = _source.Next();
                if (next is null)
                {
                    return null;
                }
                if (next is XdmAtomicValue)
                {
                    _position++;
                    return next;
                }
                if (next is XdmNode node)
                {
                    _currentTyped = node.GetTypedValue().GetIterator();
                    continue;
                }
                throw new InvalidOperationException($"Unknown XdmItem subtype: {next.GetType().FullName}");
            }
        }

        public IXdmIterator Clone() => new AtomizingIterator(_source.Clone());
    }
}
