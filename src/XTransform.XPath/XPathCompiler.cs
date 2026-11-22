// XPath 2.0 §2.1 — static context; §2.2 — processing model (compile + evaluate).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;
// IReadOnlyDictionary is in System.Collections.Generic on all target frameworks.
using XTransform.XPath.Ast;
using XTransform.XPath.ExpressionNodes;

namespace XTransform.XPath;

/// <summary>
/// The result of compiling an XPath expression: the root <see cref="ExpressionNode"/> and
/// the total local-variable slot count required to evaluate it.
/// </summary>
internal sealed record CompiledExpression(ExpressionNode Root, int SlotCount);

/// <summary>
/// Compiles an XPath 2.0 AST into an executable <see cref="ExpressionNode"/> tree.
/// Phase 2 scaffolds the tree; evaluation bodies land in later tasks.
/// </summary>
/// <remarks>
/// The compiler is stateless across calls: each <see cref="Compile"/> or
/// <see cref="ParseAndCompile"/> call constructs a fresh internal state object and
/// returns it wrapped in a <see cref="CompiledExpression"/>.
/// </remarks>
internal static class XPathCompiler
{
    // ── Public entry points ────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <paramref name="ast"/> against the given namespace and function resolvers.
    /// </summary>
    /// <param name="ast">The XPath AST produced by <see cref="XPathParser"/>.</param>
    /// <param name="namespaces">In-scope namespace bindings used by the static context.</param>
    /// <param name="functions">The function library used to resolve function calls.</param>
    /// <param name="externalVariables">
    /// Optional externally-scoped variable bindings (e.g. xsl:param, xsl:variable from the caller).
    /// Each entry is a QName mapped to its declared <see cref="SequenceType"/>.
    /// Pass <see langword="null"/> when no external variables are in scope.
    /// </param>
    /// <returns>A <see cref="CompiledExpression"/> holding the root node and slot count.</returns>
    /// <exception cref="XPathStaticException">
    /// Thrown for static errors: XPST0008 (undeclared variable), XPST0017 (unknown/arity-mismatch
    /// function), XPTY0004 (certain-fail type mismatch — deferred to later tasks).
    /// </exception>
    public static CompiledExpression Compile(
        XPathAst ast,
        IStaticNamespaceResolver namespaces,
        FunctionLibrary functions,
        IReadOnlyDictionary<QName, SequenceType>? externalVariables = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (ast is null)
        {
            throw new ArgumentNullException(nameof(ast));
        }

        if (namespaces is null)
        {
            throw new ArgumentNullException(nameof(namespaces));
        }

        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }
#pragma warning restore CA1510

        CompilerState state = new CompilerState(namespaces, functions, externalVariables);
        ExpressionNode root = state.CompileAst(ast);
        return new CompiledExpression(root, state.Slots.SlotCount);
    }

    /// <summary>
    /// Convenience overload: parses and compiles.
    /// </summary>
    /// <param name="expression">The XPath 2.0 expression string.</param>
    /// <param name="span">Source span used in diagnostics.</param>
    /// <param name="namespaces">In-scope namespace bindings.</param>
    /// <param name="functions">The function library for resolving calls.</param>
    /// <param name="externalVariables">
    /// Optional externally-scoped variable bindings. See <see cref="Compile"/> for details.
    /// </param>
    /// <returns>A <see cref="CompiledExpression"/> holding the root node and slot count.</returns>
    /// <exception cref="XPathStaticException">
    /// Thrown for both parser errors (XPST0003) and compiler static errors.
    /// </exception>
    public static CompiledExpression ParseAndCompile(
        string expression,
        SourceSpan span,
        IStaticNamespaceResolver namespaces,
        FunctionLibrary functions,
        IReadOnlyDictionary<QName, SequenceType>? externalVariables = null)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }
#pragma warning restore CA1510

        XPathAst ast = XPathParser.ParseExpression(expression, span, namespaces);
        return Compile(ast, namespaces, functions, externalVariables);
    }

    // ── Internal compiler state ────────────────────────────────────────────────

    /// <summary>
    /// All mutable state for a single compilation run. Keeping this in an instance class
    /// makes the outer static class stateless across concurrent calls.
    /// </summary>
    private sealed class CompilerState
    {
        private readonly IStaticNamespaceResolver _namespaces;
        private readonly FunctionLibrary _functions;
        internal readonly SlotAllocator Slots = new SlotAllocator();
        private readonly CompileScope _scope;

        internal CompilerState(IStaticNamespaceResolver namespaces, FunctionLibrary functions,
                               IReadOnlyDictionary<QName, SequenceType>? externalVariables = null)
        {
            _namespaces = namespaces;
            _functions = functions;
            _scope = new CompileScope(Slots);

            // Pre-seed any externally-provided variable bindings (e.g. xsl:param, xsl:variable).
            if (externalVariables != null)
            {
                foreach (System.Collections.Generic.KeyValuePair<QName, SequenceType> kv in externalVariables)
                {
                    _scope.PushBinding(kv.Key, kv.Value);
                }
            }
        }

        // ── Entry ──────────────────────────────────────────────────────────────

        /// <summary>Dispatches to the per-AST-type compile method.</summary>
        internal ExpressionNode CompileAst(XPathAst ast)
        {
            return ast switch
            {
                Literal lit => CompileLiteral(lit),
                VariableRef varRef => CompileVariableRef(varRef),
                ContextItemExpr ctxItem => CompileContextItem(ctxItem),
                ParenthesizedExpr paren => CompileParenthesized(paren),
                SequenceExpr seq => CompileSequence(seq),
                ForExpr forExpr => CompileFor(forExpr),
                QuantifiedExpr quantExpr => CompileQuantified(quantExpr),
                IfExpr ifExpr => CompileIf(ifExpr),
                OrExpr orExpr => CompileOr(orExpr),
                AndExpr andExpr => CompileAnd(andExpr),
                ComparisonExpr cmp => CompileComparison(cmp),
                RangeExpr range => CompileRange(range),
                ArithmeticExpr arith => CompileArithmetic(arith),
                SetExpr set => CompileSet(set),
                InstanceOfExpr instOf => CompileInstanceOf(instOf),
                TreatExpr treat => CompileTreat(treat),
                CastableExpr castable => CompileCastable(castable),
                CastExpr cast => CompileCast(cast),
                UnaryExpr unary => CompileUnary(unary),
                PathExpr path => CompilePath(path),
                AxisStep step => CompileAxisStep(step),
                FilterExpr filter => CompileFilter(filter),
                FunctionCall call => CompileFunctionCall(call),
                _ => throw new XPathStaticException(
                    XPathErrors.XPST0003,
                    "Unsupported AST node type: " + ast.GetType().Name,
                    ast.Span),
            };
        }

        // ── Literals and primaries ─────────────────────────────────────────────

        private static LiteralNode CompileLiteral(Literal lit)
            => new LiteralNode(lit.Span, lit.Value);

        private VariableRefNode CompileVariableRef(VariableRef varRef)
        {
            ScopeEntry? binding = _scope.Lookup(varRef.Name);
            if (binding is null)
            {
                // XPath 2.0 §2.3.1 — XPST0008: undeclared variable
                throw new XPathStaticException(
                    XPathErrors.XPST0008,
                    "Undeclared variable '$" + FormatQName(varRef.Name) + "'",
                    varRef.Span);
            }

            return new VariableRefNode(varRef.Span, varRef.Name, binding.Slot, binding.Type);
        }

        private static ContextItemNode CompileContextItem(ContextItemExpr ci)
            => new ContextItemNode(ci.Span);

        private ExpressionNode CompileParenthesized(ParenthesizedExpr paren)
        {
            if (paren.Inner is null)
            {
                return new EmptySequenceNode(paren.Span);
            }

            // Transparent wrapper — compile the inner expression directly.
            return CompileAst(paren.Inner);
        }

        private ExpressionNode CompileSequence(SequenceExpr seq)
        {
            if (seq.Items.Length == 0)
            {
                return new EmptySequenceNode(seq.Span);
            }

            if (seq.Items.Length == 1)
            {
                return CompileAst(seq.Items[0]);
            }

            ImmutableArray<ExpressionNode>.Builder builder =
                ImmutableArray.CreateBuilder<ExpressionNode>(seq.Items.Length);
            foreach (XPathAst item in seq.Items)
            {
                builder.Add(CompileAst(item));
            }

            return new SequenceConcatNode(seq.Span, builder.MoveToImmutable());
        }

        // ── Control flow ───────────────────────────────────────────────────────

        private ForNode CompileFor(ForExpr forExpr)
        {
            // XPath 2.0 §3.8 — for expression: each clause is scoped after the previous.
            ImmutableArray<ForClauseBinding>.Builder clauseBuilder =
                ImmutableArray.CreateBuilder<ForClauseBinding>(forExpr.Clauses.Length);

            foreach (ForClause clause in forExpr.Clauses)
            {
                // Compile the sequence in the outer scope, then push the binding.
                ExpressionNode seqNode = CompileAst(clause.SequenceExpr);
                // The binding type is the item type of the sequence (each iteration binds one item).
                SequenceType bindingType = new SequenceType(
                    seqNode.StaticType.ItemType, Occurrence.ExactlyOne);
                int slot = _scope.PushBinding(clause.Variable, bindingType);
                clauseBuilder.Add(new ForClauseBinding(clause.Variable, slot, seqNode, bindingType));
            }

            ExpressionNode returnNode = CompileAst(forExpr.ReturnExpr);

            // Pop bindings in reverse order.
            for (int i = 0; i < forExpr.Clauses.Length; i++)
            {
                _scope.PopBinding();
            }

            return new ForNode(forExpr.Span, clauseBuilder.MoveToImmutable(), returnNode);
        }

        private QuantifiedNode CompileQuantified(QuantifiedExpr quantExpr)
        {
            // XPath 2.0 §3.9 — quantified expression: same scoping as for.
            ImmutableArray<ForClauseBinding>.Builder clauseBuilder =
                ImmutableArray.CreateBuilder<ForClauseBinding>(quantExpr.Clauses.Length);

            foreach (ForClause clause in quantExpr.Clauses)
            {
                ExpressionNode seqNode = CompileAst(clause.SequenceExpr);
                SequenceType bindingType = new SequenceType(
                    seqNode.StaticType.ItemType, Occurrence.ExactlyOne);
                int slot = _scope.PushBinding(clause.Variable, bindingType);
                clauseBuilder.Add(new ForClauseBinding(clause.Variable, slot, seqNode, bindingType));
            }

            ExpressionNode satisfiesNode = CompileAst(quantExpr.Satisfies);

            for (int i = 0; i < quantExpr.Clauses.Length; i++)
            {
                _scope.PopBinding();
            }

            return new QuantifiedNode(quantExpr.Span, quantExpr.Kind,
                clauseBuilder.MoveToImmutable(), satisfiesNode);
        }

        private IfNode CompileIf(IfExpr ifExpr)
        {
            ExpressionNode test = CompileAst(ifExpr.Test);
            ExpressionNode then = CompileAst(ifExpr.Then);
            ExpressionNode else_ = CompileAst(ifExpr.Else);
            return new IfNode(ifExpr.Span, test, then, else_);
        }

        // ── Logical ────────────────────────────────────────────────────────────

        private OrNode CompileOr(OrExpr orExpr)
        {
            ExpressionNode left = CompileAst(orExpr.Left);
            ExpressionNode right = CompileAst(orExpr.Right);
            return new OrNode(orExpr.Span, left, right);
        }

        private AndNode CompileAnd(AndExpr andExpr)
        {
            ExpressionNode left = CompileAst(andExpr.Left);
            ExpressionNode right = CompileAst(andExpr.Right);
            return new AndNode(andExpr.Span, left, right);
        }

        // ── Comparison ─────────────────────────────────────────────────────────

        private ExpressionNode CompileComparison(ComparisonExpr cmp)
        {
            ExpressionNode left = CompileAst(cmp.Left);
            ExpressionNode right = CompileAst(cmp.Right);

            switch (cmp.Op)
            {
                // General comparisons
                case ComparisonOp.Equals:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.Equals, left, right);
                case ComparisonOp.NotEquals:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.NotEquals, left, right);
                case ComparisonOp.LessThan:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.LessThan, left, right);
                case ComparisonOp.LessThanOrEqual:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.LessThanOrEqual, left, right);
                case ComparisonOp.GreaterThan:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.GreaterThan, left, right);
                case ComparisonOp.GreaterThanOrEqual:
                    return new GeneralComparisonNode(cmp.Span, GeneralComparisonOp.GreaterThanOrEqual, left, right);

                // Value comparisons
                case ComparisonOp.Eq:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Eq, left, right);
                case ComparisonOp.Ne:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Ne, left, right);
                case ComparisonOp.Lt:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Lt, left, right);
                case ComparisonOp.Le:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Le, left, right);
                case ComparisonOp.Gt:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Gt, left, right);
                case ComparisonOp.Ge:
                    return new ValueComparisonNode(cmp.Span, ValueComparisonOp.Ge, left, right);

                // Node identity / order
                case ComparisonOp.Is:
                    return new NodeComparisonNode(cmp.Span, NodeComparisonOp.Is, left, right);
                case ComparisonOp.Precedes:
                    return new NodeComparisonNode(cmp.Span, NodeComparisonOp.Precedes, left, right);
                case ComparisonOp.Follows:
                    return new NodeComparisonNode(cmp.Span, NodeComparisonOp.Follows, left, right);

                default:
                    throw new XPathStaticException(
                        XPathErrors.XPST0003,
                        "Unknown comparison operator: " + cmp.Op,
                        cmp.Span);
            }
        }

        // ── Arithmetic and range ───────────────────────────────────────────────

        private RangeNode CompileRange(RangeExpr range)
        {
            ExpressionNode left = CompileAst(range.Left);
            ExpressionNode right = CompileAst(range.Right);
            return new RangeNode(range.Span, left, right);
        }

        private ArithmeticNode CompileArithmetic(ArithmeticExpr arith)
        {
            ExpressionNode left = CompileAst(arith.Left);
            ExpressionNode right = CompileAst(arith.Right);
            // Reuse the ArithmeticOp enum directly from the AST layer.
            return new ArithmeticNode(arith.Span, arith.Op, left, right);
        }

        // ── Set operations ─────────────────────────────────────────────────────

        private SetOpNode CompileSet(SetExpr set)
        {
            ExpressionNode left = CompileAst(set.Left);
            ExpressionNode right = CompileAst(set.Right);
            ExpressionNodes.SetOp op;
            if (set.Op == Ast.SetOp.Union)
            {
                op = ExpressionNodes.SetOp.Union;
            }
            else if (set.Op == Ast.SetOp.Intersect)
            {
                op = ExpressionNodes.SetOp.Intersect;
            }
            else if (set.Op == Ast.SetOp.Except)
            {
                op = ExpressionNodes.SetOp.Except;
            }
            else
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0003,
                    "Unknown set operator: " + set.Op,
                    set.Span);
            }

            return new SetOpNode(set.Span, op, left, right);
        }

        // ── Type operations ────────────────────────────────────────────────────

        private InstanceOfNode CompileInstanceOf(InstanceOfExpr instOf)
        {
            ExpressionNode expr = CompileAst(instOf.Expr);
            return new InstanceOfNode(instOf.Span, expr, instOf.TargetType);
        }

        private TreatNode CompileTreat(TreatExpr treat)
        {
            ExpressionNode expr = CompileAst(treat.Expr);
            return new TreatNode(treat.Span, expr, treat.TargetType);
        }

        private CastableNode CompileCastable(CastableExpr castable)
        {
            ExpressionNode expr = CompileAst(castable.Expr);
            bool allowEmpty = castable.TargetType.Occurrence == Occurrence.ZeroOrOne;
            return new CastableNode(castable.Span, expr, castable.TargetType, allowEmpty);
        }

        private CastNode CompileCast(CastExpr cast)
        {
            ExpressionNode expr = CompileAst(cast.Expr);
            bool allowEmpty = cast.TargetType.Occurrence == Occurrence.ZeroOrOne;
            return new CastNode(cast.Span, expr, cast.TargetType, allowEmpty);
        }

        // ── Unary ──────────────────────────────────────────────────────────────

        private UnaryNode CompileUnary(UnaryExpr unary)
        {
            ExpressionNode operand = CompileAst(unary.Operand);
            ExpressionNodes.UnaryOp op = unary.Op == Ast.UnaryOp.Negate
                ? ExpressionNodes.UnaryOp.Negate
                : ExpressionNodes.UnaryOp.Plus;
            return new UnaryNode(unary.Span, op, operand);
        }

        // ── Path ───────────────────────────────────────────────────────────────

        private PathNode CompilePath(PathExpr path)
        {
            ImmutableArray<ExpressionNode>.Builder builder =
                ImmutableArray.CreateBuilder<ExpressionNode>(path.Steps.Length);
            foreach (XPathAst step in path.Steps)
            {
                builder.Add(CompileAst(step));
            }

            return new PathNode(path.Span, path.Anchor, builder.MoveToImmutable());
        }

        private AxisStepNode CompileAxisStep(AxisStep step)
        {
            ImmutableArray<ExpressionNode>.Builder predBuilder =
                ImmutableArray.CreateBuilder<ExpressionNode>(step.Predicates.Length);
            foreach (XPathAst pred in step.Predicates)
            {
                predBuilder.Add(CompileAst(pred));
            }

            return new AxisStepNode(step.Span, step.Axis, step.Test, predBuilder.MoveToImmutable());
        }

        private FilterNode CompileFilter(FilterExpr filter)
        {
            ExpressionNode primary = CompileAst(filter.Primary);
            ImmutableArray<ExpressionNode>.Builder predBuilder =
                ImmutableArray.CreateBuilder<ExpressionNode>(filter.Predicates.Length);
            foreach (XPathAst pred in filter.Predicates)
            {
                predBuilder.Add(CompileAst(pred));
            }

            return new FilterNode(filter.Span, primary, predBuilder.MoveToImmutable());
        }

        // ── Function calls ─────────────────────────────────────────────────────

        private FunctionCallNode CompileFunctionCall(FunctionCall call)
        {
            // XPath 2.0 §2.3.3 — XPST0017: unknown function or arity mismatch.
            int arity = call.Arguments.Length;
            if (!_functions.TryResolve(call.Name, arity, out FunctionSignature? signature))
            {
                throw new XPathStaticException(
                    XPathErrors.XPST0017,
                    "Unknown function '" + FormatQName(call.Name) + "' with arity " + arity,
                    call.Span);
            }

            ImmutableArray<ExpressionNode>.Builder argBuilder =
                ImmutableArray.CreateBuilder<ExpressionNode>(call.Arguments.Length);
            foreach (XPathAst arg in call.Arguments)
            {
                argBuilder.Add(CompileAst(arg));
            }

            return new FunctionCallNode(call.Span, signature!, argBuilder.MoveToImmutable());
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Formats a QName for error messages (prefix:local or local).</summary>
        private static string FormatQName(QName name)
        {
            if (name.Prefix.Length > 0)
            {
                return name.Prefix + ":" + name.LocalName;
            }

            return name.LocalName;
        }
    }

    // ── CompileScope ───────────────────────────────────────────────────────────

    /// <summary>
    /// A single variable binding entry in the compile-time scope stack.
    /// Uses a class rather than a ValueTuple to avoid the net462 ValueTuple dependency.
    /// </summary>
    private sealed class ScopeEntry
    {
        /// <summary>Initializes a scope entry.</summary>
        internal ScopeEntry(QName name, int slot, SequenceType type)
        {
            Name = name;
            Slot = slot;
            Type = type;
        }

        /// <summary>Gets the variable name.</summary>
        internal QName Name { get; }

        /// <summary>Gets the local-frame slot index.</summary>
        internal int Slot { get; }

        /// <summary>Gets the declared sequence type.</summary>
        internal SequenceType Type { get; }
    }

    /// <summary>
    /// Stack of in-scope variable bindings. Threaded through the recursive-descent compilation.
    /// Not thread-safe — one instance per <see cref="CompilerState"/>.
    /// </summary>
    private sealed class CompileScope
    {
        private readonly List<ScopeEntry> _bindings = new List<ScopeEntry>();
        private readonly SlotAllocator _slots;

        /// <summary>Initializes a scope backed by the given slot allocator.</summary>
        internal CompileScope(SlotAllocator slots)
        {
            _slots = slots;
        }

        /// <summary>
        /// Reserves a new slot and pushes a binding for <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="type">The declared sequence type for the variable.</param>
        /// <returns>The allocated slot index.</returns>
        internal int PushBinding(QName name, SequenceType type)
        {
            int slot = _slots.Allocate();
            _bindings.Add(new ScopeEntry(name, slot, type));
            return slot;
        }

        /// <summary>Removes the most recently pushed binding.</summary>
        internal void PopBinding()
        {
            _bindings.RemoveAt(_bindings.Count - 1);
        }

        /// <summary>
        /// Returns the innermost binding for <paramref name="name"/>, or
        /// <see langword="null"/> when none is in scope.
        /// </summary>
        /// <param name="name">The variable name to look up.</param>
        /// <returns>The matching entry, or <see langword="null"/>.</returns>
        internal ScopeEntry? Lookup(QName name)
        {
            // XPath 2.0 §2.1.1 — innermost binding shadows outer ones.
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (_bindings[i].Name.Equals(name))
                {
                    return _bindings[i];
                }
            }

            return null;
        }
    }
}
