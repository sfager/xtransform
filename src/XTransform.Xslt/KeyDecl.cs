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

    /// <summary>The <c>@use</c> expression text.</summary>
    public string UseExpression { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location => Element.Location;
}

/// <summary>Lazily-built index for one <c>(document, key name)</c> pair.</summary>
internal sealed class KeyIndex
{
    private readonly Dictionary<string, List<XdmNode>> _buckets =
        new Dictionary<string, List<XdmNode>>(System.StringComparer.Ordinal);

    /// <summary>Appends <paramref name="node"/> under <paramref name="key"/>.</summary>
    public void Add(string key, XdmNode node)
    {
        if (!_buckets.TryGetValue(key, out List<XdmNode>? list))
        {
            list = new List<XdmNode>();
            _buckets[key] = list;
        }

        list.Add(node);
    }

    /// <summary>Returns true when at least one node is indexed under <paramref name="key"/>.</summary>
    public bool TryGetValue(string key, out IReadOnlyList<XdmNode>? nodes)
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
