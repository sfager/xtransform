// XPath 2.0 §2.5.4 — predicates

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.ExpressionNodes;

namespace XTransform.XPath.Operators;

/// <summary>
/// Applies a predicate filter to a source iterator per XPath 2.0 §2.5.4.
/// </summary>
internal static class PredicateApplicator
{
    /// <summary>
    /// Wraps <paramref name="source"/> with a lazy predicate filter.
    /// </summary>
    /// <remarks>
    /// XPath 2.0 §2.5.4 predicate semantics:
    /// <list type="bullet">
    ///   <item>Integer-literal predicate [N] → take the N-th item (1-based, lazy).</item>
    ///   <item>Numeric singleton result → match when value equals the context position.</item>
    ///   <item>Otherwise → effective boolean value.</item>
    /// </list>
    /// </remarks>
    /// <param name="source">The input sequence to filter.</param>
    /// <param name="predicate">The compiled predicate expression.</param>
    /// <param name="parentContext">The surrounding dynamic context (frame + run-state).</param>
    /// <returns>A lazy iterator over the items that satisfy the predicate.</returns>
    internal static IXdmIterator Apply(
        IXdmIterator source,
        ExpressionNode predicate,
        DynamicContext parentContext)
    {
        // XPath 2.0 §2.5.4 — numeric-literal shortcut: [N] where N > 0.
        // A LiteralNode whose value is a positive xs:integer can be satisfied by
        // consuming exactly N items from the source (lazy, constant memory).
        if (predicate is LiteralNode lit
            && lit.Value.TypeCode == XdmTypeCode.Integer
            && lit.Value.AsInt64() > 0)
        {
            long n = lit.Value.AsInt64();
            return new PositionalFilterIterator(source, n);
        }

        // General predicate: needs context position and optionally context size.
        // If the predicate references last(), we must know the total count before
        // iterating — buffer or clone-count the source.
        if ((predicate.Dependencies & ExpressionDependencies.Last) != ExpressionDependencies.None)
        {
            // Pre-count the source, then re-create a new iterator via Clone.
            IXdmIterator countClone = source.Clone();
            int size = 0;
            while (countClone.Next() is not null)
            {
                size++;
            }

            return new GeneralPredicateIterator(source, predicate, parentContext, size);
        }

        // No last() dependency: context size is unknown until we're done (or not needed).
        return new GeneralPredicateIterator(source, predicate, parentContext, -1);
    }

    // ── Positional filter (integer literal shortcut) ───────────────────────────

    /// <summary>
    /// Lazy iterator that skips to the N-th item (1-based) and yields only it.
    /// Satisfies the lazy-evaluation requirement for <c>(1 to 1000000)[1]</c>.
    /// </summary>
    private sealed class PositionalFilterIterator : IXdmIterator
    {
        private readonly IXdmIterator _source;
        private readonly long _target;    // 1-based target position
        private bool _done;
        private int _position;

        internal PositionalFilterIterator(IXdmIterator source, long target)
        {
            _source = source;
            _target = target;
        }

        /// <inheritdoc/>
        public int Position => _position;

        /// <inheritdoc/>
        public XdmItem? Next()
        {
            if (_done)
            {
                return null;
            }

            long consumed = 0;
            while (true)
            {
                XdmItem? item = _source.Next();
                if (item is null)
                {
                    _done = true;
                    return null;
                }

                consumed++;
                if (consumed == _target)
                {
                    _done = true;    // only yield one item
                    _position++;
                    return item;
                }
            }
        }

        /// <inheritdoc/>
        public IXdmIterator Clone() => new PositionalFilterIterator(_source.Clone(), _target);
    }

    // ── General predicate iterator ─────────────────────────────────────────────

    /// <summary>
    /// Lazy iterator that evaluates the predicate for each source item
    /// with the item's 1-based position in the INPUT sequence as context position.
    /// </summary>
    private sealed class GeneralPredicateIterator : IXdmIterator
    {
        private readonly IXdmIterator _source;
        private readonly ExpressionNode _predicate;
        private readonly DynamicContext _parentContext;
        private readonly int _size;         // -1 when unknown (no last() dependency)
        private int _inputPosition;         // 1-based position in source
        private int _outputPosition;        // 1-based position of items yielded

        internal GeneralPredicateIterator(
            IXdmIterator source,
            ExpressionNode predicate,
            DynamicContext parentContext,
            int size)
        {
            _source = source;
            _predicate = predicate;
            _parentContext = parentContext;
            _size = size;
        }

        /// <inheritdoc/>
        public int Position => _outputPosition;

        /// <inheritdoc/>
        public XdmItem? Next()
        {
            // XPath 2.0 §2.5.4 — iterate source, evaluate predicate for each item.
            while (true)
            {
                XdmItem? item = _source.Next();
                if (item is null)
                {
                    return null;
                }

                _inputPosition++;
                int sizeToReport = _size >= 0 ? _size : 0;

                // Bind context: position = inputPosition, size = sizeToReport (or 0 if unknown).
                DynamicContext predicateCtx = _parentContext.WithContextItem(item, _inputPosition, sizeToReport);

                if (EvaluatePredicate(item, predicateCtx))
                {
                    _outputPosition++;
                    return item;
                }
            }
        }

        private bool EvaluatePredicate(XdmItem item, DynamicContext predicateCtx)
        {
            // XPath 2.0 §2.5.4 — numeric result → compare with context position.
            XdmItem? resultItem = _predicate.EvaluateItem(predicateCtx);

            if (resultItem is XdmAtomicValue atom)
            {
                // If the predicate result is numeric, compare with context position.
                if (IsNumericType(atom.TypeCode))
                {
                    // Convert to double for comparison (handles integers, decimals, etc.)
                    double numVal = ToDouble(atom);
                    return numVal == _inputPosition;
                }
            }

            // Non-numeric (or empty): effective boolean value.
            return _predicate.EffectiveBooleanValue(predicateCtx);
        }

        private static bool IsNumericType(XdmTypeCode t)
        {
            return t == XdmTypeCode.Integer
                || t == XdmTypeCode.Decimal
                || t == XdmTypeCode.Double
                || t == XdmTypeCode.Float
                || CastTable.IsSubtypeOf(t, XdmTypeCode.Integer)
                || CastTable.IsSubtypeOf(t, XdmTypeCode.Decimal);
        }

        private static double ToDouble(XdmAtomicValue v)
        {
            switch (v.TypeCode)
            {
                case XdmTypeCode.Double:
                    return v.AsDouble();
                case XdmTypeCode.Float:
                    return v.AsFloat();
                case XdmTypeCode.Decimal:
                    return (double)v.AsDecimal().Value;
                default:
                    // Integer family
                    return (double)(long)v.AsInt64();
            }
        }

        /// <inheritdoc/>
        public IXdmIterator Clone()
            => new GeneralPredicateIterator(_source.Clone(), _predicate, _parentContext, _size);
    }
}
