// XSLT 2.0 §16.2 — xsl:key declaration.
// A key definition is a 3-tuple of (match pattern, use expression, collation URI). We
// collect these per expanded name during P3-11; multiple xsl:key with the same name
// combine at lookup time (XSLT 2.0 §16.2.3).

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>A collected <c>xsl:key</c> declaration.</summary>
internal sealed class KeyDecl
{
    /// <summary>Initializes a key declaration.</summary>
    public KeyDecl(
        StylesheetElement element,
        int importPrecedence,
        QName name,
        string matchPattern,
        string useExpression)
    {
        Element = element;
        ImportPrecedence = importPrecedence;
        Name = name;
        MatchPattern = matchPattern;
        UseExpression = useExpression;
    }

    /// <summary>Source element.</summary>
    public StylesheetElement Element { get; }

    /// <summary>Import precedence (key lookups union across precedences).</summary>
    public int ImportPrecedence { get; }

    /// <summary>Expanded key name.</summary>
    public QName Name { get; }

    /// <summary>The <c>@match</c> pattern text.</summary>
    public string MatchPattern { get; }

    /// <summary>The <c>@use</c> expression text; empty when the value comes from the content of the element.</summary>
    public string UseExpression { get; }

    /// <summary>True when the declaring stylesheet is backwards-compatible: a key value that is not a string is compared as one.</summary>
    public bool BackwardsCompatible { get; init; }

    /// <summary>The compiled <c>@use</c> expression; set when the stylesheet is compiled.</summary>
    public Instructions.BoundExpression? UseCompiled { get; set; }

    /// <summary>The compiled content of the element when it has no <c>@use</c> (a sequence constructor); set when the stylesheet is compiled.</summary>
    public Instructions.VariableValueBody? UseBody { get; set; }

    /// <summary>Source location.</summary>
    public SourceSpan Location => Element.Location;
}

/// <summary>
/// Compares key values as the <c>eq</c> operator does (XSLT 2.0 §16.3): numbers by value (NaN equals nothing), dates and times as
/// instants, and <c>xs:untypedAtomic</c>, <c>xs:string</c> and <c>xs:anyURI</c> as strings. Values of types that cannot be compared are different.
/// </summary>
internal sealed class KeyValueComparer : IEqualityComparer<XdmAtomicValue>
{
    /// <summary>The shared instance.</summary>
    public static KeyValueComparer Instance { get; } = new KeyValueComparer();

    /// <summary>The atomic value a key item stands for: a node gives its untyped string value.</summary>
    public static XdmAtomicValue ToKey(XdmItem item) =>
        item as XdmAtomicValue ?? XdmAtomicValue.UntypedAtomic(item.GetStringValue());

    /// <inheritdoc/>
    public bool Equals(XdmAtomicValue? x, XdmAtomicValue? y)
    {
        if (x is null || y is null)
        {
            return false;
        }

        x = AsComparable(x);
        y = AsComparable(y);
        try
        {
            return XTransform.XPath.Operators.ComparisonOperators.ValueEqual(x, y) == true;
        }
        catch (XTransformException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public int GetHashCode(XdmAtomicValue value)
    {
        value = AsComparable(value);
        XdmTypeCode type = value.TypeCode;
        if (XTransform.XPath.Operators.NumericPromotion.IsNumeric(type))
        {
            double d = XTransform.XPath.Operators.NumericPromotion.PromoteNumeric(value, XdmTypeCode.Double, SourceSpan.None).AsDouble();
            return d == 0 ? 0 : d.GetHashCode();
        }

        if (type == XdmTypeCode.String || type == XdmTypeCode.AnyUri)
        {
            return value.GetStringValue().GetHashCode();
        }

        // Date and time values compare as instants, whatever their time zone, so they share a bucket by kind.
        return (int)type;
    }

    private static XdmAtomicValue AsComparable(XdmAtomicValue value) =>
        value.TypeCode == XdmTypeCode.UntypedAtomic ? XdmAtomicValue.String(value.GetStringValue()) : value;
}

/// <summary>Lazily-built index for one <c>(document, key name)</c> pair.</summary>
internal sealed class KeyIndex
{
    private readonly Dictionary<XdmAtomicValue, List<XdmNode>> _buckets =
        new Dictionary<XdmAtomicValue, List<XdmNode>>(KeyValueComparer.Instance);

    /// <summary>True when a backwards-compatible declaration put string keys in the index.</summary>
    public bool HasBackwardsCompatibleKeys { get; private set; }

    /// <summary>Appends <paramref name="node"/> under <paramref name="key"/>.</summary>
    public void Add(XdmAtomicValue key, XdmNode node, bool backwardsCompatible = false)
    {
        // A backwards-compatible declaration gives string keys.
        if (backwardsCompatible)
        {
            HasBackwardsCompatibleKeys = true;
            key = XdmAtomicValue.String(key.GetStringValue());
        }

        if (!_buckets.TryGetValue(key, out List<XdmNode>? list))
        {
            list = new List<XdmNode>();
            _buckets[key] = list;
        }

        list.Add(node);
    }

    /// <summary>Returns true when at least one node is indexed under <paramref name="key"/>.</summary>
    public bool TryGetValue(XdmAtomicValue key, out IReadOnlyList<XdmNode>? nodes)
    {
        if (_buckets.TryGetValue(key, out List<XdmNode>? list))
        {
            nodes = list;
            return true;
        }

        nodes = null;
        return false;
    }
}
