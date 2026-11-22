// Adapter that wraps an IEnumerator<XdmItem> produced by a yield-return method and
// exposes it as an IXdmIterator. The enumerator factory is captured so Clone() can
// restart iteration from the beginning.

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// Adapts a lazy <see cref="IEnumerator{XdmItem}"/> (typically produced by a
/// <c>yield return</c> method on an ExpressionNode) to the <see cref="IXdmIterator"/>
/// pull-iterator interface used by the evaluator. Clone restarts iteration via the
/// captured factory.
/// </summary>
internal sealed class EnumeratorIterator : IXdmIterator
{
    private readonly Func<IEnumerator<XdmItem>> _factory;
    private IEnumerator<XdmItem>? _current;
    private int _position;

    /// <summary>Initializes a new adapter. The factory is called once for this iterator.</summary>
    public EnumeratorIterator(Func<IEnumerator<XdmItem>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _current = factory();
    }

    /// <inheritdoc/>
    public int Position => _position;

    /// <inheritdoc/>
    public XdmItem? Next()
    {
        if (_current is null)
        {
            return null;
        }
        if (!_current.MoveNext())
        {
            _current.Dispose();
            _current = null;
            return null;
        }
        _position++;
        return _current.Current;
    }

    /// <inheritdoc/>
    public IXdmIterator Clone() => new EnumeratorIterator(_factory);
}
