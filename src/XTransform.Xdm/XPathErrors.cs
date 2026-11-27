namespace XTransform.Xdm;

/// <summary>
/// W3C XPath 2.0 and XQuery 1.0 error codes.
/// All codes share the namespace http://www.w3.org/2005/xqt-errors (prefix "err").
/// Add new codes here as later tasks need them.
/// </summary>
public static class XPathErrors
{
    /// <summary>Namespace URI for all W3C XPath/XQuery/XSLT error codes.</summary>
    public const string Namespace = "http://www.w3.org/2005/xqt-errors";

    /// <summary>It is a static error if an expression is not a valid instance of the grammar. (XPath 2.0 §2.3.1)</summary>
    public static readonly QName XPST0003 = new QName("err", Namespace, "XPST0003");

    /// <summary>It is a static error if an expression refers to a variable that is not in the in-scope variable bindings.</summary>
    public static readonly QName XPST0008 = new QName("err", Namespace, "XPST0008");

    /// <summary>It is a static error if the number of arguments passed to a function does not match the declared arity.</summary>
    public static readonly QName XPST0017 = new QName("err", Namespace, "XPST0017");

    /// <summary>It is a static error if the type of an expression refers to an unknown atomic type.</summary>
    public static readonly QName XPST0051 = new QName("err", Namespace, "XPST0051");

    /// <summary>It is a static error if the prefix of an expanded QName is not in-scope.</summary>
    public static readonly QName XPST0081 = new QName("err", Namespace, "XPST0081");

    /// <summary>It is a type error if the type of a value does not match the required type.</summary>
    public static readonly QName XPTY0004 = new QName("err", Namespace, "XPTY0004");

    /// <summary>It is a type error if the last step of a path yields both nodes and non-nodes.</summary>
    public static readonly QName XPTY0018 = new QName("err", Namespace, "XPTY0018");

    /// <summary>It is a type error if a step other than the last in a path yields a non-node.</summary>
    public static readonly QName XPTY0019 = new QName("err", Namespace, "XPTY0019");

    /// <summary>It is a type error if, in an axis step (or a rooted path), the context item is not a node.</summary>
    public static readonly QName XPTY0020 = new QName("err", Namespace, "XPTY0020");

    /// <summary>fn:resolve-uri: a URI argument is invalid, or the base is not an absolute URI.</summary>
    public static readonly QName FORG0002 = new QName("err", Namespace, "FORG0002");

    /// <summary>The static base URI is needed (fn:resolve-uri with one argument, fn:static-base-uri) but is not defined.</summary>
    public static readonly QName FONS0005 = new QName("err", Namespace, "FONS0005");

    /// <summary>It is a dynamic error if the context item is undefined where it is needed.</summary>
    public static readonly QName XPDY0002 = new QName("err", Namespace, "XPDY0002");

    /// <summary>It is a type error if the value of a cast cannot be converted to the target type. (F&amp;O §17.1)</summary>
    public static readonly QName FORG0001 = new QName("err", Namespace, "FORG0001");

    /// <summary>Arithmetic overflow or underflow (division by zero or similar).</summary>
    public static readonly QName FOAR0001 = new QName("err", Namespace, "FOAR0001");

    /// <summary>Invalid lexical value for the target type.</summary>
    public static readonly QName FOCA0002 = new QName("err", Namespace, "FOCA0002");

    /// <summary>fn:error called with no argument or an error QName (F&amp;O §3.1.1 / FOER0000).</summary>
    public static readonly QName FOER0000 = new QName("err", Namespace, "FOER0000");

    /// <summary>
    /// An invalid code point was supplied as an argument to fn:codepoints-to-string.
    /// (F&amp;O §7.2.1 FOCH0001)
    /// </summary>
    public static readonly QName FOCH0001 = new QName("err", Namespace, "FOCH0001");

    /// <summary>
    /// The collation URI is not in the statically known collations. (F&amp;O §7.3.4 FOCH0002)
    /// </summary>
    public static readonly QName FOCH0002 = new QName("err", Namespace, "FOCH0002");

    /// <summary>
    /// The normalization form is not supported. (F&amp;O §7.4.6 FOCH0003)
    /// </summary>
    public static readonly QName FOCH0003 = new QName("err", Namespace, "FOCH0003");

    /// <summary>
    /// The timezone duration is out of range −14:00..+14:00. (F&amp;O §10.7 FODT0003)
    /// </summary>
    public static readonly QName FODT0003 = new QName("err", Namespace, "FODT0003");

    /// <summary>
    /// Both arguments to fn:dateTime have timezones but they disagree. (F&amp;O §10.8.1 FORG0008)
    /// </summary>
    public static readonly QName FORG0008 = new QName("err", Namespace, "FORG0008");

    /// <summary>
    /// fn:zero-or-one called with a sequence containing more than one item. (F&amp;O §15.2.3 FORG0003)
    /// </summary>
    public static readonly QName FORG0003 = new QName("err", Namespace, "FORG0003");

    /// <summary>
    /// fn:one-or-more called with a sequence containing no items. (F&amp;O §15.2.4 FORG0004)
    /// </summary>
    public static readonly QName FORG0004 = new QName("err", Namespace, "FORG0004");

    /// <summary>
    /// fn:exactly-one called with a sequence containing zero or more than one item. (F&amp;O §15.2.5 FORG0005)
    /// </summary>
    public static readonly QName FORG0005 = new QName("err", Namespace, "FORG0005");

    /// <summary>
    /// Invalid regular expression flags. (F&amp;O §7.6 FORX0001)
    /// </summary>
    public static readonly QName FORX0001 = new QName("err", Namespace, "FORX0001");

    /// <summary>
    /// Invalid or unsupported regular expression pattern. (F&amp;O §7.6 FORX0002)
    /// Raised for invalid patterns, lookaround assertions, and other unsupported XPath 2.0 regex constructs.
    /// </summary>
    public static readonly QName FORX0002 = new QName("err", Namespace, "FORX0002");

    /// <summary>
    /// Regular expression matches the empty string; not permitted in fn:replace or fn:tokenize. (F&amp;O §7.6 FORX0003)
    /// </summary>
    public static readonly QName FORX0003 = new QName("err", Namespace, "FORX0003");

    /// <summary>
    /// Invalid replacement string in fn:replace. (F&amp;O §7.6.4 FORX0004)
    /// Raised when '$' in the replacement is not followed by a digit or another '$'.
    /// </summary>
    public static readonly QName FORX0004 = new QName("err", Namespace, "FORX0004");

    /// <summary>
    /// A prefix used in a QName has not been declared in the in-scope namespaces. (F&amp;O §16.1.11 FONS0004)
    /// Raised by fn:resolve-QName when the prefix cannot be resolved.
    /// </summary>
    public static readonly QName FONS0004 = new QName("err", Namespace, "FONS0004");

    /// <summary>Dynamic error: the operand of treat as does not match the required sequence type. (XPath 2.0 §3.10.5)</summary>
    public static readonly QName XPDY0050 = new QName("err", XPathErrors.Namespace, "XPDY0050");

    /// <summary>Invalid argument type: for example fn:min, fn:max or fn:sum over values that cannot be compared or added, or an effective boolean value that is not defined. (F&amp;O §15)</summary>
    public static readonly QName FORG0006 = new QName("err", XPathErrors.Namespace, "FORG0006");
}
