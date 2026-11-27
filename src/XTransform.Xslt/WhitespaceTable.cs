// XSLT 2.0 §4.3 / §4.4 — xsl:strip-space and xsl:preserve-space.
//
// Each declaration lists name tests (*, prefix:*, *:local, QName, and the Q{uri}local forms). When the
// whitespace-only text nodes of a source document are decided, the rule for an element name is the
// matching one with the highest import precedence, then the highest priority (* = -0.5, prefix:* and
// *:local = -0.25, QName = 0); a tie between strip and preserve is the recoverable error XTRE0270, resolved
// by taking the declaration that appears last.

using System;
using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>One name test of an <c>xsl:strip-space</c> or <c>xsl:preserve-space</c> declaration.</summary>
internal sealed class WhitespaceRule
{
    /// <summary>Initializes a rule; a null <paramref name="namespaceUri"/> or <paramref name="localName"/> is a wildcard.</summary>
    public WhitespaceRule(
        bool strip,
        string? namespaceUri,
        string? localName,
        int precedence,
        int order,
        SourceSpan location)
    {
        Strip = strip;
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Precedence = precedence;
        Order = order;
        Location = location;
    }

    /// <summary>True for strip-space, false for preserve-space.</summary>
    public bool Strip { get; }

    /// <summary>The namespace the element must be in, or null for any.</summary>
    public string? NamespaceUri { get; }

    /// <summary>The local name the element must have, or null for any.</summary>
    public string? LocalName { get; }

    /// <summary>Import precedence of the declaring module (higher wins).</summary>
    public int Precedence { get; }

    /// <summary>Position among all declarations in stylesheet order (later wins a tie).</summary>
    public int Order { get; }

    /// <summary>Where the declaration is.</summary>
    public SourceSpan Location { get; }

    /// <summary>XSLT 2.0 §4.4 default priority of the name test.</summary>
    public double Priority =>
        NamespaceUri is null && LocalName is null ? -0.5
        : NamespaceUri is null || LocalName is null ? -0.25
        : 0.0;

    /// <summary>True when an element with this name is covered by the name test.</summary>
    public bool Matches(string namespaceUri, string localName) =>
        (NamespaceUri is null || string.Equals(NamespaceUri, namespaceUri, StringComparison.Ordinal)) &&
        (LocalName is null || string.Equals(LocalName, localName, StringComparison.Ordinal));
}

/// <summary>The whitespace-stripping declarations of a stylesheet, immutable and safe to share.</summary>
internal sealed class WhitespaceTable
{
    private readonly WhitespaceRule[] _rules;

    /// <summary>A table with no rules: nothing is stripped.</summary>
    public static WhitespaceTable Empty { get; } = new WhitespaceTable(Array.Empty<WhitespaceRule>());

    /// <summary>Initializes a table from <paramref name="rules"/> in stylesheet order.</summary>
    public WhitespaceTable(IReadOnlyList<WhitespaceRule> rules)
    {
        _rules = new WhitespaceRule[rules.Count];
        for (int i = 0; i < _rules.Length; i++)
        {
            _rules[i] = rules[i];
        }

        foreach (WhitespaceRule rule in _rules)
        {
            HasStripRules |= rule.Strip;
        }
    }

    /// <summary>The rules, in stylesheet order.</summary>
    public IReadOnlyList<WhitespaceRule> Rules => _rules;

    /// <summary>True when at least one <c>xsl:strip-space</c> name test exists (otherwise nothing can be stripped).</summary>
    public bool HasStripRules { get; }

    /// <summary>True when whitespace-only text children of an element named <paramref name="namespaceUri"/>:<paramref name="localName"/> are stripped.</summary>
    public bool ShouldStrip(string namespaceUri, string localName)
    {
        WhitespaceRule? best = null;
        foreach (WhitespaceRule rule in _rules)
        {
            if (!rule.Matches(namespaceUri, localName))
            {
                continue;
            }

            if (best is null ||
                rule.Precedence > best.Precedence ||
                (rule.Precedence == best.Precedence &&
                 (rule.Priority > best.Priority || (rule.Priority == best.Priority && rule.Order > best.Order))))
            {
                best = rule;
            }
        }

        return best is not null && best.Strip;
    }
}
