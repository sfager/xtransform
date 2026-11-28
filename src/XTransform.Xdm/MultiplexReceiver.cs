using System;
using System.Collections.Generic;

namespace XTransform.Xdm;

/// <summary>
/// Fans every <see cref="IResultReceiver"/> event out to each registered sink in
/// registration order. <see cref="Close"/> is idempotent: subsequent calls are no-ops, and
/// any sink implementing <see cref="IDisposable"/> is disposed exactly once.
/// </summary>
internal sealed class MultiplexReceiver : IResultReceiver, IDisposable
{
    private readonly IResultReceiver[] _sinks;
    private bool _closed;

    /// <summary>Initializes the multiplexer with the given sinks.</summary>
    public MultiplexReceiver(params IResultReceiver[] sinks)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (sinks is null)
        {
            throw new ArgumentNullException(nameof(sinks));
        }
#pragma warning restore CA1510

        _sinks = new IResultReceiver[sinks.Length];
        for (int i = 0; i < sinks.Length; i++)
        {
            _sinks[i] = sinks[i] ?? throw new ArgumentNullException(nameof(sinks));
        }
    }

    /// <summary>Convenience overload taking an <see cref="IEnumerable{T}"/>.</summary>
    public MultiplexReceiver(IEnumerable<IResultReceiver> sinks)
        : this(ToArray(sinks)) { }

    private static IResultReceiver[] ToArray(IEnumerable<IResultReceiver> sinks)
    {
#pragma warning disable CA1510
        if (sinks is null)
        {
            throw new ArgumentNullException(nameof(sinks));
        }
#pragma warning restore CA1510

        List<IResultReceiver> list = new List<IResultReceiver>();
        foreach (IResultReceiver s in sinks) { list.Add(s); }
        return list.ToArray();
    }

    /// <summary>The number of registered sinks.</summary>
    public int SinkCount => _sinks.Length;

    /// <inheritdoc/>
    public void StartDocument()
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].StartDocument(); }
    }

    /// <inheritdoc/>
    public void EndDocument()
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].EndDocument(); }
    }

    /// <inheritdoc/>
    public void StartElement(int nameCode, SourceSpan location)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].StartElement(nameCode, location); }
    }

    /// <inheritdoc/>
    public void BaseUri(string baseUri)
    {
        for (int i = 0; i < _sinks.Length; i++) { (_sinks[i] as IBaseUriReceiver)?.BaseUri(baseUri); }
    }

    /// <inheritdoc/>
    public void Namespace(string prefix, string uri)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].Namespace(prefix, uri); }
    }

    /// <inheritdoc/>
    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].Attribute(nameCode, value, location); }
    }

    /// <inheritdoc/>
    public void EndElement()
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].EndElement(); }
    }

    /// <inheritdoc/>
    public void Text(string text)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].Text(text); }
    }

    /// <inheritdoc/>
    public void Comment(string text)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].Comment(text); }
    }

    /// <inheritdoc/>
    public void ProcessingInstruction(string target, string data)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].ProcessingInstruction(target, data); }
    }

    /// <inheritdoc/>
    public void Append(XdmItem item)
    {
        for (int i = 0; i < _sinks.Length; i++) { _sinks[i].Append(item); }
    }

    /// <summary>
    /// Closes the multiplexer. Each sink implementing <see cref="IDisposable"/> is disposed
    /// exactly once regardless of how many times <see cref="Close"/> is called.
    /// </summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        for (int i = 0; i < _sinks.Length; i++)
        {
            if (_sinks[i] is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Close();
}
