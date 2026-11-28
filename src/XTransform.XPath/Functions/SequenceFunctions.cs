// F&O §15.1 and §15.2 — sequence functions.

using System;
using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.Functions;

/// <summary>
/// Implementations of the F&amp;O §15.1 and §15.2 sequence manipulation functions:
/// fn:empty, fn:exists, fn:index-of, fn:distinct-values, fn:insert-before,
/// fn:remove, fn:reverse, fn:subsequence, fn:unordered, fn:zero-or-one,
/// fn:one-or-more, fn:exactly-one, fn:deep-equal, fn:id, fn:idref.
/// </summary>
internal static class SequenceFunctions
{
    // =========================================================================
    // F&O §15.1.1 — fn:empty
    // =========================================================================

    /// <summary>Implements fn:empty($arg as item()*) as xs:boolean.</summary>
    // F&O §15.1.1
    internal static IXdmIterator FnEmpty(DynamicContext ctx, XdmSequence[] args)
    {
        // Arg is item()* — FunctionConversion materialises to XdmSequence before we see it.
        bool isEmpty = args[0].Count == 0;
        return new SingleItemIterator(XdmAtomicValue.Boolean(isEmpty));
    }

    // =========================================================================
    // F&O §15.1.2 — fn:exists
    // =========================================================================

    /// <summary>Implements fn:exists($arg as item()*) as xs:boolean.</summary>
    // F&O §15.1.2
    internal static IXdmIterator FnExists(DynamicContext ctx, XdmSequence[] args)
    {
        bool exists = args[0].Count > 0;
        return new SingleItemIterator(XdmAtomicValue.Boolean(exists));
    }

    // =========================================================================
    // F&O §15.1.3 — fn:distinct-values
    // =========================================================================

    /// <summary>Implements fn:distinct-values($arg as xs:anyAtomicType*) as xs:anyAtomicType*.</summary>
    // F&O §15.1.3
    internal static IXdmIterator FnDistinctValues1(DynamicContext ctx, XdmSequence[] args)
    {
        return DistinctValuesImpl(args[0]);
    }

    /// <summary>Implements fn:distinct-values($arg as xs:anyAtomicType*, $collation as xs:string) as xs:anyAtomicType*.</summary>
    // F&O §15.1.3 — collation parameter accepted but ignored (default collation used).
    internal static IXdmIterator FnDistinctValues2(DynamicContext ctx, XdmSequence[] args)
    {
        // Collation is accepted but we use the default (Unicode code point) collation.
        return DistinctValuesImpl(args[0]);
    }

    /// <summary>
    /// Core distinct-values implementation: yields the first occurrence of each distinct value.
    /// Two values are distinct unless ValueEqual returns true.
    /// Special case: NaN == NaN per F&amp;O §15.1.3.
    /// O(n²) — acceptable for typical sequence sizes.
    /// </summary>
    // F&O §15.1.3
    private static IXdmIterator DistinctValuesImpl(XdmSequence seq)
    {
        if (seq.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        List<XdmAtomicValue> result = new List<XdmAtomicValue>(seq.Count);
        int seqCount = seq.Count;
        for (int i = 0; i < seqCount; i++)
        {
            XdmAtomicValue av = (XdmAtomicValue)seq[i];
            bool alreadySeen = false;
            int resultCount = result.Count;
            for (int j = 0; j < resultCount; j++)
            {
                XdmAtomicValue existing = result[j];
                // F&O §15.1.3: NaN equals NaN in distinct-values (unlike op:numeric-equal).
                if (BothNaN(av, existing))
                {
                    alreadySeen = true;
                    break;
                }

                if (Operators.ComparisonOperators.ValueEqual(av, existing) == true)
                {
                    alreadySeen = true;
                    break;
                }
            }

            if (!alreadySeen)
            {
                result.Add(av);
            }
        }

        if (result.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        XdmItem[] items = new XdmItem[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            items[i] = result[i];
        }

        return new ArrayIterator(items);
    }

    // =========================================================================
    // F&O §15.1.4 — fn:index-of
    // =========================================================================

    /// <summary>
    /// Implements fn:index-of($seqParam as xs:anyAtomicType*, $search as xs:anyAtomicType)
    /// as xs:integer*.
    /// </summary>
    // F&O §15.1.4
    internal static IXdmIterator FnIndexOf2(DynamicContext ctx, XdmSequence[] args)
    {
        return IndexOfImpl(args[0], args[1]);
    }

    /// <summary>
    /// Implements fn:index-of($seqParam as xs:anyAtomicType*, $search as xs:anyAtomicType,
    /// $collation as xs:string) as xs:integer*.
    /// </summary>
    // F&O §15.1.4 — collation accepted, default used.
    internal static IXdmIterator FnIndexOf3(DynamicContext ctx, XdmSequence[] args)
    {
        return IndexOfImpl(args[0], args[1]);
    }

    /// <summary>Core index-of implementation.</summary>
    // F&O §15.1.4
    private static IXdmIterator IndexOfImpl(XdmSequence seq, XdmSequence targetSeq)
    {
        XdmItem? targetItem = targetSeq.FirstOrNull();
        if (targetItem is null)
        {
            return EmptyIterator.Instance;
        }

        XdmAtomicValue target = (XdmAtomicValue)targetItem;
        List<XdmItem> matches = new List<XdmItem>();
        int seqCount = seq.Count;
        for (int i = 0; i < seqCount; i++)
        {
            XdmAtomicValue item = (XdmAtomicValue)seq[i];
            bool equal = BothNaN(item, target) || Operators.ComparisonOperators.ValueEqual(item, target) == true;
            if (equal)
            {
                matches.Add(XdmAtomicValue.Integer((long)(i + 1)));
            }
        }

        if (matches.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        return new ArrayIterator(matches.ToArray());
    }

    // =========================================================================
    // F&O §15.1.5 — fn:insert-before
    // =========================================================================

    /// <summary>
    /// Implements fn:insert-before($target as item()*, $position as xs:integer,
    /// $inserts as item()*) as item()*.
    /// </summary>
    // F&O §15.1.5
    internal static IXdmIterator FnInsertBefore(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence target = args[0];
        long pos = ((XdmAtomicValue)args[1][0]).AsInt64();
        XdmSequence inserts = args[2];

        int targetCount = target.Count;
        int insertsCount = inserts.Count;

        if (insertsCount == 0)
        {
            return target.GetIterator();
        }

        // F&O §15.1.5: pos ≤ 0 → prepend; pos > length → append; else insert at pos.
        int insertAt;
        if (pos <= 0)
        {
            insertAt = 0; // before first item
        }
        else if (pos > targetCount)
        {
            insertAt = targetCount; // after last item
        }
        else
        {
            insertAt = (int)(pos - 1); // 0-based index (insert before this index)
        }

        XdmItem[] result = new XdmItem[targetCount + insertsCount];
        int idx = 0;

        // Items before insertion point.
        for (int i = 0; i < insertAt; i++)
        {
            result[idx++] = target[i];
        }

        // Inserted items.
        for (int i = 0; i < insertsCount; i++)
        {
            result[idx++] = inserts[i];
        }

        // Items from insertion point onward.
        for (int i = insertAt; i < targetCount; i++)
        {
            result[idx++] = target[i];
        }

        return new ArrayIterator(result);
    }

    // =========================================================================
    // F&O §15.1.6 — fn:remove
    // =========================================================================

    /// <summary>
    /// Implements fn:remove($target as item()*, $position as xs:integer) as item()*.
    /// </summary>
    // F&O §15.1.6
    internal static IXdmIterator FnRemove(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence target = args[0];
        long pos = ((XdmAtomicValue)args[1][0]).AsInt64();
        int targetCount = target.Count;

        // F&O §15.1.6: position out of range → return original sequence.
        if (pos < 1 || pos > targetCount)
        {
            return target.GetIterator();
        }

        if (targetCount == 1)
        {
            // The only item is removed.
            return EmptyIterator.Instance;
        }

        int removeIndex = (int)(pos - 1); // 0-based
        XdmItem[] result = new XdmItem[targetCount - 1];
        int idx = 0;
        for (int i = 0; i < targetCount; i++)
        {
            if (i != removeIndex)
            {
                result[idx++] = target[i];
            }
        }

        return new ArrayIterator(result);
    }

    // =========================================================================
    // F&O §15.1.7 — fn:reverse
    // =========================================================================

    /// <summary>Implements fn:reverse($arg as item()*) as item()*.</summary>
    // F&O §15.1.7
    internal static IXdmIterator FnReverse(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence seq = args[0];
        int count = seq.Count;

        if (count <= 1)
        {
            return seq.GetIterator();
        }

        XdmItem[] result = new XdmItem[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = seq[count - 1 - i];
        }

        return new ArrayIterator(result);
    }

    // =========================================================================
    // F&O §15.1.10 — fn:subsequence
    // =========================================================================

    /// <summary>
    /// Implements fn:subsequence($sourceSeq as item()*, $startingLoc as xs:double) as item()*.
    /// </summary>
    // F&O §15.1.10
    internal static IXdmIterator FnSubsequence2(DynamicContext ctx, XdmSequence[] args)
    {
        return SubsequenceImpl(args[0], args[1], null);
    }

    /// <summary>
    /// Implements fn:subsequence($sourceSeq as item()*, $startingLoc as xs:double,
    /// $length as xs:double) as item()*.
    /// </summary>
    // F&O §15.1.10
    internal static IXdmIterator FnSubsequence3(DynamicContext ctx, XdmSequence[] args)
    {
        return SubsequenceImpl(args[0], args[1], args[2]);
    }

    /// <summary>
    /// Core subsequence implementation.
    /// Rounds start and length using half-toward-+∞ (i.e. floor(x + 0.5)).
    /// </summary>
    // F&O §15.1.10
    private static IXdmIterator SubsequenceImpl(
        XdmSequence seq, XdmSequence startArg, XdmSequence? lengthArg)
    {
        double startD = ((XdmAtomicValue)startArg[0]).AsDouble();

        // F&O §15.1.10: NaN start → empty.
        if (double.IsNaN(startD))
        {
            return EmptyIterator.Instance;
        }

        double? lengthD = null;
        if (lengthArg != null && lengthArg.Count > 0)
        {
            lengthD = ((XdmAtomicValue)lengthArg[0]).AsDouble();
            // F&O §15.1.10: NaN length → empty.
            if (double.IsNaN(lengthD.Value))
            {
                return EmptyIterator.Instance;
            }
        }

        // F&O §15.1.10: round half toward +∞: floor(x + 0.5).
        long iStart = (long)Math.Floor(startD + 0.5);

        long iLength;
        bool hasLength = lengthD.HasValue;
        if (hasLength)
        {
            // Handle Infinity: +Inf length from finite start → take to end.
            // -Inf start + +Inf length is undefined; spec says "empty" in practice.
            if (double.IsPositiveInfinity(lengthD!.Value))
            {
                iLength = long.MaxValue;
            }
            else if (double.IsNegativeInfinity(lengthD.Value))
            {
                iLength = long.MinValue;
            }
            else
            {
                iLength = (long)Math.Floor(lengthD.Value + 0.5);
            }
        }
        else
        {
            iLength = long.MaxValue;
        }

        int seqCount = seq.Count;

        // Effective 1-based window: [first, last].
        long first = iStart < 1 ? 1 : iStart;
        long last;
        if (iLength == long.MaxValue)
        {
            last = (long)seqCount;
        }
        else if (iLength == long.MinValue)
        {
            // Negative-infinite length → empty.
            return EmptyIterator.Instance;
        }
        else
        {
            // last = iStart + iLength - 1 (may overflow; clamp to seqCount).
            long rawLast = iStart + iLength - 1L;
            last = rawLast < 0 ? 0 : (rawLast > seqCount ? (long)seqCount : rawLast);
        }

        if (first > last || first > seqCount)
        {
            return EmptyIterator.Instance;
        }

        int ifirst = (int)first;        // first is clamped ≥ 1 and ≤ seqCount above by check
        int ilast = (int)last;          // last ≤ seqCount
        int resultCount = ilast - ifirst + 1;

        if (resultCount <= 0)
        {
            return EmptyIterator.Instance;
        }

        // Return a window into the already-materialised XdmSequence. Build a small array.
        // For large lazy sequences note that FunctionConversion already drained the iterator.
        XdmItem[] result = new XdmItem[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            result[i] = seq[ifirst - 1 + i]; // seq is 0-indexed
        }

        return new ArrayIterator(result);
    }

    // =========================================================================
    // F&O §15.1.11 — fn:unordered
    // =========================================================================

    /// <summary>
    /// Implements fn:unordered($sourceSeq as item()*) as item()*.
    /// Order is implementation-defined; we preserve the input order.
    /// </summary>
    // F&O §15.1.11
    internal static IXdmIterator FnUnordered(DynamicContext ctx, XdmSequence[] args)
    {
        // F&O §15.1.11: return the items in any order. We preserve the input order.
        return args[0].GetIterator();
    }

    // =========================================================================
    // F&O §15.2.3 — fn:zero-or-one
    // =========================================================================

    /// <summary>
    /// Implements fn:zero-or-one($arg as item()*) as item()?.
    /// Raises FORG0003 if the sequence contains more than one item.
    /// </summary>
    // F&O §15.2.3
    internal static IXdmIterator FnZeroOrOne(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence seq = args[0];
        if (seq.Count > 1)
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0003,
                "fn:zero-or-one: sequence contains more than one item.",
                default);
        }

        return seq.GetIterator();
    }

    // =========================================================================
    // F&O §15.2.4 — fn:one-or-more
    // =========================================================================

    /// <summary>
    /// Implements fn:one-or-more($arg as item()*) as item()+.
    /// Raises FORG0004 if the sequence is empty.
    /// </summary>
    // F&O §15.2.4
    internal static IXdmIterator FnOneOrMore(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence seq = args[0];
        if (seq.Count == 0)
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0004,
                "fn:one-or-more: sequence is empty.",
                default);
        }

        return seq.GetIterator();
    }

    // =========================================================================
    // F&O §15.2.5 — fn:exactly-one
    // =========================================================================

    /// <summary>
    /// Implements fn:exactly-one($arg as item()*) as item().
    /// Raises FORG0005 if the sequence does not contain exactly one item.
    /// </summary>
    // F&O §15.2.5
    internal static IXdmIterator FnExactlyOne(DynamicContext ctx, XdmSequence[] args)
    {
        XdmSequence seq = args[0];
        if (seq.Count != 1)
        {
            throw new XPathDynamicException(
                XPathErrors.FORG0005,
                $"fn:exactly-one: sequence contains {seq.Count} item(s); exactly one required.",
                default);
        }

        return seq.GetIterator();
    }

    // =========================================================================
    // F&O §15.3.1 — fn:deep-equal
    // =========================================================================

    /// <summary>
    /// Implements fn:deep-equal($parameter1 as item()*, $parameter2 as item()*) as xs:boolean.
    /// </summary>
    // F&O §15.3.1
    internal static IXdmIterator FnDeepEqual2(DynamicContext ctx, XdmSequence[] args)
    {
        bool result = DeepEqualSeq(args[0], args[1]);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>
    /// Implements fn:deep-equal($parameter1 as item()*, $parameter2 as item()*,
    /// $collation as xs:string) as xs:boolean.
    /// Collation parameter accepted; default collation used.
    /// </summary>
    // F&O §15.3.1
    internal static IXdmIterator FnDeepEqual3(DynamicContext ctx, XdmSequence[] args)
    {
        // Collation is accepted but we use the default (Unicode code-point) collation.
        bool result = DeepEqualSeq(args[0], args[1]);
        return new SingleItemIterator(XdmAtomicValue.Boolean(result));
    }

    /// <summary>Structural equality of two sequences.</summary>
    // F&O §15.3.1
    private static bool DeepEqualSeq(XdmSequence a, XdmSequence b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        int count = a.Count;
        for (int i = 0; i < count; i++)
        {
            if (!DeepEqualItem(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Structural equality of two items.</summary>
    // F&O §15.3.1
    private static bool DeepEqualItem(XdmItem a, XdmItem b)
    {
        if (a is XdmAtomicValue av1 && b is XdmAtomicValue av2)
        {
            // F&O §15.3.1: NaN deep-equals NaN.
            if (BothNaN(av1, av2))
            {
                return true;
            }

            // The atomic values are compared with eq, where xs:untypedAtomic counts as xs:string.
            if (av1.TypeCode == XdmTypeCode.UntypedAtomic)
            {
                av1 = XdmAtomicValue.String(av1.GetStringValue());
            }

            if (av2.TypeCode == XdmTypeCode.UntypedAtomic)
            {
                av2 = XdmAtomicValue.String(av2.GetStringValue());
            }

            bool? eq = Operators.ComparisonOperators.ValueEqual(av1, av2);
            return eq == true;
        }

        if (a is XdmNode na && b is XdmNode nb)
        {
            return DeepEqualNode(na, nb);
        }

        // One atomic, one node → not equal.
        return false;
    }

    /// <summary>Structural equality of two nodes.</summary>
    // F&O §15.3.1
    private static bool DeepEqualNode(XdmNode a, XdmNode b)
    {
        if (a.NodeKind != b.NodeKind)
        {
            return false;
        }

        switch (a.NodeKind)
        {
            case XdmNodeKind.Document:
            {
                // F&O §15.3.1: deep-equal of document nodes compares their children.
                return DeepEqualChildren(a, b);
            }

            case XdmNodeKind.Element:
            {
                if (!QNameEqual(a.NodeName, b.NodeName))
                {
                    return false;
                }

                if (!DeepEqualAttributes(a, b))
                {
                    return false;
                }

                return DeepEqualChildren(a, b);
            }

            case XdmNodeKind.Attribute:
            {
                if (!QNameEqual(a.NodeName, b.NodeName))
                {
                    return false;
                }

                // F&O §15.3.1: for untyped attributes, compare string values.
                return string.Equals(a.GetStringValue(), b.GetStringValue(), StringComparison.Ordinal);
            }

            case XdmNodeKind.Text:
            case XdmNodeKind.Comment:
            {
                return string.Equals(a.GetStringValue(), b.GetStringValue(), StringComparison.Ordinal);
            }

            case XdmNodeKind.ProcessingInstruction:
            {
                // F&O §15.3.1: same target (local name) and same content (string value).
                string? aLocal = a.NodeName?.LocalName;
                string? bLocal = b.NodeName?.LocalName;
                return string.Equals(aLocal, bLocal, StringComparison.Ordinal)
                    && string.Equals(a.GetStringValue(), b.GetStringValue(), StringComparison.Ordinal);
            }

            case XdmNodeKind.Namespace:
            {
                // F&O §15.3.1: same prefix and same URI.
                string? aLocal = a.NodeName?.LocalName;
                string? bLocal = b.NodeName?.LocalName;
                return string.Equals(aLocal, bLocal, StringComparison.Ordinal)
                    && string.Equals(a.GetStringValue(), b.GetStringValue(), StringComparison.Ordinal);
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Compares the child node sequences of two nodes for deep-equality.
    /// </summary>
    // F&O §15.3.1
    private static bool DeepEqualChildren(XdmNode a, XdmNode b)
    {
        // Collect children via the child axis.
        List<XdmNode> aChildren = new List<XdmNode>();
        List<XdmNode> bChildren = new List<XdmNode>();

        foreach (XdmNode child in a.Axis(Xdm.Axis.Child))
        {
            aChildren.Add(child);
        }

        foreach (XdmNode child in b.Axis(Xdm.Axis.Child))
        {
            bChildren.Add(child);
        }

        if (aChildren.Count != bChildren.Count)
        {
            return false;
        }

        int count = aChildren.Count;
        for (int i = 0; i < count; i++)
        {
            if (!DeepEqualNode(aChildren[i], bChildren[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares the attribute sets of two element nodes for deep-equality (order-independent).
    /// </summary>
    // F&O §15.3.1
    private static bool DeepEqualAttributes(XdmNode a, XdmNode b)
    {
        List<XdmNode> aAttrs = new List<XdmNode>();
        List<XdmNode> bAttrs = new List<XdmNode>();

        foreach (XdmNode attr in a.Axis(Xdm.Axis.Attribute))
        {
            aAttrs.Add(attr);
        }

        foreach (XdmNode attr in b.Axis(Xdm.Axis.Attribute))
        {
            bAttrs.Add(attr);
        }

        if (aAttrs.Count != bAttrs.Count)
        {
            return false;
        }

        // F&O §15.3.1: attribute order is implementation-defined; use name-matching.
        // For each attribute in a, find the matching attribute in b.
        int count = aAttrs.Count;
        for (int i = 0; i < count; i++)
        {
            XdmNode aAttr = aAttrs[i];
            bool found = false;
            for (int j = 0; j < count; j++)
            {
                XdmNode bAttr = bAttrs[j];
                if (QNameEqual(aAttr.NodeName, bAttr.NodeName))
                {
                    if (!string.Equals(aAttr.GetStringValue(), bAttr.GetStringValue(),
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    // =========================================================================
    // F&O §15.5.1 — fn:id
    // =========================================================================

    /// <summary>
    /// Implements fn:id($arg as xs:string*) as element()*.
    /// Uses the context item's root as the target tree.
    /// </summary>
    // F&O §15.5.1
    internal static IXdmIterator FnId1(DynamicContext ctx, XdmSequence[] args)
    {
        if (ctx.ContextItem is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:id: no context item is available.",
                default);
        }

        if (!(ctx.ContextItem is XdmNode contextNode))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:id: context item is not a node.",
                default);
        }

        XdmNode root = GetRoot(contextNode);
        return IdImpl(args[0], root);
    }

    /// <summary>
    /// Implements fn:id($arg as xs:string*, $node as node()) as element()*.
    /// Uses the given node's root tree.
    /// </summary>
    // F&O §15.5.1
    internal static IXdmIterator FnId2(DynamicContext ctx, XdmSequence[] args)
    {
        XdmItem? nodeItem = args[1].FirstOrNull();
        if (nodeItem is null)
        {
            return EmptyIterator.Instance;
        }

        if (!(nodeItem is XdmNode node))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:id: second argument must be a node.",
                default);
        }

        XdmNode root = GetRoot(node);
        return IdImpl(args[0], root);
    }

    /// <summary>Core id implementation: collect IDs, walk descendants, match @id attributes.</summary>
    // F&O §15.5.1 — simplified for v1 untyped trees: looks up elements by @id attribute value.
    private static IXdmIterator IdImpl(XdmSequence idStrings, XdmNode root)
    {
        // Collect the set of ID values from whitespace-separated tokens in each string arg.
        HashSet<string> idSet = new HashSet<string>(StringComparer.Ordinal);
        int count = idStrings.Count;
        for (int i = 0; i < count; i++)
        {
            string s = ((XdmAtomicValue)idStrings[i]).AsString();
            CollectTokens(s, idSet);
        }

        if (idSet.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        // A tree with ID information (a DTD, or xml:id) answers from its ID table (F&O §15.5.1).
        TinyTree idTree = root.Handle.Tree;
        if (idTree.HasIdInformation)
        {
            List<int> found = new List<int>();
            foreach (string id in idSet)
            {
                if (idTree.IdMap.TryGetValue(id, out int element))
                {
                    found.Add(element);
                }
            }

            found.Sort();
            List<XdmItem> elements = new List<XdmItem>(found.Count);
            foreach (int element in found)
            {
                elements.Add(new XdmNode(NodeHandle.ForNode(idTree, element)));
            }

            return elements.Count == 0 ? EmptyIterator.Instance : new ArrayIterator(elements.ToArray());
        }

        // Without any, the elements with an id attribute stand for it.
        // Walk all element descendants of root, checking @id attribute.
        // Use a HashSet for deduplication; order by document order (descendant axis is doc-order).
        List<XdmItem> matches = new List<XdmItem>();
        foreach (XdmNode desc in root.Axis(Xdm.Axis.DescendantOrSelf))
        {
            if (desc.NodeKind != XdmNodeKind.Element)
            {
                continue;
            }

            foreach (XdmNode attr in desc.Axis(Xdm.Axis.Attribute))
            {
                // F&O §15.5.1: look for attribute named "id" (no namespace for untyped trees).
                QName? attrName = attr.NodeName;
                if (attrName is null)
                {
                    continue;
                }

                QName attrNameVal = attrName.Value;
                if (!string.Equals(attrNameVal.LocalName, "id", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(attrNameVal.NamespaceUri))
                {
                    string attrVal = attr.GetStringValue();
                    if (idSet.Contains(attrVal))
                    {
                        matches.Add(desc);
                    }
                }
            }
        }

        if (matches.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        return new ArrayIterator(matches.ToArray());
    }

    // =========================================================================
    // F&O §15.5.2 — fn:idref
    // =========================================================================

    /// <summary>
    /// Implements fn:idref($arg as xs:string*) as node()*.
    /// Uses the context item's root.
    /// </summary>
    // F&O §15.5.2
    internal static IXdmIterator FnIdRef1(DynamicContext ctx, XdmSequence[] args)
    {
        if (ctx.ContextItem is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "fn:idref: no context item is available.",
                default);
        }

        if (!(ctx.ContextItem is XdmNode contextNode))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:idref: context item is not a node.",
                default);
        }

        XdmNode root = GetRoot(contextNode);
        return IdRefImpl(args[0], root);
    }

    /// <summary>
    /// Implements fn:idref($arg as xs:string*, $node as node()) as node()*.
    /// Uses the given node's root tree.
    /// </summary>
    // F&O §15.5.2
    internal static IXdmIterator FnIdRef2(DynamicContext ctx, XdmSequence[] args)
    {
        XdmItem? nodeItem = args[1].FirstOrNull();
        if (nodeItem is null)
        {
            return EmptyIterator.Instance;
        }

        if (!(nodeItem is XdmNode node))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "fn:idref: second argument must be a node.",
                default);
        }

        XdmNode root = GetRoot(node);
        return IdRefImpl(args[0], root);
    }

    /// <summary>
    /// Core idref implementation: find nodes whose attribute values contain the given IDs
    /// as whitespace-separated tokens.
    /// V1 untyped: walks all attribute nodes and checks token membership.
    /// </summary>
    // F&O §15.5.2 — simplified for v1 untyped trees.
    private static IXdmIterator IdRefImpl(XdmSequence idStrings, XdmNode root)
    {
        HashSet<string> idSet = new HashSet<string>(StringComparer.Ordinal);
        int count = idStrings.Count;
        for (int i = 0; i < count; i++)
        {
            string s = ((XdmAtomicValue)idStrings[i]).AsString();
            CollectTokens(s, idSet);
        }

        if (idSet.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        // A tree with ID information answers from the attributes declared IDREF or IDREFS (F&O §15.5.2).
        TinyTree refTree = root.Handle.Tree;
        if (refTree.HasIdInformation)
        {
            List<XdmItem> refs = new List<XdmItem>();
            for (int a = 0; a < refTree.AttributeCount; a++)
            {
                if ((refTree.GetAttributeFlags(a) & DtdInfo.IdRefFlag) != 0 && ContainsAnyToken(refTree.GetAttributeValue(a), idSet))
                {
                    refs.Add(new XdmNode(NodeHandle.ForAttribute(refTree, a)));
                }
            }

            return refs.Count == 0 ? EmptyIterator.Instance : new ArrayIterator(refs.ToArray());
        }

        // Walk all element descendants; check each attribute for token membership.
        List<XdmItem> matches = new List<XdmItem>();
        foreach (XdmNode desc in root.Axis(Xdm.Axis.DescendantOrSelf))
        {
            if (desc.NodeKind != XdmNodeKind.Element)
            {
                continue;
            }

            foreach (XdmNode attr in desc.Axis(Xdm.Axis.Attribute))
            {
                string attrVal = attr.GetStringValue();
                if (ContainsAnyToken(attrVal, idSet))
                {
                    matches.Add(attr);
                }
            }
        }

        if (matches.Count == 0)
        {
            return EmptyIterator.Instance;
        }

        return new ArrayIterator(matches.ToArray());
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    /// <summary>
    /// Returns true if both values are numeric NaN (xs:double NaN or xs:float NaN).
    /// Used for NaN handling in distinct-values, index-of, and deep-equal per F&amp;O.
    /// </summary>
    private static bool BothNaN(XdmAtomicValue a, XdmAtomicValue b)
    {
        return IsNaN(a) && IsNaN(b);
    }

    /// <summary>Returns true if the value is numeric NaN (xs:double NaN or xs:float NaN).</summary>
    private static bool IsNaN(XdmAtomicValue v)
    {
        switch (v.TypeCode)
        {
            case XdmTypeCode.Double:
                return double.IsNaN(v.AsDouble());
            case XdmTypeCode.Float:
                return float.IsNaN(v.AsFloat());
            default:
                return false;
        }
    }

    /// <summary>Null-safe expanded-name equality.</summary>
    private static bool QNameEqual(QName? a, QName? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        QName av = a.Value;
        QName bv = b.Value;
        return string.Equals(av.NamespaceUri, bv.NamespaceUri, StringComparison.Ordinal)
            && string.Equals(av.LocalName, bv.LocalName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Navigates to the root of the tree containing <paramref name="node"/>
    /// by following the ancestor-or-self axis to the topmost node.
    /// </summary>
    private static XdmNode GetRoot(XdmNode node)
    {
        XdmNode current = node;
        while (current.Parent != null)
        {
            current = current.Parent;
        }

        return current;
    }

    /// <summary>
    /// Splits <paramref name="value"/> on XML whitespace and adds each token to
    /// <paramref name="set"/>.
    /// </summary>
    private static void CollectTokens(string value, HashSet<string> set)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // Split on XML whitespace: space (0x20), tab (0x09), newline (0x0A), CR (0x0D).
        int start = -1;
        int length = value.Length;
        for (int i = 0; i <= length; i++)
        {
            bool isWs = i == length || IsXmlWhitespace(value[i]);
            if (!isWs && start < 0)
            {
                start = i;
            }
            else if (isWs && start >= 0)
            {
                set.Add(value.Substring(start, i - start));
                start = -1;
            }
        }
    }

    /// <summary>
    /// Returns true if any whitespace-separated token in <paramref name="value"/> is in
    /// <paramref name="idSet"/>.
    /// </summary>
    private static bool ContainsAnyToken(string value, HashSet<string> idSet)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int start = -1;
        int length = value.Length;
        for (int i = 0; i <= length; i++)
        {
            bool isWs = i == length || IsXmlWhitespace(value[i]);
            if (!isWs && start < 0)
            {
                start = i;
            }
            else if (isWs && start >= 0)
            {
                string token = value.Substring(start, i - start);
                if (idSet.Contains(token))
                {
                    return true;
                }

                start = -1;
            }
        }

        return false;
    }

    /// <summary>Returns true for XML 1.0 whitespace characters.</summary>
    private static bool IsXmlWhitespace(char c)
    {
        return c == ' ' || c == '\t' || c == '\n' || c == '\r';
    }
}
