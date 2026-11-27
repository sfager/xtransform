using System.Text;
using XTransform.Xdm;

// XPath 2.0 §A.1 productions: NodeTest, KindTest, NameTest, Wildcard
namespace XTransform.XPath.Ast;

/// <summary>Abstract base for node-test AST nodes used in axis steps.</summary>
internal abstract record NodeTestAst(SourceSpan Span) : XPathAst(Span);

/// <summary>Specifies how a name test matches nodes.</summary>
internal enum NameTestKind
{
    /// <summary>An exact QName match, e.g. <c>foo</c> or <c>ns:foo</c>.</summary>
    QualifiedName,
    /// <summary>Wildcard matching any name: <c>*</c>.</summary>
    AnyName,
    /// <summary>Prefix wildcard: <c>prefix:*</c>.</summary>
    PrefixWildcard,
    /// <summary>Local-name wildcard: <c>*:local</c>.</summary>
    LocalWildcard,
}

/// <summary>A name-test node: QName, *, prefix:*, or *:local.</summary>
internal sealed record NameTestAst(SourceSpan Span, QName? Name, NameTestKind Kind) : NodeTestAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        switch (Kind)
        {
            case NameTestKind.AnyName:
                sb.AppendLine("NameTest(*)");
                break;
            case NameTestKind.PrefixWildcard:
                sb.Append("NameTest(").Append(Name!.Value.Prefix).AppendLine(":*)");
                break;
            case NameTestKind.LocalWildcard:
                sb.Append("NameTest(*:").Append(Name!.Value.LocalName).AppendLine(")");
                break;
            default:
                sb.Append("NameTest(").Append(Name!.Value.ToString()).AppendLine(")");
                break;
        }
    }
}

/// <summary>A kind test: element(), text(), comment(), processing-instruction(), document-node(), attribute().</summary>
internal sealed record KindTestAst(SourceSpan Span, XdmNodeKind Kind, QName? NameArg, NodeTestAst? ElementArg, QName? TypeArg = null) : NodeTestAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        string kindName = KindToName(Kind);
        if (ElementArg != null)
        {
            sb.Append("KindTest(").Append(kindName).Append('(');
            // Inline the element arg without newline
            var inner = new StringBuilder();
            ElementArg.AppendDebug(inner, 0);
            sb.Append(inner.ToString().TrimEnd());
            sb.AppendLine("))");
        }
        else if (NameArg.HasValue)
        {
            sb.Append("KindTest(").Append(kindName).Append('(').Append(NameArg.Value.ToString()).AppendLine("))");
        }
        else
        {
            sb.Append("KindTest(").Append(kindName).AppendLine("())");
        }
    }

    private static string KindToName(XdmNodeKind kind)
    {
        return kind switch
        {
            XdmNodeKind.Element => "element",
            XdmNodeKind.Attribute => "attribute",
            XdmNodeKind.Text => "text",
            XdmNodeKind.Comment => "comment",
            XdmNodeKind.ProcessingInstruction => "processing-instruction",
            XdmNodeKind.Document => "document-node",
            XdmNodeKind.Namespace => "namespace-node",
            _ => kind.ToString().ToLowerInvariant(),
        };
    }
}

/// <summary>Matches any node: <c>node()</c>.</summary>
internal sealed record AnyKindTestAst(SourceSpan Span) : NodeTestAst(Span)
{
    internal override void AppendDebug(StringBuilder sb, int indent)
    {
        Indent(sb, indent);
        sb.AppendLine("KindTest(node())");
    }
}
