using System;
using System.Collections.Generic;
using System.Threading;

namespace XTransform.Xdm;

/// <summary>
/// Interns expanded names as integer codes so name tests are integer comparisons.
/// Thread-safe: one pool is shared by a compiled stylesheet and every transformation run
/// from it. Codes are stable for the pool's lifetime and never reused.
/// </summary>
internal sealed class NamePool
{
    public const int NoName = -1;

    // Name codes: (namespaceUri, localName) → code
    private readonly Dictionary<NameKey, int> _nameCodes = new Dictionary<NameKey, int>();
    private readonly List<NameEntry> _names = new List<NameEntry>();

    // URI codes: namespaceUri → code
    private readonly Dictionary<string, int> _uriCodes = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> _uris = new List<string>();

    private readonly object _lock = new object();

    public int Allocate(string namespaceUri, string localName)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (namespaceUri is null)
        {
            throw new ArgumentNullException(nameof(namespaceUri));
        }
        if (localName is null)
        {
            throw new ArgumentNullException(nameof(localName));
        }
#pragma warning restore CA1510

        var key = new NameKey(namespaceUri, localName);

        lock (_lock)
        {
            if (_nameCodes.TryGetValue(key, out int existing))
            {
                return existing;
            }

            int uriCode = AllocateUriLocked(namespaceUri);
            int code = _names.Count;
            _names.Add(new NameEntry(namespaceUri, localName, uriCode));
            _nameCodes[key] = code;
            return code;
        }
    }

    public int Allocate(QName name) => Allocate(name.NamespaceUri, name.LocalName);

    public bool TryGetCode(string namespaceUri, string localName, out int code)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (namespaceUri is null)
        {
            throw new ArgumentNullException(nameof(namespaceUri));
        }
        if (localName is null)
        {
            throw new ArgumentNullException(nameof(localName));
        }
#pragma warning restore CA1510

        lock (_lock)
        {
            return _nameCodes.TryGetValue(new NameKey(namespaceUri, localName), out code);
        }
    }

    public string GetNamespaceUri(int nameCode)
    {
        lock (_lock)
        {
            return _names[nameCode].Uri;
        }
    }

    public string GetLocalName(int nameCode)
    {
        lock (_lock)
        {
            return _names[nameCode].Local;
        }
    }

    public QName GetQName(int nameCode, string prefix = "")
    {
        lock (_lock)
        {
            NameEntry e = _names[nameCode];
            return new QName(prefix ?? "", e.Uri, e.Local);
        }
    }

    /// <summary>Code for the namespace URI alone, used by "ns:*" name tests.</summary>
    public int GetUriCode(int nameCode)
    {
        lock (_lock)
        {
            return _names[nameCode].UriCode;
        }
    }

    public int AllocateUri(string namespaceUri)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (namespaceUri is null)
        {
            throw new ArgumentNullException(nameof(namespaceUri));
        }
#pragma warning restore CA1510

        lock (_lock)
        {
            return AllocateUriLocked(namespaceUri);
        }
    }

    // Must be called with _lock held.
    private int AllocateUriLocked(string namespaceUri)
    {
        if (_uriCodes.TryGetValue(namespaceUri, out int existing))
        {
            return existing;
        }
        int code = _uris.Count;
        _uris.Add(namespaceUri);
        _uriCodes[namespaceUri] = code;
        return code;
    }

    private readonly struct NameKey : IEquatable<NameKey>
    {
        internal readonly string Uri;
        internal readonly string Local;

        internal NameKey(string uri, string local)
        {
            Uri = uri;
            Local = local;
        }

        public bool Equals(NameKey other) =>
            string.Equals(Uri, other.Uri, StringComparison.Ordinal) &&
            string.Equals(Local, other.Local, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is NameKey other && Equals(other);

        public override int GetHashCode()
        {
            int h = 17;
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Uri);
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Local);
            return h;
        }
    }

    private readonly struct NameEntry
    {
        internal readonly string Uri;
        internal readonly string Local;
        internal readonly int UriCode;

        internal NameEntry(string uri, string local, int uriCode)
        {
            Uri = uri;
            Local = local;
            UriCode = uriCode;
        }
    }
}
