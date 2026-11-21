# Core interfaces

The shapes every layer builds on. Implement these signatures as written; if one turns out
to be wrong, change this file in the same pull request as the code and say why.

Conventions used below:

- `public` types are part of the shipped API (and appear in `PublicAPI.*.txt`).
- `internal` types are engine internals, visible to tests via `InternalsVisibleTo`.
- Everything must compile for `netstandard2.0` (see [decisions.md D5](decisions.md#d5-target-frameworks-and-polyfills)).
- Bodies are omitted; `// …` marks members the implementer fills in.

## Contents

1. [Names](#names) — `QName`, `NamePool`
2. [Source locations](#source-locations) — `SourceSpan`
3. [Errors](#errors) — exception hierarchy, `Diagnostic`, error-code constants
4. [Atomic types](#atomic-types) — `XdmTypeCode`, `XdmAtomicValue`, `CastTable`
5. [Items and sequences](#items-and-sequences) — `XdmItem`, `XdmSequence`, `IXdmIterator`
6. [Nodes and trees](#nodes-and-trees) — `XdmNode`, `XdmNodeKind`, `Axis`, `TinyTree`
7. [Sequence types](#sequence-types) — `ItemType`, `Occurrence`, `SequenceType`
8. [XPath front end](#xpath-front-end) — tokens, lexer, AST
9. [Evaluation](#evaluation) — `ExpressionNode`, `DynamicContext`
10. [Output](#output) — `IResultReceiver`
11. [Public API](#public-api) — `XsltCompiler`, `XsltExecutable`, `XsltTransformer`

---

## Names

Project: `XTransform.Xdm`, namespace `XTransform.Xdm`.

```csharp
/// <summary>An expanded QName. Equality ignores the prefix.</summary>
public readonly struct QName : IEquatable<QName>
{
    public QName(string localName);                                   // no namespace
    public QName(string namespaceUri, string localName);
    public QName(string prefix, string namespaceUri, string localName);

    public string Prefix { get; }        // "" when none; never null
    public string NamespaceUri { get; }  // "" for no namespace; never null
    public string LocalName { get; }

    /// <summary>Clark notation: "{uri}local", or "local" when there is no namespace.</summary>
    public string ClarkName { get; }

    public static QName ParseClark(string clarkName);
    public bool Equals(QName other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public override string ToString();   // prefix:local or local
    public static bool operator ==(QName left, QName right);
    public static bool operator !=(QName left, QName right);
}

/// <summary>
/// Interns expanded names as integer codes so name tests are integer comparisons.
/// Thread-safe: one pool is shared by a compiled stylesheet and every transformation run
/// from it. Codes are stable for the pool's lifetime and never reused.
/// </summary>
internal sealed class NamePool
{
    public const int NoName = -1;

    public int Allocate(string namespaceUri, string localName);   // returns existing code if present
    public int Allocate(QName name);
    public bool TryGetCode(string namespaceUri, string localName, out int code);

    public string GetNamespaceUri(int nameCode);
    public string GetLocalName(int nameCode);
    public QName GetQName(int nameCode, string prefix = "");

    /// <summary>Code for the namespace URI alone, used by "ns:*" name tests.</summary>
    public int GetUriCode(int nameCode);
    public int AllocateUri(string namespaceUri);
}
```

## Source locations

```csharp
/// <summary>
/// A location in a stylesheet module or XPath expression. Offsets are within the
/// expression text (0-based, UTF-16 units); Line/Column locate the containing element or
/// attribute in the module (1-based, 0 when unknown).
/// </summary>
public readonly struct SourceSpan
{
    public SourceSpan(string? moduleUri, int line, int column, int start = 0, int length = 0);

    public string? ModuleUri { get; }
    public int Line { get; }
    public int Column { get; }
    public int Start { get; }
    public int Length { get; }

    public static SourceSpan None { get; }
    public SourceSpan WithRange(int start, int length);
    public override string ToString();   // "style.xsl(12,5)" or "(unknown)"
}
```

## Errors

Project: `XTransform.Xdm`, namespace `XTransform.Xdm` (the root exception and codes are
needed by every layer). Model and rules: [decisions.md D3](decisions.md#d3-error-model).

```csharp
public abstract class XTransformException : Exception
{
    protected XTransformException(QName errorCode, string message, SourceSpan location,
                                  Exception? innerException = null);

    /// <summary>The W3C error QName, e.g. {http://www.w3.org/2005/xqt-errors}XPTY0004.</summary>
    public QName ErrorCode { get; }
    public SourceSpan Location { get; }
}

/// <summary>Static (compile-time) error in a single XPath expression.</summary>
public sealed class XPathStaticException : XTransformException { /* ctor(QName, string, SourceSpan) */ }

/// <summary>Dynamic error raised while evaluating XPath, including fn:error.</summary>
public class XPathDynamicException : XTransformException
{
    public XPathDynamicException(QName errorCode, string message, SourceSpan location);
    /// <summary>The error object passed to fn:error, if any.</summary>
    public XdmSequence? ErrorObject { get; init; }
}

/// <summary>Dynamic error with an XSLT-specific code; carries the template stack.</summary>
public sealed class XsltDynamicException : XPathDynamicException
{
    public XsltDynamicException(QName errorCode, string message, SourceSpan location);
    public IReadOnlyList<SourceSpan> TemplateStack { get; init; }
}

/// <summary>Thrown once by XsltCompiler.Compile with every static error found.</summary>
public sealed class XsltStaticException : XTransformException
{
    public XsltStaticException(IReadOnlyList<Diagnostic> errors);
    public IReadOnlyList<Diagnostic> Errors { get; }   // the first one supplies ErrorCode/Location
}

public enum DiagnosticSeverity { Warning, Error }

public sealed class Diagnostic
{
    public Diagnostic(DiagnosticSeverity severity, QName code, string message, SourceSpan location);
    public DiagnosticSeverity Severity { get; }
    public QName Code { get; }
    public string Message { get; }
    public SourceSpan Location { get; }
    public override string ToString();   // "style.xsl(12,5): error XTSE0010: …"
}

internal sealed class DiagnosticBag
{
    public void Add(Diagnostic diagnostic);
    public void Error(QName code, string message, SourceSpan location);
    public void Warning(QName code, string message, SourceSpan location);
    public bool HasErrors { get; }
    public IReadOnlyList<Diagnostic> ToList();
}

/// <summary>Error-code constants. One class per spec; add codes as they are used.</summary>
public static class XPathErrors
{
    public const string Namespace = "http://www.w3.org/2005/xqt-errors";
    public static readonly QName XPST0003;   // syntax error
    public static readonly QName XPST0008;   // undeclared variable
    public static readonly QName XPST0017;   // unknown function / wrong arity
    public static readonly QName XPST0051;   // unknown atomic type
    public static readonly QName XPST0081;   // undeclared prefix
    public static readonly QName XPTY0004;   // type error
    public static readonly QName XPDY0002;   // context item undefined
    public static readonly QName FORG0001;   // invalid value for cast
    public static readonly QName FOAR0001;   // division by zero
    public static readonly QName FOCA0002;   // invalid lexical value
    // …
}

public static class XsltErrors
{
    public static readonly QName XTSE0010;   // misplaced / unknown XSLT element
    public static readonly QName XTDE0640;   // circular variable reference
    public static readonly QName XTMM9000;   // xsl:message terminate="yes"
    // …
}
```

## Atomic types

Project: `XTransform.Xdm`.

```csharp
/// <summary>
/// Built-in atomic types supported in v1, in the XSD derivation order. The numeric
/// values are stable and index the cast table; append new members at the end only.
/// </summary>
public enum XdmTypeCode : byte
{
    AnyAtomicType, UntypedAtomic,
    String, NormalizedString, Token, Language, NmToken, Name, NcName, Id, IdRef, Entity,
    Boolean,
    Decimal, Integer, NonPositiveInteger, NegativeInteger, Long, Int, Short, Byte,
    NonNegativeInteger, UnsignedLong, UnsignedInt, UnsignedShort, UnsignedByte, PositiveInteger,
    Double, Float,
    Duration, DayTimeDuration, YearMonthDuration,
    DateTime, Date, Time, GYearMonth, GYear, GMonthDay, GDay, GMonth,
    HexBinary, Base64Binary,
    AnyUri, QName, Notation,
}

/// <summary>
/// An atomic value: an immutable XdmItem holding a type code and a typed payload.
/// Payload representation per type family:
///   string-derived, untypedAtomic, anyURI → string
///   boolean → bool
///   integer-derived → long, or BigInteger when out of range
///   decimal → XsdDecimal (wraps System.Decimal; see risks table in design.md)
///   double / float → double / float
///   durations → XsdDuration (months + decimal seconds)
///   date/time family → XsdDateTime (components + optional timezone in minutes)
///   hexBinary / base64Binary → ReadOnlyMemory&lt;byte&gt;
///   QName / NOTATION → QName
/// </summary>
public sealed class XdmAtomicValue : XdmItem, IEquatable<XdmAtomicValue>
{
    public XdmTypeCode TypeCode { get; }

    public static XdmAtomicValue String(string value);
    public static XdmAtomicValue UntypedAtomic(string value);
    public static XdmAtomicValue Boolean(bool value);       // cached True/False instances
    public static XdmAtomicValue Integer(long value);
    public static XdmAtomicValue Integer(BigInteger value);
    public static XdmAtomicValue Decimal(decimal value);
    public static XdmAtomicValue Double(double value);
    public static XdmAtomicValue Float(float value);
    public static XdmAtomicValue AnyUri(string value);
    public static XdmAtomicValue QNameValue(QName value);
    // … one factory per payload family; date/time factories take the Xsd* structs

    /// <summary>Parses a lexical form per the type's XSD rules; throws FORG0001 / FOCA0002.</summary>
    public static XdmAtomicValue Parse(string lexical, XdmTypeCode type);
    public static bool TryParse(string lexical, XdmTypeCode type, out XdmAtomicValue? value);

    /// <summary>The canonical lexical representation (fn:string of the value).</summary>
    public override string GetStringValue();

    // Typed accessors; throw InvalidCastException on a type mismatch (engine bug, not user error).
    public string AsString();
    public bool AsBoolean();
    public long AsInt64();
    public BigInteger AsBigInteger();
    public XsdDecimal AsDecimal();
    public double AsDouble();
    public QName AsQName();

    /// <summary>Value equality per op:*-equal for the same primitive type; identity otherwise.</summary>
    public bool Equals(XdmAtomicValue? other);
}

// Payload structs, defined in phase 1 (task P1-04). Each is a readonly struct with
// Parse/TryParse, canonical ToString(), equality and comparison per F&O:
//   public readonly struct XsdDecimal   — System.Decimal plus overflow detection (FOAR0002)
//   public readonly struct XsdDuration  — int months + XsdDecimal seconds
//   public readonly struct XsdDateTime  — year (long, may be negative), month, day, hour,
//                                         minute, XsdDecimal second, int? timezone minutes,
//                                         plus a kind flag for date/time/gYear… variants

/// <summary>The XPath casting rules (F&amp;O §17.1) as one table plus value conversion.</summary>
internal static class CastTable
{
    /// <summary>Whether a cast from source to target type is ever allowed (Y/M in the spec table).</summary>
    public static bool IsCastAllowed(XdmTypeCode source, XdmTypeCode target);

    /// <summary>Casts or throws XPTY0004 (cast not allowed) / FORG0001 (value invalid).</summary>
    public static XdmAtomicValue Cast(XdmAtomicValue value, XdmTypeCode target, SourceSpan location);

    /// <summary>Non-throwing form for "castable as".</summary>
    public static bool TryCast(XdmAtomicValue value, XdmTypeCode target, out XdmAtomicValue? result);

    /// <summary>Type promotion for function conversion: numeric (integer→decimal→float→double) and anyURI→string.</summary>
    public static bool TryPromote(XdmAtomicValue value, XdmTypeCode target, out XdmAtomicValue? result);

    public static XdmTypeCode PrimitiveTypeOf(XdmTypeCode type);
    public static bool IsSubtypeOf(XdmTypeCode type, XdmTypeCode ancestor);
}
```

## Items and sequences

```csharp
/// <summary>Base of every item: a node or an atomic value. Immutable.</summary>
public abstract class XdmItem
{
    private protected XdmItem();          // only XdmNode and XdmAtomicValue derive
    public bool IsNode { get; }
    public bool IsAtomic { get; }
    public abstract string GetStringValue();
}

/// <summary>
/// An immutable sequence of items (item()*). Never null in the engine: the empty
/// sequence is <see cref="Empty"/>. Implementations: empty, singleton, array-backed,
/// and lazy (wraps an iterator and memoises on first full read).
/// </summary>
public abstract class XdmSequence : IEnumerable<XdmItem>
{
    public static XdmSequence Empty { get; }
    public static XdmSequence Of(XdmItem item);
    public static XdmSequence Of(params XdmItem[] items);
    public static XdmSequence From(IEnumerable<XdmItem> items);
    internal static XdmSequence Lazy(IXdmIterator iterator);

    /// <summary>Count of items; forces a lazy sequence.</summary>
    public abstract int Count { get; }
    public bool IsEmpty { get; }
    public abstract XdmItem this[int index] { get; }     // 0-based
    public XdmItem? FirstOrNull();
    public abstract IXdmIterator GetIterator();
    public IEnumerator<XdmItem> GetEnumerator();
    // IEnumerable.GetEnumerator explicit
}

/// <summary>
/// Pull iterator used throughout evaluation. Not IEnumerator: avoids the
/// MoveNext/Current double dispatch and gives cheap Position for fn:position().
/// </summary>
public interface IXdmIterator
{
    /// <summary>Advances and returns the next item, or null at the end.</summary>
    XdmItem? Next();

    /// <summary>1-based position of the last item returned; 0 before the first.</summary>
    int Position { get; }

    /// <summary>A fresh iterator over the same items from the start (for last()).</summary>
    IXdmIterator Clone();
}
```

## Nodes and trees

```csharp
public enum XdmNodeKind : byte
{
    Document, Element, Attribute, Text, Comment, ProcessingInstruction, Namespace,
}

public enum Axis : byte
{
    Child, Descendant, DescendantOrSelf, Self, Attribute, Parent, Ancestor,
    AncestorOrSelf, FollowingSibling, PrecedingSibling, Following, Preceding, Namespace,
}

/// <summary>
/// A node in some tree. Public face of the internal tree handle. Identity
/// (op:is-same-node) is tree + index; two XdmNode instances may represent one node.
/// </summary>
public sealed class XdmNode : XdmItem, IEquatable<XdmNode>
{
    public XdmNodeKind NodeKind { get; }
    public QName? NodeName { get; }                      // null for document, text, comment
    public XdmNode? Parent { get; }
    public string? BaseUri { get; }
    public string? DocumentUri { get; }
    public override string GetStringValue();
    /// <summary>Typed value (fn:data). Untyped trees: xs:untypedAtomic, or xs:string for comments/PIs.</summary>
    public XdmSequence GetTypedValue();
    public IEnumerable<XdmNode> Axis(Axis axis);

    /// <summary>Document-order comparison, also across trees (stable per run).</summary>
    public int CompareDocumentOrder(XdmNode other);
    public bool Equals(XdmNode? other);                  // node identity

    internal NodeHandle Handle { get; }
}

/// <summary>Allocation-free reference to a node: the tree plus the node's index in it.</summary>
internal readonly struct NodeHandle : IEquatable<NodeHandle>
{
    public NodeHandle(TinyTree tree, int index);
    public TinyTree Tree { get; }
    public int Index { get; }   // nodes are numbered in document order; attributes/namespaces have separate index spaces (see TinyTree)
}

/// <summary>
/// Read-only, array-based node store ("TinyTree"). One instance per document
/// (source, temporary tree or result tree). Built once by TinyTreeBuilder; immutable after.
/// Node index order = document order, so document-order comparison is an int compare.
/// </summary>
internal sealed class TinyTree
{
    public NamePool NamePool { get; }
    public long TreeId { get; }                          // global ordering of trees within a run
    public int NodeCount { get; }

    // Parallel arrays (exposed as internal properties/spans for the navigator):
    //   kind[i] (XdmNodeKind), depth[i], nameCode[i], parent[i], next[i] (next sibling or -1),
    //   firstChild derived as i+1 when depth[i+1] == depth[i]+1,
    //   textStart[i]/textLength[i] into one shared char buffer,
    //   alpha[i]/beta[i]: first attribute index and first namespace index (or -1).
    // Attributes live in their own arrays: attrParent[], attrNameCode[], attrValue[].

    public XdmNodeKind GetKind(int index);
    public int GetNameCode(int index);
    public int GetParent(int index);                     // -1 for the root
    public string GetStringValue(int index);
    public IXdmIterator Iterate(int index, Axis axis, NodeTest? test);
}

internal sealed class TinyTreeBuilder : IResultReceiver
{
    public TinyTreeBuilder(NamePool namePool, string? baseUri);
    public TinyTree Build();                             // after EndDocument
    public static TinyTree Load(XmlReader reader, NamePool namePool);
}

/// <summary>Node test for axis steps: kind test plus optional name test (by code or wildcard).</summary>
internal abstract class NodeTest
{
    public abstract bool Matches(XdmNodeKind kind, int nameCode, NamePool pool);
    public abstract double DefaultPriority { get; }      // XSLT pattern default priority
}
```

## Sequence types

```csharp
public enum Occurrence : byte { ExactlyOne, ZeroOrOne, ZeroOrMore, OneOrMore, Empty }

/// <summary>An item type: item(), a kind test, or an atomic type.</summary>
public abstract class ItemType
{
    public static ItemType AnyItem { get; }
    public static ItemType AnyNode { get; }
    public static ItemType Atomic(XdmTypeCode type);
    public static ItemType Kind(XdmNodeKind kind, QName? name = null);
    public abstract bool Matches(XdmItem item);
    public abstract bool IsSubtypeOf(ItemType other);
}

public sealed class SequenceType
{
    public SequenceType(ItemType itemType, Occurrence occurrence);
    public ItemType ItemType { get; }
    public Occurrence Occurrence { get; }
    public static SequenceType EmptySequence { get; }
    public static SequenceType AnySequence { get; }      // item()*
    public bool Matches(XdmSequence value);
    public bool IsSubtypeOf(SequenceType other);
    public override string ToString();                   // "xs:integer+"
}
```

## XPath front end

Project: `XTransform.XPath`, namespace `XTransform.XPath` (all `internal`).

```csharp
internal enum TokenKind
{
    EndOfInput,
    // literals and names
    StringLiteral, IntegerLiteral, DecimalLiteral, DoubleLiteral,
    Name,                 // NCName or QName; keywords are Names the parser interprets in context
    PrefixWildcard,       // prefix:*
    LocalWildcard,        // *:local
    Star,                 // * (wildcard or multiply; the lexer decides from context)
    VariableMarker,       // $
    // punctuation
    LeftParen, RightParen, LeftBracket, RightBracket, Comma, Dot, DotDot,
    Slash, DoubleSlash, At, DoubleColon, Colon, QuestionMark,
    // operators
    Plus, Minus, Pipe, Equals, NotEquals, LessThan, LessThanOrEqual,
    GreaterThan, GreaterThanOrEqual, Precedes /* << */, Follows /* >> */,
    // operator keywords recognised only in operator position
    And, Or, Div, IDiv, Mod, Union, Intersect, Except, To,
    Eq, Ne, Lt, Le, Gt, Ge, Is, InstanceOf, TreatAs, CastableAs, CastAs,
    Return, Satisfies, Then, Else, In,
}

internal readonly struct Token
{
    public Token(TokenKind kind, int start, int length, string? text = null);
    public TokenKind Kind { get; }
    public int Start { get; }
    public int Length { get; }
    public string? Text { get; }      // name or decoded literal value; null for punctuation
}

/// <summary>
/// Context-sensitive XPath 2.0 lexer. Tracks whether the next token is in operator or
/// operand position (spec A.2.2) so "div", "*", "-" and keywords lex correctly.
/// Comments "(: … :)" nest and are skipped.
/// </summary>
internal sealed class XPathLexer
{
    public XPathLexer(string expression, SourceSpan baseSpan);
    public Token Current { get; }
    public Token Peek(int lookahead = 1);
    public void Advance();
    /// <summary>Throws XPathStaticException XPST0003 at the current position.</summary>
    public Exception SyntaxError(string message);
}

internal static class XPathParser
{
    /// <summary>Parses an Expr; throws XPathStaticException (XPST0003) on the first syntax error.</summary>
    public static XPathAst ParseExpression(string expression, SourceSpan span, IStaticNamespaceResolver namespaces);

    /// <summary>Parses an XSLT pattern (restricted grammar).</summary>
    public static PatternAst ParsePattern(string pattern, SourceSpan span, IStaticNamespaceResolver namespaces);
}

internal interface IStaticNamespaceResolver
{
    /// <summary>Returns the URI for a prefix, or null if undeclared (caller raises XPST0081).</summary>
    string? LookupNamespace(string prefix);
    string DefaultElementNamespace { get; }
    string DefaultFunctionNamespace { get; }
}

/// <summary>
/// Immutable syntax tree. Abstract records; one subclass per grammar construct, e.g.
/// ForExpr, QuantifiedExpr, IfExpr, BinaryExpr(op), ComparisonExpr(kind, op), RangeExpr,
/// UnaryExpr, PathExpr, AxisStep(axis, NodeTestAst, predicates), FilterExpr,
/// VariableRef(QName), FunctionCall(QName, args), Literal(XdmAtomicValue), ContextItemExpr,
/// InstanceOfExpr, TreatExpr, CastableExpr, CastExpr, SequenceExpr.
/// </summary>
internal abstract record XPathAst(SourceSpan Span);
internal abstract record PatternAst(SourceSpan Span);
```

## Evaluation

Project: `XTransform.XPath` (expressions) and `XTransform.Xslt` (instructions).

```csharp
[Flags]
internal enum ExpressionDependencies : short
{
    None = 0,
    ContextItem = 1, Position = 2, Last = 4,
    LocalVariables = 8, CurrentGroup = 16, RegexGroup = 32, CurrentTemplateRule = 64,
    Focus = ContextItem | Position | Last,
}

/// <summary>A compiled, immutable expression node. Thread-safe: holds no run state.</summary>
internal abstract class ExpressionNode
{
    public SourceSpan Location { get; }
    public SequenceType StaticType { get; }
    public ExpressionDependencies Dependencies { get; }

    /// <summary>Pull evaluation. Must be lazy wherever the result can be consumed partially.</summary>
    public abstract IXdmIterator Iterate(DynamicContext context);

    /// <summary>Single-item evaluation; default calls Iterate. Throws XPTY0004 on >1 item.</summary>
    public virtual XdmItem? EvaluateItem(DynamicContext context);

    /// <summary>fn:boolean semantics; default calls Iterate and stops early.</summary>
    public virtual bool EffectiveBooleanValue(DynamicContext context);

    /// <summary>Push evaluation into a receiver (used by XSLT instructions); default iterates.</summary>
    public virtual void Process(DynamicContext context, IResultReceiver output);
}

/// <summary>
/// All mutable state of one evaluation. Created per transformation; child contexts
/// are cheap copies that share the run state and replace the focus or frame.
/// </summary>
internal sealed class DynamicContext
{
    public XdmItem? ContextItem { get; }
    public int ContextPosition { get; }
    public int ContextSize { get; }                      // computed lazily on first read
    public XdmSequence[] LocalFrame { get; }             // indexed by compiler slot
    public RunState Run { get; }                         // globals cache, documents, keys, receivers, cancellation

    public DynamicContext WithFocus(IXdmIterator focus); // focus moves with focus.Next()
    public DynamicContext WithFrame(int slotCount);
    public void CheckCancellation();                     // called at loop back-edges and template entry
}
```

## Output

Project: `XTransform.Xdm` (the interface) and `XTransform.Serialization` (emitters).

```csharp
/// <summary>
/// Push-based event sink for constructed content. Implemented by the sequence normalizer,
/// the serializer emitters, TinyTreeBuilder and test recorders. Calls arrive in document
/// order; attributes and namespaces come after StartElement and before any child content.
/// </summary>
internal interface IResultReceiver
{
    void StartDocument();
    void EndDocument();
    void StartElement(int nameCode, SourceSpan location);
    void Namespace(string prefix, string uri);
    void Attribute(int nameCode, string value, SourceSpan location);
    void EndElement();
    void Text(string text);
    void Comment(string text);
    void ProcessingInstruction(string target, string data);
    /// <summary>An item appended to the result sequence (atomic values, or existing nodes to copy).</summary>
    void Append(XdmItem item);
}
```

## Public API

Project: `XTransform`, namespace `XTransform`. Usage example in [design.md](design.md#public-api-surface).

```csharp
public sealed class XsltCompilerOptions
{
    public IResourceResolver? ResourceResolver { get; init; }       // default: file system, no network
    public IReadOnlyDictionary<string, IExtensionFunction>? ExtensionFunctions { get; init; }
    public bool TreatWarningsAsErrors { get; init; }
    public Uri? BaseUri { get; init; }
}

public sealed class XsltCompiler
{
    public XsltCompiler(XsltCompilerOptions? options = null);
    public XsltExecutable Compile(Uri stylesheetUri);
    public XsltExecutable Compile(XmlReader stylesheet, Uri? baseUri = null);
    public XsltExecutable Compile(TextReader stylesheet, Uri? baseUri = null);
    public IReadOnlyList<Diagnostic> Warnings { get; }              // from the last Compile
}

/// <summary>Immutable and thread-safe; cache and share it.</summary>
public sealed class XsltExecutable
{
    public XsltTransformer CreateTransformer();
    public IReadOnlyCollection<QName> GlobalParameters { get; }
    public XsltOutputSettings OutputSettings { get; }               // from the unnamed xsl:output
}

/// <summary>Serialization parameters (design.md, Stage 4); one property per xsl:output attribute.</summary>
public sealed class XsltOutputSettings { /* Method, Encoding, Indent, OmitXmlDeclaration, … — defined in phase 3 */ }

/// <summary>One per run (not thread-safe). Cheap to create.</summary>
public sealed class XsltTransformer
{
    public void SetParameter(QName name, XdmSequence value);
    public void SetParameter(QName name, XdmItem value);
    public QName? InitialMode { get; set; }
    public QName? InitialTemplate { get; set; }
    public Uri? BaseOutputUri { get; set; }
    public Action<XsltMessage>? MessageListener { get; set; }
    public IResultDocumentHandler? ResultDocumentHandler { get; set; }
    public int MaxRecursionDepth { get; set; }                      // default 10_000

    public void Transform(Stream input, Stream output);
    public void Transform(XmlReader input, TextWriter output);
    public void Transform(XmlReader input, XmlWriter output);
    public XdmSequence TransformToSequence(XmlReader? input);
    public Task TransformAsync(Stream input, Stream output, CancellationToken cancellationToken = default);
    // input == null (or the Transform overloads without input) starts from InitialTemplate
}

public interface IResourceResolver
{
    /// <summary>Returns the resource, or null if not found (caller raises the spec error).</summary>
    Stream? Open(Uri uri);
}

public interface IExtensionFunction
{
    QName Name { get; }
    IReadOnlyList<SequenceType> ArgumentTypes { get; }
    SequenceType ResultType { get; }
    XdmSequence Invoke(IReadOnlyList<XdmSequence> arguments);
}

public interface IResultDocumentHandler
{
    /// <summary>Destination for xsl:result-document href (already resolved against BaseOutputUri).</summary>
    Stream Open(Uri href);
    void Close(Uri href, Stream stream);
}

public sealed class XsltMessage
{
    public string Text { get; }
    public XdmSequence Content { get; }
    public bool Terminate { get; }
    public SourceSpan Location { get; }
}
```
