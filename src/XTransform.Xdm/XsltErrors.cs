namespace XTransform.Xdm;

/// <summary>
/// W3C XSLT 2.0 error codes.
/// All codes share the namespace http://www.w3.org/2005/xqt-errors (prefix "err").
/// Add new codes here as later tasks need them.
/// </summary>
public static class XsltErrors
{
    /// <summary>It is a static error if the stylesheet contains a construct that is not permitted at that point. (XSLT 2.0 §2.5)</summary>
    public static readonly QName XTSE0010 = new QName("err", XPathErrors.Namespace, "XTSE0010");

    /// <summary>It is a static error if the value of the version attribute on xsl:stylesheet is not a valid xs:decimal. (XSLT 2.0 §3.6)</summary>
    public static readonly QName XTSE0110 = new QName("err", XPathErrors.Namespace, "XTSE0110");

    /// <summary>It is a static error if a child element of xsl:stylesheet/xsl:transform is in a null namespace. (XSLT 2.0 §3.6)</summary>
    public static readonly QName XTSE0130 = new QName("err", XPathErrors.Namespace, "XTSE0130");

    /// <summary>A forwards-compatible processing warning: unknown construct accepted but ignored. (XSLT 2.0 §3.7)</summary>
    public static readonly QName XTSE0150 = new QName("err", XPathErrors.Namespace, "XTSE0150");

    /// <summary>It is a static error if the pattern contains a construct that is not allowed in a pattern. (XSLT 2.0 §5.5.2)</summary>
    public static readonly QName XTSE0340 = new QName("err", XPathErrors.Namespace, "XTSE0340");

    /// <summary>It is a static error if an attribute value template contains an unmatched left or right curly bracket. (XSLT 2.0 §5.6.1)</summary>
    public static readonly QName XTSE0370 = new QName("err", XPathErrors.Namespace, "XTSE0370");

    /// <summary>It is a static error if an element from the XSLT namespace is used in a context where it is not permitted, or if a required attribute is missing, or if an unknown element is encountered outside forwards-compatible mode. (XSLT 2.0 §3.7)</summary>
    public static readonly QName XTSE0805 = new QName("err", XPathErrors.Namespace, "XTSE0805");

    /// <summary>It is a dynamic error if a global variable depends on itself. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTDE0640 = new QName("err", XPathErrors.Namespace, "XTDE0640");

    /// <summary>Raised by xsl:message with terminate="yes". (XSLT 2.0 §16)</summary>
    public static readonly QName XTMM9000 = new QName("err", XPathErrors.Namespace, "XTMM9000");
}
