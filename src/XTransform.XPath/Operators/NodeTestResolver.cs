// XPath 2.0 §2.5.3 — node tests; maps NodeTestAst to runtime NodeTest.

using XTransform.Xdm;
using XTransform.XPath.Ast;

namespace XTransform.XPath.Operators;

/// <summary>
/// Resolves a <see cref="NodeTestAst"/> into a runtime <see cref="NodeTest"/>
/// against a given <see cref="NamePool"/>.
/// </summary>
/// <remarks>
/// No caching is applied here — per-step resolution is cheap relative to tree
/// traversal.  Caching can be added in a later task if profiling shows it matters.
/// </remarks>
internal static class NodeTestResolver
{
    /// <summary>
    /// Compiles a <see cref="NodeTestAst"/> into a runtime <see cref="NodeTest"/>.
    /// </summary>
    /// <param name="ast">The AST node produced by the parser.</param>
    /// <param name="pool">The name pool used to intern expanded names.</param>
    /// <param name="axis">The axis on which the test will be evaluated (determines default kind).</param>
    /// <returns>A <see cref="NodeTest"/> that can be passed to <see cref="TinyTree.Iterate"/>.</returns>
    internal static NodeTest Resolve(NodeTestAst ast, NamePool pool, Axis axis)
    {
        // XPath 2.0 §2.5.3 — principal node kind depends on axis
        XdmNodeKind defaultKind = PrincipalKind(axis);

        switch (ast)
        {
            case AnyKindTestAst:
                // node() — any kind
                return AnyKindTest.Instance;

            case KindTestAst kt:
                return ResolveKindTest(kt, pool);

            case NameTestAst nt:
                return ResolveNameTest(nt, pool, defaultKind);

            default:
                // Should not happen with the current parser, but guard anyway.
                return AnyKindTest.Instance;
        }
    }

    // ── Kind-test resolution ───────────────────────────────────────────────────

    private static NodeTest ResolveKindTest(KindTestAst kt, NamePool pool)
    {
        // KindTestAst with a NameArg: e.g. processing-instruction("target")
        // or element(foo) / attribute(bar) with a name argument.
        if (kt.NameArg.HasValue)
        {
            QName nameArg = kt.NameArg.Value;
            int nameCode;

            if (kt.Kind == XdmNodeKind.ProcessingInstruction)
            {
                // PI targets have no namespace (XPath 2.0 §2.5.3).
                nameCode = pool.Allocate(string.Empty, nameArg.LocalName);
            }
            else
            {
                // element(foo) or attribute(bar): expand using the full QName.
                nameCode = pool.Allocate(nameArg.NamespaceUri, nameArg.LocalName);
            }

            return new NamedKindTest(kt.Kind, nameCode);
        }

        // KindTestAst with an ElementArg: document-node(element(...)).
        // For P2-06 simplicity: match the document kind; inner element constraint is
        // approximated (a full DocumentElementKindTest can be added in a later task).
        if (kt.ElementArg != null)
        {
            return new KindTest(kt.Kind);
        }

        // Plain kind test: element(), text(), comment(), etc.
        return new KindTest(kt.Kind);
    }

    // ── Name-test resolution ───────────────────────────────────────────────────

    private static NodeTest ResolveNameTest(NameTestAst nt, NamePool pool, XdmNodeKind defaultKind)
    {
        switch (nt.Kind)
        {
            case NameTestKind.AnyName:
                // *  — any name on the principal kind
                return WildcardNameTest.AnyName(defaultKind);

            case NameTestKind.PrefixWildcard:
            {
                // prefix:*  — any local name in the given namespace URI
                // nt.Name carries the already-resolved namespace URI in NamespaceUri.
                int uriCode = pool.AllocateUri(nt.Name!.Value.NamespaceUri);
                return WildcardNameTest.AnyLocalInUri(defaultKind, uriCode);
            }

            case NameTestKind.LocalWildcard:
                // *:local  — any namespace URI with the given local name
                return WildcardNameTest.AnyUriWithLocal(defaultKind, nt.Name!.Value.LocalName);

            default:
            {
                // Exact QName match
                QName name = nt.Name!.Value;
                int nameCode = pool.Allocate(name.NamespaceUri, name.LocalName);
                return new NamedKindTest(defaultKind, nameCode);
            }
        }
    }

    // ── Principal node kind per axis ───────────────────────────────────────────

    /// <summary>
    /// Returns the principal node kind for the given axis per XPath 2.0 §2.5.3:
    /// attribute → Attribute; namespace → Namespace; all others → Element.
    /// </summary>
    private static XdmNodeKind PrincipalKind(Axis axis)
    {
        // XPath 2.0 §2.5.3 — principal node kind
        switch (axis)
        {
            case Axis.Attribute:
                return XdmNodeKind.Attribute;

            case Axis.Namespace:
                return XdmNodeKind.Namespace;

            default:
                return XdmNodeKind.Element;
        }
    }
}
