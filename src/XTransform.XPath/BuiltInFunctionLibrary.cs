// XPath 2.0 §2.1.1 — built-in function library populated from F&O.

using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Functions;

namespace XTransform.XPath;

/// <summary>
/// Built-in XPath 2.0 / F&amp;O function library.
/// Populated with accessor, boolean, context, error, string, numeric, date/time functions,
/// and xs: constructor functions for all v1 atomic types.
/// Thread-safe singleton — all registrations happen in the constructor.
/// </summary>
internal sealed class BuiltInFunctionLibrary : FunctionLibrary
{
    /// <summary>Shared singleton instance.</summary>
    public static BuiltInFunctionLibrary Instance { get; } = new BuiltInFunctionLibrary();

    /// <summary>Namespace URI for the XPath built-in functions ("fn:" prefix).</summary>
    public const string FnNamespace = "http://www.w3.org/2005/xpath-functions";

    /// <summary>Namespace URI for XML Schema types ("xs:" prefix).</summary>
    public const string XsNamespace = "http://www.w3.org/2001/XMLSchema";

    private readonly Dictionary<string, FunctionSignature> _byKey;

    private BuiltInFunctionLibrary()
    {
        _byKey = new Dictionary<string, FunctionSignature>(256);
        RegisterAccessors();
        RegisterBooleans();
        RegisterContext();
        RegisterErrors();
        RegisterStrings();
        RegisterNumerics();
        RegisterDateTime();
        RegisterSequences();
        RegisterRegex();
        RegisterConstructors();
        RegisterQNames();
    }

    // ── FunctionLibrary contract ───────────────────────────────────────────────

    /// <inheritdoc/>
    public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
    {
        string key = MakeKey(name.NamespaceUri, name.LocalName, arity);
        if (_byKey.TryGetValue(key, out FunctionSignature? sig))
        {
            signature = sig;
            return true;
        }

        // F&O §7.4.1: fn:concat takes any number of arguments (two or more); the fixed arities above
        // are registered for speed, longer calls get a signature on demand.
        if (arity > 10 &&
            string.Equals(name.NamespaceUri, FnNamespace, System.StringComparison.Ordinal) &&
            string.Equals(name.LocalName, "concat", System.StringComparison.Ordinal))
        {
            SequenceType[] parameters = new SequenceType[arity];
            for (int i = 0; i < arity; i++)
            {
                parameters[i] = ST_AnyAtomicType_ZeroOrOne;
            }

            QName qn = new QName("fn", FnNamespace, "concat");
            signature = new FunctionSignature(qn, arity, ST_String_ExactlyOne, parameters.ToImmutableArray(), StringFunctions.FnConcatN);
            return true;
        }

        signature = null;
        return false;
    }

    // ── Registration helper ────────────────────────────────────────────────────

    private void Register(string localName, int arity, SequenceType returnType,
                          SequenceType[] parameters, FunctionInvoker invoker,
                          ExpressionDependencies callDeps = ExpressionDependencies.None)
    {
        QName qn = new QName("fn", FnNamespace, localName);
        ImmutableArray<SequenceType> paramTypes = parameters.ToImmutableArray();
        FunctionSignature sig = new FunctionSignature(qn, arity, returnType, paramTypes, invoker)
        {
            CallDependencies = callDeps,
        };
        _byKey[MakeKey(FnNamespace, localName, arity)] = sig;
    }

    private static string MakeKey(string ns, string local, int arity)
        => "{" + ns + "}" + local + "#" + arity;

    // ── Shared SequenceType shorthands ─────────────────────────────────────────

    private static SequenceType ST_Item_Star => SequenceType.AnySequence;
    private static SequenceType ST_AnyAtomicType_Star =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrMore);
    private static SequenceType ST_Item_ZeroOrOne =>
        new SequenceType(ItemType.AnyItem, Occurrence.ZeroOrOne);
    private static SequenceType ST_Node_ZeroOrOne =>
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrOne);
    private static SequenceType ST_Node_ExactlyOne =>
        new SequenceType(ItemType.AnyNode, Occurrence.ExactlyOne);
    private static SequenceType ST_QName_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.QName), Occurrence.ZeroOrOne);
    private static SequenceType ST_Boolean_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);
    private static SequenceType ST_Boolean_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ZeroOrOne);
    private static SequenceType ST_String_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ExactlyOne);
    private static SequenceType ST_AnyUri_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyUri), Occurrence.ZeroOrOne);
    private static SequenceType ST_AnyUri_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyUri), Occurrence.ExactlyOne);
    private static SequenceType ST_Integer_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
    private static SequenceType ST_DateTime_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.DateTime), Occurrence.ExactlyOne);
    private static SequenceType ST_Date_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Date), Occurrence.ExactlyOne);
    private static SequenceType ST_Time_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Time), Occurrence.ExactlyOne);
    private static SequenceType ST_DayTimeDuration_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.DayTimeDuration), Occurrence.ExactlyOne);
    private static SequenceType ST_QName_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.QName), Occurrence.ExactlyOne);

    // ── Registration groups ────────────────────────────────────────────────────

    private void RegisterAccessors()
    {
        // F&O §2.1 fn:node-name: 0-arg (uses context) and 1-arg
        Register("node-name", 0, ST_QName_ZeroOrOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnNodeName0);
        Register("node-name", 1, ST_QName_ZeroOrOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnNodeName1);

        // F&O §2.2 fn:nilled: 0-arg and 1-arg
        Register("nilled", 0, ST_Boolean_ZeroOrOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnNilled0);
        Register("nilled", 1, ST_Boolean_ZeroOrOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnNilled1);

        // F&O §2.3 fn:string: 0-arg and 1-arg
        Register("string", 0, ST_String_ExactlyOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnString0);
        Register("string", 1, ST_String_ExactlyOne, new[] { ST_Item_ZeroOrOne },
            AccessorFunctions.FnString1);

        // F&O §2.4 fn:data: 1-arg (item()*)
        Register("data", 1, ST_AnyAtomicType_Star, new[] { ST_Item_Star },
            AccessorFunctions.FnData);

        // F&O §2.5 fn:base-uri: 0-arg and 1-arg
        Register("base-uri", 0, ST_AnyUri_ZeroOrOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnBaseUri0);
        Register("base-uri", 1, ST_AnyUri_ZeroOrOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnBaseUri1);

        // F&O §2.6 fn:document-uri: 1-arg
        Register("document-uri", 1, ST_AnyUri_ZeroOrOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnDocumentUri);

        // F&O §15.5.3 fn:root: 0-arg and 1-arg
        Register("root", 0, ST_Node_ExactlyOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnRoot0);
        Register("root", 1, ST_Node_ZeroOrOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnRoot1);

        // F&O §2.7 fn:local-name: 0-arg and 1-arg
        Register("local-name", 0, ST_String_ExactlyOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnLocalName0);
        Register("local-name", 1, ST_String_ExactlyOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnLocalName1);

        // F&O §2.8 fn:namespace-uri: 0-arg and 1-arg
        Register("namespace-uri", 0, ST_AnyUri_ExactlyOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnNamespaceUri0);
        Register("namespace-uri", 1, ST_AnyUri_ExactlyOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnNamespaceUri1);

        // F&O §2.9 fn:name: 0-arg and 1-arg
        Register("name", 0, ST_String_ExactlyOne, System.Array.Empty<SequenceType>(),
            AccessorFunctions.FnName0);
        Register("name", 1, ST_String_ExactlyOne, new[] { ST_Node_ZeroOrOne },
            AccessorFunctions.FnName1);
    }

    private void RegisterBooleans()
    {
        // F&O §9.1.1 fn:true()
        Register("true", 0, ST_Boolean_ExactlyOne, System.Array.Empty<SequenceType>(),
            BooleanFunctions.FnTrue);

        // F&O §9.1.2 fn:false()
        Register("false", 0, ST_Boolean_ExactlyOne, System.Array.Empty<SequenceType>(),
            BooleanFunctions.FnFalse);

        // F&O §15.1.1 fn:boolean($arg as item()*)
        Register("boolean", 1, ST_Boolean_ExactlyOne, new[] { ST_Item_Star },
            BooleanFunctions.FnBoolean);

        // F&O §9.3.3 fn:not($arg as item()*)
        Register("not", 1, ST_Boolean_ExactlyOne, new[] { ST_Item_Star },
            BooleanFunctions.FnNot);
    }

    private void RegisterContext()
    {
        // F&O §15.2.1 fn:position() — reads context position
        Register("position", 0, ST_Integer_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnPosition, ExpressionDependencies.Position);

        // F&O §15.2.2 fn:last() — reads context size (triggers pre-counting in predicates)
        Register("last", 0, ST_Integer_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnLast, ExpressionDependencies.Last);

        // F&O §16.2 fn:current-dateTime()
        Register("current-dateTime", 0, ST_DateTime_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnCurrentDateTime);

        // F&O §16.3 fn:current-date()
        Register("current-date", 0, ST_Date_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnCurrentDate);

        // F&O §16.4 fn:current-time()
        Register("current-time", 0, ST_Time_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnCurrentTime);

        // F&O §16.6 fn:implicit-timezone()
        Register("implicit-timezone", 0, ST_DayTimeDuration_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnImplicitTimezone);

        // F&O §17.1 fn:default-collation()
        Register("default-collation", 0, ST_String_ExactlyOne, System.Array.Empty<SequenceType>(),
            ContextFunctions.FnDefaultCollation);
    }

    private static SequenceType ST_String_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ZeroOrOne);
    private static SequenceType ST_String_Star =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ZeroOrMore);
    private static SequenceType ST_Integer_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrOne);
    private static SequenceType ST_Integer_Star =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ZeroOrMore);
    private static SequenceType ST_Double_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Double), Occurrence.ExactlyOne);
    private static SequenceType ST_AnyAtomicType_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrOne);

    private void RegisterErrors()
    {
        // F&O §3.1.1 fn:error() — 0-arg
        Register("error", 0, SequenceType.EmptySequence, System.Array.Empty<SequenceType>(),
            ErrorFunctions.FnError0);

        // F&O §3.1.1 fn:error($code as xs:QName) — 1-arg
        Register("error", 1, SequenceType.EmptySequence, new[] { ST_QName_ExactlyOne },
            ErrorFunctions.FnError1);

        // F&O §17.1.5 fn:trace($value as item()*, $label as xs:string) as item()*
        Register("trace", 2, ST_Item_Star, new[] { ST_Item_Star, ST_String_ExactlyOne },
            ErrorFunctions.FnTrace);
    }

    private void RegisterStrings()
    {
        // F&O §7.4.1 fn:concat — variadic (arities 2..10)
        // Each parameter: xs:anyAtomicType? per spec
        Register("concat", 2, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat2);
        Register("concat", 3, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat3);
        Register("concat", 4, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat4);
        Register("concat", 5, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat5);
        Register("concat", 6, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat6);
        Register("concat", 7, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat7);
        Register("concat", 8, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat8);
        Register("concat", 9, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat9);
        Register("concat", 10, ST_String_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne, ST_AnyAtomicType_ZeroOrOne },
            StringFunctions.FnConcat10);

        // F&O §7.4.2 fn:string-join($seq as xs:string*, $sep as xs:string) as xs:string
        Register("string-join", 2, ST_String_ExactlyOne,
            new[] { ST_String_Star, ST_String_ExactlyOne },
            StringFunctions.FnStringJoin);

        // F&O §7.4.3 fn:substring
        Register("substring", 2, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_Double_ExactlyOne },
            StringFunctions.FnSubstring2);
        Register("substring", 3, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_Double_ExactlyOne, ST_Double_ExactlyOne },
            StringFunctions.FnSubstring3);

        // F&O §7.4.4 fn:string-length
        Register("string-length", 0, ST_Integer_ExactlyOne, System.Array.Empty<SequenceType>(),
            StringFunctions.FnStringLength0);
        Register("string-length", 1, ST_Integer_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnStringLength1);

        // F&O §7.4.5 fn:normalize-space
        Register("normalize-space", 0, ST_String_ExactlyOne, System.Array.Empty<SequenceType>(),
            StringFunctions.FnNormalizeSpace0);
        Register("normalize-space", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnNormalizeSpace1);

        // F&O §7.4.6 fn:normalize-unicode
        Register("normalize-unicode", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnNormalizeUnicode1);
        Register("normalize-unicode", 2, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnNormalizeUnicode2);

        // F&O §7.4.7 fn:upper-case
        Register("upper-case", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnUpperCase);

        // F&O §7.4.8 fn:lower-case
        Register("lower-case", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnLowerCase);

        // F&O §7.4.9 fn:translate
        Register("translate", 3, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne, ST_String_ExactlyOne },
            StringFunctions.FnTranslate);

        // F&O §7.4.10 fn:encode-for-uri
        Register("encode-for-uri", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnEncodeForUri);

        // F&O §7.4.11 fn:iri-to-uri
        Register("iri-to-uri", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnIriToUri);

        // F&O §7.4.12 fn:escape-html-uri
        Register("escape-html-uri", 1, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnEscapeHtmlUri);

        // F&O §7.5.1 fn:contains
        Register("contains", 2, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnContains2);
        Register("contains", 3, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnContains3);

        // F&O §7.5.2 fn:starts-with
        Register("starts-with", 2, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnStartsWith2);
        Register("starts-with", 3, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnStartsWith3);

        // F&O §7.5.3 fn:ends-with
        Register("ends-with", 2, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnEndsWith2);
        Register("ends-with", 3, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnEndsWith3);

        // F&O §7.5.4 fn:substring-before
        Register("substring-before", 2, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnSubstringBefore2);
        Register("substring-before", 3, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnSubstringBefore3);

        // F&O §7.5.5 fn:substring-after
        Register("substring-after", 2, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnSubstringAfter2);
        Register("substring-after", 3, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnSubstringAfter3);

        // F&O §7.2.1 fn:string-to-codepoints
        Register("string-to-codepoints", 1, ST_Integer_Star,
            new[] { ST_String_ZeroOrOne },
            StringFunctions.FnStringToCodepoints);

        // F&O §7.2.2 fn:codepoints-to-string
        Register("codepoints-to-string", 1, ST_String_ExactlyOne,
            new[] { ST_Integer_Star },
            StringFunctions.FnCodepointsToString);

        // F&O §7.3.2 fn:compare
        Register("compare", 2, ST_Integer_ZeroOrOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnCompare2);
        Register("compare", 3, ST_Integer_ZeroOrOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne, ST_String_ExactlyOne },
            StringFunctions.FnCompare3);

        // F&O §7.3.3 fn:codepoint-equal
        Register("codepoint-equal", 2, ST_Boolean_ZeroOrOne,
            new[] { ST_String_ZeroOrOne, ST_String_ZeroOrOne },
            StringFunctions.FnCodepointEqual);
    }

    // ── Additional shorthands for numerics and date/time ──────────────────────

    private static SequenceType ST_AnyAtomicType_ZeroOrMore =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrMore);

    private static SequenceType ST_Decimal_ExactlyOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ExactlyOne);
    private static SequenceType ST_Decimal_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Decimal), Occurrence.ZeroOrOne);
    private static SequenceType ST_DateTime_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.DateTime), Occurrence.ZeroOrOne);
    private static SequenceType ST_Date_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Date), Occurrence.ZeroOrOne);
    private static SequenceType ST_Time_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Time), Occurrence.ZeroOrOne);
    private static SequenceType ST_Duration_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Duration), Occurrence.ZeroOrOne);
    private static SequenceType ST_DayTimeDuration_ZeroOrOne =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.DayTimeDuration), Occurrence.ZeroOrOne);

    // Register a function in the xs: namespace.
    private void RegisterXs(string localName, int arity, SequenceType returnType,
                             SequenceType[] parameters, FunctionInvoker invoker)
    {
        QName qn = new QName("xs", XsNamespace, localName);
        ImmutableArray<SequenceType> paramTypes = parameters.ToImmutableArray();
        FunctionSignature sig = new FunctionSignature(qn, arity, returnType, paramTypes, invoker);
        _byKey[MakeKey(XsNamespace, localName, arity)] = sig;
    }

    private void RegisterNumerics()
    {
        // F&O §6.4.1 fn:abs($arg as xs:numeric?) as xs:numeric?
        Register("abs", 1, ST_AnyAtomicType_ZeroOrOne, new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnAbs);

        // F&O §6.4.2 fn:ceiling($arg as xs:numeric?) as xs:numeric?
        Register("ceiling", 1, ST_AnyAtomicType_ZeroOrOne, new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnCeiling);

        // F&O §6.4.3 fn:floor($arg as xs:numeric?) as xs:numeric?
        Register("floor", 1, ST_AnyAtomicType_ZeroOrOne, new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnFloor);

        // F&O §6.4.4 fn:round($arg as xs:numeric?) as xs:numeric?
        Register("round", 1, ST_AnyAtomicType_ZeroOrOne, new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnRound);

        // F&O §6.4.5 fn:round-half-to-even($arg as xs:numeric?) as xs:numeric?
        Register("round-half-to-even", 1, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnRoundHalfToEven1);

        // F&O §6.4.5 fn:round-half-to-even($arg as xs:numeric?, $precision as xs:integer) as xs:numeric?
        Register("round-half-to-even", 2, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrOne, ST_Integer_ExactlyOne },
            NumericFunctions.FnRoundHalfToEven2);

        // F&O §15.4.9 fn:sum($arg as xs:anyAtomicType*) as xs:anyAtomicType
        Register("sum", 1, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore },
            NumericFunctions.FnSum1);

        // F&O §15.4.9 fn:sum($arg as xs:anyAtomicType*, $zero as xs:anyAtomicType?) as xs:anyAtomicType?
        Register("sum", 2, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore, ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnSum2);

        // F&O §15.4.4 fn:avg($arg as xs:anyAtomicType*) as xs:anyAtomicType?
        Register("avg", 1, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore },
            NumericFunctions.FnAvg);

        // F&O §15.4.5 fn:min($arg as xs:anyAtomicType*) as xs:anyAtomicType?
        Register("min", 1, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore },
            NumericFunctions.FnMin1);

        // F&O §15.4.5 fn:min($arg as xs:anyAtomicType*, $collation as xs:string) as xs:anyAtomicType?
        Register("min", 2, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore, ST_String_ExactlyOne },
            NumericFunctions.FnMin2);

        // F&O §15.4.6 fn:max($arg as xs:anyAtomicType*) as xs:anyAtomicType?
        Register("max", 1, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore },
            NumericFunctions.FnMax1);

        // F&O §15.4.6 fn:max($arg as xs:anyAtomicType*, $collation as xs:string) as xs:anyAtomicType?
        Register("max", 2, ST_AnyAtomicType_ZeroOrOne,
            new[] { ST_AnyAtomicType_ZeroOrMore, ST_String_ExactlyOne },
            NumericFunctions.FnMax2);

        // F&O §15.4.3 fn:count($arg as item()*) as xs:integer
        Register("count", 1, ST_Integer_ExactlyOne,
            new[] { ST_Item_Star },
            NumericFunctions.FnCount);

        // F&O §6.4.6 fn:number() as xs:double — uses context item
        Register("number", 0, ST_Double_ExactlyOne, System.Array.Empty<SequenceType>(),
            NumericFunctions.FnNumber0, ExpressionDependencies.ContextItem);

        // F&O §6.4.6 fn:number($arg as xs:anyAtomicType?) as xs:double
        Register("number", 1, ST_Double_ExactlyOne,
            new[] { ST_AnyAtomicType_ZeroOrOne },
            NumericFunctions.FnNumber1);
    }

    private void RegisterDateTime()
    {
        // F&O §10.5.1 fn:year-from-dateTime
        Register("year-from-dateTime", 1, ST_Integer_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnYearFromDateTime);

        // F&O §10.5.2 fn:month-from-dateTime
        Register("month-from-dateTime", 1, ST_Integer_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnMonthFromDateTime);

        // F&O §10.5.3 fn:day-from-dateTime
        Register("day-from-dateTime", 1, ST_Integer_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnDayFromDateTime);

        // F&O §10.5.4 fn:hours-from-dateTime
        Register("hours-from-dateTime", 1, ST_Integer_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnHoursFromDateTime);

        // F&O §10.5.5 fn:minutes-from-dateTime
        Register("minutes-from-dateTime", 1, ST_Integer_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnMinutesFromDateTime);

        // F&O §10.5.6 fn:seconds-from-dateTime
        Register("seconds-from-dateTime", 1, ST_Decimal_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnSecondsFromDateTime);

        // F&O §10.5.7 fn:timezone-from-dateTime
        Register("timezone-from-dateTime", 1, ST_DayTimeDuration_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnTimezoneFromDateTime);

        // F&O §10.5.8 fn:year-from-date
        Register("year-from-date", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne },
            DateTimeFunctions.FnYearFromDate);

        // F&O §10.5.9 fn:month-from-date
        Register("month-from-date", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne },
            DateTimeFunctions.FnMonthFromDate);

        // F&O §10.5.10 fn:day-from-date
        Register("day-from-date", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne },
            DateTimeFunctions.FnDayFromDate);

        // F&O §10.5.11 fn:timezone-from-date
        Register("timezone-from-date", 1, ST_DayTimeDuration_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne },
            DateTimeFunctions.FnTimezoneFromDate);

        // F&O §10.5.12 fn:hours-from-time
        Register("hours-from-time", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne },
            DateTimeFunctions.FnHoursFromTime);

        // F&O §10.5.13 fn:minutes-from-time
        Register("minutes-from-time", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne },
            DateTimeFunctions.FnMinutesFromTime);

        // F&O §10.5.14 fn:seconds-from-time
        Register("seconds-from-time", 1, ST_Decimal_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne },
            DateTimeFunctions.FnSecondsFromTime);

        // F&O §10.5.15 fn:timezone-from-time
        Register("timezone-from-time", 1, ST_DayTimeDuration_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne },
            DateTimeFunctions.FnTimezoneFromTime);

        // F&O §10.6.1 fn:years-from-duration
        Register("years-from-duration", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnYearsFromDuration);

        // F&O §10.6.2 fn:months-from-duration
        Register("months-from-duration", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnMonthsFromDuration);

        // F&O §10.6.3 fn:days-from-duration
        Register("days-from-duration", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnDaysFromDuration);

        // F&O §10.6.4 fn:hours-from-duration
        Register("hours-from-duration", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnHoursFromDuration);

        // F&O §10.6.5 fn:minutes-from-duration
        Register("minutes-from-duration", 1, ST_Integer_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnMinutesFromDuration);

        // F&O §10.6.6 fn:seconds-from-duration
        Register("seconds-from-duration", 1, ST_Decimal_ZeroOrOne,
            new[] { ST_Duration_ZeroOrOne },
            DateTimeFunctions.FnSecondsFromDuration);

        // F&O §10.7.6 fn:adjust-dateTime-to-timezone — 1-arg (uses implicit timezone) and 2-arg
        Register("adjust-dateTime-to-timezone", 1, ST_DateTime_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne },
            DateTimeFunctions.FnAdjustDateTimeToTimezone1);
        Register("adjust-dateTime-to-timezone", 2, ST_DateTime_ZeroOrOne,
            new[] { ST_DateTime_ZeroOrOne, ST_DayTimeDuration_ZeroOrOne },
            DateTimeFunctions.FnAdjustDateTimeToTimezone2);

        // F&O §10.7.7 fn:adjust-date-to-timezone — 1-arg (uses implicit timezone) and 2-arg
        Register("adjust-date-to-timezone", 1, ST_Date_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne },
            DateTimeFunctions.FnAdjustDateToTimezone1);
        Register("adjust-date-to-timezone", 2, ST_Date_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne, ST_DayTimeDuration_ZeroOrOne },
            DateTimeFunctions.FnAdjustDateToTimezone2);

        // F&O §10.7.8 fn:adjust-time-to-timezone — 1-arg (uses implicit timezone) and 2-arg
        Register("adjust-time-to-timezone", 1, ST_Time_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne },
            DateTimeFunctions.FnAdjustTimeToTimezone1);
        Register("adjust-time-to-timezone", 2, ST_Time_ZeroOrOne,
            new[] { ST_Time_ZeroOrOne, ST_DayTimeDuration_ZeroOrOne },
            DateTimeFunctions.FnAdjustTimeToTimezone2);

        // F&O §10.8.1 fn:dateTime($date, $time)
        Register("dateTime", 2, ST_DateTime_ZeroOrOne,
            new[] { ST_Date_ZeroOrOne, ST_Time_ZeroOrOne },
            DateTimeFunctions.FnDateTime);
    }

    private static SequenceType ST_Item_OneOrMore =>
        new SequenceType(ItemType.AnyItem, Occurrence.OneOrMore);
    private static SequenceType ST_Element_Star =>
        new SequenceType(ItemType.Kind(XdmNodeKind.Element), Occurrence.ZeroOrMore);
    private static SequenceType ST_Element_ExactlyOne =>
        new SequenceType(ItemType.Kind(XdmNodeKind.Element), Occurrence.ExactlyOne);
    private static SequenceType ST_Node_Star =>
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrMore);

    private void RegisterSequences()
    {
        // F&O §15.1.1 fn:empty($arg as item()*) as xs:boolean
        Register("empty", 1, ST_Boolean_ExactlyOne, new[] { ST_Item_Star },
            SequenceFunctions.FnEmpty);

        // F&O §15.1.2 fn:exists($arg as item()*) as xs:boolean
        Register("exists", 1, ST_Boolean_ExactlyOne, new[] { ST_Item_Star },
            SequenceFunctions.FnExists);

        // F&O §15.1.3 fn:distinct-values
        Register("distinct-values", 1, ST_AnyAtomicType_Star,
            new[] { ST_AnyAtomicType_Star },
            SequenceFunctions.FnDistinctValues1);
        Register("distinct-values", 2, ST_AnyAtomicType_Star,
            new[] { ST_AnyAtomicType_Star, ST_String_ExactlyOne },
            SequenceFunctions.FnDistinctValues2);

        // F&O §15.1.4 fn:index-of
        Register("index-of", 2, ST_Integer_Star,
            new[] { ST_AnyAtomicType_Star, ST_AnyAtomicType_ZeroOrOne },
            SequenceFunctions.FnIndexOf2);
        Register("index-of", 3, ST_Integer_Star,
            new[] { ST_AnyAtomicType_Star, ST_AnyAtomicType_ZeroOrOne, ST_String_ExactlyOne },
            SequenceFunctions.FnIndexOf3);

        // F&O §15.1.5 fn:insert-before($target, $position, $inserts)
        Register("insert-before", 3, ST_Item_Star,
            new[] { ST_Item_Star, ST_Integer_ExactlyOne, ST_Item_Star },
            SequenceFunctions.FnInsertBefore);

        // F&O §15.1.6 fn:remove($target, $position)
        Register("remove", 2, ST_Item_Star,
            new[] { ST_Item_Star, ST_Integer_ExactlyOne },
            SequenceFunctions.FnRemove);

        // F&O §15.1.7 fn:reverse($arg)
        Register("reverse", 1, ST_Item_Star, new[] { ST_Item_Star },
            SequenceFunctions.FnReverse);

        // F&O §15.1.10 fn:subsequence
        Register("subsequence", 2, ST_Item_Star,
            new[] { ST_Item_Star, ST_Double_ExactlyOne },
            SequenceFunctions.FnSubsequence2);
        Register("subsequence", 3, ST_Item_Star,
            new[] { ST_Item_Star, ST_Double_ExactlyOne, ST_Double_ExactlyOne },
            SequenceFunctions.FnSubsequence3);

        // F&O §15.1.11 fn:unordered($sourceSeq)
        Register("unordered", 1, ST_Item_Star, new[] { ST_Item_Star },
            SequenceFunctions.FnUnordered);

        // F&O §15.2.3 fn:zero-or-one($arg)
        Register("zero-or-one", 1, ST_Item_ZeroOrOne, new[] { ST_Item_Star },
            SequenceFunctions.FnZeroOrOne);

        // F&O §15.2.4 fn:one-or-more($arg)
        Register("one-or-more", 1, ST_Item_OneOrMore, new[] { ST_Item_Star },
            SequenceFunctions.FnOneOrMore);

        // F&O §15.2.5 fn:exactly-one($arg)
        Register("exactly-one", 1, ST_Item_ZeroOrOne, new[] { ST_Item_Star },
            SequenceFunctions.FnExactlyOne);

        // F&O §15.3.1 fn:deep-equal
        Register("deep-equal", 2, ST_Boolean_ExactlyOne,
            new[] { ST_Item_Star, ST_Item_Star },
            SequenceFunctions.FnDeepEqual2);
        Register("deep-equal", 3, ST_Boolean_ExactlyOne,
            new[] { ST_Item_Star, ST_Item_Star, ST_String_ExactlyOne },
            SequenceFunctions.FnDeepEqual3);

        // F&O §15.5.1 fn:id
        Register("id", 1, ST_Element_Star, new[] { ST_String_Star },
            SequenceFunctions.FnId1,
            ExpressionDependencies.ContextItem);
        Register("id", 2, ST_Element_Star,
            new[] { ST_String_Star, ST_Node_ZeroOrOne },
            SequenceFunctions.FnId2);

        // F&O §15.5.2 fn:idref
        Register("idref", 1, ST_Node_Star, new[] { ST_String_Star },
            SequenceFunctions.FnIdRef1,
            ExpressionDependencies.ContextItem);
        Register("idref", 2, ST_Node_Star,
            new[] { ST_String_Star, ST_Node_ZeroOrOne },
            SequenceFunctions.FnIdRef2);
    }

    // F&O §17 — xs: constructor functions for all v1 atomic types.
    // Each constructor takes a single xs:anyAtomicType? and casts it to the target type.
    // The two non-constructible types (AnyAtomicType, Notation) are excluded per F&O §17.1.
    private void RegisterConstructors()
    {
        // Build input parameter type once: xs:anyAtomicType?
        SequenceType paramType = ST_AnyAtomicType_ZeroOrOne;

        // Explicit list of all constructible type codes — inline to avoid static field
        // initialization ordering issues (Instance is initialized before any other static field).
        // AnyAtomicType and Notation are intentionally excluded per F&O §17.1.
        XdmTypeCode[] targets =
        {
            XdmTypeCode.UntypedAtomic,
            XdmTypeCode.String, XdmTypeCode.NormalizedString, XdmTypeCode.Token,
            XdmTypeCode.Language, XdmTypeCode.NmToken, XdmTypeCode.Name,
            XdmTypeCode.NcName, XdmTypeCode.Id, XdmTypeCode.IdRef, XdmTypeCode.Entity,
            XdmTypeCode.Boolean,
            XdmTypeCode.Decimal, XdmTypeCode.Integer, XdmTypeCode.NonPositiveInteger,
            XdmTypeCode.NegativeInteger, XdmTypeCode.Long, XdmTypeCode.Int,
            XdmTypeCode.Short, XdmTypeCode.Byte, XdmTypeCode.NonNegativeInteger,
            XdmTypeCode.UnsignedLong, XdmTypeCode.UnsignedInt, XdmTypeCode.UnsignedShort,
            XdmTypeCode.UnsignedByte, XdmTypeCode.PositiveInteger,
            XdmTypeCode.Double, XdmTypeCode.Float,
            XdmTypeCode.Duration, XdmTypeCode.DayTimeDuration, XdmTypeCode.YearMonthDuration,
            XdmTypeCode.DateTime, XdmTypeCode.Date, XdmTypeCode.Time,
            XdmTypeCode.GYearMonth, XdmTypeCode.GYear, XdmTypeCode.GMonthDay,
            XdmTypeCode.GDay, XdmTypeCode.GMonth,
            XdmTypeCode.HexBinary, XdmTypeCode.Base64Binary,
            XdmTypeCode.AnyUri, XdmTypeCode.QName,
        };

        // Register each type code as xs:localName(xs:anyAtomicType?) → target-type?
        foreach (XdmTypeCode target in targets)
        {
            string localName = CastTable.GetXsLocalName(target);
            XdmTypeCode capturedTarget = target;
            SequenceType returnType = new SequenceType(ItemType.Atomic(capturedTarget), Occurrence.ZeroOrOne);

            // F&O §17.1: constructor takes xs:anyAtomicType?, returns target-type?.
            RegisterXs(localName, 1, returnType, new[] { paramType },
                (ctx, cargs) => ConstructorInvoker(capturedTarget, cargs));
        }
    }

    private void RegisterRegex()
    {
        // F&O §7.6.2 fn:matches($input as xs:string?, $pattern as xs:string) as xs:boolean
        Register("matches", 2, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne },
            RegexFunctions.FnMatches2);

        // F&O §7.6.2 fn:matches($input, $pattern, $flags) as xs:boolean
        Register("matches", 3, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne, ST_String_ExactlyOne },
            RegexFunctions.FnMatches3);

        // F&O §7.6.3 fn:replace($input, $pattern, $replacement) as xs:string
        Register("replace", 3, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne, ST_String_ExactlyOne },
            RegexFunctions.FnReplace3);

        // F&O §7.6.3 fn:replace($input, $pattern, $replacement, $flags) as xs:string
        Register("replace", 4, ST_String_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne, ST_String_ExactlyOne, ST_String_ExactlyOne },
            RegexFunctions.FnReplace4);

        // F&O §7.6.4 fn:tokenize($input as xs:string?, $pattern as xs:string) as xs:string*
        Register("tokenize", 2, ST_String_Star,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne },
            RegexFunctions.FnTokenize2);

        // F&O §7.6.4 fn:tokenize($input, $pattern, $flags) as xs:string*
        Register("tokenize", 3, ST_String_Star,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne, ST_String_ExactlyOne },
            RegexFunctions.FnTokenize3);
    }

    private void RegisterQNames()
    {
        // F&O §16.1.1 fn:QName($paramURI as xs:string?, $paramQName as xs:string) as xs:QName
        Register("QName", 2, ST_QName_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_String_ExactlyOne },
            QNameFunctions.FnQName);

        // F&O §16.1.2 fn:resolve-QName($qname as xs:string?, $element as element()) as xs:QName?
        Register("resolve-QName", 2, ST_QName_ZeroOrOne,
            new[] { ST_String_ZeroOrOne, ST_Element_ExactlyOne },
            QNameFunctions.FnResolveQName);

        // F&O §16.1.3 fn:prefix-from-QName($arg as xs:QName?) as xs:NCName?
        // Return type is xs:NCName? — represented as xs:string? here (NCName is-a string).
        Register("prefix-from-QName", 1, ST_String_ZeroOrOne,
            new[] { ST_QName_ZeroOrOne },
            QNameFunctions.FnPrefixFromQName);

        // F&O §16.1.4 fn:local-name-from-QName($arg as xs:QName?) as xs:NCName?
        Register("local-name-from-QName", 1, ST_String_ZeroOrOne,
            new[] { ST_QName_ZeroOrOne },
            QNameFunctions.FnLocalNameFromQName);

        // F&O §16.1.5 fn:namespace-uri-from-QName($arg as xs:QName?) as xs:anyURI?
        Register("namespace-uri-from-QName", 1, ST_AnyUri_ZeroOrOne,
            new[] { ST_QName_ZeroOrOne },
            QNameFunctions.FnNamespaceUriFromQName);

        // F&O §16.1.6 fn:namespace-uri-for-prefix($prefix as xs:string?, $element as element()) as xs:anyURI?
        Register("namespace-uri-for-prefix", 2, ST_AnyUri_ZeroOrOne,
            new[] { ST_String_ZeroOrOne, ST_Element_ExactlyOne },
            QNameFunctions.FnNamespaceUriForPrefix);

        // F&O §16.1.7 fn:in-scope-prefixes($element as element()) as xs:string*
        Register("in-scope-prefixes", 1, ST_String_Star,
            new[] { ST_Element_ExactlyOne },
            QNameFunctions.FnInScopePrefixes);

        // F&O §13.3.2 fn:lang($testlang as xs:string?) as xs:boolean — 1-arg uses context item
        Register("lang", 1, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne },
            QNameFunctions.FnLang1,
            ExpressionDependencies.ContextItem);

        // F&O §13.3.2 fn:lang($testlang as xs:string?, $node as node()) as xs:boolean — 2-arg
        Register("lang", 2, ST_Boolean_ExactlyOne,
            new[] { ST_String_ZeroOrOne, ST_Node_ExactlyOne },
            QNameFunctions.FnLang2);
    }

    /// <summary>
    /// Runtime invoker for xs:TYPE(xs:anyAtomicType?) constructor functions.
    /// F&amp;O §17.1: empty → empty; otherwise cast the single atomic value to the target type.
    /// </summary>
    // F&O §17.1
    private static IXdmIterator ConstructorInvoker(XdmTypeCode target, XdmSequence[] args)
    {
        XdmItem? item = args[0].FirstOrNull();
        if (item is null)
        {
            // F&O §17.1: empty → empty.
            return EmptyIterator.Instance;
        }

        if (!(item is XdmAtomicValue av))
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                $"xs:{CastTable.GetXsLocalName(target)}() constructor requires an atomic argument.",
                default);
        }

        // F&O §17.1: cast the atomic value to the target type.
        XdmAtomicValue result = CastTable.Cast(av, target, default);
        return new SingleItemIterator(result);
    }
}
