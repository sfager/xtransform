using System.Text;
using XTransform.Xdm;

// XPath 2.0 §A.1 — grammar productions (base AST node)
namespace XTransform.XPath.Ast;

/// <summary>Abstract base for every XPath expression AST node.</summary>
internal abstract record XPathAst(SourceSpan Span)
{
    /// <summary>Pretty-printed form used by golden tests. Deterministic across runs.</summary>
    public virtual string ToDebugString()
    {
        var sb = new StringBuilder();
        AppendDebug(sb, 0);
        return sb.ToString();
    }

    internal abstract void AppendDebug(StringBuilder sb, int indent);

    /// <summary>Writes an indentation prefix of <paramref name="depth"/> * 2 spaces.</summary>
    protected static void Indent(StringBuilder sb, int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            sb.Append("  ");
        }
    }
}
