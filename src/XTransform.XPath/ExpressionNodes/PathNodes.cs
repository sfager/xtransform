// XPath 2.0 §3.2 — path expressions; §3.2.1 — steps; §3.2.3 — predicates; §3.2.4 — filter expressions.

using System;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath.Ast;

namespace XTransform.XPath.ExpressionNodes;

// TODO (P2-06): implement Iterate for PathNode, AxisStepNode, FilterNode

/// <summary>
/// A compiled path expression. Static type is <c>node()*</c> (over-approximate).
/// </summary>
internal sealed class PathNode : ExpressionNode
{
    private static readonly SequenceType s_nodesStar =
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrMore);

    /// <summary>Initializes a path-expression node.</summary>
    /// <param name="location">Source span of the full path expression.</param>
    /// <param name="anchor">How the path is anchored (relative, <c>/</c>, or <c>//</c>).</param>
    /// <param name="steps">The compiled steps in document order.</param>
    internal PathNode(SourceSpan location, PathAnchor anchor, ImmutableArray<ExpressionNode> steps)
        : base(location, s_nodesStar, MergeDependencies(steps, anchor))
    {
        Anchor = anchor;
        Steps = steps;
    }

    /// <summary>Gets the path anchor.</summary>
    internal PathAnchor Anchor { get; }

    /// <summary>Gets the compiled steps.</summary>
    internal ImmutableArray<ExpressionNode> Steps { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-06): implement Iterate
        throw new NotImplementedException("Evaluation for PathNode is implemented in P2-06.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string anchorStr = Anchor switch
        {
            PathAnchor.AbsoluteSlash => "/",
            PathAnchor.AbsoluteDoubleSlash => "//",
            _ => "relative",
        };

        StringBuilder sb = new StringBuilder();
        sb.Append("PathNode(").Append(anchorStr).AppendLine(")");
        foreach (ExpressionNode step in Steps)
        {
            AppendIndented(sb, step.ToDebugString(), 1);
        }

        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies MergeDependencies(
        ImmutableArray<ExpressionNode> steps, PathAnchor anchor)
    {
        ExpressionDependencies deps = ExpressionDependencies.None;
        if (anchor == PathAnchor.Relative)
        {
            deps |= ExpressionDependencies.ContextItem;
        }

        foreach (ExpressionNode step in steps)
        {
            deps |= step.Dependencies;
        }

        return deps;
    }
}

/// <summary>
/// A compiled axis step: an axis + node test + zero or more predicates.
/// Static type is <c>node()*</c> (over-approximate based on axis).
/// </summary>
internal sealed class AxisStepNode : ExpressionNode
{
    private static readonly SequenceType s_nodesStar =
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrMore);

    /// <summary>Initializes an axis-step node.</summary>
    /// <param name="location">Source span of the axis step.</param>
    /// <param name="axis">The axis being navigated.</param>
    /// <param name="test">The node test AST from the parser (not yet resolved to a runtime NodeTest).</param>
    /// <param name="predicates">The compiled predicate expressions.</param>
    internal AxisStepNode(SourceSpan location, Axis axis, NodeTestAst test,
                          ImmutableArray<ExpressionNode> predicates)
        : base(location, s_nodesStar, ComputeDependencies(predicates))
    {
        Axis = axis;
        Test = test;
        Predicates = predicates;
    }

    /// <summary>Gets the axis.</summary>
    internal Axis Axis { get; }

    /// <summary>
    /// Gets the node-test AST. The runtime <c>NodeTest</c> is built in P2-06 when the
    /// TinyTree and NamePool are available.
    /// </summary>
    internal NodeTestAst Test { get; }

    /// <summary>Gets the compiled predicate expressions.</summary>
    internal ImmutableArray<ExpressionNode> Predicates { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-06): implement Iterate
        throw new NotImplementedException("Evaluation for AxisStepNode is implemented in P2-06.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        string axisName = Axis switch
        {
            Axis.Child => "child",
            Axis.Descendant => "descendant",
            Axis.DescendantOrSelf => "descendant-or-self",
            Axis.Self => "self",
            Axis.Attribute => "attribute",
            Axis.Parent => "parent",
            Axis.Ancestor => "ancestor",
            Axis.AncestorOrSelf => "ancestor-or-self",
            Axis.FollowingSibling => "following-sibling",
            Axis.PrecedingSibling => "preceding-sibling",
            Axis.Following => "following",
            Axis.Preceding => "preceding",
            Axis.Namespace => "namespace",
            _ => Axis.ToString().ToLowerInvariant(),
        };

        StringBuilder sb = new StringBuilder();
        sb.Append("AxisStepNode(").Append(axisName).AppendLine(")");
        AppendIndented(sb, FormatNodeTest(Test), 1);
        foreach (ExpressionNode pred in Predicates)
        {
            AppendIndented(sb, "Predicate\n  " + pred.ToDebugString(), 1);
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatNodeTest(NodeTestAst test)
    {
        return test switch
        {
            AnyKindTestAst => "KindTest(node())",
            KindTestAst kt => FormatKindTest(kt),
            NameTestAst nt => FormatNameTest(nt),
            _ => test.GetType().Name,
        };
    }

    private static string FormatNameTest(NameTestAst nt)
    {
        return nt.Kind switch
        {
            NameTestKind.AnyName => "NameTest(*)",
            NameTestKind.PrefixWildcard => "NameTest(" + nt.Name!.Value.Prefix + ":*)",
            NameTestKind.LocalWildcard => "NameTest(*:" + nt.Name!.Value.LocalName + ")",
            _ => "NameTest(" + nt.Name!.Value.ToString() + ")",
        };
    }

    private static string FormatKindTest(KindTestAst kt)
    {
        string kindName = kt.Kind switch
        {
            XdmNodeKind.Element => "element",
            XdmNodeKind.Attribute => "attribute",
            XdmNodeKind.Text => "text",
            XdmNodeKind.Comment => "comment",
            XdmNodeKind.ProcessingInstruction => "processing-instruction",
            XdmNodeKind.Document => "document-node",
            XdmNodeKind.Namespace => "namespace-node",
            _ => kt.Kind.ToString().ToLowerInvariant(),
        };

        if (kt.ElementArg != null)
        {
            return "KindTest(" + kindName + "(" + FormatNodeTest(kt.ElementArg) + "))";
        }

        if (kt.NameArg.HasValue)
        {
            return "KindTest(" + kindName + "(" + FormatQName(kt.NameArg.Value) + "))";
        }

        return "KindTest(" + kindName + "())";
    }

    private static ExpressionDependencies ComputeDependencies(ImmutableArray<ExpressionNode> predicates)
    {
        ExpressionDependencies deps = ExpressionDependencies.ContextItem;
        foreach (ExpressionNode pred in predicates)
        {
            deps |= pred.Dependencies;
        }

        return deps;
    }
}

/// <summary>
/// A compiled filter expression: a primary expression with one or more predicates.
/// Static type is the primary's type with occurrence widened to <c>*</c>.
/// </summary>
internal sealed class FilterNode : ExpressionNode
{
    /// <summary>Initializes a filter-expression node.</summary>
    /// <param name="location">Source span of the full filter expression.</param>
    /// <param name="primary">The compiled primary expression.</param>
    /// <param name="predicates">The compiled predicate expressions.</param>
    internal FilterNode(SourceSpan location, ExpressionNode primary,
                        ImmutableArray<ExpressionNode> predicates)
        : base(location,
               new SequenceType(primary.StaticType.ItemType, Occurrence.ZeroOrMore),
               ComputeDependencies(primary, predicates))
    {
        Primary = primary;
        Predicates = predicates;
    }

    /// <summary>Gets the primary expression.</summary>
    internal ExpressionNode Primary { get; }

    /// <summary>Gets the predicate expressions.</summary>
    internal ImmutableArray<ExpressionNode> Predicates { get; }

    /// <inheritdoc/>
    public override IXdmIterator Iterate(DynamicContext context)
    {
        // TODO (P2-06): implement Iterate
        throw new NotImplementedException("Evaluation for FilterNode is implemented in P2-06.");
    }

    /// <inheritdoc/>
    public override string ToDebugString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("FilterNode");
        AppendIndented(sb, Primary.ToDebugString(), 1);
        foreach (ExpressionNode pred in Predicates)
        {
            AppendIndented(sb, "Predicate\n  " + pred.ToDebugString(), 1);
        }

        return sb.ToString().TrimEnd();
    }

    private static ExpressionDependencies ComputeDependencies(
        ExpressionNode primary, ImmutableArray<ExpressionNode> predicates)
    {
        ExpressionDependencies deps = primary.Dependencies;
        foreach (ExpressionNode pred in predicates)
        {
            deps |= pred.Dependencies;
        }

        return deps;
    }
}
