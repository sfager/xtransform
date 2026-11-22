// XPath 2.0 §3.2 — primary expressions: literals, variable references, context item, etc.

using System;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;

namespace XTransform.XPath.ExpressionNodes;


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
        // Literal values are context-independent; produce a one-item iterator.
        return new SingleItemIterator(Value);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context) => Value;

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
        XdmItem? item = EvaluateItem(context);
        return item is null ? EmptyIterator.Instance : new SingleItemIterator(item);
    }

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context)
    {
        // XPath 2.0 §2.1.2 — reading the context item when none is in scope raises XPDY0002.
        XdmItem? item = context.ContextItem;
        if (item is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "The context item is undefined",
                Location);
        }
        return item;
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
    public override IXdmIterator Iterate(DynamicContext context) => EmptyIterator.Instance;

    /// <inheritdoc/>
    public override XdmItem? EvaluateItem(DynamicContext context) => null;

    /// <inheritdoc/>
    public override bool EffectiveBooleanValue(DynamicContext context) => false;

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
        return new ExpressionConcatIterator(Items, context);
    }

    /// <summary>
    /// Pull-style concatenation of expression sub-iterators. Each child expression's
    /// <see cref="ExpressionNode.Iterate"/> is only invoked when the previous child has
    /// been fully consumed, so a consumer that stops after N items only evaluates the
    /// children producing those N items.
    /// </summary>
    private sealed class ExpressionConcatIterator : IXdmIterator
    {
        private readonly ImmutableArray<ExpressionNode> _nodes;
        private readonly DynamicContext _context;
        private int _nodeIndex;
        private IXdmIterator? _currentInner;
        private int _position;

        public ExpressionConcatIterator(ImmutableArray<ExpressionNode> nodes, DynamicContext context)
        {
            _nodes = nodes;
            _context = context;
        }

        public int Position => _position;

        public XdmItem? Next()
        {
            while (true)
            {
                if (_currentInner is null)
                {
                    if (_nodeIndex >= _nodes.Length)
                    {
                        return null;
                    }
                    _currentInner = _nodes[_nodeIndex].Iterate(_context);
                    _nodeIndex++;
                }
                XdmItem? item = _currentInner.Next();
                if (item is not null)
                {
                    _position++;
                    return item;
                }
                _currentInner = null;
            }
        }

        public IXdmIterator Clone()
        {
            return new ExpressionConcatIterator(_nodes, _context);
        }
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
        // Variable references read from the local-variable frame set up by the enclosing
        // for / quantified clause (or by the caller for externally-bound variables).
        // The compiler guarantees SlotIndex is in range.
        return context.LocalFrame[SlotIndex].GetIterator();
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
               MergeDependencies(arguments) | signature.CallDependencies)
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
        // F&O §3.1.5 — apply function-conversion rules to each argument, then invoke.
        XdmSequence[] converted = new XdmSequence[Arguments.Length];
        for (int i = 0; i < Arguments.Length; i++)
        {
            IXdmIterator raw = Arguments[i].Iterate(context);
            converted[i] = FunctionConversion.Apply(raw, Signature.ParameterTypes[i], Location);
        }

        return Signature.Invoker(context, converted);
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
