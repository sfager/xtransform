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

    /// <summary>It is a static error if an attribute value does not match the type the XSLT specification requires for it (for example a yes/no attribute). (XSLT 2.0 §3.5)</summary>
    public static readonly QName XTSE0020 = new QName("err", XPathErrors.Namespace, "XTSE0020");

    /// <summary>It is a static error if two xsl:output declarations with the same name and import precedence specify different values for the same attribute. (XSLT 2.0 §20)</summary>
    public static readonly QName XTSE1560 = new QName("err", XPathErrors.Namespace, "XTSE1560");

    /// <summary>Serialization error: the requested output encoding is not supported. (Serialization 1.0 §3)</summary>
    public static readonly QName SESU0007 = new QName("err", XPathErrors.Namespace, "SESU0007");

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

    /// <summary>It is a dynamic error if the initial named template does not exist in the stylesheet. (XSLT 2.0 §2.3)</summary>
    public static readonly QName XTDE0040 = new QName("err", XPathErrors.Namespace, "XTDE0040");

    /// <summary>It is a dynamic error if the initial mode is not used by any template rule. (XSLT 2.0 §2.3)</summary>
    public static readonly QName XTDE0045 = new QName("err", XPathErrors.Namespace, "XTDE0045");

    /// <summary>It is a dynamic error if no value is supplied for a required global stylesheet parameter. (XSLT 2.0 §9.5)</summary>
    public static readonly QName XTDE0050 = new QName("err", XPathErrors.Namespace, "XTDE0050");

    /// <summary>It is a dynamic error if a global variable depends on itself. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTDE0640 = new QName("err", XPathErrors.Namespace, "XTDE0640");
    /// <summary>XTDE1360: fn:current() was called when there is no current item.</summary>
    public static readonly QName XTDE1360 = new QName("err", XPathErrors.Namespace, "XTDE1360");
    /// <summary>XTDE0980: an xsl:number value cannot be converted to a non-negative integer. (XSLT 2.0 §12.1)</summary>
    public static readonly QName XTDE0980 = new QName("err", XPathErrors.Namespace, "XTDE0980");
    /// <summary>XTTE0990: xsl:number has no value or select and the context item is not a node. (XSLT 2.0 §12.2)</summary>
    public static readonly QName XTTE0990 = new QName("err", XPathErrors.Namespace, "XTTE0990");
    /// <summary>XTTE1000: the select of xsl:number is not a single node. (XSLT 2.0 §12.2)</summary>
    public static readonly QName XTTE1000 = new QName("err", XPathErrors.Namespace, "XTTE1000");
    /// <summary>XTSE0975: xsl:number has a value attribute together with select, level, count or from. (XSLT 2.0 §12.1)</summary>
    public static readonly QName XTSE0975 = new QName("err", XPathErrors.Namespace, "XTSE0975");
    /// <summary>XTDE1140: the regex of xsl:analyze-string is not a valid regular expression. (XSLT 2.0 §15.1)</summary>
    public static readonly QName XTDE1140 = new QName("err", XPathErrors.Namespace, "XTDE1140");
    /// <summary>XTDE1145: the flags of xsl:analyze-string are not valid. (XSLT 2.0 §15.1)</summary>
    public static readonly QName XTDE1145 = new QName("err", XPathErrors.Namespace, "XTDE1145");
    /// <summary>XTDE1150: the regex of xsl:analyze-string matches a zero-length string. (XSLT 2.0 §15.1)</summary>
    public static readonly QName XTDE1150 = new QName("err", XPathErrors.Namespace, "XTDE1150");
    /// <summary>XTSE1130: xsl:analyze-string has neither xsl:matching-substring nor xsl:non-matching-substring. (XSLT 2.0 §15.1)</summary>
    public static readonly QName XTSE1130 = new QName("err", XPathErrors.Namespace, "XTSE1130");
    /// <summary>XTSE0280: a name test in xsl:strip-space / xsl:preserve-space uses an undeclared prefix. (XSLT 2.0 §4.3)</summary>
    public static readonly QName XTSE0280 = new QName("err", XPathErrors.Namespace, "XTSE0280");
    /// <summary>XTRE0270: an element matches conflicting xsl:strip-space and xsl:preserve-space declarations. Recoverable. (XSLT 2.0 §4.4)</summary>
    public static readonly QName XTRE0270 = new QName("err", XPathErrors.Namespace, "XTRE0270");
    /// <summary>XTDE1270: the third argument of key() is not a single node in a document. (XSLT 2.0 §16.4)</summary>
    public static readonly QName XTDE1270 = new QName("err", XPathErrors.Namespace, "XTDE1270");

    /// <summary>It is a dynamic error if xsl:call-template is invoked without a value for a required xsl:param. (XSLT 2.0 §9.5)</summary>
    public static readonly QName XTDE0700 = new QName("err", XPathErrors.Namespace, "XTDE0700");

    /// <summary>Implementation-defined limit exceeded — recursion depth. (XSLT 2.0 §2.9)</summary>
    public static readonly QName XTDE0810 = new QName("err", XPathErrors.Namespace, "XTDE0810");

    /// <summary>Raised by xsl:message with terminate="yes". (XSLT 2.0 §16)</summary>
    public static readonly QName XTMM9000 = new QName("err", XPathErrors.Namespace, "XTMM9000");
}
