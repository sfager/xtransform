// XSLT 2.0 §12.2 — numbering based on position in a document: the integers produced by
// xsl:number when no value attribute is given.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt;

/// <summary>The level attribute of xsl:number.</summary>
internal enum NumberLevel
{
    Single,
    Multiple,
    Any,
}

internal static class NumberingCounter
{
    /// <summary>
    /// Computes the place-marker integers for <paramref name="selected"/>. The result is empty when the
    /// node has no counted ancestor within the <paramref name="from"/> boundary.
    /// </summary>
    public static List<long> Compute(
        NumberLevel level,
        XdmNode selected,
        PatternAst? count,
        PatternAst? from,
        DynamicContext context)
    {
        switch (level)
        {
            case NumberLevel.Multiple:
                return Multiple(selected, count, from, context);
            case NumberLevel.Any:
                return Any(selected, count, from, context);
            default:
                return Single(selected, count, from, context);
        }
    }

    // level="single": $A = $S/ancestor-or-self::node()[matches-count(.)][1];
    // $F = $S/ancestor-or-self::node()[matches-from(.)][1]; $AF = $A[ancestor-or-self::node()[. is $F]];
    // result 1 + count($AF/preceding-sibling::node()[matches-count(.)]). XSLT 2.0 §12.2.
    private static List<long> Single(XdmNode s, PatternAst? count, PatternAst? from, DynamicContext context)
    {
        List<long> result = new List<long>();
        XdmNode? a = null;
        for (XdmNode? n = s; n is not null; n = n.Parent)
        {
            if (MatchesCount(n, s, count, context))
            {
                a = n;
                break;
            }
        }

        if (a is null)
        {
            return result;
        }

        XdmNode? f = NearestFrom(s, from, context);
        if (f is null || !IsAncestorOrSelf(f, a))
        {
            return result;
        }

        result.Add(1 + PrecedingSiblingCount(a, s, count, context));
        return result;
    }

    // level="multiple": every counted ancestor-or-self inside the $F subtree, outermost first.
    private static List<long> Multiple(XdmNode s, PatternAst? count, PatternAst? from, DynamicContext context)
    {
        List<long> result = new List<long>();
        XdmNode? f = NearestFrom(s, from, context);
        if (f is null)
        {
            return result;
        }

        List<XdmNode> counted = new List<XdmNode>();
        for (XdmNode? n = s; n is not null; n = n.Parent)
        {
            if (MatchesCount(n, s, count, context) && IsAncestorOrSelf(f, n))
            {
                counted.Add(n);
            }
        }

        for (int i = counted.Count - 1; i >= 0; i--)
        {
            result.Add(1 + PrecedingSiblingCount(counted[i], s, count, context));
        }

        return result;
    }

    // level="any": $A = $S/(preceding::node()|ancestor-or-self::node())[matches-count(.)];
    // $F = ...[matches-from(.)][last()]; $AF = $A[. is $F or . >> $F]; result count($AF).
    private static List<long> Any(XdmNode s, PatternAst? count, PatternAst? from, DynamicContext context)
    {
        List<long> result = new List<long>();
        List<XdmNode> candidates = new List<XdmNode>();
        foreach (XdmNode n in s.Axis(Axis.Preceding))
        {
            candidates.Add(n);
        }

        for (XdmNode? n = s; n is not null; n = n.Parent)
        {
            candidates.Add(n);
        }

        XdmNode? f = null;
        foreach (XdmNode n in candidates)
        {
            if (MatchesFrom(n, from, context) && (f is null || n.CompareDocumentOrder(f) > 0))
            {
                f = n;
            }
        }

        if (f is null)
        {
            return result;
        }

        long total = 0;
        foreach (XdmNode n in candidates)
        {
            if (n.CompareDocumentOrder(f) >= 0 && MatchesCount(n, s, count, context))
            {
                total++;
            }
        }

        if (total > 0)
        {
            result.Add(total);
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static long PrecedingSiblingCount(XdmNode node, XdmNode selected, PatternAst? count, DynamicContext context)
    {
        long n = 0;
        foreach (XdmNode sibling in node.Axis(Axis.PrecedingSibling))
        {
            if (MatchesCount(sibling, selected, count, context))
            {
                n++;
            }
        }

        return n;
    }

    private static XdmNode? NearestFrom(XdmNode s, PatternAst? from, DynamicContext context)
    {
        for (XdmNode? n = s; n is not null; n = n.Parent)
        {
            if (MatchesFrom(n, from, context))
            {
                return n;
            }
        }

        return null;
    }

    private static bool IsAncestorOrSelf(XdmNode ancestor, XdmNode node)
    {
        for (XdmNode? n = node; n is not null; n = n.Parent)
        {
            if (n.Equals(ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // matches-from($node): the node matches the from pattern, or it is the root of its tree.
    private static bool MatchesFrom(XdmNode node, PatternAst? from, DynamicContext context) =>
        node.Parent is null || (from is not null && PatternMatcher.Matches(from, node, context));

    // matches-count($node): the node matches the count pattern or, when count is omitted, has the
    // same node kind and (if the selected node has one) the same expanded QName as the selected node.
    private static bool MatchesCount(XdmNode node, XdmNode selected, PatternAst? count, DynamicContext context)
    {
        if (count is not null)
        {
            return PatternMatcher.Matches(count, node, context);
        }

        if (node.NodeKind != selected.NodeKind)
        {
            return false;
        }

        QName? name = selected.NodeName;
        if (name is null)
        {
            return true;
        }

        QName? other = node.NodeName;
        return other is not null &&
            string.Equals(other.Value.NamespaceUri, name.Value.NamespaceUri, System.StringComparison.Ordinal) &&
            string.Equals(other.Value.LocalName, name.Value.LocalName, System.StringComparison.Ordinal);
    }
}
