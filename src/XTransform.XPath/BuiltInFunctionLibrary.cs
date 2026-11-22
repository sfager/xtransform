// XPath 2.0 §2.1.1 — built-in function library populated from F&O.

using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;
using XTransform.XPath.Functions;

namespace XTransform.XPath;

/// <summary>
/// Built-in XPath 2.0 / F&amp;O function library.
/// Populated with the P2-08 function tranche: accessor, boolean, context, and error functions.
/// Thread-safe singleton — all registrations happen in the constructor.
/// </summary>
internal sealed class BuiltInFunctionLibrary : FunctionLibrary
{
    /// <summary>Shared singleton instance.</summary>
    public static BuiltInFunctionLibrary Instance { get; } = new BuiltInFunctionLibrary();

    /// <summary>Namespace URI for the XPath built-in functions ("fn:" prefix).</summary>
    public const string FnNamespace = "http://www.w3.org/2005/xpath-functions";

    private readonly Dictionary<string, FunctionSignature> _byKey;

    private BuiltInFunctionLibrary()
    {
        _byKey = new Dictionary<string, FunctionSignature>(128);
        RegisterAccessors();
        RegisterBooleans();
        RegisterContext();
        RegisterErrors();
        RegisterStrings();
        RegisterNumerics();
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

    // ── Additional shorthands for numerics ─────────────────────────────────────

    private static SequenceType ST_AnyAtomicType_ZeroOrMore =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrMore);

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
}
