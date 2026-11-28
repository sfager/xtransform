using System;
using System.Collections.Generic;

namespace XTransform.Xdm;

/// <summary>
/// Walks a <see cref="TinyTree"/> in document order and emits the corresponding stream of
/// <see cref="IResultReceiver"/> events. Round-tripping a tree through
/// <see cref="TinyTreeEmitter"/> → <see cref="TreeBuilder"/> yields a deep-equal copy.
/// </summary>
internal static class TinyTreeEmitter
{
    /// <summary>
    /// Emits the subtree rooted at <paramref name="startIndex"/> in document order. For the
    /// document node this produces the whole tree; for an element it produces a single
    /// element event stream (no enclosing document markers).
    /// </summary>
    public static void EmitSubtree(TinyTree tree, int startIndex, IResultReceiver sink)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (sink is null)
        {
            throw new ArgumentNullException(nameof(sink));
        }
#pragma warning restore CA1510

        Stack<Frame> open = new Stack<Frame>();
        int baseDepth = tree.GetDepth(startIndex);
        int nodeCount = tree.NodeCount;

        for (int i = startIndex; i < nodeCount; i++)
        {
            int depth = tree.GetDepth(i);

            // Stop when we move back to or above the start node's depth (but only after we've
            // emitted the start node itself).
            if (i > startIndex && depth <= baseDepth)
            {
                break;
            }

            while (open.Count > 0 && open.Peek().Depth >= depth)
            {
                EmitClose(open.Pop(), sink);
            }

            EmitOne(tree, i, depth, sink, open);
        }

        while (open.Count > 0)
        {
            EmitClose(open.Pop(), sink);
        }
    }

    /// <summary>Emits every node in <paramref name="tree"/> to <paramref name="sink"/>.</summary>
    public static void Emit(TinyTree tree, IResultReceiver sink)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        if (sink is null)
        {
            throw new ArgumentNullException(nameof(sink));
        }
#pragma warning restore CA1510

        Stack<Frame> open = new Stack<Frame>();
        int nodeCount = tree.NodeCount;

        for (int i = 0; i < nodeCount; i++)
        {
            int depth = tree.GetDepth(i);
            while (open.Count > 0 && open.Peek().Depth >= depth)
            {
                EmitClose(open.Pop(), sink);
            }

            EmitOne(tree, i, depth, sink, open);
        }

        while (open.Count > 0)
        {
            EmitClose(open.Pop(), sink);
        }
    }

    private static void EmitOne(TinyTree tree, int i, int depth, IResultReceiver sink, Stack<Frame> open)
    {
        XdmNodeKind kind = tree.GetKind(i);
        switch (kind)
        {
            case XdmNodeKind.Document:
                sink.StartDocument();
                open.Push(new Frame(i, depth, XdmNodeKind.Document));
                break;

            case XdmNodeKind.Element:
                sink.StartElement(tree.GetNameCode(i), SourceSpan.None);

                // A copy keeps the base URI of what it copies: the root of the copy, and any element that had one of its own.
                if (sink is IBaseUriReceiver baseReceiver && open.Count == 0 && tree.GetBaseUri(i) is { } copiedBase)
                {
                    baseReceiver.BaseUri(copiedBase);
                }

                EmitNamespaces(tree, i, sink);
                EmitAttributes(tree, i, sink);
                open.Push(new Frame(i, depth, XdmNodeKind.Element));
                break;

            case XdmNodeKind.Text:
                sink.Text(tree.GetStringValue(i));
                break;

            case XdmNodeKind.Comment:
                sink.Comment(tree.GetStringValue(i));
                break;

            case XdmNodeKind.ProcessingInstruction:
            {
                string target = tree.NamePool.GetQName(tree.GetNameCode(i)).LocalName;
                sink.ProcessingInstruction(target, tree.GetStringValue(i));
                break;
            }

            default:
                break;
        }
    }

    private static void EmitClose(Frame frame, IResultReceiver sink)
    {
        switch (frame.Kind)
        {
            case XdmNodeKind.Element:
                sink.EndElement();
                break;
            case XdmNodeKind.Document:
                sink.EndDocument();
                break;
        }
    }

    private static void EmitNamespaces(TinyTree tree, int elementIndex, IResultReceiver sink)
    {
        int ns = tree.GetFirstNamespace(elementIndex);
        if (ns < 0)
        {
            return;
        }

        // Namespaces for this element are contiguous starting at index ns; the parent
        // changes when we move past them (another element's ns records follow).
        for (int k = ns; k < tree.NamespaceCount; k++)
        {
            if (tree.GetNamespaceParent(k) != elementIndex)
            {
                break;
            }

            sink.Namespace(tree.GetNamespacePrefix(k), tree.GetNamespaceUri(k));
        }
    }

    private static void EmitAttributes(TinyTree tree, int elementIndex, IResultReceiver sink)
    {
        int a = tree.GetFirstAttribute(elementIndex);
        if (a < 0)
        {
            return;
        }

        for (int k = a; k < tree.AttributeCount; k++)
        {
            if (tree.GetAttributeParent(k) != elementIndex)
            {
                break;
            }

            sink.Attribute(tree.GetAttributeNameCode(k), tree.GetAttributeValue(k), SourceSpan.None);
        }
    }

    private readonly struct Frame
    {
        public Frame(int index, int depth, XdmNodeKind kind)
        {
            Index = index;
            Depth = depth;
            Kind = kind;
        }

        public int Index { get; }
        public int Depth { get; }
        public XdmNodeKind Kind { get; }
    }
}
