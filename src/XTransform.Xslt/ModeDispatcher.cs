// XSLT 2.0 §6 — template dispatch for xsl:apply-templates.
// For each node, assemble the candidate rules (specific-name bucket + wildcard + any-node)
// of the chosen mode, test each pattern bottom-up, and run the first matching rule. Fall
// through to the built-in rules when nothing matches.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;
using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>Dispatches a single context node to the best template rule in a mode.</summary>
internal static class ModeDispatcher
{
    /// <summary>
    /// Finds the best-matching template rule for <see cref="DynamicContext.ContextItem"/> in
    /// <paramref name="mode"/> and runs it; falls back to the built-in rules on no match.
    /// </summary>
    public static void Dispatch(
        DynamicContext context,
        IResultReceiver receiver,
        XsltRunState run,
        QName mode,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan callSite)
    {
        if (context.ContextItem is not XdmNode node)
        {
            // Atomic values go through the built-in text rule (string value); only nodes
            // participate in the match tables.
            BuiltInRules.Apply(context, receiver, run, withParams, callSite);
            return;
        }

        if (!run.Stylesheet.Modes.TryGetValue(mode, out ModeTable? table))
        {
            BuiltInRules.Apply(context, receiver, run, withParams, callSite);
            return;
        }

        TemplateRule? chosen = FindMatchingRule(table, node, context);
        if (chosen is null)
        {
            BuiltInRules.Apply(context, receiver, run, withParams, callSite);
            return;
        }

        InvokeRule(context, receiver, run, chosen, withParams, callSite);
    }

    // ── Candidate assembly + first-match selection ─────────────────────────

    private static TemplateRule? FindMatchingRule(ModeTable table, XdmNode node, DynamicContext context)
    {
        // Within each bucket the ModeCompiler has already sorted by
        // (precedence desc, priority desc, ordinal desc) so the first matching rule wins.
        // When we have more than one candidate bucket, we merge their heads by rank.
        List<IReadOnlyList<TemplateRule>> buckets = new List<IReadOnlyList<TemplateRule>>(3);

        switch (node.NodeKind)
        {
            case XdmNodeKind.Element:
                QName? nm = node.NodeName;
                if (nm.HasValue)
                {
                    QName lookup = NormaliseForLookup(nm.Value);
                    if (table.ElementsByName.TryGetValue(lookup, out List<TemplateRule>? named))
                    {
                        buckets.Add(named);
                    }
                }

                buckets.Add(table.ElementWildcard);
                break;

            case XdmNodeKind.Attribute:
                QName? an = node.NodeName;
                if (an.HasValue)
                {
                    QName lookup = NormaliseForLookup(an.Value);
                    if (table.AttributesByName.TryGetValue(lookup, out List<TemplateRule>? namedA))
                    {
                        buckets.Add(namedA);
                    }
                }

                buckets.Add(table.AttributeWildcard);
                break;

            case XdmNodeKind.Text:
            case XdmNodeKind.Comment:
            case XdmNodeKind.ProcessingInstruction:
            case XdmNodeKind.Document:
            case XdmNodeKind.Namespace:
                if (table.ByKind.TryGetValue(node.NodeKind, out List<TemplateRule>? kindBucket))
                {
                    buckets.Add(kindBucket);
                }

                break;
        }

        buckets.Add(table.AnyNode);

        // Merge-walk the pre-sorted buckets, taking the highest-rank candidate at each step
        // and testing it against the pattern matcher; return the first that matches.
        int[] cursors = new int[buckets.Count];
        while (true)
        {
            int bestBucket = -1;
            TemplateRule? best = null;

            for (int i = 0; i < buckets.Count; i++)
            {
                if (cursors[i] >= buckets[i].Count)
                {
                    continue;
                }

                TemplateRule candidate = buckets[i][cursors[i]];
                if (best is null || RankGreater(candidate, best))
                {
                    best = candidate;
                    bestBucket = i;
                }
            }

            if (best is null)
            {
                return null;
            }

            cursors[bestBucket]++;
            if (PatternMatcher.Matches(best.Pattern, node, context))
            {
                return best;
            }
        }
    }

    private static bool RankGreater(TemplateRule a, TemplateRule b)
    {
        if (a.ImportPrecedence != b.ImportPrecedence)
        {
            return a.ImportPrecedence > b.ImportPrecedence;
        }

        if (a.Priority != b.Priority)
        {
            return a.Priority > b.Priority;
        }

        return a.DocumentOrderOrdinal > b.DocumentOrderOrdinal;
    }

    private static QName NormaliseForLookup(QName name) =>
        new QName(string.Empty, name.NamespaceUri, name.LocalName);

    // ── Invocation ─────────────────────────────────────────────────────────

    private static void InvokeRule(
        DynamicContext context,
        IResultReceiver receiver,
        XsltRunState run,
        TemplateRule rule,
        IReadOnlyList<WithParamInstruction> withParams,
        SourceSpan callSite)
    {
        if (!run.Stylesheet.CompiledTemplates.TryGetValue(rule.Source, out CompiledTemplate? compiled))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0700,
                $"Template rule at {rule.Location} has no compiled body",
                callSite);
        }

        TemplateInvoker.Invoke(context, receiver, run, compiled, rule.Source, withParams, callSite);
    }
}
