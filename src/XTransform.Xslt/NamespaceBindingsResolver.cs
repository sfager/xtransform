using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>
/// Adapts <see cref="NamespaceBindings"/> (the per-element in-scope namespace snapshot) to
/// <see cref="IStaticNamespaceResolver"/> so the XPath compiler can look up prefixes.
/// </summary>
internal sealed class NamespaceBindingsResolver : IStaticNamespaceResolver
{
    private readonly NamespaceBindings _bindings;

    /// <summary>Initializes a resolver over the given bindings with default namespaces supplied separately.</summary>
    public NamespaceBindingsResolver(
        NamespaceBindings bindings,
        string defaultElementNamespace,
        string defaultFunctionNamespace)
    {
        _bindings = bindings;
        DefaultElementNamespace = defaultElementNamespace;
        DefaultFunctionNamespace = defaultFunctionNamespace;
    }

    /// <inheritdoc/>
    public string? LookupNamespace(string prefix) => _bindings.Resolve(prefix);

    /// <inheritdoc/>
    public string DefaultElementNamespace { get; }

    /// <inheritdoc/>
    public string DefaultFunctionNamespace { get; }
}
