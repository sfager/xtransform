// XPath 2.0 §3.2 — path expressions; §3.2.1 — steps; §3.2.3 — predicates; §3.2.4 — filter expressions.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath.Ast;
using XTransform.XPath.Operators;

namespace XTransform.XPath.ExpressionNodes;

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
        // XPath 2.0 §3.2 — path expression evaluation

        // Determine the input iterator based on the path anchor.
        IXdmIterator input;

        switch (Anchor)
        {
            case PathAnchor.AbsoluteSlash:
            {
                // /step1/step2 — input is the document root node of the context item's tree.
                XdmNode root = GetDocumentRoot(context);
                input = new SingleItemIterator(root);
                break;
            }

            case PathAnchor.AbsoluteDoubleSlash:
            {
                // //step1 — equivalent to /descendant-or-self::node()/step1
                // Start from the document root and emit all nodes in the subtree (descendant-or-self).
                XdmNode root = GetDocumentRoot(context);
                input = root.Handle.Tree.Iterate(root.Handle, Axis.DescendantOrSelf, null);
                break;
            }

            default:
            {
                // Relative path — input is the context item (must be a node).
                if (context.ContextItem is null)
                {
                    throw new XPathDynamicException(
                        XPathErrors.XPDY0002,
                        "The context item is undefined",
                        Location);
                }

                input = new SingleItemIterator(context.ContextItem);
                break;
            }
        }

        // Thread the input through each step in turn.
        // XPath 2.0 §3.2: each step takes a sequence of nodes and produces a new sequence.
        // Between steps we apply document-order deduplication when input had >1 node.
        for (int i = 0; i < Steps.Length; i++)
        {
            ExpressionNode step = Steps[i];
            bool isLastStep = i == Steps.Length - 1;

            // Peek whether we have a single-item input or multiple items — by consuming
            // the first item and checking for a second.
            XdmItem? first = input.Next();
            if (first is null)
            {
                // No input → empty result for this and all remaining steps.
                return EmptyIterator.Instance;
            }

            XdmItem? second = input.Next();

            if (second is null)
            {
                // Single input item — evaluate step lazily (no dedup needed).
                if (first is not XdmNode firstNode)
                {
                    throw new XPathDynamicException(
                        XPathErrors.XPTY0004,
                        "A path step is being applied to an atomic value",
                        step.Location);
                }

                DynamicContext stepCtx = context.WithContextItem(firstNode, 1, 1);
                input = step.Iterate(stepCtx);
            }
            else
            {
                // Multiple input nodes — buffer all step outputs, deduplicate, sort.
                // This is required by XPath 2.0 §3.2: the result of a path is in document order
                // with no duplicates regardless of how many input nodes produced output.
                List<XdmNode> allOutput = new List<XdmNode>();

                // Process first item (already consumed).
                AppendStepOutput(first, step, context, allOutput);

                // Process second item (already consumed).
                AppendStepOutput(second, step, context, allOutput);

                // Process remaining items.
                XdmItem? more;
                while ((more = input.Next()) is not null)
                {
                    AppendStepOutput(more, step, context, allOutput);
                }

                // Deduplicate and sort by document order.
                List<XdmNode> sorted = DeduplicateAndSort(allOutput);

                // For the final step the caller will consume the result lazily via ArrayIterator.
                input = new ArrayIterator(sorted.ToArray());
            }
        }

        return input;
    }

    /// <summary>
    /// Evaluates one step with <paramref name="inputItem"/> as the context node and
    /// appends any node results to <paramref name="output"/>.
    /// </summary>
    private static void AppendStepOutput(
        XdmItem inputItem,
        ExpressionNode step,
        DynamicContext parentContext,
        List<XdmNode> output)
    {
        if (inputItem is not XdmNode inputNode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "A path step is being applied to an atomic value",
                step.Location);
        }

        DynamicContext stepCtx = parentContext.WithContextItem(inputNode, 1, 1);
        IXdmIterator stepResult = step.Iterate(stepCtx);
        XdmItem? item;
        while ((item = stepResult.Next()) is not null)
        {
            if (item is XdmNode node)
            {
                output.Add(node);
            }
            else
            {
                throw new XPathDynamicException(
                    XPathErrors.XPTY0004,
                    "A path step produced an atomic value; steps must yield nodes",
                    step.Location);
            }
        }
    }

    /// <summary>
    /// Returns a de-duplicated list of nodes sorted in document order.
    /// </summary>
    private static List<XdmNode> DeduplicateAndSort(List<XdmNode> nodes)
    {
        // Insertion-sort-based dedup: check each candidate against the growing result.
        // For typical small result sets (a handful of nodes) this is fine.
        // For large sets a HashSet + Sort would be faster; can be changed if profiling warrants.
        List<XdmNode> result = new List<XdmNode>(nodes.Count);

        foreach (XdmNode node in nodes)
        {
            bool found = false;
            foreach (XdmNode existing in result)
            {
                if (node.Equals(existing))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result.Add(node);
            }
        }

        // Sort by document order.
        result.Sort((a, b) => a.CompareDocumentOrder(b));
        return result;
    }

    /// <summary>
    /// Returns the document root node for the context item.  For absolute paths the
    /// root is always index 0 in the TinyTree (the document node per builder convention).
    /// </summary>
    private XdmNode GetDocumentRoot(DynamicContext context)
    {
        if (context.ContextItem is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "The context item is undefined; cannot evaluate an absolute path expression",
                Location);
        }

        if (context.ContextItem is not XdmNode contextNode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "The context item is not a node; cannot evaluate an absolute path expression",
                Location);
        }

        // TinyTree index 0 is always the document node (see TinyTreeBuilder convention P1-08).
        NodeHandle docHandle = NodeHandle.ForNode(contextNode.Handle.Tree, 0);
        return new XdmNode(docHandle);
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
        // XPath 2.0 §3.2.1 — axis steps

        // The context item must be a node.  When called from PathNode it is always a node
        // because PathNode checks before calling us; when called standalone (e.g. in test),
        // we need to validate here too.
        if (context.ContextItem is null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPDY0002,
                "The context item is undefined",
                Location);
        }

        if (context.ContextItem is not XdmNode contextNode)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "A path step is being applied to an atomic value",
                Location);
        }

        // Resolve the NodeTestAst into a runtime NodeTest against this node's tree.
        NodeTest resolvedTest = NodeTestResolver.Resolve(Test, contextNode.Handle.Tree.NamePool, Axis);

        // Delegate to TinyTree to produce the axis iterator.
        IXdmIterator axisResult = contextNode.Handle.Tree.Iterate(contextNode.Handle, Axis, resolvedTest);

        // Apply predicates in order.  Each predicate wraps the previous iterator lazily.
        // XPath 2.0 §2.5.4 — predicates filter the principal node list of the step.
        foreach (ExpressionNode predicate in Predicates)
        {
            axisResult = PredicateApplicator.Apply(axisResult, predicate, context);
        }

        return axisResult;
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
        // XPath 2.0 §3.2.4 — filter expressions
        // The primary is evaluated; then each predicate is applied in turn (lazily).
        IXdmIterator result = Primary.Iterate(context);

        foreach (ExpressionNode predicate in Predicates)
        {
            result = PredicateApplicator.Apply(result, predicate, context);
        }

        return result;
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
