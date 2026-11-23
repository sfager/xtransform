// Compiled form of an AttributeValueTemplate: literal strings and evaluated XPath
// expressions interleaved. Evaluate concatenates each part's text value at runtime.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled attribute value template, ready to stringify at runtime.</summary>
internal sealed class CompiledAvt
{
    private readonly CompiledAvtPart[] _parts;

    /// <summary>Initializes a compiled AVT with the given parts.</summary>
    public CompiledAvt(CompiledAvtPart[] parts)
    {
        _parts = parts;
    }

    /// <summary>Returns the AVT's parts in document order.</summary>
    public IReadOnlyList<CompiledAvtPart> Parts => _parts;

    /// <summary>
    /// Stringifies the AVT at runtime: each expression part is evaluated against
    /// <paramref name="context"/> and its atomized string value is appended verbatim.
    /// </summary>
    public string Stringify(DynamicContext context, XsltRunState run)
    {
        if (_parts.Length == 1 && _parts[0].Literal is string lit)
        {
            return lit;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < _parts.Length; i++)
        {
            CompiledAvtPart part = _parts[i];
            if (part.Literal is not null)
            {
                sb.Append(part.Literal);
                continue;
            }

            // Expression: concatenate string values of each produced item, separated by a
            // single space (XSLT 2.0 §5.6.1 — same convention as fn:string-join default).
            IXdmIterator iter = part.Expression!.Iterate(context, run);
            XdmItem? item;
            bool first = true;
            while ((item = iter.Next()) is not null)
            {
                if (!first)
                {
                    sb.Append(' ');
                }

                sb.Append(item.GetStringValue());
                first = false;
            }
        }

        return sb.ToString();
    }
}

/// <summary>One part of a <see cref="CompiledAvt"/>: either a literal or a compiled expression.</summary>
internal sealed class CompiledAvtPart
{
    private CompiledAvtPart(string? literal, BoundExpression? expression)
    {
        Literal = literal;
        Expression = expression;
    }

    /// <summary>The literal text, when this is a literal part.</summary>
    public string? Literal { get; }

    /// <summary>The compiled expression, when this is an expression part.</summary>
    public BoundExpression? Expression { get; }

    /// <summary>Creates a literal part.</summary>
    public static CompiledAvtPart Literal_(string text) => new CompiledAvtPart(text, null);

    /// <summary>Creates an expression part.</summary>
    public static CompiledAvtPart Expression_(BoundExpression expr) => new CompiledAvtPart(null, expr);
}
