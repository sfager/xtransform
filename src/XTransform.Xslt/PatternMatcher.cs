// XSLT 2.0 §5.5.3 — pattern matching semantics.
// A pattern matches a node N when there exists a sequence of nodes reachable from the
// document root such that applying the equivalent XPath expression selects N. We implement
// this bottom-up: test the node against the pattern's final step, then walk up the parent
// chain for the preceding steps. Predicates are evaluated as XPath expressions in a child
// context focused on the candidate node.

using System;
using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;
using XTransform.XPath.Operators;
using XTransform.Xdm.Iterators;
using XTransform.Xslt.Instructions;

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
                return MatchId(idp, node, context);

            case KeyPatternAst kp:
                return MatchKey(kp, node, context);

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
        // current() is the node being matched, also inside the predicates of the steps that precede the last (§5.5.3).
        context = context.WithCurrentItem(node, 1, 1);

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

    // A step is on the attribute axis when it says so (@x) or when its test is an attribute kind test: attribute(), attribute(n).
    private static bool UsesAttributeAxis(StepPatternAst step) =>
        step.Axis == Axis.Attribute || (!step.ExplicitChildAxis && step.Test is KindTestAst { Kind: XdmNodeKind.Attribute });

    private static bool MatchStep(StepPatternAst step, XdmNode node, DynamicContext context)
    {
        // Axis must agree with the node kind.
        bool wantAttribute = UsesAttributeAxis(step);
        bool isAttribute = node.NodeKind == XdmNodeKind.Attribute;
        if (wantAttribute != isAttribute)
        {
            return false;
        }

        // A step without an axis is on the child axis, and the document node is nobody's child: match="node()" does
        // not match it (XSLT 2.0 §5.5.2).
        // An explicit child:: step never matches it, whatever the test (child::document-node() is legal but matches nothing).
        if (!wantAttribute && node.NodeKind == XdmNodeKind.Document && (step.ExplicitChildAxis || step.Test is AnyKindTestAst))
        {
            return false;
        }

        if (!MatchNodeTest(step.Test, node))
        {
            return false;
        }

        return step.Predicates.IsEmpty || MatchPredicates(step, node, context);
    }

    // XSLT 2.0 §5.5.3: a node matches a step with predicates when it is selected by the equivalent
    // expression, so each predicate is evaluated with the focus of that step — the position and size
    // are those of the candidate among the siblings (children, or attributes of the same element) that
    // satisfy the node test and the earlier predicates. current() is the node being matched.
    private static bool MatchPredicates(StepPatternAst step, XdmNode node, DynamicContext context)
    {
        XsltRunState? run = context.Run.Tag as XsltRunState;
        BoundExpression[]? predicates = run?.Stylesheet.PatternPredicates.Get(step);
        if (run is null || predicates is null)
        {
            throw new InvalidOperationException(
                "The predicates of this pattern were not compiled; patterns must be matched within a compiled stylesheet");
        }

        // Fast path: no predicate can depend on the position or yield a number, so each can be
        // decided on the node alone without looking at its siblings.
        bool simple = true;
        foreach (BoundExpression predicate in predicates)
        {
            if (!IsSimplePredicate(predicate.Root))
            {
                simple = false;
                break;
            }
        }

        if (simple)
        {
            foreach (BoundExpression predicate in predicates)
            {
                DynamicContext ctx = predicate.Prepare(context, run).WithContextItem(node, 1, 1);
                if (!predicate.Root.EffectiveBooleanValue(ctx))
                {
                    return false;
                }
            }

            return true;
        }

        List<XdmItem> candidates = new List<XdmItem>();
        XdmNode? parent = node.Parent;
        if (parent is null)
        {
            candidates.Add(node);
        }
        else
        {
            foreach (XdmNode sibling in parent.Axis(UsesAttributeAxis(step) ? Axis.Attribute : Axis.Child))
            {
                if (MatchNodeTest(step.Test, sibling))
                {
                    candidates.Add(sibling);
                }
            }
        }

        IXdmIterator selected = new ArrayIterator(candidates.ToArray());
        foreach (BoundExpression predicate in predicates)
        {
            DynamicContext ctx = predicate.Prepare(context, run).WithContextItem(node, 1, 1);
            selected = PredicateApplicator.Apply(selected, predicate.Root, ctx);
        }

        XdmItem? item;
        while ((item = selected.Next()) is not null)
        {
            if (item is XdmNode n && n.Equals(node))
            {
                return true;
            }
        }

        return false;
    }

    // True when the predicate cannot read position() / last() and cannot produce a number
    // (a numeric value is a position test): its truth depends on the candidate node alone.
    private static bool IsSimplePredicate(ExpressionNode predicate)
    {
        if ((predicate.Dependencies & (ExpressionDependencies.Position | ExpressionDependencies.Last)) != ExpressionDependencies.None)
        {
            return false;
        }

        ItemType type = predicate.StaticType.ItemType;
        return type.IsSubtypeOf(ItemType.AnyNode) ||
               type.IsSubtypeOf(ItemType.Atomic(XdmTypeCode.Boolean)) ||
               type.IsSubtypeOf(ItemType.Atomic(XdmTypeCode.String));
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

    private static bool UntypedTypeMatches(XdmNodeKind kind, QName type)
    {
        if (!string.Equals(type.NamespaceUri, "http://www.w3.org/2001/XMLSchema", StringComparison.Ordinal))
        {
            return false;
        }

        return kind == XdmNodeKind.Element
            ? type.LocalName is "untyped" or "anyType"
            : type.LocalName is "untypedAtomic" or "anyAtomicType" or "anySimpleType";
    }

    private static bool MatchKindTest(KindTestAst kt, XdmNode node)
    {
        if (node.NodeKind != kt.Kind)
        {
            return false;
        }

        // A type argument: schema-unaware nodes are xs:untyped (elements) and xs:untypedAtomic (attributes), so only
        // those types, and their ancestors, can match.
        if (kt.TypeArg.HasValue && !UntypedTypeMatches(kt.Kind, kt.TypeArg.Value))
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

    private static bool MatchId(IdPatternAst pattern, XdmNode node, DynamicContext context)
    {
        // The ids to look for: the literal, or the (space-separated) strings of the variable (XSLT 2.0 §5.5.2).
        List<string> ids = new List<string>();
        if (pattern.IdLiteral is not null)
        {
            ids.AddRange(pattern.IdLiteral.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        else if (pattern.VariableRef is QName variable && context.Run.Tag is XsltRunState idRun)
        {
            foreach (XdmItem item in idRun.LookupVariable(variable))
            {
                ids.AddRange(item.GetStringValue().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        if (ids.Count == 0)
        {
            return false;
        }


        if (node.NodeKind != XdmNodeKind.Element)
        {
            return false;
        }

        // A tree with ID information (a DTD, or xml:id) says which attributes are IDs.
        TinyTree idTree = node.Handle.Tree;
        if (idTree.HasIdInformation)
        {
            foreach (string id in ids)
            {
                if (idTree.IdMap.TryGetValue(id, out int owner) && owner == node.Handle.Index)
                {
                    return true;
                }
            }

            return false;
        }

        // XSLT 2.0 §15.5.2: without schema typing, treat an attribute named 'id' as the ID.
        foreach (XdmNode attr in node.Axis(Axis.Attribute))
        {
            QName? name = attr.NodeName;
            if (name.HasValue && string.Equals(name.Value.LocalName, "id", StringComparison.Ordinal))
            {
                return ids.Contains(attr.GetStringValue());
            }
        }

        return false;
    }

    // key(name, value) pattern (XSLT 2.0 §5.5.2): the node is one of the nodes the key function returns for the value.
    private static bool MatchKey(KeyPatternAst pattern, XdmNode node, DynamicContext context)
    {
        if (context.Run.Tag is not XsltRunState run)
        {
            return false;
        }

        XdmSequence values = pattern.ValueAtomic is not null
            ? XdmSequence.Of(pattern.ValueAtomic)
            : pattern.ValueLiteral is not null
            ? XdmSequence.Of(XdmAtomicValue.String(pattern.ValueLiteral))
            : pattern.VariableRef is QName variable ? run.LookupVariable(variable) : XdmSequence.Empty;
        string name = pattern.KeyName.Prefix.Length > 0 ? pattern.KeyName.Prefix + ":" + pattern.KeyName.LocalName : pattern.KeyName.LocalName;
        DynamicContext at = context.WithCurrentItem(node, position: 1, size: 1);
        IXdmIterator hits = XsltFunctionLibrary.KeyLookup(at, new[] { XdmSequence.Of(XdmAtomicValue.String(name)), values }, top: null);
        while (hits.Next() is XdmItem hit)
        {
            if (hit is XdmNode hitNode && hitNode.Equals(node))
            {
                return true;
            }
        }

        return false;
    }

    // id(...) or key(...) followed by a path (XSLT 2.0 §5.5.2): the node matches the steps, and the node they lead back to, or an
    // ancestor of it after a //, is one the id or key function returns.
    private static bool MatchIdKeyWithPath(IdKeyWithPathAst pattern, XdmNode node, DynamicContext context)
    {
        if (pattern.TailSteps.IsEmpty)
        {
            return false;
        }

        context = context.WithCurrentItem(node, 1, 1);
        int last = pattern.TailSteps.Length - 1;
        if (!MatchStep(pattern.TailSteps[last], node, context))
        {
            return false;
        }

        XdmNode current = node;
        for (int i = last - 1; i >= 0; i--)
        {
            XdmNode? up = AscendMatching(pattern.TailSteps[i], current, pattern.TailSteps[i + 1].Combinator, context);
            if (up is null)
            {
                return false;
            }

            current = up;
        }

        if (pattern.TailSteps[0].Combinator == StepCombinator.DoubleSlash)
        {
            for (XdmNode? ancestor = current.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (MatchHead(pattern.Head, ancestor, context))
                {
                    return true;
                }
            }

            return false;
        }

        return current.Parent is { } parent && MatchHead(pattern.Head, parent, context);
    }

    private static bool MatchHead(PatternAst head, XdmNode node, DynamicContext context) =>
        head is IdPatternAst idp ? MatchId(idp, node, context) : head is KeyPatternAst kp && MatchKey(kp, node, context);
}
