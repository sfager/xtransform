using System.Xml;

namespace XTransform.Xslt;

/// <summary>Lexical checks for names that XSLT reads from attributes and from run-time values (XML Names, §3.1 of XSLT 2.0).</summary>
internal static class LexicalNames
{
    /// <summary>True when <paramref name="value"/> is an NCName: a name with no colon.</summary>
    public static bool IsNCName(string value)
    {
        if (value.Length == 0 || !XmlConvert.IsStartNCNameChar(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!XmlConvert.IsNCNameChar(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when <paramref name="value"/> is a lexical QName: an NCName, or two NCNames joined by one colon.</summary>
    public static bool IsQName(string value)
    {
        int colon = value.IndexOf(':');
        return colon < 0
            ? IsNCName(value)
            : IsNCName(value.Substring(0, colon)) && IsNCName(value.Substring(colon + 1));
    }

    /// <summary>Splits a lexical QName into its prefix (empty when there is none) and its local part.</summary>
    public static void Split(string lexicalQName, out string prefix, out string local)
    {
        int colon = lexicalQName.IndexOf(':');
        prefix = colon < 0 ? string.Empty : lexicalQName.Substring(0, colon);
        local = colon < 0 ? lexicalQName : lexicalQName.Substring(colon + 1);
    }
}
