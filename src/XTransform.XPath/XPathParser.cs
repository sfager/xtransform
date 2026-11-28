using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using XTransform.Xdm;
using XTransform.XPath.Ast;

// XPath 2.0 §A.1 — full grammar, hand-written recursive-descent parser
namespace XTransform.XPath;

/// <summary>
/// Parses an XPath 2.0 expression string into an <see cref="XPathAst"/> tree.
/// </summary>
internal static class XPathParser
{
    // ── Reserved function names per XPath 2.0 §A.3 ───────────────────────────
    // These cannot appear as FunctionCall names in primary position.
    private static readonly HashSet<string> s_reservedFunctionNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "attribute", "comment", "document-node", "element", "empty-sequence",
        "if", "item", "node", "processing-instruction", "schema-attribute",
        "schema-element", "text", "typeswitch",
    };

    // ── KindTest keywords ─────────────────────────────────────────────────────
    private static readonly HashSet<string> s_kindTestNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "element", "attribute", "text", "comment", "processing-instruction",
        "document-node", "node", "schema-element", "schema-attribute",
    };

    // ── Axis names ────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, Axis> s_axisNames = new Dictionary<string, Axis>(StringComparer.Ordinal)
    {
        ["child"] = Axis.Child,
        ["descendant"] = Axis.Descendant,
        ["descendant-or-self"] = Axis.DescendantOrSelf,
        ["self"] = Axis.Self,
        ["attribute"] = Axis.Attribute,
        ["parent"] = Axis.Parent,
        ["ancestor"] = Axis.Ancestor,
        ["ancestor-or-self"] = Axis.AncestorOrSelf,
        ["following-sibling"] = Axis.FollowingSibling,
        ["preceding-sibling"] = Axis.PrecedingSibling,
        ["following"] = Axis.Following,
        ["preceding"] = Axis.Preceding,
        ["namespace"] = Axis.Namespace,
    };

    /// <summary>
    /// Parses <paramref name="expression"/> as an XPath 2.0 Expr.
    /// </summary>
    /// <param name="expression">The XPath expression text.</param>
    /// <param name="span">Source location to embed in thrown exceptions.</param>
    /// <param name="namespaces">Namespace resolver for prefix lookups.</param>
    /// <returns>The parsed <see cref="XPathAst"/>.</returns>
    /// <exception cref="XPathStaticException">
    ///   Thrown with <see cref="XPathErrors.XPST0003"/> on syntax errors, or
    ///   <see cref="XPathErrors.XPST0081"/> for undeclared prefixes.
    /// </exception>
    public static XPathAst ParseExpression(string expression, SourceSpan span, IStaticNamespaceResolver namespaces)
    {
#pragma warning disable CA1510
        if (expression is null) { throw new ArgumentNullException(nameof(expression)); }
        if (namespaces is null) { throw new ArgumentNullException(nameof(namespaces)); }
#pragma warning restore CA1510

        var parser = new Parser(expression, span, namespaces);
        return parser.ParseExpr();
    }

    /// <summary>Parses <paramref name="text"/> as a SequenceType (XPath 2.0 §2.5.4), for the as attribute of XSLT elements.</summary>
    /// <param name="text">The sequence type text.</param>
    /// <param name="span">Source location to embed in thrown exceptions.</param>
    /// <param name="namespaces">Namespace resolver for prefix lookups.</param>
    /// <returns>The parsed sequence type.</returns>
    public static SequenceType ParseSequenceType(string text, SourceSpan span, IStaticNamespaceResolver namespaces)
    {
#pragma warning disable CA1510
        if (text is null) { throw new ArgumentNullException(nameof(text)); }
        if (namespaces is null) { throw new ArgumentNullException(nameof(namespaces)); }
#pragma warning restore CA1510

        var parser = new Parser(text, span, namespaces);
        return parser.ParseSequenceTypeToEnd();
    }

    /// <summary>
    /// Parses <paramref name="pattern"/> as an XSLT 2.0 Pattern (§5.5.2).
    /// </summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="span">Source location to embed in thrown exceptions.</param>
    /// <param name="namespaces">Namespace resolver for prefix lookups.</param>
    /// <returns>The parsed <see cref="PatternAst"/>.</returns>
    /// <exception cref="XsltStaticException">
    ///   Thrown with <see cref="XsltErrors.XTSE0340"/> for constructs that are
    ///   syntactically valid XPath but invalid as patterns (wrong axis, arithmetic, etc.).
    /// </exception>
    public static PatternAst ParsePattern(string pattern, SourceSpan span, IStaticNamespaceResolver namespaces)
    {
#pragma warning disable CA1510
        if (pattern is null) { throw new ArgumentNullException(nameof(pattern)); }
        if (namespaces is null) { throw new ArgumentNullException(nameof(namespaces)); }
#pragma warning restore CA1510

        var patternParser = new PatternParser(pattern, span, namespaces);
        return patternParser.ParseUnionPattern();
    }

    // ── Inner parser class ────────────────────────────────────────────────────

    private sealed class Parser
    {
        private readonly XPathLexer _lexer;
        private readonly SourceSpan _baseSpan;
        private readonly IStaticNamespaceResolver _ns;

        internal Parser(string expression, SourceSpan baseSpan, IStaticNamespaceResolver ns)
        {
            _lexer = new XPathLexer(expression, baseSpan);
            _baseSpan = baseSpan;
            _ns = ns;
            // Prime the lexer — Current is EndOfInput until first Advance.
            _lexer.Advance();
        }

        /// <summary>
        /// Initializes a parser sharing an already-primed <paramref name="lexer"/>.
        /// Used by <see cref="PatternParser"/> so that predicate expressions are parsed
        /// using the same lexer position.
        /// </summary>
        internal Parser(XPathLexer lexer, SourceSpan baseSpan, IStaticNamespaceResolver ns)
        {
            _lexer = lexer;
            _baseSpan = baseSpan;
            _ns = ns;
            // Lexer is already primed; do not call Advance() again.
        }

        // ── Token helpers ─────────────────────────────────────────────────────

        private Token Current => _lexer.Current;
        private TokenKind Kind => _lexer.Current.Kind;

        private Token Peek(int ahead = 1) => _lexer.Peek(ahead);

        private Token Consume()
        {
            Token t = Current;
            _lexer.Advance();
            return t;
        }

        private Token Expect(TokenKind kind)
        {
            if (Kind != kind)
            {
                throw _lexer.SyntaxError($"Expected {kind} but found {Kind} ('{Current.Text ?? Kind.ToString()}')");
            }
            return Consume();
        }

        private bool TryConsume(TokenKind kind)
        {
            if (Kind != kind) { return false; }
            Consume();
            return true;
        }

        /// <summary>
        /// Returns a SourceSpan covering from <paramref name="startOffset"/> to the end of
        /// the current (just consumed) token.
        /// </summary>
        private SourceSpan SpanFrom(int startOffset)
        {
            int end = Current.Start; // current token starts where previous ended
            int len = end - startOffset;
            if (len < 0) { len = 0; }
            return _baseSpan.WithRange(startOffset, len);
        }

        /// <summary>Returns a SourceSpan for a single token.</summary>
        private SourceSpan TokenSpan(Token t) => _baseSpan.WithRange(t.Start, t.Length);

        // ── QName resolution ──────────────────────────────────────────────────

        /// <summary>
        /// Resolves a raw name token text into a <see cref="QName"/>, applying the given
        /// default namespace when there is no prefix.
        /// </summary>
        private QName ResolveQName(string rawName, string defaultNamespace, Token atToken)
        {
            int colon = rawName.IndexOf(':');
            if (colon < 0)
            {
                // No prefix: use the supplied default namespace
                return new QName("", defaultNamespace, rawName);
            }

            string prefix = rawName.Substring(0, colon);
            string local = rawName.Substring(colon + 1);
            string? uri = _ns.LookupNamespace(prefix);
            if (uri == null)
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0081,
                    $"Undeclared namespace prefix '{prefix}'",
                    _baseSpan.WithRange(atToken.Start, atToken.Length));
            }
            return new QName(prefix, uri, local);
        }

        /// <summary>Resolves a name as an atomic type (xs: prefix → XSD namespace).</summary>
        private SequenceType ResolveAtomicType(string rawName, Token atToken, bool allowQuestionMark)
        {
            QName typeName = ResolveQName(rawName, "http://www.w3.org/2001/XMLSchema", atToken);

            // Must be in XSD namespace
            if (!string.Equals(typeName.NamespaceUri, "http://www.w3.org/2001/XMLSchema", StringComparison.Ordinal))
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0051,
                    $"Unknown atomic type '{rawName}' — only xs: types are supported",
                    _baseSpan.WithRange(atToken.Start, atToken.Length));
            }

            if (!CastTable.TryGetTypeCode(typeName.LocalName, out XdmTypeCode typeCode))
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0051,
                    $"Unknown xs: atomic type '{typeName.LocalName}'",
                    _baseSpan.WithRange(atToken.Start, atToken.Length));
            }

            Occurrence occ = Occurrence.ExactlyOne;
            if (allowQuestionMark && Kind == TokenKind.QuestionMark)
            {
                Consume();
                occ = Occurrence.ZeroOrOne;
            }

            return new SequenceType(ItemType.Atomic(typeCode), occ);
        }

        // ── Production: Expr (§A.1 prod 1) ───────────────────────────────────

        // XPath 2.0 §A.1 production 1: Expr ::= ExprSingle ("," ExprSingle)*
        internal SequenceType ParseSequenceTypeToEnd()
        {
            SequenceType type = ParseSequenceType();
            if (Kind != TokenKind.EndOfInput)
            {
                throw _lexer.SyntaxError($"Unexpected token '{Current.Text ?? Kind.ToString()}' after sequence type");
            }

            return type;
        }

        internal XPathAst ParseExpr()
        {
            int start = Current.Start;
            XPathAst first = ParseExprSingle();

            if (Kind != TokenKind.Comma)
            {
                // Ensure nothing is left over
                if (Kind != TokenKind.EndOfInput)
                {
                    throw _lexer.SyntaxError($"Unexpected token '{Current.Text ?? Kind.ToString()}' after expression");
                }
                return first;
            }

            // Multiple items — build SequenceExpr
            var items = ImmutableArray.CreateBuilder<XPathAst>();
            items.Add(first);
            while (TryConsume(TokenKind.Comma))
            {
                items.Add(ParseExprSingle());
            }

            if (Kind != TokenKind.EndOfInput)
            {
                throw _lexer.SyntaxError($"Unexpected token '{Current.Text ?? Kind.ToString()}' after expression");
            }

            return new SequenceExpr(_baseSpan.WithRange(start, Current.Start - start), items.ToImmutable());
        }

        /// <summary>Parses an ExprSingle but does not enforce end-of-input.</summary>
        private XPathAst ParseExprSingleInternal()
        {
            return ParseExprSingle();
        }

        // XPath 2.0 §A.1 production 2: ExprSingle ::= ForExpr | QuantifiedExpr | IfExpr | OrExpr
        private XPathAst ParseExprSingle()
        {
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;
                if (string.Equals(name, "for", StringComparison.Ordinal)) { return ParseForExpr(); }
                if (string.Equals(name, "some", StringComparison.Ordinal)) { return ParseQuantifiedExpr(QuantifierKind.Some); }
                if (string.Equals(name, "every", StringComparison.Ordinal)) { return ParseQuantifiedExpr(QuantifierKind.Every); }
                if (string.Equals(name, "if", StringComparison.Ordinal)) { return ParseIfExpr(); }
            }
            return ParseOrExpr();
        }

        // XPath 2.0 §A.1: ForExpr ::= "for" "$" VarName "in" ExprSingle ("," "$" VarName "in" ExprSingle)* "return" ExprSingle
        private ForExpr ParseForExpr()
        {
            int start = Current.Start;
            Consume(); // consume "for"

            var clauses = ImmutableArray.CreateBuilder<ForClause>();
            clauses.Add(ParseForClause());

            while (Kind == TokenKind.Comma)
            {
                Consume(); // ","
                clauses.Add(ParseForClause());
            }

            Expect(TokenKind.Return); // "return" keyword
            XPathAst returnExpr = ParseExprSingleInternal();

            return new ForExpr(
                _baseSpan.WithRange(start, Current.Start - start),
                clauses.ToImmutable(),
                returnExpr);
        }

        private ForClause ParseForClause()
        {
            int start = Current.Start;
            Expect(TokenKind.VariableMarker); // "$"
            Token nameToken = Expect(TokenKind.Name);
            QName varName = ResolveQName(nameToken.Text!, "", nameToken);
            Expect(TokenKind.In); // "in" keyword
            XPathAst seqExpr = ParseExprSingleInternal();
            return new ForClause(varName, seqExpr, _baseSpan.WithRange(start, Current.Start - start));
        }

        // XPath 2.0 §A.1: QuantifiedExpr ::= ("some"|"every") "$" VarName "in" ExprSingle ("," "$" VarName "in" ExprSingle)* "satisfies" ExprSingle
        private QuantifiedExpr ParseQuantifiedExpr(QuantifierKind kind)
        {
            int start = Current.Start;
            Consume(); // consume "some" or "every"

            var clauses = ImmutableArray.CreateBuilder<ForClause>();
            clauses.Add(ParseForClause());

            while (Kind == TokenKind.Comma)
            {
                Consume(); // ","
                clauses.Add(ParseForClause());
            }

            Expect(TokenKind.Satisfies); // "satisfies" keyword
            XPathAst satisfies = ParseExprSingleInternal();

            return new QuantifiedExpr(
                _baseSpan.WithRange(start, Current.Start - start),
                kind,
                clauses.ToImmutable(),
                satisfies);
        }

        // XPath 2.0 §A.1: IfExpr ::= "if" "(" Expr ")" "then" ExprSingle "else" ExprSingle
        private IfExpr ParseIfExpr()
        {
            int start = Current.Start;
            Consume(); // consume "if"
            Expect(TokenKind.LeftParen);
            XPathAst test = ParseExprForParen();
            Expect(TokenKind.RightParen);
            Expect(TokenKind.Then);
            XPathAst then = ParseExprSingleInternal();
            Expect(TokenKind.Else);
            XPathAst elseExpr = ParseExprSingleInternal();

            return new IfExpr(
                _baseSpan.WithRange(start, Current.Start - start),
                test, then, elseExpr);
        }

        // XPath 2.0 §A.1 production 3: OrExpr ::= AndExpr ("or" AndExpr)*
        private XPathAst ParseOrExpr()
        {
            XPathAst left = ParseAndExpr();
            while (Kind == TokenKind.Or)
            {
                int start = left.Span.Start;
                Consume();
                XPathAst right = ParseAndExpr();
                left = new OrExpr(_baseSpan.WithRange(start, Current.Start - start), left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 4: AndExpr ::= ComparisonExpr ("and" ComparisonExpr)*
        private XPathAst ParseAndExpr()
        {
            XPathAst left = ParseComparisonExpr();
            while (Kind == TokenKind.And)
            {
                int start = left.Span.Start;
                Consume();
                XPathAst right = ParseComparisonExpr();
                left = new AndExpr(_baseSpan.WithRange(start, Current.Start - start), left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 5: ComparisonExpr ::= RangeExpr (valueComp|generalComp|nodeComp RangeExpr)?
        // Non-associative: only one comparison allowed.
        private XPathAst ParseComparisonExpr()
        {
            XPathAst left = ParseRangeExpr();

            ComparisonOp? op = TryGetComparisonOp();
            if (op.HasValue)
            {
                int start = left.Span.Start;
                Consume(); // consume operator
                XPathAst right = ParseRangeExpr();
                left = new ComparisonExpr(_baseSpan.WithRange(start, Current.Start - start), op.Value, left, right);
            }

            return left;
        }

        private ComparisonOp? TryGetComparisonOp()
        {
            return Kind switch
            {
                TokenKind.Equals => ComparisonOp.Equals,
                TokenKind.NotEquals => ComparisonOp.NotEquals,
                TokenKind.LessThan => ComparisonOp.LessThan,
                TokenKind.LessThanOrEqual => ComparisonOp.LessThanOrEqual,
                TokenKind.GreaterThan => ComparisonOp.GreaterThan,
                TokenKind.GreaterThanOrEqual => ComparisonOp.GreaterThanOrEqual,
                TokenKind.Eq => ComparisonOp.Eq,
                TokenKind.Ne => ComparisonOp.Ne,
                TokenKind.Lt => ComparisonOp.Lt,
                TokenKind.Le => ComparisonOp.Le,
                TokenKind.Gt => ComparisonOp.Gt,
                TokenKind.Ge => ComparisonOp.Ge,
                TokenKind.Is => ComparisonOp.Is,
                TokenKind.Precedes => ComparisonOp.Precedes,
                TokenKind.Follows => ComparisonOp.Follows,
                _ => (ComparisonOp?)null,
            };
        }

        // XPath 2.0 §A.1 production 6: RangeExpr ::= AdditiveExpr ("to" AdditiveExpr)?
        private XPathAst ParseRangeExpr()
        {
            XPathAst left = ParseAdditiveExpr();
            if (Kind == TokenKind.To)
            {
                int start = left.Span.Start;
                Consume();
                XPathAst right = ParseAdditiveExpr();
                left = new RangeExpr(_baseSpan.WithRange(start, Current.Start - start), left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 7: AdditiveExpr ::= MultiplicativeExpr (("+"|"-") MultiplicativeExpr)*
        private XPathAst ParseAdditiveExpr()
        {
            XPathAst left = ParseMultiplicativeExpr();
            while (Kind == TokenKind.Plus || Kind == TokenKind.Minus)
            {
                int start = left.Span.Start;
                ArithmeticOp op = Kind == TokenKind.Plus ? ArithmeticOp.Add : ArithmeticOp.Subtract;
                Consume();
                XPathAst right = ParseMultiplicativeExpr();
                left = new ArithmeticExpr(_baseSpan.WithRange(start, Current.Start - start), op, left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 8: MultiplicativeExpr ::= UnionExpr (("*"|"div"|"idiv"|"mod") UnionExpr)*
        private XPathAst ParseMultiplicativeExpr()
        {
            XPathAst left = ParseUnionExpr();
            while (true)
            {
                ArithmeticOp? op = Kind switch
                {
                    TokenKind.Star => ArithmeticOp.Multiply,
                    TokenKind.Div => ArithmeticOp.Divide,
                    TokenKind.IDiv => ArithmeticOp.IntegerDivide,
                    TokenKind.Mod => ArithmeticOp.Modulo,
                    _ => (ArithmeticOp?)null,
                };
                if (!op.HasValue) { break; }
                int start = left.Span.Start;
                Consume();
                XPathAst right = ParseUnionExpr();
                left = new ArithmeticExpr(_baseSpan.WithRange(start, Current.Start - start), op.Value, left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 9: UnionExpr ::= IntersectExceptExpr (("union"|"|") IntersectExceptExpr)*
        private XPathAst ParseUnionExpr()
        {
            XPathAst left = ParseIntersectExceptExpr();
            while (Kind == TokenKind.Union || Kind == TokenKind.Pipe)
            {
                int start = left.Span.Start;
                Consume();
                XPathAst right = ParseIntersectExceptExpr();
                left = new SetExpr(_baseSpan.WithRange(start, Current.Start - start), SetOp.Union, left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 10: IntersectExceptExpr ::= InstanceofExpr (("intersect"|"except") InstanceofExpr)*
        private XPathAst ParseIntersectExceptExpr()
        {
            XPathAst left = ParseInstanceofExpr();
            while (Kind == TokenKind.Intersect || Kind == TokenKind.Except)
            {
                int start = left.Span.Start;
                SetOp op = Kind == TokenKind.Intersect ? SetOp.Intersect : SetOp.Except;
                Consume();
                XPathAst right = ParseInstanceofExpr();
                left = new SetExpr(_baseSpan.WithRange(start, Current.Start - start), op, left, right);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 11: InstanceofExpr ::= TreatExpr ("instance" "of" SequenceType)?
        private XPathAst ParseInstanceofExpr()
        {
            XPathAst left = ParseTreatExpr();
            if (Kind == TokenKind.InstanceOf)
            {
                int start = left.Span.Start;
                Consume(); // "instance of" (combined token)
                SequenceType st = ParseSequenceType();
                left = new InstanceOfExpr(_baseSpan.WithRange(start, Current.Start - start), left, st);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 12: TreatExpr ::= CastableExpr ("treat" "as" SequenceType)?
        private XPathAst ParseTreatExpr()
        {
            XPathAst left = ParseCastableExpr();
            if (Kind == TokenKind.TreatAs)
            {
                int start = left.Span.Start;
                Consume(); // "treat as" (combined token)
                SequenceType st = ParseSequenceType();
                left = new TreatExpr(_baseSpan.WithRange(start, Current.Start - start), left, st);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 13: CastableExpr ::= CastExpr ("castable" "as" SingleType)?
        private XPathAst ParseCastableExpr()
        {
            XPathAst left = ParseCastExpr();
            if (Kind == TokenKind.CastableAs)
            {
                int start = left.Span.Start;
                Consume(); // "castable as" (combined token)
                SequenceType st = ParseSingleType();
                left = new CastableExpr(_baseSpan.WithRange(start, Current.Start - start), left, st);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 14: CastExpr ::= UnaryExpr ("cast" "as" SingleType)?
        private XPathAst ParseCastExpr()
        {
            XPathAst left = ParseUnaryExpr();
            if (Kind == TokenKind.CastAs)
            {
                int start = left.Span.Start;
                Consume(); // "cast as" (combined token)
                SequenceType st = ParseSingleType();
                left = new CastExpr(_baseSpan.WithRange(start, Current.Start - start), left, st);
            }
            return left;
        }

        // XPath 2.0 §A.1 production 15: UnaryExpr ::= ("-"|"+")* ValueExpr
        private XPathAst ParseUnaryExpr()
        {
            if (Kind == TokenKind.Minus || Kind == TokenKind.Plus)
            {
                int start = Current.Start;
                UnaryOp op = Kind == TokenKind.Minus ? UnaryOp.Negate : UnaryOp.Plus;
                Consume();
                XPathAst operand = ParseUnaryExpr(); // recursive for chaining: --x
                return new UnaryExpr(_baseSpan.WithRange(start, Current.Start - start), op, operand);
            }
            return ParseValueExpr();
        }

        // XPath 2.0 §A.1 production 16: ValueExpr ::= PathExpr
        private XPathAst ParseValueExpr()
        {
            return ParsePathExpr();
        }

        // XPath 2.0 §A.1 production 17: PathExpr
        //   "/" | "/" RelativePathExpr | "//" RelativePathExpr | RelativePathExpr
        private XPathAst ParsePathExpr()
        {
            if (Kind == TokenKind.Slash)
            {
                int start = Current.Start;
                Consume(); // "/"

                // xgs: leading-lone-slash — if next token can begin a RelativePathExpr, include it
                if (CanStartStepExpr())
                {
                    var steps = ImmutableArray.CreateBuilder<XPathAst>();
                    steps.Add(ParseStepExpr()); // first step
                    ParseRelativeSteps(steps);  // subsequent (/ step)*
                    return new PathExpr(_baseSpan.WithRange(start, Current.Start - start), PathAnchor.AbsoluteSlash, steps.ToImmutable());
                }

                // Bare "/" — document root
                return new PathExpr(_baseSpan.WithRange(start, 1), PathAnchor.AbsoluteSlash, ImmutableArray<XPathAst>.Empty);
            }

            if (Kind == TokenKind.DoubleSlash)
            {
                // "//" expands to "/descendant-or-self::node()/" prefix + RelativePathExpr
                int start = Current.Start;
                Consume(); // "//"
                var steps = ImmutableArray.CreateBuilder<XPathAst>();
                // Insert the implicit descendant-or-self::node() step
                var anyNode = new AnyKindTestAst(_baseSpan.WithRange(start, 2));
                steps.Add(new AxisStep(_baseSpan.WithRange(start, 2), Axis.DescendantOrSelf, anyNode, ImmutableArray<XPathAst>.Empty));
                steps.Add(ParseStepExpr()); // first step of relative path
                ParseRelativeSteps(steps);  // subsequent (/ step)*
                return new PathExpr(_baseSpan.WithRange(start, Current.Start - start), PathAnchor.AbsoluteDoubleSlash, steps.ToImmutable());
            }

            return ParseRelativePathExpr();
        }

        // XPath 2.0 §A.1 production 18: RelativePathExpr ::= StepExpr (("/" | "//") StepExpr)*
        private XPathAst ParseRelativePathExpr()
        {
            int start = Current.Start;
            XPathAst firstStep = ParseStepExpr();

            if (Kind != TokenKind.Slash && Kind != TokenKind.DoubleSlash)
            {
                return firstStep;
            }

            var steps = ImmutableArray.CreateBuilder<XPathAst>();
            steps.Add(firstStep);
            ParseRelativeSteps(steps);

            return new PathExpr(
                _baseSpan.WithRange(start, Current.Start - start),
                PathAnchor.Relative,
                steps.ToImmutable());
        }

        /// <summary>Parses (("/" | "//") StepExpr)* into <paramref name="steps"/>.</summary>
        private void ParseRelativeSteps(ImmutableArray<XPathAst>.Builder steps)
        {
            while (Kind == TokenKind.Slash || Kind == TokenKind.DoubleSlash)
            {
                bool isDoubleSlash = Kind == TokenKind.DoubleSlash;
                int separatorStart = Current.Start;
                Consume();

                if (isDoubleSlash)
                {
                    // "//" expands to /descendant-or-self::node()/ implicitly
                    // Insert a synthetic AxisStep for descendant-or-self::node()
                    var anyNode = new AnyKindTestAst(_baseSpan.WithRange(separatorStart, 2));
                    steps.Add(new AxisStep(
                        _baseSpan.WithRange(separatorStart, 2),
                        Axis.DescendantOrSelf,
                        anyNode,
                        ImmutableArray<XPathAst>.Empty));
                }

                steps.Add(ParseStepExpr());
            }
        }

        /// <summary>Returns true when the current token can start a StepExpr.</summary>
        private bool CanStartStepExpr()
        {
            switch (Kind)
            {
                case TokenKind.Name:
                case TokenKind.At:
                case TokenKind.DotDot:
                case TokenKind.Dot:
                case TokenKind.Star:
                case TokenKind.PrefixWildcard:
                case TokenKind.LocalWildcard:
                case TokenKind.LeftParen:
                case TokenKind.VariableMarker:
                case TokenKind.StringLiteral:
                case TokenKind.IntegerLiteral:
                case TokenKind.DecimalLiteral:
                case TokenKind.DoubleLiteral:
                    return true;
                default:
                    return false;
            }
        }

        // XPath 2.0 §A.1 production 19: StepExpr ::= FilterExpr | AxisStep
        private XPathAst ParseStepExpr()
        {
            // ".." is always parent::node()
            if (Kind == TokenKind.DotDot)
            {
                int start = Current.Start;
                Consume();
                var anyNode = new AnyKindTestAst(_baseSpan.WithRange(start, 2));
                return new AxisStep(_baseSpan.WithRange(start, 2), Axis.Parent, anyNode, ImmutableArray<XPathAst>.Empty);
            }

            // "@" means attribute axis
            if (Kind == TokenKind.At)
            {
                return ParseAxisStepWithExplicitAxis(Axis.Attribute);
            }

            // Check for axis name followed by "::"
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;
                // Check for axis::  (look ahead for "::")
                if (Peek(1).Kind == TokenKind.DoubleColon && s_axisNames.TryGetValue(name, out Axis axis))
                {
                    return ParseAxisStepWithExplicitAxis(axis);
                }

                // Check for kindTest (e.g. element(...), node(), text(), etc.)
                if (s_kindTestNames.Contains(name) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    // This is an AxisStep with implicit child axis and kind test
                    int start = Current.Start;
                    NodeTestAst kindTest = ParseKindTest();
                    ImmutableArray<XPathAst> predicates = ParsePredicateList();
                    // XPath 2.0 §3.2.1.1: an abbreviated step is on the child axis, except that attribute() and
                    // schema-attribute() are on the attribute axis.
                    Axis implicitAxis = kindTest is KindTestAst { Kind: XdmNodeKind.Attribute } ? Axis.Attribute : Axis.Child;
                    return new AxisStep(_baseSpan.WithRange(start, Current.Start - start), implicitAxis, kindTest, predicates);
                }
            }

            // "." is ContextItemExpr in primary position (may be a FilterExpr or step)
            if (Kind == TokenKind.Dot)
            {
                // "." as a step — treat as ContextItemExpr that may have predicates
                return ParseFilterExprOrPrimary();
            }

            // Name without axis:: — could be child::Name or FilterExpr
            // Check if it's a primary expression (variable, literal, paren, function call)
            if (IsPrimaryStart())
            {
                return ParseFilterExprOrPrimary();
            }

            // Otherwise treat as implicit child:: with name test
            if (Kind == TokenKind.Name || Kind == TokenKind.Star || Kind == TokenKind.PrefixWildcard || Kind == TokenKind.LocalWildcard)
            {
                int start = Current.Start;
                NodeTestAst nameTest = ParseNameTest(Axis.Child);
                ImmutableArray<XPathAst> predicates = ParsePredicateList();
                return new AxisStep(_baseSpan.WithRange(start, Current.Start - start), Axis.Child, nameTest, predicates);
            }

            throw _lexer.SyntaxError($"Expected a step expression but found '{Current.Text ?? Kind.ToString()}'");
        }

        /// <summary>Parses an AxisStep where we already know the axis (after consuming "@" or "axis::").</summary>
        private AxisStep ParseAxisStepWithExplicitAxis(Axis axis)
        {
            int start = Current.Start;

            if (Kind == TokenKind.At)
            {
                Consume(); // "@"
            }
            else
            {
                // axis name was lookaheaded; consume axis + "::"
                Consume(); // axis name
                Expect(TokenKind.DoubleColon); // "::"
            }

            NodeTestAst test = ParseNodeTest(axis);
            ImmutableArray<XPathAst> predicates = ParsePredicateList();
            return new AxisStep(_baseSpan.WithRange(start, Current.Start - start), axis, test, predicates);
        }

        // XPath 2.0 §A.1 production 21: FilterExpr ::= PrimaryExpr PredicateList
        private XPathAst ParseFilterExprOrPrimary()
        {
            int start = Current.Start;
            XPathAst primary = ParsePrimaryExpr();
            ImmutableArray<XPathAst> predicates = ParsePredicateList();

            if (predicates.IsEmpty)
            {
                return primary;
            }

            return new FilterExpr(_baseSpan.WithRange(start, Current.Start - start), primary, predicates);
        }

        // XPath 2.0 §A.1: PredicateList ::= Predicate*
        // Predicate ::= "[" Expr "]"
        private ImmutableArray<XPathAst> ParsePredicateList()
        {
            if (Kind != TokenKind.LeftBracket)
            {
                return ImmutableArray<XPathAst>.Empty;
            }

            var predicates = ImmutableArray.CreateBuilder<XPathAst>();
            while (Kind == TokenKind.LeftBracket)
            {
                Consume(); // "["
                predicates.Add(ParseExprForParen());
                Expect(TokenKind.RightBracket);
            }
            return predicates.ToImmutable();
        }

        // XPath 2.0 §A.1 production 22: PrimaryExpr
        private XPathAst ParsePrimaryExpr()
        {
            switch (Kind)
            {
                case TokenKind.IntegerLiteral:
                    return ParseIntegerLiteral();

                case TokenKind.DecimalLiteral:
                    return ParseDecimalLiteral();

                case TokenKind.DoubleLiteral:
                    return ParseDoubleLiteral();

                case TokenKind.StringLiteral:
                    return ParseStringLiteral();

                case TokenKind.VariableMarker:
                    return ParseVariableRef();

                case TokenKind.LeftParen:
                    return ParseParenthesizedExpr();

                case TokenKind.Dot:
                {
                    int start = Current.Start;
                    Consume();
                    return new ContextItemExpr(_baseSpan.WithRange(start, 1));
                }

                case TokenKind.Name:
                {
                    // Could be function call or implicit axis step (handled before)
                    string name = Current.Text!;
                    if (Peek(1).Kind == TokenKind.LeftParen)
                    {
                        return ParseFunctionCall();
                    }
                    // Treat as a path step — this shouldn't be reached normally
                    // since ParseStepExpr handles names
                    goto default;
                }

                default:
                    throw _lexer.SyntaxError($"Expected a primary expression but found '{Current.Text ?? Kind.ToString()}'");
            }
        }

        private bool IsPrimaryStart()
        {
            switch (Kind)
            {
                case TokenKind.IntegerLiteral:
                case TokenKind.DecimalLiteral:
                case TokenKind.DoubleLiteral:
                case TokenKind.StringLiteral:
                case TokenKind.VariableMarker:
                case TokenKind.LeftParen:
                case TokenKind.Dot:
                    return true;
                case TokenKind.Name:
                    // Function call check: name followed by "("
                    return Peek(1).Kind == TokenKind.LeftParen;
                default:
                    return false;
            }
        }

        // ── Literals ──────────────────────────────────────────────────────────

        private Literal ParseIntegerLiteral()
        {
            Token t = Consume();
            SourceSpan span = TokenSpan(t);
            if (!long.TryParse(t.Text!, NumberStyles.None, CultureInfo.InvariantCulture, out long lv))
            {
                // Overflow: use BigInteger
                if (!BigInteger.TryParse(t.Text!, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger bv))
                {
                    throw _lexer.SyntaxError($"Invalid integer literal: {t.Text}", t.Start);
                }
                return new Literal(span, XdmAtomicValue.Integer(bv));
            }
            return new Literal(span, XdmAtomicValue.Integer(lv));
        }

        private Literal ParseDecimalLiteral()
        {
            Token t = Consume();
            SourceSpan span = TokenSpan(t);
            if (!decimal.TryParse(t.Text!, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal dv))
            {
                throw _lexer.SyntaxError($"Invalid decimal literal: {t.Text}", t.Start);
            }
            return new Literal(span, XdmAtomicValue.Decimal(dv));
        }

        private Literal ParseDoubleLiteral()
        {
            Token t = Consume();
            SourceSpan span = TokenSpan(t);
            if (!double.TryParse(t.Text!, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
            {
                throw _lexer.SyntaxError($"Invalid double literal: {t.Text}", t.Start);
            }
            return new Literal(span, XdmAtomicValue.Double(dv));
        }

        private Literal ParseStringLiteral()
        {
            Token t = Consume();
            SourceSpan span = TokenSpan(t);
            return new Literal(span, XdmAtomicValue.String(t.Text!));
        }

        // ── Variable reference ────────────────────────────────────────────────

        private VariableRef ParseVariableRef()
        {
            int start = Current.Start;
            Consume(); // "$"
            Token nameToken = Expect(TokenKind.Name);
            QName name = ResolveQName(nameToken.Text!, "", nameToken);
            return new VariableRef(_baseSpan.WithRange(start, nameToken.Start + nameToken.Length - start), name);
        }

        // ── Function call ─────────────────────────────────────────────────────

        private FunctionCall ParseFunctionCall()
        {
            int start = Current.Start;
            Token nameToken = Consume(); // function name
            string rawName = nameToken.Text!;

            // Check for reserved function names
            int colon = rawName.IndexOf(':');
            string localName = colon < 0 ? rawName : rawName.Substring(colon + 1);
            if (colon < 0 && s_reservedFunctionNames.Contains(localName))
            {
                throw _lexer.SyntaxError($"'{rawName}' is a reserved function name and cannot be used as a user function", nameToken.Start);
            }

            // Resolve the function name
            QName funcName;
            if (colon >= 0)
            {
                funcName = ResolveQName(rawName, _ns.DefaultFunctionNamespace, nameToken);
            }
            else
            {
                funcName = new QName("", _ns.DefaultFunctionNamespace, rawName);
            }

            Expect(TokenKind.LeftParen);
            var args = ImmutableArray.CreateBuilder<XPathAst>();

            if (Kind != TokenKind.RightParen)
            {
                args.Add(ParseExprSingleInternal());
                while (TryConsume(TokenKind.Comma))
                {
                    args.Add(ParseExprSingleInternal());
                }
            }

            Expect(TokenKind.RightParen);
            return new FunctionCall(_baseSpan.WithRange(start, Current.Start - start), funcName, args.ToImmutable());
        }

        // ── Parenthesized expression ──────────────────────────────────────────

        private ParenthesizedExpr ParseParenthesizedExpr()
        {
            int start = Current.Start;
            Consume(); // "("

            if (Kind == TokenKind.RightParen)
            {
                Consume(); // ")"
                return new ParenthesizedExpr(_baseSpan.WithRange(start, Current.Start - start), null);
            }

            // Parse inner expression (may be a sequence)
            XPathAst inner = ParseExprSingleInternal();

            if (Kind == TokenKind.Comma)
            {
                // Sequence inside parens
                var items = ImmutableArray.CreateBuilder<XPathAst>();
                items.Add(inner);
                while (TryConsume(TokenKind.Comma))
                {
                    items.Add(ParseExprSingleInternal());
                }
                int seqStart = inner.Span.Start;
                inner = new SequenceExpr(_baseSpan.WithRange(seqStart, Current.Start - seqStart), items.ToImmutable());
            }

            Expect(TokenKind.RightParen);
            return new ParenthesizedExpr(_baseSpan.WithRange(start, Current.Start - start), inner);
        }

        /// <summary>Parses an expression inside parentheses (for if/predicate), no EOI check.</summary>
        private XPathAst ParseExprForParen()
        {
            XPathAst first = ParseExprSingleInternal();

            if (Kind != TokenKind.Comma)
            {
                return first;
            }

            var items = ImmutableArray.CreateBuilder<XPathAst>();
            items.Add(first);
            while (TryConsume(TokenKind.Comma))
            {
                items.Add(ParseExprSingleInternal());
            }

            int start2 = first.Span.Start;
            return new SequenceExpr(_baseSpan.WithRange(start2, Current.Start - start2), items.ToImmutable());
        }

        // ── Node tests ────────────────────────────────────────────────────────

        private NodeTestAst ParseNodeTest(Axis axis)
        {
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;
                if (s_kindTestNames.Contains(name) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    return ParseKindTest();
                }
            }
            return ParseNameTest(axis);
        }

        private NameTestAst ParseNameTest(Axis axis)
        {
            int start = Current.Start;
            switch (Kind)
            {
                case TokenKind.Star:
                {
                    Consume();
                    return new NameTestAst(_baseSpan.WithRange(start, 1), null, NameTestKind.AnyName);
                }

                case TokenKind.PrefixWildcard:
                {
                    // prefix:*
                    Token t = Consume();
                    string prefix = t.Text!;
                    string? uri = _ns.LookupNamespace(prefix);
                    if (uri == null)
                    {
                        throw new XPathStaticException(
                            XPathErrors.XPST0081,
                            $"Undeclared namespace prefix '{prefix}'",
                            _baseSpan.WithRange(t.Start, t.Length));
                    }
                    var qn = new QName(prefix, uri, "*");
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.PrefixWildcard);
                }

                case TokenKind.LocalWildcard:
                {
                    // *:local
                    Token t = Consume();
                    string local = t.Text!;
                    var qn = new QName("", "", local);
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.LocalWildcard);
                }

                case TokenKind.Name:
                {
                    Token t = Consume();
                    string rawName = t.Text!;
                    // Determine default namespace based on axis
                    string defaultNs;
                    if (axis == Axis.Attribute || axis == Axis.Namespace)
                    {
                        defaultNs = ""; // attributes have no default namespace
                    }
                    else
                    {
                        defaultNs = _ns.DefaultElementNamespace;
                    }
                    QName qn = ResolveQName(rawName, defaultNs, t);
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.QualifiedName);
                }

                default:
                    throw _lexer.SyntaxError($"Expected a name test but found '{Current.Text ?? Kind.ToString()}'");
            }
        }

        private NodeTestAst ParseKindTest()
        {
            int start = Current.Start;
            Token kindToken = Consume(); // kind name
            string kindName = kindToken.Text!;
            Expect(TokenKind.LeftParen); // "("

            XdmNodeKind nodeKind = KindNameToNodeKind(kindName);

            if (string.Equals(kindName, "node", StringComparison.Ordinal))
            {
                Expect(TokenKind.RightParen);
                return new AnyKindTestAst(_baseSpan.WithRange(start, Current.Start - start));
            }

            if (string.Equals(kindName, "text", StringComparison.Ordinal) ||
                string.Equals(kindName, "comment", StringComparison.Ordinal))
            {
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, null);
            }

            if (string.Equals(kindName, "processing-instruction", StringComparison.Ordinal))
            {
                QName? piName = null;
                if (Kind != TokenKind.RightParen)
                {
                    // PI target: either StringLiteral or Name
                    if (Kind == TokenKind.StringLiteral)
                    {
                        Token piTarget = Consume();
                        piName = new QName("", "", piTarget.Text!);
                    }
                    else if (Kind == TokenKind.Name)
                    {
                        Token piTarget = Consume();
                        piName = new QName("", "", piTarget.Text!);
                    }
                    else
                    {
                        throw _lexer.SyntaxError("Expected PI target name or string literal");
                    }
                }
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, piName, null);
            }

            if (string.Equals(kindName, "document-node", StringComparison.Ordinal))
            {
                NodeTestAst? elementArg = null;
                if (Kind != TokenKind.RightParen)
                {
                    // Must be element(...) kind test
                    if (Kind == TokenKind.Name &&
                        (string.Equals(Current.Text, "element", StringComparison.Ordinal) ||
                         string.Equals(Current.Text, "schema-element", StringComparison.Ordinal)) &&
                        Peek(1).Kind == TokenKind.LeftParen)
                    {
                        elementArg = ParseKindTest();
                    }
                    else
                    {
                        throw _lexer.SyntaxError("Expected element() inside document-node()");
                    }
                }
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, elementArg);
            }

            if (string.Equals(kindName, "element", StringComparison.Ordinal) ||
                string.Equals(kindName, "schema-element", StringComparison.Ordinal) ||
                string.Equals(kindName, "attribute", StringComparison.Ordinal) ||
                string.Equals(kindName, "schema-attribute", StringComparison.Ordinal))
            {
                QName? nameArg = null;
                if (Kind != TokenKind.RightParen)
                {
                    if (Kind == TokenKind.Star)
                    {
                        Consume(); // wildcard — name stays null
                    }
                    else if (Kind == TokenKind.Name)
                    {
                        Token nameToken = Consume();
                        string defaultNs = string.Equals(kindName, "attribute", StringComparison.Ordinal) ||
                                           string.Equals(kindName, "schema-attribute", StringComparison.Ordinal)
                            ? ""
                            : _ns.DefaultElementNamespace;
                        nameArg = ResolveQName(nameToken.Text!, defaultNs, nameToken);
                    }
                    else
                    {
                        throw _lexer.SyntaxError($"Expected name or * in {kindName}() test");
                    }

                    // Skip optional second type argument (schema-aware, out of scope for P1-12)
                    if (Kind == TokenKind.Comma)
                    {
                        Consume(); // ","
                        // Consume the type name
                        if (Kind == TokenKind.Name)
                        {
                            Consume(); // type name
                            TryConsume(TokenKind.QuestionMark);
                        }
                        else if (Kind == TokenKind.Star)
                        {
                            Consume();
                        }
                    }
                }
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, nameArg, null);
            }

            Expect(TokenKind.RightParen);
            return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, null);
        }

        private static XdmNodeKind KindNameToNodeKind(string name)
        {
            return name switch
            {
                "element" => XdmNodeKind.Element,
                "schema-element" => XdmNodeKind.Element,
                "attribute" => XdmNodeKind.Attribute,
                "schema-attribute" => XdmNodeKind.Attribute,
                "text" => XdmNodeKind.Text,
                "comment" => XdmNodeKind.Comment,
                "processing-instruction" => XdmNodeKind.ProcessingInstruction,
                "document-node" => XdmNodeKind.Document,
                "node" => XdmNodeKind.Element, // won't be used (AnyKindTestAst instead)
                _ => XdmNodeKind.Element,
            };
        }

        // ── SequenceType / SingleType parsing ─────────────────────────────────

        // XPath 2.0 §A.1: SequenceType ::= "empty-sequence()" | ItemType OccurrenceIndicator?
        private SequenceType ParseSequenceType()
        {
            if (Kind == TokenKind.Name && string.Equals(Current.Text, "empty-sequence", StringComparison.Ordinal) &&
                Peek(1).Kind == TokenKind.LeftParen)
            {
                Consume(); // "empty-sequence"
                Expect(TokenKind.LeftParen);
                Expect(TokenKind.RightParen);
                return SequenceType.EmptySequence;
            }

            ItemType itemType = ParseItemType();

            Occurrence occ = Occurrence.ExactlyOne;
            switch (Kind)
            {
                case TokenKind.QuestionMark:
                    Consume();
                    occ = Occurrence.ZeroOrOne;
                    break;
                case TokenKind.Star:
                    Consume();
                    occ = Occurrence.ZeroOrMore;
                    break;
                case TokenKind.Plus:
                    Consume();
                    occ = Occurrence.OneOrMore;
                    break;
            }

            return new SequenceType(itemType, occ);
        }

        // XPath 2.0 §A.1: SingleType ::= AtomicType "?"?
        private SequenceType ParseSingleType()
        {
            if (Kind != TokenKind.Name)
            {
                throw _lexer.SyntaxError("Expected atomic type name");
            }
            Token typeToken = Consume();
            return ResolveAtomicType(typeToken.Text!, typeToken, allowQuestionMark: true);
        }

        // XPath 2.0 §A.1: ItemType ::= KindTest | "item" "()" | AtomicType
        private ItemType ParseItemType()
        {
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;

                if (string.Equals(name, "item", StringComparison.Ordinal) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    Consume(); // "item"
                    Expect(TokenKind.LeftParen);
                    Expect(TokenKind.RightParen);
                    return ItemType.AnyItem;
                }

                // KindTest
                if (s_kindTestNames.Contains(name) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    NodeTestAst kt = ParseKindTest();
                    return KindTestToItemType(kt);
                }

                // AtomicType (e.g. xs:integer)
                Token typeToken = Consume();
                return ResolveAtomicType(typeToken.Text!, typeToken, allowQuestionMark: false).ItemType;
            }

            throw _lexer.SyntaxError("Expected item type");
        }

        private static ItemType KindTestToItemType(NodeTestAst kt)
        {
            if (kt is AnyKindTestAst)
            {
                return ItemType.AnyNode;
            }

            if (kt is KindTestAst kindTest)
            {
                if (kindTest.Kind == XdmNodeKind.Document)
                {
                    if (kindTest.ElementArg != null)
                    {
                        ItemType innerType = KindTestToItemType(kindTest.ElementArg);
                        return ItemType.DocumentNode(innerType);
                    }
                    return ItemType.DocumentNode();
                }
                return ItemType.Kind(kindTest.Kind, kindTest.NameArg);
            }

            // NameTestAst in ItemType position shouldn't happen, but handle gracefully
            return ItemType.AnyNode;
        }

        // ── Internal predicate helper (used by PatternParser) ─────────────────

        /// <summary>
        /// Parses a full XPath Expr from the shared lexer and asserts the next token is ']'.
        /// Used by the XSLT pattern parser to parse predicate expressions.
        /// </summary>
        internal XPathAst ParsePredicateExpr()
        {
            return ParseExprForParen();
        }
    }

    // ── XSLT pattern parser ───────────────────────────────────────────────────

    /// <summary>
    /// Parses XSLT 2.0 patterns (§5.5.2). The XPath lexer and namespace resolver are shared
    /// with a <see cref="Parser"/> instance so that predicates can be parsed via full XPath.
    /// </summary>
    private sealed class PatternParser
    {
        private readonly XPathLexer _lexer;
        private readonly SourceSpan _baseSpan;
        private readonly IStaticNamespaceResolver _ns;

        // A Parser instance sharing the same lexer, used to parse predicate expressions.
        private readonly Parser _xpathParser;

        internal PatternParser(string pattern, SourceSpan baseSpan, IStaticNamespaceResolver ns)
        {
            _lexer = new XPathLexer(pattern, baseSpan);
            _baseSpan = baseSpan;
            _ns = ns;
            _lexer.Advance();
            _xpathParser = new Parser(_lexer, baseSpan, ns);
        }

        // ── Token helpers ─────────────────────────────────────────────────────

        private Token Current => _lexer.Current;
        private TokenKind Kind => _lexer.Current.Kind;

        private Token Peek(int ahead = 1) => _lexer.Peek(ahead);

        private Token Consume()
        {
            Token t = Current;
            _lexer.Advance();
            return t;
        }

        private Token Expect(TokenKind kind)
        {
            if (Kind != kind)
            {
                throw PatternError($"Expected {kind} but found {Kind} ('{Current.Text ?? Kind.ToString()}')");
            }
            return Consume();
        }

        private bool TryConsume(TokenKind kind)
        {
            if (Kind != kind) { return false; }
            Consume();
            return true;
        }

        private SourceSpan TokenSpan(Token t) => _baseSpan.WithRange(t.Start, t.Length);

        private XsltStaticException PatternError(string message, int? offset = null)
        {
            int pos = offset ?? Current.Start;
            int len = offset.HasValue ? 0 : Current.Length;
            return new XsltStaticException(new Diagnostic[]
            {
                new Diagnostic(DiagnosticSeverity.Error, XsltErrors.XTSE0340,
                               message, _baseSpan.WithRange(pos, len)),
            });
        }

        // ── QName resolution ──────────────────────────────────────────────────

        private QName ResolveQName(string rawName, string defaultNamespace, Token atToken)
        {
            int colon = rawName.IndexOf(':');
            if (colon < 0)
            {
                return new QName("", defaultNamespace, rawName);
            }

            string prefix = rawName.Substring(0, colon);
            string local = rawName.Substring(colon + 1);
            string? uri = _ns.LookupNamespace(prefix);
            if (uri == null)
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0081,
                    $"Undeclared namespace prefix '{prefix}'",
                    _baseSpan.WithRange(atToken.Start, atToken.Length));
            }
            return new QName(prefix, uri, local);
        }

        // ── Pattern grammar ───────────────────────────────────────────────────

        // XSLT 2.0 §5.5.2: Pattern ::= PathPattern ("|" PathPattern)*
        internal PatternAst ParseUnionPattern()
        {
            int start = Current.Start;
            PatternAst first = ParsePathPattern();

            if (Kind != TokenKind.Pipe)
            {
                // Ensure nothing is left over
                if (Kind != TokenKind.EndOfInput)
                {
                    throw PatternError($"Unexpected token '{Current.Text ?? Kind.ToString()}' in pattern");
                }
                return first;
            }

            var alternatives = ImmutableArray.CreateBuilder<PatternAst>();
            alternatives.Add(first);
            while (TryConsume(TokenKind.Pipe))
            {
                alternatives.Add(ParsePathPattern());
            }

            if (Kind != TokenKind.EndOfInput)
            {
                throw PatternError($"Unexpected token '{Current.Text ?? Kind.ToString()}' in pattern");
            }

            return new UnionPatternAst(_baseSpan.WithRange(start, Current.Start - start), alternatives.ToImmutable());
        }

        // XSLT 2.0 §5.5.2:
        // PathPattern ::= RelativePathPattern
        //               | "/" RelativePathPattern?
        //               | "//" RelativePathPattern
        //               | IdKeyPattern (("/" | "//") RelativePathPattern)?
        private PatternAst ParsePathPattern()
        {
            int start = Current.Start;

            // "/" — rooted
            if (Kind == TokenKind.Slash)
            {
                Consume(); // "/"
                if (!CanStartStepPattern())
                {
                    // Bare "/"
                    return new PathPatternAst(
                        _baseSpan.WithRange(start, Current.Start - start),
                        PatternAnchor.Root,
                        ImmutableArray<StepPatternAst>.Empty);
                }
                var steps = ParseRelativePathPattern(StepCombinator.Leading);
                return new PathPatternAst(
                    _baseSpan.WithRange(start, Current.Start - start),
                    PatternAnchor.Root, steps);
            }

            // "//" — root descendant
            if (Kind == TokenKind.DoubleSlash)
            {
                Consume(); // "//"
                var steps = ParseRelativePathPattern(StepCombinator.Leading);
                return new PathPatternAst(
                    _baseSpan.WithRange(start, Current.Start - start),
                    PatternAnchor.RootDescendantOrSelf, steps);
            }

            // id(...) or key(...)  — but only when followed by "("
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;
                if (string.Equals(name, "id", StringComparison.Ordinal) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    return ParseIdKeyPattern(start);
                }
                if (string.Equals(name, "key", StringComparison.Ordinal) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    return ParseIdKeyPattern(start);
                }
            }

            // RelativePathPattern
            {
                var steps = ParseRelativePathPattern(StepCombinator.Leading);
                return new PathPatternAst(
                    _baseSpan.WithRange(start, Current.Start - start),
                    PatternAnchor.Relative, steps);
            }
        }

        // XSLT 2.0 §5.5.2:
        // RelativePathPattern ::= StepPattern (("/" | "//") StepPattern)*
        private ImmutableArray<StepPatternAst> ParseRelativePathPattern(StepCombinator firstCombinator)
        {
            var steps = ImmutableArray.CreateBuilder<StepPatternAst>();
            steps.Add(ParseStepPattern(firstCombinator));

            while (Kind == TokenKind.Slash || Kind == TokenKind.DoubleSlash)
            {
                StepCombinator comb = Kind == TokenKind.DoubleSlash
                    ? StepCombinator.DoubleSlash
                    : StepCombinator.Slash;
                Consume();
                steps.Add(ParseStepPattern(comb));
            }

            return steps.ToImmutable();
        }

        // XSLT 2.0 §5.5.2:
        // StepPattern ::= (ChildOrAttributeAxisSpecifier NodeTest PredicateList)
        // ChildOrAttributeAxisSpecifier ::= ("child" "::") | ("attribute" "::") | "@"  (child is default)
        private StepPatternAst ParseStepPattern(StepCombinator combinator)
        {
            int start = Current.Start;

            // "@" → attribute axis
            if (Kind == TokenKind.At)
            {
                Consume(); // "@"
                NodeTestAst attrTest = ParsePatternNodeTest(Axis.Attribute);
                ImmutableArray<XPathAst> attrPreds = ParsePatternPredicateList();
                return new StepPatternAst(
                    _baseSpan.WithRange(start, Current.Start - start),
                    Axis.Attribute, attrTest, attrPreds, combinator);
            }

            // Check for axis name followed by "::"
            if (Kind == TokenKind.Name && Current.Text != null && Peek(1).Kind == TokenKind.DoubleColon)
            {
                string axisName = Current.Text;
                // Only child:: and attribute:: are allowed in pattern steps
                if (string.Equals(axisName, "child", StringComparison.Ordinal))
                {
                    Consume(); // axis name
                    Consume(); // "::"
                    NodeTestAst childTest = ParsePatternNodeTest(Axis.Child);
                    ImmutableArray<XPathAst> childPreds = ParsePatternPredicateList();
                    return new StepPatternAst(
                        _baseSpan.WithRange(start, Current.Start - start),
                        Axis.Child, childTest, childPreds, combinator)
                    {
                        ExplicitChildAxis = true,
                    };
                }
                else if (string.Equals(axisName, "attribute", StringComparison.Ordinal))
                {
                    Consume(); // axis name
                    Consume(); // "::"
                    NodeTestAst attrTest2 = ParsePatternNodeTest(Axis.Attribute);
                    ImmutableArray<XPathAst> attrPreds2 = ParsePatternPredicateList();
                    return new StepPatternAst(
                        _baseSpan.WithRange(start, Current.Start - start),
                        Axis.Attribute, attrTest2, attrPreds2, combinator);
                }
                else if (s_axisNames.ContainsKey(axisName))
                {
                    // A known axis that is not child or attribute — XTSE0340
                    throw PatternError($"Axis '{axisName}' is not allowed in a pattern step; only 'child' and 'attribute' are permitted", Current.Start);
                }
                // Not a known axis — fall through to treat as a name test
            }

            // ".." is parent::node() — forbidden in patterns
            if (Kind == TokenKind.DotDot)
            {
                throw PatternError("'parent::node()' (..) is not allowed in a pattern step", Current.Start);
            }

            // Name, *, or kind test — implicit child axis
            if (Kind == TokenKind.Name || Kind == TokenKind.Star ||
                Kind == TokenKind.PrefixWildcard || Kind == TokenKind.LocalWildcard)
            {
                NodeTestAst test = ParsePatternNodeTest(Axis.Child);
                ImmutableArray<XPathAst> preds = ParsePatternPredicateList();
                return new StepPatternAst(
                    _baseSpan.WithRange(start, Current.Start - start),
                    Axis.Child, test, preds, combinator);
            }

            throw PatternError($"Expected a step pattern but found '{Current.Text ?? Kind.ToString()}'");
        }

        // Node tests valid in pattern context — same as full XPath node tests
        private NodeTestAst ParsePatternNodeTest(Axis axis)
        {
            if (Kind == TokenKind.Name && Current.Text != null)
            {
                string name = Current.Text;
                if (s_kindTestNames.Contains(name) && Peek(1).Kind == TokenKind.LeftParen)
                {
                    return ParseKindTestForPattern(axis);
                }
            }
            return ParsePatternNameTest(axis);
        }

        private NameTestAst ParsePatternNameTest(Axis axis)
        {
            int start = Current.Start;
            switch (Kind)
            {
                case TokenKind.Star:
                {
                    Consume();
                    return new NameTestAst(_baseSpan.WithRange(start, 1), null, NameTestKind.AnyName);
                }

                case TokenKind.PrefixWildcard:
                {
                    Token t = Consume();
                    string prefix = t.Text!;
                    string? uri = _ns.LookupNamespace(prefix);
                    if (uri == null)
                    {
                        throw new XPathStaticException(
                            XPathErrors.XPST0081,
                            $"Undeclared namespace prefix '{prefix}'",
                            _baseSpan.WithRange(t.Start, t.Length));
                    }
                    var qn = new QName(prefix, uri, "*");
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.PrefixWildcard);
                }

                case TokenKind.LocalWildcard:
                {
                    Token t = Consume();
                    string local = t.Text!;
                    var qn = new QName("", "", local);
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.LocalWildcard);
                }

                case TokenKind.Name:
                {
                    Token t = Consume();
                    string rawName = t.Text!;
                    string defaultNs = (axis == Axis.Attribute) ? "" : _ns.DefaultElementNamespace;
                    QName qn = ResolveQName(rawName, defaultNs, t);
                    return new NameTestAst(_baseSpan.WithRange(start, t.Length), qn, NameTestKind.QualifiedName);
                }

                default:
                    throw PatternError($"Expected a name test but found '{Current.Text ?? Kind.ToString()}'");
            }
        }

        // Parse kind tests in pattern context — delegates to the XPath Parser's ParseKindTest
        // by recreating a local inline parsing loop (the kind test grammar is identical).
        private NodeTestAst ParseKindTestForPattern(Axis axis)
        {
            int start = Current.Start;
            Token kindToken = Consume(); // kind name
            string kindName = kindToken.Text!;
            Expect(TokenKind.LeftParen); // "("

            XdmNodeKind nodeKind = KindNameToNodeKindPattern(kindName);

            if (string.Equals(kindName, "node", StringComparison.Ordinal))
            {
                Expect(TokenKind.RightParen);
                return new AnyKindTestAst(_baseSpan.WithRange(start, Current.Start - start));
            }

            if (string.Equals(kindName, "text", StringComparison.Ordinal) ||
                string.Equals(kindName, "comment", StringComparison.Ordinal))
            {
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, null);
            }

            if (string.Equals(kindName, "processing-instruction", StringComparison.Ordinal))
            {
                QName? piName = null;
                if (Kind != TokenKind.RightParen)
                {
                    if (Kind == TokenKind.StringLiteral)
                    {
                        Token piTarget = Consume();
                        piName = new QName("", "", piTarget.Text!);
                    }
                    else if (Kind == TokenKind.Name)
                    {
                        // XSLT 2.0 §5.5.2 (XTSE0340): a PI target in a pattern is an NCName, so it has no prefix.
#pragma warning disable CA2249 // string.Contains(char) is not available on netstandard2.0
                        if (Current.Text!.IndexOf(':') >= 0)
#pragma warning restore CA2249
                        {
                            throw PatternError("A processing-instruction() test in a pattern takes an NCName, not a prefixed name");
                        }

                        Token piTarget = Consume();
                        piName = new QName("", "", piTarget.Text!);
                    }
                    else
                    {
                        throw PatternError("Expected PI target name or string literal");
                    }
                }
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, piName, null);
            }

            if (string.Equals(kindName, "document-node", StringComparison.Ordinal))
            {
                NodeTestAst? elementArg = null;
                if (Kind != TokenKind.RightParen)
                {
                    if (Kind == TokenKind.Name &&
                        (string.Equals(Current.Text, "element", StringComparison.Ordinal) ||
                         string.Equals(Current.Text, "schema-element", StringComparison.Ordinal)) &&
                        Peek(1).Kind == TokenKind.LeftParen)
                    {
                        elementArg = ParseKindTestForPattern(Axis.Child);
                    }
                    else
                    {
                        throw PatternError("Expected element() inside document-node()");
                    }
                }
                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, elementArg);
            }

            if (string.Equals(kindName, "element", StringComparison.Ordinal) ||
                string.Equals(kindName, "schema-element", StringComparison.Ordinal) ||
                string.Equals(kindName, "attribute", StringComparison.Ordinal) ||
                string.Equals(kindName, "schema-attribute", StringComparison.Ordinal))
            {
                QName? nameArg = null;
                QName? typeArg = null;
                if (Kind != TokenKind.RightParen)
                {
                    if (Kind == TokenKind.Star)
                    {
                        Consume(); // wildcard — name stays null, but mark as "explicit wildcard"
                        // We use a sentinel: null NameArg means both "empty" and "explicit *"
                        // We need to distinguish element(*) from element() for priority.
                        // Use a special QName with LocalName="*" to mark explicit wildcard.
                        nameArg = new QName("", "", "*");
                    }
                    else if (Kind == TokenKind.Name)
                    {
                        Token nameToken = Consume();
                        string defaultNs = (string.Equals(kindName, "attribute", StringComparison.Ordinal) ||
                                            string.Equals(kindName, "schema-attribute", StringComparison.Ordinal))
                            ? ""
                            : _ns.DefaultElementNamespace;
                        nameArg = ResolveQName(nameToken.Text!, defaultNs, nameToken);
                    }
                    else
                    {
                        throw PatternError($"Expected name or * in {kindName}() test");
                    }

                    // The optional type argument: kept so the default priority is right and so that a type an
                    // untyped node cannot have does not match (schema-aware typing is out of scope).
                    if (Kind == TokenKind.Comma)
                    {
                        Consume();
                        if (Kind == TokenKind.Name)
                        {
                            Token typeToken = Consume();
                            typeArg = ResolveQName(typeToken.Text!, "http://www.w3.org/2001/XMLSchema", typeToken);
                            TryConsume(TokenKind.QuestionMark);
                        }
                        else if (Kind == TokenKind.Star)
                        {
                            Consume();
                        }
                    }
                }

                Expect(TokenKind.RightParen);
                return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, nameArg, null, typeArg);
            }

            Expect(TokenKind.RightParen);
            return new KindTestAst(_baseSpan.WithRange(start, Current.Start - start), nodeKind, null, null);
        }

        private static XdmNodeKind KindNameToNodeKindPattern(string name)
        {
            return name switch
            {
                "element" => XdmNodeKind.Element,
                "schema-element" => XdmNodeKind.Element,
                "attribute" => XdmNodeKind.Attribute,
                "schema-attribute" => XdmNodeKind.Attribute,
                "text" => XdmNodeKind.Text,
                "comment" => XdmNodeKind.Comment,
                "processing-instruction" => XdmNodeKind.ProcessingInstruction,
                "document-node" => XdmNodeKind.Document,
                _ => XdmNodeKind.Element,
            };
        }

        // Parse predicate list: "[" Expr "]"*
        // Full XPath is allowed inside predicates (not just pattern syntax).
        private ImmutableArray<XPathAst> ParsePatternPredicateList()
        {
            if (Kind != TokenKind.LeftBracket)
            {
                return ImmutableArray<XPathAst>.Empty;
            }

            var predicates = ImmutableArray.CreateBuilder<XPathAst>();
            while (Kind == TokenKind.LeftBracket)
            {
                Consume(); // "["
                XPathAst predExpr = _xpathParser.ParsePredicateExpr();
                Expect(TokenKind.RightBracket);
                predicates.Add(predExpr);
            }
            return predicates.ToImmutable();
        }

        // ── id() and key() patterns ───────────────────────────────────────────

        // XSLT 2.0 §5.5.2:
        // IdKeyPattern ::= "id" "(" (StringLiteral | VariableReference) ")"
        //                | "key" "(" StringLiteral "," (StringLiteral | VariableReference) ")"
        private PatternAst ParseIdKeyPattern(int start)
        {
            string funcName = Current.Text!;
            Consume(); // "id" or "key"
            Expect(TokenKind.LeftParen);

            PatternAst head;

            if (string.Equals(funcName, "id", StringComparison.Ordinal))
            {
                if (Kind == TokenKind.StringLiteral)
                {
                    Token lit = Consume();
                    Expect(TokenKind.RightParen);
                    head = new IdPatternAst(_baseSpan.WithRange(start, Current.Start - start), lit.Text!, null);
                }
                else if (Kind == TokenKind.VariableMarker)
                {
                    Consume(); // "$"
                    Token varName = Expect(TokenKind.Name);
                    QName varRef = ResolveQName(varName.Text!, "", varName);
                    Expect(TokenKind.RightParen);
                    head = new IdPatternAst(_baseSpan.WithRange(start, Current.Start - start), null, varRef);
                }
                else
                {
                    throw PatternError("Expected string literal or variable reference in id()");
                }
            }
            else // "key"
            {
                Token keyNameToken = Expect(TokenKind.StringLiteral);
                QName keyQName = new QName("", "", keyNameToken.Text!);
                Expect(TokenKind.Comma);

                KeyPatternAst keyHead;
                if (Kind == TokenKind.StringLiteral)
                {
                    Token valLit = Consume();
                    Expect(TokenKind.RightParen);
                    keyHead = new KeyPatternAst(_baseSpan.WithRange(start, Current.Start - start), keyQName, valLit.Text!, null);
                }
                else if (Kind == TokenKind.VariableMarker)
                {
                    Consume(); // "$"
                    Token varName = Expect(TokenKind.Name);
                    QName varRef = ResolveQName(varName.Text!, "", varName);
                    Expect(TokenKind.RightParen);
                    keyHead = new KeyPatternAst(_baseSpan.WithRange(start, Current.Start - start), keyQName, null, varRef);
                }
                else if (Kind == TokenKind.IntegerLiteral || Kind == TokenKind.DecimalLiteral || Kind == TokenKind.DoubleLiteral)
                {
                    TokenKind numberKind = Kind;
                    Token numberToken = Consume();
                    Expect(TokenKind.RightParen);
                    string numberText = numberToken.Text!;
                    XdmAtomicValue number = numberKind == TokenKind.IntegerLiteral
                        ? (long.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out long whole)
                            ? XdmAtomicValue.Integer(whole)
                            : XdmAtomicValue.Integer(BigInteger.Parse(numberText, NumberStyles.None, CultureInfo.InvariantCulture)))
                        : numberKind == TokenKind.DecimalLiteral
                            ? XdmAtomicValue.Decimal(decimal.Parse(numberText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture))
                            : XdmAtomicValue.Double(double.Parse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture));
                    keyHead = new KeyPatternAst(_baseSpan.WithRange(start, Current.Start - start), keyQName, null, null, number);
                }
                else
                {
                    throw PatternError("Expected string literal, number or variable reference as second argument of key()");
                }
                head = keyHead;
            }

            // Optional tail: ("/" | "//") RelativePathPattern
            if (Kind == TokenKind.Slash || Kind == TokenKind.DoubleSlash)
            {
                StepCombinator comb = Kind == TokenKind.DoubleSlash
                    ? StepCombinator.DoubleSlash
                    : StepCombinator.Slash;
                Consume();
                // The combinator of the first step says how the id or key head is related to it.
                ImmutableArray<StepPatternAst> tail = ParseRelativePathPattern(comb);
                return new IdKeyWithPathAst(_baseSpan.WithRange(start, Current.Start - start), head, tail);
            }

            return head;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Returns true when the current token can start a step pattern.</summary>
        private bool CanStartStepPattern()
        {
            switch (Kind)
            {
                case TokenKind.Name:
                case TokenKind.At:
                case TokenKind.Star:
                case TokenKind.PrefixWildcard:
                case TokenKind.LocalWildcard:
                    return true;
                default:
                    return false;
            }
        }
    }
}
