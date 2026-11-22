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

    /// <summary>It is a dynamic error if a global variable depends on itself. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTDE0640 = new QName("err", XPathErrors.Namespace, "XTDE0640");

    /// <summary>Raised by xsl:message with terminate="yes". (XSLT 2.0 §16)</summary>
    public static readonly QName XTMM9000 = new QName("err", XPathErrors.Namespace, "XTMM9000");
}
