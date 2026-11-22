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
}
