using System.Collections.Generic;

// XPath 2.0 §2.1.1 — static context namespace bindings
namespace XTransform.XPath;

/// <summary>
/// An <see cref="IStaticNamespaceResolver"/> backed by an in-memory dictionary.
/// Suitable for unit tests and simple host applications.
/// </summary>
internal sealed class DictionaryNamespaceResolver : IStaticNamespaceResolver
{
    private readonly IReadOnlyDictionary<string, string> _prefixes;

    /// <summary>
    /// Initializes a new resolver with optional prefix bindings and default namespaces.
    /// </summary>
    /// <param name="prefixes">Prefix-to-URI map; <see langword="null"/> means empty.</param>
    /// <param name="defaultElementNamespace">Default element/type namespace.</param>
    /// <param name="defaultFunctionNamespace">Default function namespace.</param>
    public DictionaryNamespaceResolver(
        IReadOnlyDictionary<string, string>? prefixes = null,
        string defaultElementNamespace = "",
        string defaultFunctionNamespace = "http://www.w3.org/2005/xpath-functions")
    {
        _prefixes = prefixes ?? _emptyDict;
        DefaultElementNamespace = defaultElementNamespace;
        DefaultFunctionNamespace = defaultFunctionNamespace;
    }

    private static readonly Dictionary<string, string> _emptyDict = new Dictionary<string, string>(0);

    /// <inheritdoc/>
    public string? LookupNamespace(string prefix)
    {
        return _prefixes.TryGetValue(prefix, out string? uri) ? uri : null;
    }

    /// <inheritdoc/>
    public string DefaultElementNamespace { get; }

    /// <inheritdoc/>
    public string DefaultFunctionNamespace { get; }

    /// <summary>
    /// A resolver that always knows <c>xs</c> → XSD namespace and <c>fn</c> → XPath functions namespace.
    /// Suitable for the vast majority of unit tests.
    /// </summary>
    public static DictionaryNamespaceResolver Default { get; } = new DictionaryNamespaceResolver(
        new Dictionary<string, string>
        {
            ["xs"] = "http://www.w3.org/2001/XMLSchema",
            ["fn"] = "http://www.w3.org/2005/xpath-functions",
            ["xdt"] = "http://www.w3.org/2005/xpath-datatypes",
            ["err"] = "http://www.w3.org/2005/xqt-errors",
        },
        defaultElementNamespace: "",
        defaultFunctionNamespace: "http://www.w3.org/2005/xpath-functions");
}
