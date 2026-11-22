// XPath 2.0 §3.2 — primary expressions: literals, variable references, context item, etc.

using System;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-03): implement Iterate for LiteralNode, ContextItemNode, EmptySequenceNode,
// SequenceConcatNode, VariableRefNode, FunctionCallNode

/// <summary>
/// A compiled literal value — xs:integer, xs:decimal, xs:double, or xs:string.
/// Static type is the exact atomic singleton (e.g. xs:integer).
/// </summary>
internal sealed class LiteralNode : ExpressionNode
{
    /// <summary>Initializes a literal node wrapping <paramref name="value"/>.</summary>
    /// <param name="location">Source span of the literal.</param>
    /// <param name="value">The XDM atomic value produced by this literal.</param>
    internal LiteralNode(SourceSpan location, XdmAtomicValue value)
        : base(location,
               new SequenceType(ItemType.Atomic(value.TypeCode), Occurrence.ExactlyOne),
               ExpressionDependencies.None)
    {
        Value = value;
    }

    /// <summary>Gets the constant atomic value.</summary>
    internal XdmAtomicValue Value { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-03): implement Iterate
        throw new NotImplementedException("Evaluation for LiteralNode is implemented in P2-03.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string typeName = "xs:" + CastTable.GetXsLocalName(Value.TypeCode);
        return "LiteralNode(" + typeName + ", " + Value.GetStringValue() + ")";
    }
}

/// <summary>
/// The context-item expression <c>.</c>.
/// Static type is <c>item()</c>; depends on <see cref="ExpressionDependencies.ContextItem"/>.
/// </summary>
internal sealed class ContextItemNode : ExpressionNode
{
    /// <summary>Initializes a context-item node at the given source location.</summary>
    /// <param name="location">Source span of the <c>.</c> token.</param>
    internal ContextItemNode(SourceSpan location)
        : base(location,
               new SequenceType(ItemType.AnyItem, Occurrence.ExactlyOne),
               ExpressionDependencies.ContextItem)
    {
    }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-03): implement Iterate
        throw new NotImplementedException("Evaluation for ContextItemNode is implemented in P2-03.");
    }

    /// <inheritdoc/>
    public override string ToDebugString() => "ContextItemNode";
}

/// <summary>
/// The empty sequence <c>()</c>. Static type is <c>empty-sequence()</c>.
/// </summary>
internal sealed class EmptySequenceNode : ExpressionNode
{
    /// <summary>Initializes an empty-sequence node at the given source location.</summary>
    /// <param name="location">Source span of the <c>()</c> token.</param>
    internal EmptySequenceNode(SourceSpan location)
        : base(location, SequenceType.EmptySequence, ExpressionDependencies.None)
    {
    }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-03): implement Iterate
        throw new NotImplementedException("Evaluation for EmptySequenceNode is implemented in P2-03.");
    }

    /// <inheritdoc/>
    public override string ToDebugString() => "EmptySequenceNode";
}

/// <summary>
/// Concatenation of two or more sub-expressions into a single sequence (comma operator).
/// Static type is <c>item()*</c> (over-approximate).
/// </summary>
internal sealed class SequenceConcatNode : ExpressionNode
{
    /// <summary>
    /// Initializes a sequence-concat node.
    /// </summary>
    /// <param name="location">Source span of the full sequence expression.</param>
    /// <param name="items">The sub-expressions to concatenate; must have at least two entries.</param>
    internal SequenceConcatNode(SourceSpan location, ImmutableArray<ExpressionNode> items)
        : base(location,
               SequenceType.AnySequence,
               MergeDependencies(items))
    {
        Items = items;
    }

    /// <summary>Gets the sub-expressions concatenated by this node.</summary>
    internal ImmutableArray<ExpressionNode> Items { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-03): implement Iterate
        throw new NotImplementedException("Evaluation for SequenceConcatNode is implemented in P2-03.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("SequenceConcatNode(").Append(Items.Length).AppendLine(" items)");
        foreach (ExpressionNode item in Items)
        {
            AppendIndented(sb, item.ToDebugString(), 1);
        }
        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies MergeDependencies(ImmutableArray<ExpressionNode> items)
    {
        ExpressionDependencies deps = ExpressionDependencies.None;
        foreach (ExpressionNode item in items)
        {
            deps |= item.Dependencies;
        }
        return deps;
    }
}

/// <summary>
/// A compiled variable reference <c>$name</c>. The slot index is assigned by the compiler.
/// </summary>
internal sealed class VariableRefNode : ExpressionNode
{
    /// <summary>
    /// Initializes a variable-reference node.
    /// </summary>
    /// <param name="location">Source span of the <c>$name</c> token.</param>
    /// <param name="name">The expanded variable name.</param>
    /// <param name="slotIndex">The local-frame slot index assigned at compile time.</param>
    /// <param name="declaredType">The static type of the variable as declared at its binding site.</param>
    internal VariableRefNode(SourceSpan location, QName name, int slotIndex, SequenceType declaredType)
        : base(location, declaredType, ExpressionDependencies.LocalVariables)
    {
        Name = name;
        SlotIndex = slotIndex;
    }

    /// <summary>Gets the expanded variable name.</summary>
    internal QName Name { get; }

    /// <summary>Gets the local-frame slot index assigned at compile time.</summary>
    internal int SlotIndex { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-03): implement Iterate
        throw new NotImplementedException("Evaluation for VariableRefNode is implemented in P2-03.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
        => "VariableRefNode(" + FormatQName(Name) + "@slot" + SlotIndex + ")";
}

/// <summary>
/// A compiled function call. Static type is <see cref="FunctionSignature.ReturnType"/>.
/// </summary>
internal sealed class FunctionCallNode : ExpressionNode
{
    /// <summary>
    /// Initializes a function-call node.
    /// </summary>
    /// <param name="location">Source span of the function call expression.</param>
    /// <param name="signature">The resolved compile-time function signature.</param>
    /// <param name="arguments">The compiled argument expressions.</param>
    internal FunctionCallNode(SourceSpan location, FunctionSignature signature,
                               ImmutableArray<ExpressionNode> arguments)
        : base(location,
               signature.ReturnType,
               MergeDependencies(arguments))
    {
        Signature = signature;
        Arguments = arguments;
    }

    /// <summary>Gets the resolved function signature.</summary>
    internal FunctionSignature Signature { get; }

    /// <summary>Gets the compiled argument expressions.</summary>
    internal ImmutableArray<ExpressionNode> Arguments { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-08): implement Iterate for built-in functions
        throw new NotImplementedException("Evaluation for FunctionCallNode is implemented in P2-08.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("FunctionCallNode(").Append(FormatQName(Signature.Name)).AppendLine(")");
        foreach (ExpressionNode arg in Arguments)
        {
            AppendIndented(sb, arg.ToDebugString(), 1);
        }
        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies MergeDependencies(ImmutableArray<ExpressionNode> args)
    {
        ExpressionDependencies deps = ExpressionDependencies.None;
        foreach (ExpressionNode arg in args)
        {
            deps |= arg.Dependencies;
        }
        return deps;
    }
}
