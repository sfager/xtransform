// XSLT 2.0 §6.4 — per-mode template-rule index. Rules are bucketed by the node kind and,
// when applicable, the element/attribute name of the pattern's final step. The runtime
// combines the bucket matching the current node with the "any kind" bucket and iterates in
// (precedence desc, priority desc, document-order desc) order; the first matching rule wins.

using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Per-mode compiled template-rule table.</summary>
internal sealed class ModeTable
{
    private readonly Dictionary<QName, List<TemplateRule>> _elementsByName;
    private readonly Dictionary<QName, List<TemplateRule>> _attributesByName;
    private readonly Dictionary<XdmNodeKind, List<TemplateRule>> _byKind;
    private readonly List<TemplateRule> _elementWildcard;
    private readonly List<TemplateRule> _attributeWildcard;
    private readonly List<TemplateRule> _anyNode;
    private readonly List<TemplateRule> _all;

    /// <summary>Initializes an empty table for <paramref name="mode"/>.</summary>
    public ModeTable(QName mode)
    {
        Mode = mode;
        _elementsByName = new Dictionary<QName, List<TemplateRule>>();
        _attributesByName = new Dictionary<QName, List<TemplateRule>>();
        _byKind = new Dictionary<XdmNodeKind, List<TemplateRule>>();
        _elementWildcard = new List<TemplateRule>();
        _attributeWildcard = new List<TemplateRule>();
        _anyNode = new List<TemplateRule>();
        _all = new List<TemplateRule>();
    }

    /// <summary>The mode identifier (empty-name QName for the default mode).</summary>
    public QName Mode { get; }

    /// <summary>Every rule registered in this mode, in registration order.</summary>
    public IReadOnlyList<TemplateRule> AllRules => _all;

    /// <summary>Rules bucketed by specific element name.</summary>
    public IReadOnlyDictionary<QName, List<TemplateRule>> ElementsByName => _elementsByName;

    /// <summary>Rules bucketed by specific attribute name.</summary>
    public IReadOnlyDictionary<QName, List<TemplateRule>> AttributesByName => _attributesByName;

    /// <summary>Rules bucketed by non-element/attribute node kind (text/comment/pi/document/namespace).</summary>
    public IReadOnlyDictionary<XdmNodeKind, List<TemplateRule>> ByKind => _byKind;

    /// <summary>Rules matching any element regardless of name (e.g. <c>element()</c>, <c>*</c>, <c>ns:*</c>).</summary>
    public IReadOnlyList<TemplateRule> ElementWildcard => _elementWildcard;

    /// <summary>Rules matching any attribute regardless of name.</summary>
    public IReadOnlyList<TemplateRule> AttributeWildcard => _attributeWildcard;

    /// <summary>Rules matching any node regardless of kind (e.g. <c>node()</c>).</summary>
    public IReadOnlyList<TemplateRule> AnyNode => _anyNode;

    /// <summary>Registers a rule in the element bucket for a specific name.</summary>
    public void AddElementByName(QName name, TemplateRule rule)
    {
        GetOrCreate(_elementsByName, name).Add(rule);
        _all.Add(rule);
    }

    /// <summary>Registers a rule in the attribute bucket for a specific name.</summary>
    public void AddAttributeByName(QName name, TemplateRule rule)
    {
        GetOrCreate(_attributesByName, name).Add(rule);
        _all.Add(rule);
    }

    /// <summary>Registers a rule in the bucket for a specific node kind.</summary>
    public void AddByKind(XdmNodeKind kind, TemplateRule rule)
    {
        GetOrCreate(_byKind, kind).Add(rule);
        _all.Add(rule);
    }

    /// <summary>Registers a rule that matches any element.</summary>
    public void AddElementWildcard(TemplateRule rule)
    {
        _elementWildcard.Add(rule);
        _all.Add(rule);
    }

    /// <summary>Registers a rule that matches any attribute.</summary>
    public void AddAttributeWildcard(TemplateRule rule)
    {
        _attributeWildcard.Add(rule);
        _all.Add(rule);
    }

    /// <summary>Registers a rule that matches any node of any kind.</summary>
    public void AddAnyNode(TemplateRule rule)
    {
        _anyNode.Add(rule);
        _all.Add(rule);
    }

    /// <summary>Sorts every bucket by (precedence desc, priority desc, ordinal desc).</summary>
    public void SortBuckets()
    {
        foreach (List<TemplateRule> bucket in _elementsByName.Values) { Sort(bucket); }
        foreach (List<TemplateRule> bucket in _attributesByName.Values) { Sort(bucket); }
        foreach (List<TemplateRule> bucket in _byKind.Values) { Sort(bucket); }
        Sort(_elementWildcard);
        Sort(_attributeWildcard);
        Sort(_anyNode);
    }

    private static void Sort(List<TemplateRule> rules)
    {
        // Later wins on ties: a rule added later has a higher ordinal. We want the first
        // position in the sorted list to be the one that wins at runtime, so sort by
        // descending precedence, descending priority, descending ordinal.
        rules.Sort(static (a, b) =>
        {
            int c = b.ImportPrecedence.CompareTo(a.ImportPrecedence);
            if (c != 0) { return c; }
            c = b.Priority.CompareTo(a.Priority);
            if (c != 0) { return c; }
            return b.DocumentOrderOrdinal.CompareTo(a.DocumentOrderOrdinal);
        });
    }

    private static List<TemplateRule> GetOrCreate<TKey>(Dictionary<TKey, List<TemplateRule>> map, TKey key)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out List<TemplateRule>? list))
        {
            list = new List<TemplateRule>();
            map[key] = list;
        }

        return list;
    }
}
