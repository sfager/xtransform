using System;

namespace XTransform.Xdm;

/// <summary>An expanded QName. Equality ignores the prefix.</summary>
public readonly struct QName : IEquatable<QName>
{
    /// <summary>Initializes a QName with a local name and no namespace.</summary>
    public QName(string localName)
        : this("", "", localName) { }

    /// <summary>Initializes a QName with a namespace URI and local name, no prefix.</summary>
    public QName(string namespaceUri, string localName)
        : this("", namespaceUri, localName) { }

    /// <summary>Initializes a QName with a prefix, namespace URI, and local name.</summary>
    public QName(string prefix, string namespaceUri, string localName)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }
        if (namespaceUri is null)
        {
            throw new ArgumentNullException(nameof(namespaceUri));
        }
        if (localName is null)
        {
            throw new ArgumentNullException(nameof(localName));
        }
#pragma warning restore CA1510

        Prefix = prefix;
        NamespaceUri = namespaceUri;
        LocalName = localName;
    }

    /// <summary>The namespace prefix; "" when none. Never null.</summary>
    public string Prefix { get; }

    /// <summary>The namespace URI; "" for no namespace. Never null.</summary>
    public string NamespaceUri { get; }

    /// <summary>The local part of the name.</summary>
    public string LocalName { get; }

    /// <summary>Clark notation: "{uri}local", or "local" when there is no namespace.</summary>
    public string ClarkName =>
        NamespaceUri.Length == 0 ? LocalName : "{" + NamespaceUri + "}" + LocalName;

    /// <summary>Parses Clark notation ("{uri}local" or "local").</summary>
    public static QName ParseClark(string clarkName)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (clarkName is null)
        {
            throw new ArgumentNullException(nameof(clarkName));
        }
#pragma warning restore CA1510

        if (clarkName.Length > 0 && clarkName[0] == '{')
        {
            int close = clarkName.IndexOf('}');
            if (close < 0)
            {
                throw new ArgumentException("Invalid Clark name: missing closing '}'.", nameof(clarkName));
            }
            string uri = clarkName.Substring(1, close - 1);
            string local = clarkName.Substring(close + 1);
            return new QName(uri, local);
        }

        return new QName(clarkName);
    }

    /// <inheritdoc />
    public bool Equals(QName other) =>
        string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is QName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int h = 17;
        h = h * 31 + StringComparer.Ordinal.GetHashCode(NamespaceUri);
        h = h * 31 + StringComparer.Ordinal.GetHashCode(LocalName);
        return h;
    }

    /// <summary>"prefix:local" when a prefix is set, otherwise "local".</summary>
    public override string ToString() =>
        Prefix.Length > 0 ? Prefix + ":" + LocalName : LocalName;

    /// <inheritdoc />
    public static bool operator ==(QName left, QName right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(QName left, QName right) => !left.Equals(right);
}
