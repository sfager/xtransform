// XSLT 2.0 §14 — xsl:for-each-group: group-by, group-adjacent, group-starting-with, group-ending-with.
//
// The population is the result of @select. Groups are formed in population order; the body runs once per group
// with the group's initial item as the context item, position() the number of the group and last() the number of
// groups. current-group() and current-grouping-key() read the state this instruction keeps on the run state.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt.Instructions;

/// <summary>How the population of an <c>xsl:for-each-group</c> is divided into groups.</summary>
internal enum GroupingMethod
{
    /// <summary><c>group-by</c>: an item joins one group per distinct key.</summary>
    By,

    /// <summary><c>group-adjacent</c>: adjacent items with equal keys form a group.</summary>
    Adjacent,

    /// <summary><c>group-starting-with</c>: a node matching the pattern starts a new group.</summary>
    StartingWith,

    /// <summary><c>group-ending-with</c>: a node matching the pattern ends the group.</summary>
    EndingWith,
}

/// <summary>Compiled <c>xsl:for-each-group</c>.</summary>
internal sealed class ForEachGroupInstruction : XsltInstruction
{
    /// <summary>Initializes the instruction. <paramref name="key"/> is used by group-by / group-adjacent, <paramref name="pattern"/> by the others.</summary>
    public ForEachGroupInstruction(
        BoundExpression select,
        GroupingMethod method,
        BoundExpression? key,
        PatternAst? pattern,
        CompiledAvt? collation,
        IReadOnlyList<SortKey> sorts,
        XsltInstruction body,
        SourceSpan location)
        : base(location)
    {
        Select = select;
        Method = method;
        Key = key;
        Pattern = pattern;
        Collation = collation;
        Sorts = sorts;
        Body = body;
    }

    /// <summary>The population.</summary>
    public BoundExpression Select { get; }

    /// <summary>The grouping method.</summary>
    public GroupingMethod Method { get; }

    /// <summary>The key expression of group-by / group-adjacent.</summary>
    public BoundExpression? Key { get; }

    /// <summary>The pattern of group-starting-with / group-ending-with.</summary>
    public PatternAst? Pattern { get; }

    /// <summary>The <c>collation</c> AVT.</summary>
    public CompiledAvt? Collation { get; }

    /// <summary>The <c>xsl:sort</c> keys that order the groups.</summary>
    public IReadOnlyList<SortKey> Sorts { get; }

    /// <summary>The body, run once per group.</summary>
    public XsltInstruction Body { get; }

    private sealed class Group
    {
        public readonly List<XdmItem> Items = new List<XdmItem>();
        public XdmAtomicValue? Key;
        public int LastItemIndex = -1;
    }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        List<XdmItem> population = new List<XdmItem>();
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            population.Add(item);
            if ((population.Count & 0xFF) == 0)
            {
                context.CheckCancellation();
            }
        }

        CheckCollation(context, run);
        List<Group> groups = Form(population, context, run);

        IReadOnlyList<XdmItem>? savedGroup = run.CurrentGroup;
        XdmAtomicValue? savedKey = run.CurrentGroupingKey;
        TemplateRule? savedRule = run.CurrentRule;
        run.CurrentRule = null; // no current template rule inside xsl:for-each-group (XSLT 2.0 §6.7)
        try
        {
            if (Sorts.Count > 0 && groups.Count > 0)
            {
                // The sort keys are computed once per group: the initial item is the context item, and the
                // group and its key are current (§14.3).
                List<XdmItem> initials = new List<XdmItem>(groups.Count);
                foreach (Group g in groups)
                {
                    initials.Add(g.Items[0]);
                }

                int[] order = ItemSorter.SortIndices(
                    initials,
                    Sorts,
                    context,
                    run,
                    i =>
                    {
                        run.CurrentGroup = groups[i].Items;
                        run.CurrentGroupingKey = groups[i].Key;
                    });

                List<Group> sorted = new List<Group>(groups.Count);
                for (int i = 0; i < order.Length; i++)
                {
                    sorted.Add(groups[order[i]]);
                }

                groups = sorted;
            }

            int size = groups.Count;
            for (int g = 0; g < size; g++)
            {
                context.CheckCancellation();
                Group group = groups[g];
                run.CurrentGroup = group.Items;
                run.CurrentGroupingKey = group.Key;
                DynamicContext child = context.WithCurrentItem(group.Items[0], position: g + 1, size: size);
                int marker = run.VariableScopeMarker;
                try
                {
                    Body.Execute(child, receiver, run);
                }
                finally
                {
                    run.RestoreVariablesTo(marker);
                }
            }
        }
        finally
        {
            run.CurrentGroup = savedGroup;
            run.CurrentGroupingKey = savedKey;
            run.CurrentRule = savedRule;
        }
    }

    // Only the codepoint collation is available until P4-05; it is also the default.
    private void CheckCollation(DynamicContext context, XsltRunState run)
    {
        if (Collation is null)
        {
            return;
        }

        string uri = Collation.Stringify(context, run).Trim();
        if (uri.Length > 0 && !string.Equals(uri, ItemSorter.CodePointComparer.Uri, StringComparison.Ordinal))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1110,
                $"xsl:for-each-group: the collation '{uri}' is not supported",
                Location);
        }
    }

    // ── Forming the groups ───────────────────────────────────────────────────

    private List<Group> Form(List<XdmItem> population, DynamicContext context, XsltRunState run)
    {
        switch (Method)
        {
            case GroupingMethod.By:
                return FormBy(population, context, run);
            case GroupingMethod.Adjacent:
                return FormAdjacent(population, context, run);
            default:
                return FormByPattern(population, context);
        }
    }

    // group-by: an item joins one group for each distinct key; an item with no key joins none. Groups are kept
    // in order of first appearance and found through a hash of a normalised key (§14.3).
    private List<Group> FormBy(List<XdmItem> population, DynamicContext context, XsltRunState run)
    {
        List<Group> groups = new List<Group>();
        KeyTable byKey = new KeyTable();
        int n = population.Count;
        for (int i = 0; i < n; i++)
        {
            context.CheckCancellation();
            DynamicContext focus = context.WithCurrentItem(population[i], position: i + 1, size: n);
            IXdmIterator keys = Atomization.Atomize(Key!.Iterate(focus, run));
            XdmItem? raw;
            while ((raw = keys.Next()) is not null)
            {
                XdmAtomicValue key = Normalise((XdmAtomicValue)raw);
                Group? group = byKey.Find(key);
                if (group is null)
                {
                    group = new Group { Key = key };
                    byKey.Add(key, group);
                    groups.Add(group);
                }

                // An item is never added twice to one group, however many of its keys are equal.
                if (group.LastItemIndex != i)
                {
                    group.Items.Add(population[i]);
                    group.LastItemIndex = i;
                }
            }
        }

        return groups;
    }

    // group-adjacent: each item has exactly one key; an item whose key equals its predecessor's joins its group.
    private List<Group> FormAdjacent(List<XdmItem> population, DynamicContext context, XsltRunState run)
    {
        List<Group> groups = new List<Group>();
        Group? current = null;
        int n = population.Count;
        for (int i = 0; i < n; i++)
        {
            context.CheckCancellation();
            DynamicContext focus = context.WithCurrentItem(population[i], position: i + 1, size: n);
            IXdmIterator keys = Atomization.Atomize(Key!.Iterate(focus, run));
            XdmAtomicValue? first = keys.Next() as XdmAtomicValue;
            if (first is null || keys.Next() is not null)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTTE1100,
                    "The group-adjacent key of an item must be exactly one atomic value",
                    Location);
            }

            XdmAtomicValue key = Normalise(first);
            if (current is null || !SameKey(key, current.Key!))
            {
                current = new Group { Key = key };
                groups.Add(current);
            }

            current.Items.Add(population[i]);
        }

        return groups;
    }

    // group-starting-with / group-ending-with: the population must be nodes. The first node always starts a group.
    private List<Group> FormByPattern(List<XdmItem> population, DynamicContext context)
    {
        List<Group> groups = new List<Group>();
        foreach (XdmItem p in population)
        {
            if (p is not XdmNode)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTTE1120,
                    "With group-starting-with or group-ending-with every item of the population must be a node",
                    Location);
            }
        }

        Group? current = null;
        bool previousMatched = false;
        for (int i = 0; i < population.Count; i++)
        {
            context.CheckCancellation();
            XdmNode node = (XdmNode)population[i];
            bool matches = PatternMatcher.Matches(Pattern!, node, context.WithCurrentItem(node, position: i + 1, size: population.Count));
            bool startsGroup = current is null ||
                (Method == GroupingMethod.StartingWith ? matches : previousMatched);
            if (startsGroup)
            {
                current = new Group();
                groups.Add(current);
            }

            current!.Items.Add(node);
            previousMatched = matches;
        }

        return groups;
    }


    // Finds the group of a key. Strings, numbers and booleans are found through a hash of a normalised key; other
    // types (dates, times, durations, ...) are compared with eq one group at a time, because equality there depends on
    // time zones and normal forms. Values that cannot be compared with eq are different keys (§14.3).
    private sealed class KeyTable
    {
        private readonly Dictionary<string, Group> _hashed = new Dictionary<string, Group>(StringComparer.Ordinal);
        private readonly List<Group> _others = new List<Group>();

        public Group? Find(XdmAtomicValue key)
        {
            if (IsHashable(key))
            {
                return _hashed.TryGetValue(KeyId(key), out Group? found) ? found : null;
            }

            foreach (Group candidate in _others)
            {
                if (!IsHashable(candidate.Key!) && EqualUnderEq(candidate.Key!, key))
                {
                    return candidate;
                }
            }

            return null;
        }

        public void Add(XdmAtomicValue key, Group group)
        {
            if (IsHashable(key))
            {
                _hashed[KeyId(key)] = group;
            }
            else
            {
                _others.Add(group);
            }
        }
    }

    private static bool IsHashable(XdmAtomicValue key)
    {
        XdmTypeCode primitive = CastTable.PrimitiveTypeOf(key.TypeCode);
        return primitive == XdmTypeCode.String || primitive == XdmTypeCode.AnyUri || primitive == XdmTypeCode.Boolean ||
               XTransform.XPath.Operators.NumericPromotion.IsNumeric(key.TypeCode);
    }

    private static bool EqualUnderEq(XdmAtomicValue a, XdmAtomicValue b) =>
        XTransform.XPath.Operators.ComparisonOperators.ValueEqual(a, b) == true;

    // Whether two grouping keys are the same key.
    private static bool SameKey(XdmAtomicValue a, XdmAtomicValue b)
    {
        bool ha = IsHashable(a);
        bool hb = IsHashable(b);
        if (ha != hb)
        {
            return false;
        }

        return ha ? string.Equals(KeyId(a), KeyId(b), StringComparison.Ordinal) : EqualUnderEq(a, b);
    }
    // ── Keys ─────────────────────────────────────────────────────────────────

    // The grouping key of an item after atomization, with xs:untypedAtomic cast to xs:string (§14.2).
    private static XdmAtomicValue Normalise(XdmAtomicValue value) =>
        value.TypeCode == XdmTypeCode.UntypedAtomic ? XdmAtomicValue.String(value.GetStringValue()) : value;

    // A string that is equal for exactly the keys that are equal under eq (NaN equal to itself): strings and URIs by
    // their characters, numbers by value whatever their type, other types by type and lexical form. Values of types
    // that cannot be compared get different ids, so they land in different groups (§14.3).
    internal static string KeyId(XdmAtomicValue key)
    {
        XdmTypeCode primitive = CastTable.PrimitiveTypeOf(key.TypeCode);
        if (primitive == XdmTypeCode.String || primitive == XdmTypeCode.AnyUri)
        {
            return "S" + key.GetStringValue();
        }

        if (XTransform.XPath.Operators.NumericPromotion.IsNumeric(key.TypeCode))
        {
            return NumericId(key);
        }

        return "O" + ((int)primitive).ToString(CultureInfo.InvariantCulture) + ":" + key.GetStringValue();
    }

    private static string NumericId(XdmAtomicValue key)
    {
        switch (key.TypeCode)
        {
            case XdmTypeCode.Double:
                return FromDouble(key.AsDouble());
            case XdmTypeCode.Float:
                return FromDouble(key.AsFloat());
            case XdmTypeCode.Decimal:
            {
                decimal d = key.AsDecimal().Value;
                return decimal.Truncate(d) == d
                    ? "N" + new BigInteger(d).ToString(CultureInfo.InvariantCulture)
                    : "D" + ((double)d).ToString("R", CultureInfo.InvariantCulture);
            }

            default:
                return "N" + key.AsBigInteger().ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string FromDouble(double d)
    {
        if (double.IsNaN(d))
        {
            return "NaN";
        }

        if (double.IsInfinity(d))
        {
            return d > 0 ? "N+INF" : "N-INF";
        }

        // A whole number is the same key whatever its type (3, 3.0 and 3e0 are equal).
        return Math.Floor(d) == d
            ? "N" + new BigInteger(d).ToString(CultureInfo.InvariantCulture)
            : "D" + d.ToString("R", CultureInfo.InvariantCulture);
    }
}
