using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XTransform.Xdm;

/// <summary>
/// What the internal subset of a DTD says that the data model needs: the elements with element-only content (whose whitespace-only
/// text is not part of the tree) and the attributes declared as ID, IDREF or IDREFS. <c>System.Xml</c> does not report either, so the
/// <c>ELEMENT</c> and <c>ATTLIST</c> declarations are read here. The external subset and parameter entities are not read.
/// </summary>
internal sealed class DtdInfo
{
    /// <summary>Attribute flag: the attribute is an ID (or <c>xml:id</c>).</summary>
    internal const byte IdFlag = 1;

    /// <summary>Attribute flag: the attribute is an IDREF or IDREFS.</summary>
    internal const byte IdRefFlag = 2;

    private static readonly Regex s_comment = new Regex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex s_element = new Regex(@"<!ELEMENT\s+(\S+)\s+([^>]*)>", RegexOptions.CultureInvariant);

    private static readonly Regex s_attlist = new Regex(@"<!ATTLIST\s+(\S+)\s+((?:[^>""']|""[^""]*""|'[^']*')*)>", RegexOptions.CultureInvariant);

    // name  type  default, where the type is a keyword, NOTATION (...) or an enumeration, and the default is #REQUIRED, #IMPLIED or #FIXED "v" / "v".
    private static readonly Regex s_attribute = new Regex(
        @"(\S+)\s+(NOTATION\s*\([^)]*\)|\([^)]*\)|\S+)\s+(#REQUIRED|#IMPLIED|(?:#FIXED\s+)?(?:""[^""]*""|'[^']*'))",
        RegexOptions.CultureInvariant);

    private readonly HashSet<string> _elementOnly = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, byte> _attributes = new Dictionary<string, byte>(StringComparer.Ordinal);

    /// <summary>True when the subset declares an attribute as ID, IDREF or IDREFS, so the tree knows which attributes are IDs.</summary>
    public bool DeclaresIds { get; private set; }

    /// <summary>True when the subset declares anything the tree needs.</summary>
    public bool IsEmpty => _elementOnly.Count == 0 && _attributes.Count == 0;

    /// <summary>Reads the declarations of <paramref name="internalSubset"/>.</summary>
    public static DtdInfo Parse(string internalSubset)
    {
        DtdInfo info = new DtdInfo();
        string text = s_comment.Replace(internalSubset ?? string.Empty, string.Empty);

        foreach (Match m in s_element.Matches(text))
        {
            // Element-only content is a parenthesised model without #PCDATA; EMPTY, ANY and mixed content keep their whitespace.
            string model = m.Groups[2].Value.Trim();
            if (model.Length > 0 && model[0] == '(' && model.IndexOf("#PCDATA", StringComparison.Ordinal) < 0)
            {
                info._elementOnly.Add(m.Groups[1].Value);
            }
        }

        foreach (Match m in s_attlist.Matches(text))
        {
            string element = m.Groups[1].Value;
            foreach (Match a in s_attribute.Matches(m.Groups[2].Value))
            {
                string type = a.Groups[2].Value;
                byte flag = type == "ID" ? IdFlag : type == "IDREF" || type == "IDREFS" ? IdRefFlag : (byte)0;
                if (flag != 0)
                {
                    info._attributes[element + " " + a.Groups[1].Value] = flag;
                    info.DeclaresIds = true;
                }
            }
        }

        return info;
    }

    /// <summary>True when <paramref name="element"/> (as written) has element-only content.</summary>
    public bool IsElementOnly(string element) => _elementOnly.Contains(element);

    /// <summary>The ID / IDREF flag of attribute <paramref name="attribute"/> of <paramref name="element"/> (both as written); 0 when it is neither.</summary>
    public byte AttributeFlags(string element, string attribute) =>
        _attributes.TryGetValue(element + " " + attribute, out byte flag) ? flag : (byte)0;
}
