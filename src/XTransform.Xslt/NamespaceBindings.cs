using System;
using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// The set of namespace bindings in scope at a stylesheet element — an immutable
/// snapshot that the compiler needs to resolve QNames in attribute values and in
/// patterns/expressions. Prefixes are keys; URIs are values. An empty-string prefix
/// is the default element namespace; the <c>xml</c> prefix is always implicitly bound.
/// </summary>
internal sealed class NamespaceBindings
{
    private readonly Dictionary<string, string> _bindings;

    private NamespaceBindings(Dictionary<string, string> bindings)
    {
        _bindings = bindings;
    }

    /// <summary>Empty root (only the implicit <c>xml</c> binding).</summary>
    public static NamespaceBindings Root { get; } = CreateRoot();

    private static NamespaceBindings CreateRoot()
    {
        return new NamespaceBindings(new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["xml"] = XsltNamespace.XmlUri,
        });
    }

    /// <summary>Resolves a prefix to a namespace URI, or returns <see langword="null"/>.</summary>
    public string? Resolve(string prefix)
    {
        return _bindings.TryGetValue(prefix, out string? uri) ? uri : null;
    }

    /// <summary>Enumerates every (prefix, uri) binding in scope.</summary>
    public IReadOnlyDictionary<string, string> AsDictionary() => _bindings;

    /// <summary>
    /// Produces a child scope by layering <paramref name="newBindings"/> on top of this one.
    /// An empty <paramref name="newBindings"/> dictionary returns <see langword="this"/>.
    /// A binding whose URI is the empty string un-declares the prefix.
    /// </summary>
    public NamespaceBindings Extend(IReadOnlyDictionary<string, string> newBindings)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (newBindings is null)
        {
            throw new ArgumentNullException(nameof(newBindings));
        }
#pragma warning restore CA1510

        if (newBindings.Count == 0)
        {
            return this;
        }

        Dictionary<string, string> merged = new Dictionary<string, string>(_bindings, System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in newBindings)
        {
            if (kv.Value.Length == 0)
            {
                // An empty value un-declares the prefix (XML Namespaces 1.0 §6.2 — default
                // un-declaration; XML Namespaces 1.1 generalises this to any prefix).
                merged.Remove(kv.Key);
            }
            else
            {
                merged[kv.Key] = kv.Value;
            }
        }

        return new NamespaceBindings(merged);
    }
}
