using System;

namespace XTransform.Xdm;

/// <summary>Resolves the value of an <c>xml:base</c> attribute against the base URI of its parent (XML Base §4.2, RFC 3986 §5).</summary>
internal static class BaseUriResolver
{
    /// <summary>The namespace of the <c>xml</c> prefix.</summary>
    internal const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>
    /// Resolves <paramref name="reference"/> against <paramref name="baseUri"/>. An absolute reference, or a reference with no
    /// absolute base to resolve against, is returned as written.
    /// </summary>
    internal static string Resolve(string reference, string? baseUri)
    {
        reference = reference.Trim();
        if (HasScheme(reference) || string.IsNullOrEmpty(baseUri)
            || !Uri.TryCreate(baseUri, UriKind.Absolute, out Uri? baseResult)
            || !Uri.TryCreate(baseResult, reference, out Uri? resolved))
        {
            return reference.Length == 0 && baseUri is not null ? baseUri : reference;
        }

        return resolved.AbsoluteUri;
    }

    // A reference starts with a scheme when letters, digits, '+', '-' or '.' lead up to a colon; one letter is a drive, not a scheme.
    private static bool HasScheme(string reference)
    {
        int colon = reference.IndexOf(':');
        if (colon < 1 || !char.IsLetter(reference[0]) || (colon == 1 && string.CompareOrdinal(reference, 1, "://", 0, 3) != 0))
        {
            return false;
        }

        for (int i = 1; i < colon; i++)
        {
            char c = reference[i];
            if (!char.IsLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return true;
    }
}
