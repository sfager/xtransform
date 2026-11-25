// XPath 2.0 §2.1.1 — static context: function signatures and resolution

using System.Collections.Immutable;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// Invokes a built-in or user function at runtime with a pre-converted argument list.
/// Arguments have already been subject to function-conversion rules (F&amp;O §3.1.5).
/// </summary>
/// <param name="context">The dynamic evaluation context.</param>
/// <param name="arguments">Pre-evaluated and converted arguments, one entry per parameter.</param>
/// <returns>An iterator over the function result sequence.</returns>
internal delegate IXdmIterator FunctionInvoker(DynamicContext context, XdmSequence[] arguments);

/// <summary>
/// Compile-time metadata for a declared function. Used by the compiler to
/// type-check calls and bind <see cref="Ast.FunctionCall"/> AST nodes.
/// The <see cref="Invoker"/> is called at runtime by <c>FunctionCallNode.Iterate</c>.
/// </summary>
internal sealed record FunctionSignature(
    QName Name,
    int Arity,
    SequenceType ReturnType,
    ImmutableArray<SequenceType> ParameterTypes,
    FunctionInvoker Invoker)
{
    /// <summary>
    /// Dynamic-context dependencies introduced by the function itself
    /// (independent of any argument expressions). For example, fn:position() reads
    /// <see cref="ExpressionDependencies.Position"/> and fn:last() reads
    /// <see cref="ExpressionDependencies.Last"/>. Defaults to <see cref="ExpressionDependencies.None"/>.
    /// </summary>
    public ExpressionDependencies CallDependencies { get; init; } = ExpressionDependencies.None;

    /// <summary>
    /// For functions that interpret a lexical QName argument (<c>system-property</c>, <c>function-available</c>,
    /// <c>key</c>, ...): given the namespaces in scope at the call site, returns the invoker to use for that call.
    /// When null the <see cref="Invoker"/> is used as is.
    /// </summary>
    public System.Func<IStaticNamespaceResolver, FunctionInvoker>? BindStaticContext { get; init; }

    /// <summary>
    /// Creates a compile-time-only signature (no runtime invoker).
    /// Used by test helpers and the <see cref="AcceptAllFunctionLibrary"/> that compile
    /// but do not evaluate expressions containing function calls.
    /// </summary>
    public static FunctionSignature CompileTimeOnly(
        QName name,
        int arity,
        SequenceType returnType,
        ImmutableArray<SequenceType> parameterTypes)
        => new FunctionSignature(name, arity, returnType, parameterTypes, s_notImplementedInvoker);

    private static readonly FunctionInvoker s_notImplementedInvoker =
        static (_, _) => throw new System.InvalidOperationException(
            "No invoker registered for this compile-time-only function signature.");
}

/// <summary>
/// Resolves a function reference at compile time. Phase 2 tasks beyond P2-02
/// implement concrete subclasses (built-ins, extension-function registry,
/// xsl:function). For P2-02 we only need the interface and a trivial empty
/// implementation used by tests.
/// </summary>
internal abstract class FunctionLibrary
{
    /// <summary>Looks up a function by name and arity.</summary>
    /// <returns>
    /// <see langword="true"/> when found; <paramref name="signature"/> is set.
    /// </returns>
    public abstract bool TryResolve(QName name, int arity, out FunctionSignature? signature);
}

/// <summary>
/// Empty library — every lookup returns false. Used by compiler tests that do not call functions.
/// </summary>
internal sealed class EmptyFunctionLibrary : FunctionLibrary
{
    /// <summary>Shared singleton instance.</summary>
    public static EmptyFunctionLibrary Instance { get; } = new EmptyFunctionLibrary();

    private EmptyFunctionLibrary() { }

    /// <inheritdoc/>
    public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
    {
        signature = null;
        return false;
    }
}

/// <summary>
/// A function library that accepts any function call with any arity, returning
/// <c>item()*</c> as the return type and the most general parameter types.
/// Useful in tests that compile expressions containing function calls but do not
/// evaluate them.
/// </summary>
internal sealed class AcceptAllFunctionLibrary : FunctionLibrary
{
    /// <summary>Shared singleton instance.</summary>
    public static AcceptAllFunctionLibrary Instance { get; } = new AcceptAllFunctionLibrary();

    private AcceptAllFunctionLibrary() { }

    /// <inheritdoc/>
    public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
    {
        // Build a generic parameter list of xs:item()* for each argument.
        ImmutableArray<SequenceType>.Builder paramBuilder =
            ImmutableArray.CreateBuilder<SequenceType>(arity);
        for (int i = 0; i < arity; i++)
        {
            paramBuilder.Add(SequenceType.AnySequence);
        }

        signature = FunctionSignature.CompileTimeOnly(
            name,
            arity,
            SequenceType.AnySequence,
            paramBuilder.MoveToImmutable());
        return true;
    }
}

/// <summary>
/// Extension-function interface. Implementations register a function with a specific
/// name + arity + typed signature. The XPath compiler resolves calls against them when
/// configured on the <see cref="FunctionLibrary"/>.
/// </summary>
internal interface IExtensionFunction
{
    /// <summary>Gets the function signature (name, arity, parameter types, return type, invoker).</summary>
    internal FunctionSignature Signature { get; }
}
