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

    /// <summary>It is a static error if a stylesheet module referenced by xsl:include or xsl:import cannot be retrieved or is not a valid stylesheet module. (XSLT 2.0 §3.11)</summary>
    public static readonly QName XTSE0165 = new QName("err", XPathErrors.Namespace, "XTSE0165");

    /// <summary>It is a static error if a stylesheet module directly or indirectly includes or imports itself. (XSLT 2.0 §3.11)</summary>
    public static readonly QName XTSE0210 = new QName("err", XPathErrors.Namespace, "XTSE0210");

    /// <summary>It is a static error if a stylesheet contains more than one binding of a global variable with the same name and the same import precedence. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTSE0630 = new QName("err", XPathErrors.Namespace, "XTSE0630");

    /// <summary>It is a static error if a stylesheet contains more than one template with the same name and the same import precedence. (XSLT 2.0 §6.2)</summary>
    public static readonly QName XTSE0660 = new QName("err", XPathErrors.Namespace, "XTSE0660");

    /// <summary>It is a static error if a stylesheet contains an xsl:apply-templates whose mode attribute refers to a mode that is not used by any template rule. (XSLT 2.0 §6.4)</summary>
    public static readonly QName XTSE0680 = new QName("err", XPathErrors.Namespace, "XTSE0680");

    /// <summary>It is a static error if the name attribute of an xsl:function element does not have a prefix (functions must live in a namespace). (XSLT 2.0 §10.3)</summary>
    public static readonly QName XTSE0740 = new QName("err", XPathErrors.Namespace, "XTSE0740");

    /// <summary>It is a static error if the pattern contains a construct that is not allowed in a pattern. (XSLT 2.0 §5.5.2)</summary>
    public static readonly QName XTSE0340 = new QName("err", XPathErrors.Namespace, "XTSE0340");

    /// <summary>It is a static error if an attribute value template contains an unmatched left or right curly bracket. (XSLT 2.0 §5.6.1)</summary>
    public static readonly QName XTSE0370 = new QName("err", XPathErrors.Namespace, "XTSE0370");

    /// <summary>It is a static error if an element from the XSLT namespace is used in a context where it is not permitted, or if a required attribute is missing, or if an unknown element is encountered outside forwards-compatible mode. (XSLT 2.0 §3.7)</summary>
    public static readonly QName XTSE0805 = new QName("err", XPathErrors.Namespace, "XTSE0805");

    /// <summary>Recoverable dynamic error: attribute or namespace node emitted after a child node on the same element. (XSLT 2.0 §5.6.3)</summary>
    public static readonly QName XTDE0410 = new QName("err", XPathErrors.Namespace, "XTDE0410");

    /// <summary>Non-recoverable dynamic error: an attribute or namespace node was added to a document node (no open element). (XSLT 2.0 §5.7.1)</summary>
    public static readonly QName XTDE0420 = new QName("err", XPathErrors.Namespace, "XTDE0420");

    /// <summary>Recoverable dynamic error: xsl:comment body contains a two-hyphen sequence or ends with a hyphen. (XSLT 2.0 §11.8)</summary>
    public static readonly QName XTDE0450 = new QName("err", XPathErrors.Namespace, "XTDE0450");

    /// <summary>Recoverable dynamic error: multiple template rules match a node with equal import precedence and priority. (XSLT 2.0 §6.4)</summary>
    public static readonly QName XTRE0540 = new QName("err", XPathErrors.Namespace, "XTRE0540");

    /// <summary>It is a dynamic error if no value is supplied for a required global stylesheet parameter. (XSLT 2.0 §9.5)</summary>
    public static readonly QName XTDE0050 = new QName("err", XPathErrors.Namespace, "XTDE0050");

    /// <summary>It is a dynamic error if a global variable depends on itself. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTDE0640 = new QName("err", XPathErrors.Namespace, "XTDE0640");

    /// <summary>It is a dynamic error if xsl:call-template is invoked without a value for a required xsl:param. (XSLT 2.0 §9.5)</summary>
    public static readonly QName XTDE0700 = new QName("err", XPathErrors.Namespace, "XTDE0700");

    /// <summary>Implementation-defined limit exceeded — recursion depth. (XSLT 2.0 §2.9)</summary>
    public static readonly QName XTDE0810 = new QName("err", XPathErrors.Namespace, "XTDE0810");

    /// <summary>Raised by xsl:message with terminate="yes". (XSLT 2.0 §16)</summary>
    public static readonly QName XTMM9000 = new QName("err", XPathErrors.Namespace, "XTMM9000");
}
