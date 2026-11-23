using System;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// Resolves a lexical QName (<c>prefix:local</c> or <c>local</c>) against a set of in-scope
/// namespace bindings. Unprefixed names are placed in the null namespace by default.
/// </summary>
internal static class LexicalQNameParser
{
    /// <summary>
    /// Resolves <paramref name="lexical"/> to a <see cref="QName"/> or reports an error.
    /// Returns <see langword="true"/> on success.
    /// </summary>
    public static bool TryResolve(
        string lexical,
        NamespaceBindings namespaces,
        out QName result,
        out string? errorReason)
    {
        result = default;
        errorReason = null;

        if (string.IsNullOrEmpty(lexical))
        {
            errorReason = "empty QName";
            return false;
        }

        int colon = lexical.IndexOf(':');
        if (colon < 0)
        {
            result = new QName(string.Empty, string.Empty, lexical);
            return true;
        }

        string prefix = lexical.Substring(0, colon);
        string local = lexical.Substring(colon + 1);

        string? uri = namespaces.Resolve(prefix);
        if (uri is null)
        {
            errorReason = $"undeclared prefix '{prefix}'";
            return false;
        }

        result = new QName(prefix, uri, local);
        return true;
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="lexical"/> contains a prefix (at least one colon).</summary>
    public static bool HasPrefix(string lexical)
    {
        return !string.IsNullOrEmpty(lexical) && lexical.IndexOf(':') > 0;
    }
}
