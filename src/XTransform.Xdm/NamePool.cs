using System;
using System.Collections.Generic;
using System.Threading;

namespace XTransform.Xdm;

/// <summary>
/// Interns expanded names as integer codes so name tests are integer comparisons.
/// Thread-safe: one pool is shared by a compiled stylesheet and every transformation run
/// from it. Codes are stable for the pool's lifetime and never reused.
/// A name with a prefix has a code of its own, so a node keeps the prefix it was written with (XDM "node-name");
/// every such code maps to one canonical code, the code of the same name with no prefix, and name tests compare those.
/// </summary>
internal sealed class NamePool
{
    public const int NoName = -1;

    // Name codes: (prefix, namespaceUri, localName) → code
    private readonly Dictionary<NameKey, int> _nameCodes = new Dictionary<NameKey, int>();

    // Canonical code of every name code. Replaced, never mutated in place, so readers need no lock.
    private int[] _canonical = new int[64];
    private readonly List<NameEntry> _names = new List<NameEntry>();

    // URI codes: namespaceUri → code
    private readonly Dictionary<string, int> _uriCodes = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> _uris = new List<string>();

    private readonly object _lock = new object();

    public int Allocate(string namespaceUri, string localName) => Allocate(string.Empty, namespaceUri, localName);

    /// <summary>The code of a name with a prefix; an empty prefix gives the canonical code.</summary>
    public int Allocate(string prefix, string namespaceUri, string localName)
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

        // A name in no namespace has no prefix (XDM: a prefix needs a namespace to stand for).
        prefix = namespaceUri.Length == 0 ? string.Empty : prefix ?? string.Empty;
        var key = new NameKey(prefix, namespaceUri, localName);

        lock (_lock)
        {
            if (_nameCodes.TryGetValue(key, out int existing))
            {
                return existing;
            }

            // A prefixed name is a variant of the name with no prefix, which is allocated first.
            int canonical = NoName;
            if (prefix.Length > 0)
            {
                canonical = AllocateLocked(string.Empty, namespaceUri, localName);
            }

            return AllocateLocked(prefix, namespaceUri, localName, canonical);
        }
    }

    // Must be called with _lock held.
    private int AllocateLocked(string prefix, string namespaceUri, string localName, int canonical = NoName)
    {
        var key = new NameKey(prefix, namespaceUri, localName);
        if (_nameCodes.TryGetValue(key, out int existing))
        {
            return existing;
        }

        int uriCode = AllocateUriLocked(namespaceUri);
        int code = _names.Count;
        _names.Add(new NameEntry(prefix, namespaceUri, localName, uriCode));
        _nameCodes[key] = code;

        int[] canonicalCodes = _canonical;
        if (code >= canonicalCodes.Length)
        {
            int[] grown = new int[canonicalCodes.Length * 2];
            Array.Copy(canonicalCodes, grown, canonicalCodes.Length);
            canonicalCodes = grown;
        }

        canonicalCodes[code] = canonical == NoName ? code : canonical;
        Volatile.Write(ref _canonical, canonicalCodes);
        return code;
    }

    /// <summary>
    /// The canonical code of <paramref name="nameCode"/>: the code of the same expanded name with no prefix. Two codes
    /// name the same expanded name exactly when their canonical codes are equal. Takes no lock.
    /// </summary>
    public int Canonical(int nameCode) => nameCode < 0 ? nameCode : Volatile.Read(ref _canonical)[nameCode];

    public int Allocate(QName name) => Allocate(name.Prefix, name.NamespaceUri, name.LocalName);

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
            return _nameCodes.TryGetValue(new NameKey(string.Empty, namespaceUri, localName), out code);
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
            return new QName(string.IsNullOrEmpty(prefix) ? e.Prefix : prefix, e.Uri, e.Local);
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
        internal readonly string Prefix;
        internal readonly string Uri;
        internal readonly string Local;

        internal NameKey(string prefix, string uri, string local)
        {
            Prefix = prefix;
            Uri = uri;
            Local = local;
        }

        public bool Equals(NameKey other) =>
            string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
            string.Equals(Uri, other.Uri, StringComparison.Ordinal) &&
            string.Equals(Local, other.Local, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is NameKey other && Equals(other);

        public override int GetHashCode()
        {
            int h = 17;
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Prefix);
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Uri);
            h = h * 31 + StringComparer.Ordinal.GetHashCode(Local);
            return h;
        }
    }

    private readonly struct NameEntry
    {
        internal readonly string Prefix;
        internal readonly string Uri;
        internal readonly string Local;
        internal readonly int UriCode;

        internal NameEntry(string prefix, string uri, string local, int uriCode)
        {
            Prefix = prefix;
            Uri = uri;
            Local = local;
            UriCode = uriCode;
        }
    }
}
