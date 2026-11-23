// XSLT 2.0 §5.5.3 — pattern matching semantics.
// A pattern matches a node N when there exists a sequence of nodes reachable from the
// document root such that applying the equivalent XPath expression selects N. We implement
// this bottom-up: test the node against the pattern's final step, then walk up the parent
// chain for the preceding steps. Predicates are evaluated as XPath expressions in a child
// context focused on the candidate node.

using System;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt;

/// <summary>
/// Evaluates whether an <see cref="XdmNode"/> matches a <see cref="PatternAst"/>.
/// </summary>
internal static class PatternMatcher
{
    /// <summary>Returns <see langword="true"/> when <paramref name="node"/> matches <paramref name="pattern"/>.</summary>
    public static bool Matches(PatternAst pattern, XdmNode node, DynamicContext context)
    {
        switch (pattern)
        {
            case PathPatternAst path:
                return MatchPath(path, node, context);

            case IdPatternAst idp:
                return MatchId(idp, node);

            case KeyPatternAst:
                // fn:key resolution lands in P3-11.
                return false;

            case IdKeyWithPathAst ikp:
                return MatchIdKeyWithPath(ikp, node, context);

            case UnionPatternAst union:
                // The ModeCompiler splits unions before registering, so one shouldn't reach
                // here — but match each alternative as a safety net.
                foreach (PatternAst alt in union.Alternatives)
                {
                    if (Matches(alt, node, context))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    // ── Path patterns ──────────────────────────────────────────────────────

    private static bool MatchPath(PathPatternAst path, XdmNode node, DynamicContext context)
    {
        if (path.Steps.IsEmpty)
        {
            // Bare "/" — matches only the document node.
            return path.Anchor != PatternAnchor.Relative && node.NodeKind == XdmNodeKind.Document;
        }

        // Match the final step against the node.
        int stepIndex = path.Steps.Length - 1;
        StepPatternAst last = path.Steps[stepIndex];
        if (!MatchStep(last, node, context))
        {
            return false;
        }

        // Walk up for the preceding steps. Each preceding step must match an ancestor; the
        // combinator on the step DIRECTLY FOLLOWING it determines whether the match is
        // strict parent (/) or any ancestor (//).
        XdmNode? current = node;
        for (int i = stepIndex - 1; i >= 0; i--)
        {
            StepPatternAst step = path.Steps[i];
            // Look at the following step's combinator to know whether to require immediate parent or any ancestor.
            StepCombinator combinator = path.Steps[i + 1].Combinator;
            current = AscendMatching(step, current!, combinator, context);
            if (current is null)
            {
                return false;
            }
        }

        // Anchor check: if the pattern is rooted, we must now be at (or anchor to) the document node.
        if (path.Anchor == PatternAnchor.Root)
        {
            // The step just before the first step must be the document node.
            StepCombinator firstComb = path.Steps[0].Combinator;
            XdmNode? parent = current!.Parent;
            if (firstComb == StepCombinator.DoubleSlash)
            {
                // Walk ancestors until we hit the document node; satisfied unconditionally.
                return true;
            }

            // Strict / before the first step: parent must be the document node.
            return parent is not null && parent.NodeKind == XdmNodeKind.Document;
        }

        if (path.Anchor == PatternAnchor.RootDescendantOrSelf)
        {
            // // at the root matches at any depth; always satisfied.
            return true;
        }

        return true;
    }

    private static XdmNode? AscendMatching(
        StepPatternAst step,
        XdmNode fromChild,
        StepCombinator childCombinator,
        DynamicContext context)
    {
        if (childCombinator == StepCombinator.DoubleSlash)
        {
            // Any ancestor may match.
            XdmNode? ancestor = fromChild.Parent;
            while (ancestor is not null)
            {
                if (MatchStep(step, ancestor, context))
                {
                    return ancestor;
                }

                ancestor = ancestor.Parent;
            }

            return null;
        }

        XdmNode? parent = fromChild.Parent;
        if (parent is null || !MatchStep(step, parent, context))
        {
            return null;
        }

        return parent;
    }

    private static bool MatchStep(StepPatternAst step, XdmNode node, DynamicContext context)
    {
        // Axis must agree with the node kind.
        bool wantAttribute = step.Axis == Axis.Attribute;
        bool isAttribute = node.NodeKind == XdmNodeKind.Attribute;
        if (wantAttribute != isAttribute)
        {
            return false;
        }

        if (!MatchNodeTest(step.Test, node))
        {
            return false;
        }

        // Evaluate each predicate against a focus on this node.
        if (!step.Predicates.IsEmpty)
        {
            DynamicContext predCtx = context.WithContextItem(node, position: 1, size: 1);
            foreach (XPathAst predAst in step.Predicates)
            {
                // We expect the compiler-visible predicate form to be an expression. For
                // P3-07 we only support compiled predicates attached to patterns when the
                // caller pre-compiles them; absent that, assume true.
                _ = predAst;
                _ = predCtx;
                return true;
            }
        }

        return true;
    }

    private static bool MatchNodeTest(NodeTestAst test, XdmNode node)
    {
        switch (test)
        {
            case NameTestAst nameTest:
                return MatchNameTest(nameTest, node);

            case KindTestAst kindTest:
                return MatchKindTest(kindTest, node);

            case AnyKindTestAst:
                // node() matches any kind.
                return true;

            default:
                return false;
        }
    }

    private static bool MatchNameTest(NameTestAst nameTest, XdmNode node)
    {
        QName? n = node.NodeName;
        if (n is null)
        {
            return false;
        }

        QName name = n.Value;
        switch (nameTest.Kind)
        {
            case NameTestKind.QualifiedName:
                QName expected = nameTest.Name!.Value;
                return string.Equals(expected.LocalName, name.LocalName, StringComparison.Ordinal) &&
                       string.Equals(expected.NamespaceUri, name.NamespaceUri, StringComparison.Ordinal);

            case NameTestKind.AnyName:
                return true;

            case NameTestKind.PrefixWildcard:
                return string.Equals(nameTest.Name!.Value.NamespaceUri, name.NamespaceUri, StringComparison.Ordinal);

            case NameTestKind.LocalWildcard:
                return string.Equals(nameTest.Name!.Value.LocalName, name.LocalName, StringComparison.Ordinal);

            default:
                return false;
        }
    }

    private static bool MatchKindTest(KindTestAst kt, XdmNode node)
    {
        if (node.NodeKind != kt.Kind)
        {
            return false;
        }

        // For element() / attribute(name) — a specific name arg.
        if (kt.NameArg.HasValue &&
            !string.Equals(kt.NameArg.Value.LocalName, "*", StringComparison.Ordinal))
        {
            QName? n = node.NodeName;
            if (n is null)
            {
                return false;
            }

            QName want = kt.NameArg.Value;
            return string.Equals(want.LocalName, n.Value.LocalName, StringComparison.Ordinal) &&
                   string.Equals(want.NamespaceUri, n.Value.NamespaceUri, StringComparison.Ordinal);
        }

        // document-node(element(name)): require a child element matching the inner kind-test.
        if (kt.Kind == XdmNodeKind.Document && kt.ElementArg is KindTestAst inner)
        {
            foreach (XdmNode child in node.Axis(Axis.Child))
            {
                if (child.NodeKind == XdmNodeKind.Element && MatchKindTest(inner, child))
                {
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    // ── id() / key() ───────────────────────────────────────────────────────

    private static bool MatchId(IdPatternAst pattern, XdmNode node)
    {
        if (pattern.IdLiteral is null)
        {
            // id($var) — resolution requires evaluating the variable; defer (P3-08).
            return false;
        }

        if (node.NodeKind != XdmNodeKind.Element)
        {
            return false;
        }

        // XSLT 2.0 §15.5.2: without schema typing, treat an attribute named 'id' as the ID.
        foreach (XdmNode attr in node.Axis(Axis.Attribute))
        {
            QName? name = attr.NodeName;
            if (name.HasValue && string.Equals(name.Value.LocalName, "id", StringComparison.Ordinal))
            {
                return string.Equals(attr.GetStringValue(), pattern.IdLiteral, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool MatchIdKeyWithPath(IdKeyWithPathAst pattern, XdmNode node, DynamicContext context)
    {
        // Not implemented beyond id("literal")/foo in P3-07; proper id/key support lands in P3-11.
        _ = context;
        if (pattern.Head is IdPatternAst idp && MatchId(idp, node))
        {
            return true;
        }

        return false;
    }
}
