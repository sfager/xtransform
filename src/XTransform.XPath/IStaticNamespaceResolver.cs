// XPath 2.0 §2.1.1 — static context namespace bindings
namespace XTransform.XPath;

/// <summary>
/// Provides namespace prefix resolution for static analysis of XPath expressions.
/// Implementations supply the in-scope namespace bindings from the static context.
/// </summary>
internal interface IStaticNamespaceResolver
{
    /// <summary>
    /// Returns the namespace URI for <paramref name="prefix"/>, or <see langword="null"/>
    /// when the prefix is not in scope.
    /// </summary>
    public string? LookupNamespace(string prefix);

    /// <summary>Gets the default element/type namespace (may be empty string).</summary>
    public string DefaultElementNamespace { get; }

    /// <summary>Gets the default function namespace (typically the XPath functions namespace).</summary>
    public string DefaultFunctionNamespace { get; }
}
