using System.Collections.Generic;

namespace XTransform.Xslt;

/// <summary>
/// The <c>xsl:namespace-alias</c> declarations of a stylesheet (XSLT 2.0 §11.1.4): a literal result element, a literal
/// attribute or a namespace node of a literal result element in the namespace of the <c>stylesheet-prefix</c> gets the
/// namespace and the prefix of the <c>result-prefix</c> instead. Immutable once built.
/// </summary>
internal sealed class NamespaceAliasTable
{
    private readonly Dictionary<string, Alias> _aliases;

    private NamespaceAliasTable(Dictionary<string, Alias> aliases)
    {
        _aliases = aliases;
    }

    /// <summary>A table with no aliases.</summary>
    public static NamespaceAliasTable Empty => new NamespaceAliasTable(new Dictionary<string, Alias>(System.StringComparer.Ordinal));

    /// <summary>True when no namespace is aliased.</summary>
    public bool IsEmpty => _aliases.Count == 0;

    /// <summary>Looks up the alias of the namespace <paramref name="stylesheetUri"/>.</summary>
    public bool TryGetAlias(string stylesheetUri, out string resultUri, out string resultPrefix)
    {
        if (_aliases.TryGetValue(stylesheetUri, out Alias? alias))
        {
            resultUri = alias.ResultUri;
            resultPrefix = alias.ResultPrefix;
            return true;
        }

        resultUri = string.Empty;
        resultPrefix = string.Empty;
        return false;
    }

    /// <summary>Starts a builder that applies import precedence while declarations are added.</summary>
    public static Builder CreateBuilder() => new Builder();

    /// <summary>Collects declarations: the one with the higher import precedence wins for a namespace.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, Alias> _aliases = new Dictionary<string, Alias>(System.StringComparer.Ordinal);

        /// <summary>
        /// Adds a declaration. Returns false when an earlier one for the same namespace has the same import precedence but names a
        /// different result namespace (XTSE0810).
        /// </summary>
        public bool Add(string stylesheetUri, string resultUri, string resultPrefix, int importPrecedence)
        {
            if (_aliases.TryGetValue(stylesheetUri, out Alias? existing))
            {
                if (existing.ImportPrecedence > importPrecedence)
                {
                    return true;
                }

                if (existing.ImportPrecedence == importPrecedence && !string.Equals(existing.ResultUri, resultUri, System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            _aliases[stylesheetUri] = new Alias(resultUri, resultPrefix, importPrecedence);
            return true;
        }

        /// <summary>Builds the table.</summary>
        public NamespaceAliasTable Build() => new NamespaceAliasTable(new Dictionary<string, Alias>(_aliases, System.StringComparer.Ordinal));
    }

    private sealed class Alias
    {
        public Alias(string resultUri, string resultPrefix, int importPrecedence)
        {
            ResultUri = resultUri;
            ResultPrefix = resultPrefix;
            ImportPrecedence = importPrecedence;
        }

        public string ResultUri { get; }

        public string ResultPrefix { get; }

        public int ImportPrecedence { get; }
    }
}
