namespace XTransform.Xslt;

/// <summary>The collations this processor recognises (XSLT 2.0 §3.5, §16.3). Only the Unicode codepoint collation is built in.</summary>
internal static class CollationUris
{
    /// <summary>The Unicode codepoint collation.</summary>
    public const string Codepoint = "http://www.w3.org/2005/xpath-functions/collation/codepoint";

    /// <summary>True when <paramref name="uri"/> names a collation this processor supports.</summary>
    public static bool IsRecognised(string uri) => string.Equals(uri.Trim(), Codepoint, System.StringComparison.Ordinal);
}
