// Lexical scope for XSLT instruction compilation. Tracks the ordered list of variable
// names visible at a given point in the template body — globals first, then template
// parameters, then local xsl:variable bindings. The compiler hands this list to
// XPathCompiler.ParseAndCompile as externalVariables so that $var references resolve.

using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Compile-time scope chain. Each <see cref="Enter"/> returns a child that inherits the
/// parent's bindings and lets the caller add new ones; the child is independent of the
/// parent for subsequent additions, so sibling scopes don't see each other's locals.
/// </summary>
internal sealed class XsltCompileScope
{
    private readonly List<QName> _names;
    private readonly Dictionary<QName, SequenceType> _types;

    private XsltCompileScope(List<QName> names, Dictionary<QName, SequenceType> types, NamespaceAliasTable aliases)
    {
        _names = names;
        _types = types;
        NamespaceAliases = aliases;
    }

    /// <summary>The xsl:namespace-alias declarations of the stylesheet, which literal result elements apply (XSLT 2.0 §11.1.4).</summary>
    public NamespaceAliasTable NamespaceAliases { get; }

    /// <summary>Creates a root scope with no visible variables.</summary>
    public static XsltCompileScope Empty => new XsltCompileScope(
        new List<QName>(),
        new Dictionary<QName, SequenceType>(),
        NamespaceAliasTable.Empty);

    /// <summary>Creates a root scope seeded with global variables.</summary>
    public static XsltCompileScope WithGlobals(IEnumerable<QName> globalNames, NamespaceAliasTable? aliases = null)
    {
        List<QName> names = new List<QName>();
        Dictionary<QName, SequenceType> types = new Dictionary<QName, SequenceType>();
        foreach (QName name in globalNames)
        {
            names.Add(name);
            types[name] = SequenceType.AnySequence;
        }

        return new XsltCompileScope(names, types, aliases ?? NamespaceAliasTable.Empty);
    }

    /// <summary>Variables visible in slot-allocation order (slots 0..N-1 at runtime).</summary>
    public IReadOnlyList<QName> Names => _names;

    /// <summary>
    /// The map used to call <c>XPathCompiler.ParseAndCompile(..., externalVariables)</c>.
    /// Returns a <see cref="Dictionary{TKey, TValue}"/> because the XPath compiler assigns
    /// slot indices in enumeration order and <see cref="Dictionary{TKey, TValue}"/> preserves
    /// insertion order; <see cref="ImmutableDictionary{TKey, TValue}"/> does not.
    /// </summary>
    public Dictionary<QName, SequenceType> AsExternalVariables()
    {
        Dictionary<QName, SequenceType> d = new Dictionary<QName, SequenceType>(_names.Count);
        foreach (QName name in _names)
        {
            d[name] = _types[name];
        }

        return d;
    }

    /// <summary>Returns a child scope that inherits every binding currently in <see cref="Names"/>.</summary>
    public XsltCompileScope Enter()
    {
        return new XsltCompileScope(
            new List<QName>(_names),
            new Dictionary<QName, SequenceType>(_types),
            NamespaceAliases);
    }

    /// <summary>Adds a binding visible in this scope and its subsequent children.</summary>
    public void Declare(QName name, SequenceType type)
    {
        // Shadowing: an inner redeclaration simply re-registers the name; XPath compiler
        // uses the most recent binding for resolution.
        if (!_types.ContainsKey(name))
        {
            _names.Add(name);
        }

        _types[name] = type;
    }

    /// <summary>True when <paramref name="name"/> is in scope.</summary>
    public bool Contains(QName name) => _types.ContainsKey(name);
}
