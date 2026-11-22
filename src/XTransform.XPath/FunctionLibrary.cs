// XPath 2.0 §2.1.1 — static context: function signatures and resolution

using System.Collections.Immutable;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// Compile-time metadata for a declared function. Used by the compiler to
/// type-check calls and bind <see cref="Ast.FunctionCall"/> AST nodes.
/// </summary>
internal sealed record FunctionSignature(
    QName Name,
    int Arity,
    SequenceType ReturnType,
    ImmutableArray<SequenceType> ParameterTypes);

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

        signature = new FunctionSignature(
            name,
            arity,
            SequenceType.AnySequence,
            paramBuilder.MoveToImmutable());
        return true;
    }
}
