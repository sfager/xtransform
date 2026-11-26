// XSLT 2.0 §13 — one xsl:sort key and the sorter that applies a list of keys to a sequence of items.

using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>One compiled <c>xsl:sort</c> element: how to compute and compare a sort key.</summary>
internal sealed class SortKey
{
    /// <summary>Initializes a sort key. Exactly one of <paramref name="select"/> and <paramref name="content"/> is non-null.</summary>
    public SortKey(
        BoundExpression? select,
        VariableValueBody? content,
        CompiledAvt? order,
        CompiledAvt? dataType,
        CompiledAvt? caseOrder,
        CompiledAvt? lang,
        CompiledAvt? collation,
        CompiledAvt? stable,
        SourceSpan location)
    {
        Select = select;
        Content = content;
        Order = order;
        DataType = dataType;
        CaseOrder = caseOrder;
        Lang = lang;
        Collation = collation;
        Stable = stable;
        Location = location;
    }

    /// <summary>The <c>select</c> expression (default <c>.</c> when there is neither it nor content).</summary>
    public BoundExpression? Select { get; }

    /// <summary>The content (sequence constructor) that computes the key when there is no <c>select</c>.</summary>
    public VariableValueBody? Content { get; }

    /// <summary>The <c>order</c> AVT.</summary>
    public CompiledAvt? Order { get; }

    /// <summary>The <c>data-type</c> AVT.</summary>
    public CompiledAvt? DataType { get; }

    /// <summary>The <c>case-order</c> AVT.</summary>
    public CompiledAvt? CaseOrder { get; }

    /// <summary>The <c>lang</c> AVT.</summary>
    public CompiledAvt? Lang { get; }

    /// <summary>The <c>collation</c> AVT.</summary>
    public CompiledAvt? Collation { get; }

    /// <summary>The <c>stable</c> AVT (sorting is always stable here; the value is only validated).</summary>
    public CompiledAvt? Stable { get; }

    /// <summary>Where the element is.</summary>
    public SourceSpan Location { get; }
}

/// <summary>Sorts a list of items by a list of <see cref="SortKey"/>s (XSLT 2.0 §13.1). The sort is stable.</summary>
internal static class ItemSorter
{
    private enum KeyKind
    {
        Default,
        Text,
        Number,
    }

    // A key value for one item: empty (and NaN) sort before everything else.
    private struct KeyValue
    {
        public bool Least;
        public double Number;
        public string? Text;
        public XdmAtomicValue? Atomic;
    }

    private sealed class ResolvedKey
    {
        public bool Descending;
        public KeyKind Kind;
        public IComparer<string> Collation = CodePointComparer.Instance;
        public KeyValue[] Values = Array.Empty<KeyValue>();
    }

    /// <summary>Returns <paramref name="items"/> sorted by <paramref name="keys"/>; the input list is not changed.</summary>
    internal static List<XdmItem> Sort(
        List<XdmItem> items,
        IReadOnlyList<SortKey> keys,
        DynamicContext context,
        XsltRunState run)
    {
        // The attributes are AVTs evaluated once, in the context of the instruction (§13.1.1).
        ResolvedKey[] resolved = new ResolvedKey[keys.Count];
        for (int k = 0; k < keys.Count; k++)
        {
            resolved[k] = Resolve(keys[k], context, run);
        }

        int n = items.Count;
        if (n < 2)
        {
            // Keys are still evaluated so that errors in them are reported.
            for (int k = 0; k < resolved.Length; k++)
            {
                resolved[k].Values = EvaluateKeys(keys[k], resolved[k], items, context, run);
            }

            return new List<XdmItem>(items);
        }

        for (int k = 0; k < resolved.Length; k++)
        {
            resolved[k].Values = EvaluateKeys(keys[k], resolved[k], items, context, run);
        }

        int[] order = new int[n];
        for (int i = 0; i < n; i++)
        {
            order[i] = i;
        }

        MergeSort(order, new int[n], 0, n, resolved, keys);

        List<XdmItem> result = new List<XdmItem>(n);
        for (int i = 0; i < n; i++)
        {
            result.Add(items[order[i]]);
        }

        return result;
    }

    // ── Attribute resolution ─────────────────────────────────────────────────

    private static ResolvedKey Resolve(SortKey key, DynamicContext context, XsltRunState run)
    {
        ResolvedKey r = new ResolvedKey();

        string? order = Evaluate(key.Order, context, run);
        if (order is not null)
        {
            if (order == "descending")
            {
                r.Descending = true;
            }
            else if (order != "ascending")
            {
                throw Invalid("order", order, key);
            }
        }

        string? dataType = Evaluate(key.DataType, context, run);
        if (dataType is not null)
        {
            if (dataType == "text")
            {
                r.Kind = KeyKind.Text;
            }
            else if (dataType == "number")
            {
                r.Kind = KeyKind.Number;
            }
            else if (dataType.IndexOf(':') < 0)
            {
                throw Invalid("data-type", dataType, key);
            }
        }

        string? caseOrder = Evaluate(key.CaseOrder, context, run);
        if (caseOrder is not null && caseOrder != "upper-first" && caseOrder != "lower-first")
        {
            throw Invalid("case-order", caseOrder, key);
        }

        string? stable = Evaluate(key.Stable, context, run);
        if (stable is not null && stable != "yes" && stable != "no")
        {
            throw Invalid("stable", stable, key);
        }

        string? lang = Evaluate(key.Lang, context, run);
        if (lang is not null && lang.Length > 0 && !IsValidLanguage(lang))
        {
            throw Invalid("lang", lang, key);
        }

        string? collation = Evaluate(key.Collation, context, run);
        r.Collation = ResolveCollation(collation, lang, caseOrder, key);
        return r;
    }

    /// <summary>True when the text is a valid xs:language value: letters, then hyphen-separated subtags of up to eight characters.</summary>
    internal static bool IsValidLanguage(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        int start = 0;
        bool first = true;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '-')
            {
                continue;
            }

            int length = i - start;
            if (length < 1 || length > 8)
            {
                return false;
            }

            for (int j = start; j < i; j++)
            {
                char c = text[j];
                bool letter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                bool digit = c >= '0' && c <= '9';
                if (!(letter || (!first && digit)))
                {
                    return false;
                }
            }

            first = false;
            start = i + 1;
        }

        return true;
    }

    private static string? Evaluate(CompiledAvt? avt, DynamicContext context, XsltRunState run) =>
        avt is null ? null : avt.Stringify(context, run).Trim();

    private static XsltDynamicException Invalid(string attribute, string value, SortKey key) =>
        new XsltDynamicException(
            XsltErrors.XTDE0030,
            $"xsl:sort: '{value}' is not a valid value for the {attribute} attribute",
            key.Location);

    private static IComparer<string> ResolveCollation(string? collation, string? lang, string? caseOrder, SortKey key)
    {
        if (!string.IsNullOrEmpty(collation))
        {
            if (string.Equals(collation, CodePointComparer.Uri, StringComparison.Ordinal))
            {
                return CodePointComparer.Instance;
            }

            throw new XsltDynamicException(
                XsltErrors.XTDE1035,
                $"xsl:sort: the collation '{collation}' is not supported",
                key.Location);
        }

        // No collation: a language or case order selects a culture-sensitive comparison (implementation-defined,
        // XSLT 2.0 §13.1.1); otherwise the default collation, which is Unicode codepoint order.
        CompareInfo? culture = null;
        if (!string.IsNullOrEmpty(lang))
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(lang!).CompareInfo;
            }
            catch (CultureNotFoundException)
            {
                culture = null;
            }
        }

        if (culture is null && caseOrder is null)
        {
            return CodePointComparer.Instance;
        }

        return new CultureComparer(culture ?? CultureInfo.InvariantCulture.CompareInfo, caseOrder);
    }

    // ── Key values ───────────────────────────────────────────────────────────

    private static KeyValue[] EvaluateKeys(
        SortKey key,
        ResolvedKey resolved,
        List<XdmItem> items,
        DynamicContext context,
        XsltRunState run)
    {
        int n = items.Count;
        KeyValue[] values = new KeyValue[n];
        for (int i = 0; i < n; i++)
        {
            context.CheckCancellation();
            DynamicContext focus = context.WithCurrentItem(items[i], position: i + 1, size: n);
            IXdmIterator source = key.Select is not null
                ? key.Select.Iterate(focus, run)
                : key.Content is not null
                    ? key.Content.Evaluate(focus, run).GetIterator()
                    : new XTransform.Xdm.Iterators.SingleItemIterator(items[i]);

            // The key is the atomised value; more than one item is an error (§13.1.2).
            IXdmIterator atoms = Atomization.Atomize(source);
            XdmAtomicValue? first = atoms.Next() as XdmAtomicValue;
            if (first is not null && atoms.Next() is not null)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTTE1020,
                    "A sort key must evaluate to a sequence of at most one item",
                    key.Location);
            }

            values[i] = MakeValue(first, resolved.Kind);
        }

        return values;
    }

    private static KeyValue MakeValue(XdmAtomicValue? value, KeyKind kind)
    {
        KeyValue kv = new KeyValue();
        if (value is null)
        {
            kv.Least = true;
            return kv;
        }

        switch (kind)
        {
            case KeyKind.Number:
            {
                double d = ToNumber(value);
                kv.Number = d;
                kv.Least = double.IsNaN(d);
                return kv;
            }

            case KeyKind.Text:
                kv.Text = value.GetStringValue();
                return kv;

            default:
                // An untyped value sorts as a string; NaN sorts first like the empty sequence.
                if (value.TypeCode == XdmTypeCode.UntypedAtomic)
                {
                    value = XdmAtomicValue.String(value.GetStringValue());
                }

                if ((value.TypeCode == XdmTypeCode.Double && double.IsNaN(value.AsDouble())) ||
                    (value.TypeCode == XdmTypeCode.Float && float.IsNaN(value.AsFloat())))
                {
                    kv.Least = true;
                    return kv;
                }

                kv.Atomic = value;
                return kv;
        }
    }

    // data-type="number": the key is converted as by fn:number (a string that is not a number is NaN).
    private static double ToNumber(XdmAtomicValue value)
    {
        if (XTransform.XPath.Operators.NumericPromotion.IsNumeric(value.TypeCode))
        {
            return value.TypeCode switch
            {
                XdmTypeCode.Double => value.AsDouble(),
                XdmTypeCode.Float => value.AsFloat(),
                XdmTypeCode.Decimal => (double)value.AsDecimal().Value,
                _ => (double)value.AsBigInteger(),
            };
        }

        if (value.TypeCode == XdmTypeCode.Boolean)
        {
            return value.GetStringValue() == "true" ? 1 : 0;
        }

        string text = value.GetStringValue().Trim();
        if (text == "INF")
        {
            return double.PositiveInfinity;
        }

        if (text == "-INF")
        {
            return double.NegativeInfinity;
        }

        // XPath 2.0 number lexical form: no leading '+', no hex, no "Infinity" / "NaN" words.
        if (text.Length == 0 || text[0] == '+' || !double.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out double d))
        {
            return double.NaN;
        }

        return d;
    }

    // ── Comparison ───────────────────────────────────────────────────────────

    // A stable merge sort. Array.Sort is not used: it is unstable and wraps exceptions raised by the comparison.
    private static void MergeSort(int[] a, int[] scratch, int lo, int hi, ResolvedKey[] resolved, IReadOnlyList<SortKey> keys)
    {
        if (hi - lo < 2)
        {
            return;
        }

        int mid = lo + ((hi - lo) / 2);
        MergeSort(a, scratch, lo, mid, resolved, keys);
        MergeSort(a, scratch, mid, hi, resolved, keys);

        int i = lo;
        int j = mid;
        int o = lo;
        while (i < mid && j < hi)
        {
            // Take from the right only when it is strictly smaller, which keeps equal items in input order.
            if (Compare(a[j], a[i], resolved, keys) < 0)
            {
                scratch[o++] = a[j++];
            }
            else
            {
                scratch[o++] = a[i++];
            }
        }

        while (i < mid)
        {
            scratch[o++] = a[i++];
        }

        while (j < hi)
        {
            scratch[o++] = a[j++];
        }

        Array.Copy(scratch, lo, a, lo, hi - lo);
    }

    private static int Compare(int x, int y, ResolvedKey[] resolved, IReadOnlyList<SortKey> keys)
    {
        for (int k = 0; k < resolved.Length; k++)
        {
            ResolvedKey r = resolved[k];
            int c = CompareValues(r.Values[x], r.Values[y], r, keys[k]);
            if (c != 0)
            {
                return r.Descending ? -c : c;
            }
        }

        return 0;
    }

    private static int CompareValues(KeyValue a, KeyValue b, ResolvedKey r, SortKey key)
    {
        if (a.Least || b.Least)
        {
            return a.Least ? (b.Least ? 0 : -1) : 1;
        }

        switch (r.Kind)
        {
            case KeyKind.Number:
                return a.Number < b.Number ? -1 : a.Number > b.Number ? 1 : 0;
            case KeyKind.Text:
                return r.Collation.Compare(a.Text!, b.Text!);
            default:
                break;
        }

        XdmAtomicValue left = a.Atomic!;
        XdmAtomicValue right = b.Atomic!;
        if (IsStringLike(left) && IsStringLike(right))
        {
            return r.Collation.Compare(left.GetStringValue(), right.GetStringValue());
        }

        int? cmp = XTransform.XPath.Operators.ComparisonOperators.ValueCompare(left, right);
        if (cmp is null)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1030,
                $"xsl:sort: the sort keys of type xs:{CastTable.GetXsLocalName(left.TypeCode)} and xs:{CastTable.GetXsLocalName(right.TypeCode)} cannot be compared",
                key.Location);
        }

        return cmp.Value;
    }

    private static bool IsStringLike(XdmAtomicValue v) =>
        v.TypeCode == XdmTypeCode.AnyUri || CastTable.PrimitiveTypeOf(v.TypeCode) == XdmTypeCode.String;

    // ── Collations ───────────────────────────────────────────────────────────

    /// <summary>The Unicode codepoint collation: compares strings by code point, not by UTF-16 unit.</summary>
    internal sealed class CodePointComparer : IComparer<string>
    {
        /// <summary>The collation URI of the codepoint collation.</summary>
        public const string Uri = "http://www.w3.org/2005/xpath-functions/collation/codepoint";

        /// <summary>The shared instance.</summary>
        public static CodePointComparer Instance { get; } = new CodePointComparer();

        /// <inheritdoc/>
        public int Compare(string? x, string? y)
        {
            string a = x ?? string.Empty;
            string b = y ?? string.Empty;
            int i = 0;
            int j = 0;
            while (i < a.Length && j < b.Length)
            {
                int ca = char.IsHighSurrogate(a[i]) && i + 1 < a.Length && char.IsLowSurrogate(a[i + 1])
                    ? char.ConvertToUtf32(a[i], a[i + 1])
                    : a[i];
                int cb = char.IsHighSurrogate(b[j]) && j + 1 < b.Length && char.IsLowSurrogate(b[j + 1])
                    ? char.ConvertToUtf32(b[j], b[j + 1])
                    : b[j];
                if (ca != cb)
                {
                    return ca < cb ? -1 : 1;
                }

                i += ca > 0xFFFF ? 2 : 1;
                j += cb > 0xFFFF ? 2 : 1;
            }

            return (a.Length - i).CompareTo(b.Length - j);
        }
    }

    // A language-sensitive comparison; case-order puts upper or lower case first among strings that are
    // otherwise equal (XSLT 2.0 §13.1.1).
    private sealed class CultureComparer : IComparer<string>
    {
        private readonly CompareInfo _info;
        private readonly string? _caseOrder;

        public CultureComparer(CompareInfo info, string? caseOrder)
        {
            _info = info;
            _caseOrder = caseOrder;
        }

        public int Compare(string? x, string? y)
        {
            string a = x ?? string.Empty;
            string b = y ?? string.Empty;
            if (_caseOrder is null)
            {
                return _info.Compare(a, b, CompareOptions.None);
            }

            int primary = _info.Compare(a, b, CompareOptions.IgnoreCase);
            if (primary != 0)
            {
                return primary;
            }

            // Equal apart from case: the first character whose case differs decides.
            int length = Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                if (a[i] == b[i])
                {
                    continue;
                }

                bool aUpper = char.IsUpper(a[i]);
                bool bUpper = char.IsUpper(b[i]);
                if (aUpper != bUpper)
                {
                    bool upperFirst = string.Equals(_caseOrder, "upper-first", StringComparison.Ordinal);
                    return aUpper == upperFirst ? -1 : 1;
                }
            }

            return 0;
        }
    }
}
