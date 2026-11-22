using System;

namespace XTransform.Xdm;

// XPath 2.0 §2.5.3 — node tests

/// <summary>
/// Base class for XPath 2.0 node tests.  Instances are immutable and may be shared.
/// </summary>
internal abstract class NodeTest
{
    private protected NodeTest() { }

    /// <summary>
    /// Returns true when a node with the given <paramref name="kind"/> and <paramref name="nameCode"/>
    /// satisfies this node test.
    /// </summary>
    /// <param name="kind">Kind of the candidate node.</param>
    /// <param name="nameCode">NamePool code of the candidate node's expanded name, or -1 if anonymous.</param>
    /// <param name="pool">The NamePool used to resolve codes.</param>
    public abstract bool Matches(XdmNodeKind kind, int nameCode, NamePool pool);

    /// <summary>
    /// Default priority for use in XSLT pattern matching (XSLT 2.0 §6.4).
    /// </summary>
    public abstract double DefaultPriority { get; }
}

// ── node() — matches every kind ───────────────────────────────────────────────

/// <summary>
/// The <c>node()</c> kind test: matches a node of any kind.
/// XPath 2.0 §2.5.3, default priority -0.5.
/// </summary>
internal sealed class AnyKindTest : NodeTest
{
    /// <summary>Singleton instance.</summary>
    internal static readonly AnyKindTest Instance = new AnyKindTest();

    private AnyKindTest() { }

    /// <inheritdoc/>
    public override bool Matches(XdmNodeKind kind, int nameCode, NamePool pool) => true;

    /// <inheritdoc/>
    public override double DefaultPriority => -0.5;
}

// ── Pure kind tests ────────────────────────────────────────────────────────────

/// <summary>
/// A pure kind test such as <c>element()</c>, <c>attribute()</c>, <c>text()</c>, etc.
/// Matches any node of the specified kind.  Default priority -0.5 per XSLT 2.0 §6.4.
/// </summary>
internal sealed class KindTest : NodeTest
{
    /// <summary>Initializes a kind test for the given <paramref name="kind"/>.</summary>
    internal KindTest(XdmNodeKind kind)
    {
        Kind = kind;
    }

    /// <summary>The required node kind.</summary>
    internal XdmNodeKind Kind { get; }

    /// <inheritdoc/>
    public override bool Matches(XdmNodeKind kind, int nameCode, NamePool pool) => kind == Kind;

    /// <inheritdoc/>
    public override double DefaultPriority => -0.5;
}

// ── Named kind tests ───────────────────────────────────────────────────────────

/// <summary>
/// A named kind test such as <c>element(foo)</c> or <c>attribute(bar)</c>.
/// Matches a node of the specified kind whose expanded name matches <see cref="NameCode"/>.
/// Default priority 0 per XSLT 2.0 §6.4.
/// </summary>
internal sealed class NamedKindTest : NodeTest
{
    /// <summary>Initializes a named kind test.</summary>
    /// <param name="kind">The required kind (typically Element or Attribute).</param>
    /// <param name="nameCode">The NamePool code of the required expanded name.</param>
    internal NamedKindTest(XdmNodeKind kind, int nameCode)
    {
        Kind = kind;
        NameCode = nameCode;
    }

    /// <summary>The required node kind.</summary>
    internal XdmNodeKind Kind { get; }

    /// <summary>The required NamePool name code.</summary>
    internal int NameCode { get; }

    /// <inheritdoc/>
    public override bool Matches(XdmNodeKind kind, int nameCode, NamePool pool) =>
        kind == Kind && nameCode == NameCode;

    /// <inheritdoc/>
    public override double DefaultPriority => 0.0;
}

// ── Wildcard name tests ────────────────────────────────────────────────────────

/// <summary>
/// Wildcard name tests: <c>*</c>, <c>prefix:*</c> (resolved to URI), or <c>*:local</c>.
/// Used on child and attribute axes.
/// Default priorities per XSLT 2.0 §6.4:
///   <c>*</c>       → -0.5
///   <c>ns:*</c>    → -0.25
///   <c>*:local</c> → -0.25
/// </summary>
internal sealed class WildcardNameTest : NodeTest
{
    private enum WildcardKind { Any, AnyLocalInUri, AnyUriWithLocal }

    private readonly XdmNodeKind _defaultKind;
    private readonly WildcardKind _wildcardKind;
    private readonly int _uriCode;        // used by AnyLocalInUri
    private readonly string? _localName;  // used by AnyUriWithLocal

    private WildcardNameTest(XdmNodeKind defaultKind, WildcardKind wildcardKind, int uriCode, string? localName)
    {
        _defaultKind = defaultKind;
        _wildcardKind = wildcardKind;
        _uriCode = uriCode;
        _localName = localName;
    }

    /// <summary>
    /// Creates a <c>*</c> wildcard that matches any name on <paramref name="defaultKind"/> nodes.
    /// </summary>
    internal static WildcardNameTest AnyName(XdmNodeKind defaultKind) =>
        new WildcardNameTest(defaultKind, WildcardKind.Any, 0, null);

    /// <summary>
    /// Creates a <c>prefix:*</c> wildcard matching nodes whose namespace URI resolves to
    /// <paramref name="uriCode"/>.
    /// </summary>
    internal static WildcardNameTest AnyLocalInUri(XdmNodeKind defaultKind, int uriCode) =>
        new WildcardNameTest(defaultKind, WildcardKind.AnyLocalInUri, uriCode, null);

    /// <summary>
    /// Creates a <c>*:local</c> wildcard matching nodes with any namespace but a specific local name.
    /// </summary>
    internal static WildcardNameTest AnyUriWithLocal(XdmNodeKind defaultKind, string localName)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (localName is null)
        {
            throw new ArgumentNullException(nameof(localName));
        }
#pragma warning restore CA1510

        return new WildcardNameTest(defaultKind, WildcardKind.AnyUriWithLocal, 0, localName);
    }

    /// <inheritdoc/>
    public override bool Matches(XdmNodeKind kind, int nameCode, NamePool pool)
    {
        if (kind != _defaultKind)
        {
            return false;
        }

        switch (_wildcardKind)
        {
            case WildcardKind.Any:
                return true;

            case WildcardKind.AnyLocalInUri:
                if (nameCode < 0)
                {
                    return false;
                }

                return pool.GetUriCode(nameCode) == _uriCode;

            case WildcardKind.AnyUriWithLocal:
                if (nameCode < 0)
                {
                    return false;
                }

                return string.Equals(pool.GetLocalName(nameCode), _localName, StringComparison.Ordinal);

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public override double DefaultPriority =>
        _wildcardKind == WildcardKind.Any ? -0.5 : -0.25;
}
