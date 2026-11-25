// XSLT 2.0 §5.5.3 — the predicates of pattern steps, compiled once per stylesheet.
//
// A pattern is an XPath-like grammar (PatternAst); its predicates are ordinary XPath expressions.
// They are compiled when the stylesheet is compiled, against the function library and the global
// variables only (a pattern cannot refer to local variables), and looked up by the step they belong to
// while matching. Steps are keyed by reference: two textually identical steps in different patterns
// may live in different namespace contexts.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

/// <summary>Compiled predicates of the step patterns in a stylesheet, plus the parsed patterns of its keys.</summary>
internal sealed class PatternPredicateTable
{
    private readonly IReadOnlyCollection<QName> _globalNames;
    private readonly Dictionary<StepPatternAst, BoundExpression[]> _bySteps =
        new Dictionary<StepPatternAst, BoundExpression[]>(ReferenceComparer.Instance);

    private readonly Dictionary<KeyDecl, PatternAst> _keyPatterns = new Dictionary<KeyDecl, PatternAst>();

    /// <summary>Initializes an empty table; <paramref name="globalNames"/> are the variables a predicate may use.</summary>
    public PatternPredicateTable(IReadOnlyCollection<QName> globalNames)
    {
        _globalNames = globalNames;
    }

    /// <summary>
    /// Compiles every predicate of <paramref name="pattern"/> (all alternatives and steps) and records it.
    /// Errors go to <paramref name="bag"/>.
    /// </summary>
    public void Register(
        PatternAst pattern,
        IStaticNamespaceResolver namespaces,
        FunctionLibrary functions,
        SourceSpan location,
        DiagnosticBag bag)
    {
        switch (pattern)
        {
            case UnionPatternAst union:
                foreach (PatternAst alternative in union.Alternatives)
                {
                    Register(alternative, namespaces, functions, location, bag);
                }

                break;

            case PathPatternAst path:
                foreach (StepPatternAst step in path.Steps)
                {
                    RegisterStep(step, namespaces, functions, location, bag);
                }

                break;

            case IdKeyWithPathAst tail:
                foreach (StepPatternAst step in tail.TailSteps)
                {
                    RegisterStep(step, namespaces, functions, location, bag);
                }

                break;
        }
    }

    /// <summary>The compiled predicates of <paramref name="step"/> in order, or null when it has none registered.</summary>
    public BoundExpression[]? Get(StepPatternAst step) =>
        _bySteps.TryGetValue(step, out BoundExpression[]? compiled) ? compiled : null;

    /// <summary>Records the parsed <c>match</c> pattern of a key declaration.</summary>
    public void SetKeyPattern(KeyDecl key, PatternAst pattern) => _keyPatterns[key] = pattern;

    /// <summary>The parsed <c>match</c> pattern of <paramref name="key"/>, or null when none was recorded.</summary>
    public PatternAst? GetKeyPattern(KeyDecl key) => _keyPatterns.TryGetValue(key, out PatternAst? p) ? p : null;

    private void RegisterStep(
        StepPatternAst step,
        IStaticNamespaceResolver namespaces,
        FunctionLibrary functions,
        SourceSpan location,
        DiagnosticBag bag)
    {
        if (step.Predicates.IsEmpty || _bySteps.ContainsKey(step))
        {
            return;
        }

        XsltCompileScope scope = XsltCompileScope.WithGlobals(_globalNames);
        BoundExpression[] compiled = new BoundExpression[step.Predicates.Length];
        for (int i = 0; i < compiled.Length; i++)
        {
            try
            {
                CompiledExpression ce = XPathCompiler.Compile(
                    step.Predicates[i], namespaces, functions, scope.AsExternalVariables());
                compiled[i] = new BoundExpression(ce.Root, ce.SlotCount, new List<QName>(scope.Names));
            }
            catch (XTransformException ex)
            {
                bag.Error(ex.ErrorCode, ex.Message, location);
                return;
            }
        }

        _bySteps[step] = compiled;
    }

    private sealed class ReferenceComparer : IEqualityComparer<StepPatternAst>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public bool Equals(StepPatternAst? x, StepPatternAst? y) => ReferenceEquals(x, y);

        public int GetHashCode(StepPatternAst obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
