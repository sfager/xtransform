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

    /// <summary>It is a static error if the xsl:stylesheet or xsl:transform element has text content other than whitespace. (XSLT 2.0 §3.6)</summary>
    public static readonly QName XTSE0120 = new QName("err", XPathErrors.Namespace, "XTSE0120");

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

    /// <summary>XTTE0510: xsl:apply-templates selects an item that is not a node, or has no select and a non-node context item. (XSLT 2.0 §6.1)</summary>
    public static readonly QName XTTE0510 = new QName("err", XPathErrors.Namespace, "XTTE0510");

    /// <summary>XTSE0080: a name in a reserved namespace is used for a user-defined component. (XSLT 2.0 §3.5)</summary>
    public static readonly QName XTSE0080 = new QName("err", XPathErrors.Namespace, "XTSE0080");

    /// <summary>XTDE0060: the initial named template has a required non-tunnel parameter, which a processor cannot supply. (XSLT 2.0 §2.3)</summary>
    public static readonly QName XTDE0060 = new QName("err", XPathErrors.Namespace, "XTDE0060");

    /// <summary>XTSE0650: xsl:call-template names a template that does not exist. (XSLT 2.0 §10.1)</summary>
    public static readonly QName XTSE0650 = new QName("err", XPathErrors.Namespace, "XTSE0650");

    /// <summary>XTSE0690: xsl:call-template does not supply a required non-tunnel parameter of the template. (XSLT 2.0 §10.1.1)</summary>
    public static readonly QName XTSE0690 = new QName("err", XPathErrors.Namespace, "XTSE0690");

    /// <summary>XTSE0670: two xsl:with-param elements of one call name the same parameter. (XSLT 2.0 §10.1.1)</summary>
    public static readonly QName XTSE0670 = new QName("err", XPathErrors.Namespace, "XTSE0670");

    /// <summary>XTSE0870: xsl:value-of has both a select attribute and content. (XSLT 2.0 §11.4)</summary>
    public static readonly QName XTSE0870 = new QName("err", XPathErrors.Namespace, "XTSE0870");

    /// <summary>XTSE0760: an xsl:param of an xsl:function has a default value. (XSLT 2.0 §10.3)</summary>
    public static readonly QName XTSE0760 = new QName("err", XPathErrors.Namespace, "XTSE0760");

    /// <summary>XTSE0580: two parameters of one template or function have the same name. (XSLT 2.0 §9.2)</summary>
    public static readonly QName XTSE0580 = new QName("err", XPathErrors.Namespace, "XTSE0580");

    /// <summary>XTSE0620: a variable-binding element has both a select attribute and content. (XSLT 2.0 §9.3)</summary>
    public static readonly QName XTSE0620 = new QName("err", XPathErrors.Namespace, "XTSE0620");

    /// <summary>XTTE0790: an argument of a stylesheet function cannot be converted to the as type of its parameter. (XSLT 2.0 §10.3)</summary>
    public static readonly QName XTTE0790 = new QName("err", XPathErrors.Namespace, "XTTE0790");

    /// <summary>XTTE0780: the result of a stylesheet function cannot be converted to its as type. (XSLT 2.0 §10.3)</summary>
    public static readonly QName XTTE0780 = new QName("err", XPathErrors.Namespace, "XTTE0780");

    /// <summary>XTTE0505: the result of a template body cannot be converted to the as type of the template. (XSLT 2.0 §6.1)</summary>
    public static readonly QName XTTE0505 = new QName("err", XPathErrors.Namespace, "XTTE0505");

    /// <summary>XTDE0610: a parameter whose as type does not allow the empty sequence, with no default, was not supplied. (XSLT 2.0 §9.2)</summary>
    public static readonly QName XTDE0610 = new QName("err", XPathErrors.Namespace, "XTDE0610");

    /// <summary>XTTE0600: the default value of a parameter cannot be converted to its as type. (XSLT 2.0 §9.2)</summary>
    public static readonly QName XTTE0600 = new QName("err", XPathErrors.Namespace, "XTTE0600");

    /// <summary>XTTE0590: a supplied parameter value cannot be converted to the as type of the parameter. (XSLT 2.0 §9.2)</summary>
    public static readonly QName XTTE0590 = new QName("err", XPathErrors.Namespace, "XTTE0590");

    /// <summary>XTTE0570: the value of a variable cannot be converted to its as type. (XSLT 2.0 §9.1)</summary>
    public static readonly QName XTTE0570 = new QName("err", XPathErrors.Namespace, "XTTE0570");

    /// <summary>XTDE0560: xsl:apply-imports or xsl:next-match is evaluated when there is no current template rule. (XSLT 2.0 §6.7)</summary>
    public static readonly QName XTDE0560 = new QName("err", XPathErrors.Namespace, "XTDE0560");

    /// <summary>XTSE1070: current-grouping-key() is used within a pattern. (XSLT 2.0 §14.2)</summary>
    public static readonly QName XTSE1070 = new QName("err", XPathErrors.Namespace, "XTSE1070");

    /// <summary>XTSE1060: current-group() is used within a pattern. (XSLT 2.0 §14.1)</summary>
    public static readonly QName XTSE1060 = new QName("err", XPathErrors.Namespace, "XTSE1060");

    /// <summary>XTSE1017: the stable attribute of xsl:sort appears on an xsl:sort that is not the first of its siblings. (XSLT 2.0 §13.1)</summary>
    public static readonly QName XTSE1017 = new QName("err", XPathErrors.Namespace, "XTSE1017");

    /// <summary>XTTE1120: group-starting-with or group-ending-with is used and the population contains an item that is not a node. (XSLT 2.0 §14.3)</summary>
    public static readonly QName XTTE1120 = new QName("err", XPathErrors.Namespace, "XTTE1120");

    /// <summary>XTDE1110: the collation of xsl:for-each-group is not supported. (XSLT 2.0 §14.3)</summary>
    public static readonly QName XTDE1110 = new QName("err", XPathErrors.Namespace, "XTDE1110");

    /// <summary>XTTE1100: a group-adjacent key is not exactly one atomic value. (XSLT 2.0 §14.3)</summary>
    public static readonly QName XTTE1100 = new QName("err", XPathErrors.Namespace, "XTTE1100");

    /// <summary>XTSE1090: xsl:for-each-group has a collation attribute without group-by or group-adjacent. (XSLT 2.0 §14.3)</summary>
    public static readonly QName XTSE1090 = new QName("err", XPathErrors.Namespace, "XTSE1090");

    /// <summary>XTSE1080: xsl:for-each-group has none or more than one of group-by, group-adjacent, group-starting-with and group-ending-with. (XSLT 2.0 §14.3)</summary>
    public static readonly QName XTSE1080 = new QName("err", XPathErrors.Namespace, "XTSE1080");

    /// <summary>XTSE1015: xsl:sort has both a select attribute and content. (XSLT 2.0 §13.1)</summary>
    public static readonly QName XTSE1015 = new QName("err", XPathErrors.Namespace, "XTSE1015");

    /// <summary>XTTE1020: a sort key evaluates to a sequence of more than one item. (XSLT 2.0 §13.1.2)</summary>
    public static readonly QName XTTE1020 = new QName("err", XPathErrors.Namespace, "XTTE1020");

    /// <summary>XTDE1035: xsl:sort names a collation that is not supported. (XSLT 2.0 §13.1.3)</summary>
    public static readonly QName XTDE1035 = new QName("err", XPathErrors.Namespace, "XTDE1035");

    /// <summary>XTDE1030: sort keys that cannot be compared with each other. (XSLT 2.0 §13.1.2)</summary>
    public static readonly QName XTDE1030 = new QName("err", XPathErrors.Namespace, "XTDE1030");

    /// <summary>XTDE0030: an attribute value template of xsl:sort (order, data-type, case-order, stable) gives an invalid value. (XSLT 2.0 §13.1.1)</summary>
    public static readonly QName XTDE0030 = new QName("err", XPathErrors.Namespace, "XTDE0030");

    /// <summary>XTDE1350: a date or time picture refers to a component the value does not have, such as a year in a time. (XSLT 2.0 §16.5.1)</summary>
    public static readonly QName XTDE1350 = new QName("err", XPathErrors.Namespace, "XTDE1350");

    /// <summary>XTDE1340: the picture string of format-date, format-time or format-dateTime is invalid (or an unsupported-argument value is malformed). (XSLT 2.0 §16.5.1)</summary>
    public static readonly QName XTDE1340 = new QName("err", XPathErrors.Namespace, "XTDE1340");

    /// <summary>XTSE0090: an element has an attribute that is not allowed on it. (XSLT 2.0 §3.7)</summary>
    public static readonly QName XTSE0090 = new QName("err", XPathErrors.Namespace, "XTSE0090");

    /// <summary>XTDE1310: the picture string of format-number is invalid. (XSLT 2.0 §16.4.2)</summary>
    public static readonly QName XTDE1310 = new QName("err", XPathErrors.Namespace, "XTDE1310");

    /// <summary>XTDE1280: format-number names a decimal format that is not declared (or the name is not a valid QName). (XSLT 2.0 §16.4)</summary>
    public static readonly QName XTDE1280 = new QName("err", XPathErrors.Namespace, "XTDE1280");

    /// <summary>XTSE1300: two picture symbols of a decimal format use the same character. (XSLT 2.0 §16.4.1)</summary>
    public static readonly QName XTSE1300 = new QName("err", XPathErrors.Namespace, "XTSE1300");

    /// <summary>XTSE1295: the zero-digit of a decimal format is not a digit with value zero. (XSLT 2.0 §16.4.1)</summary>
    public static readonly QName XTSE1295 = new QName("err", XPathErrors.Namespace, "XTSE1295");

    /// <summary>XTSE1290: two xsl:decimal-format declarations of one format give different values for a property at the same import precedence. (XSLT 2.0 §16.4.1)</summary>
    public static readonly QName XTSE1290 = new QName("err", XPathErrors.Namespace, "XTSE1290");

    /// <summary>XTDE1390: the argument of system-property has a prefix with no namespace binding. (XSLT 2.0 §16.6.3)</summary>
    public static readonly QName XTDE1390 = new QName("err", XPathErrors.Namespace, "XTDE1390");

    /// <summary>XTDE1400: the argument of function-available has a prefix with no namespace binding. (XSLT 2.0 §16.6.1)</summary>
    public static readonly QName XTDE1400 = new QName("err", XPathErrors.Namespace, "XTDE1400");

    /// <summary>XTDE1425: the argument of type-available has a prefix with no namespace binding. (XSLT 2.0 §16.6.5)</summary>
    public static readonly QName XTDE1425 = new QName("err", XPathErrors.Namespace, "XTDE1425");

    /// <summary>XTDE1440: the argument of element-available has a prefix with no namespace binding. (XSLT 2.0 §16.6.2)</summary>
    public static readonly QName XTDE1440 = new QName("err", XPathErrors.Namespace, "XTDE1440");

    /// <summary>FOUT1170: unparsed-text could not read the resource (not found, bad encoding, invalid characters).</summary>
    public static readonly QName FOUT1170 = new QName("err", XPathErrors.Namespace, "FOUT1170");
    /// <summary>XTDE1270: the third argument of key() is not a single node in a document. (XSLT 2.0 §16.4)</summary>
    public static readonly QName XTDE1270 = new QName("err", XPathErrors.Namespace, "XTDE1270");

    /// <summary>It is a dynamic error if xsl:call-template is invoked without a value for a required xsl:param. (XSLT 2.0 §9.5)</summary>
    public static readonly QName XTDE0700 = new QName("err", XPathErrors.Namespace, "XTDE0700");

    /// <summary>Implementation-defined limit exceeded — recursion depth. (XSLT 2.0 §2.9)</summary>
    public static readonly QName XTDE0810 = new QName("err", XPathErrors.Namespace, "XTDE0810");

    /// <summary>Raised by xsl:message with terminate="yes". (XSLT 2.0 §16)</summary>
    public static readonly QName XTMM9000 = new QName("err", XPathErrors.Namespace, "XTMM9000");
}
