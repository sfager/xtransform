// XSLT 2.0 §5.6.1 — attribute value templates.
// An attribute value is a sequence of literal and expression parts; "{expr}" denotes an
// embedded XPath expression, "{{" and "}}" escape literal braces.

using System.Collections.Generic;
using System.Text;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>
/// A parsed attribute value template. Expression parts keep their XPath text as a string —
/// XPath parsing/compilation happens later in the compiler (P3-04).
/// </summary>
internal sealed class AttributeValueTemplate
{
    /// <summary>Initializes with the supplied parts.</summary>
    public AttributeValueTemplate(IReadOnlyList<AvtPart> parts)
    {
        Parts = parts;
    }

    /// <summary>The ordered parts (literal or expression).</summary>
    public IReadOnlyList<AvtPart> Parts { get; }

    /// <summary>True when every part is a literal (no expression to compile at runtime).</summary>
    public bool IsLiteral
    {
        get
        {
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i] is AvtExpression)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Concatenates every literal part into one string (only valid when <see cref="IsLiteral"/>).</summary>
    public string LiteralValue
    {
        get
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Parts.Count; i++)
            {
                if (Parts[i] is AvtLiteral lit)
                {
                    sb.Append(lit.Text);
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Parses <paramref name="value"/> as an attribute value template. The <paramref name="location"/>
    /// is used in <see cref="XsltStaticException"/> diagnostics for unmatched braces.
    /// </summary>
    /// <exception cref="XsltStaticException">XTSE0370 for unmatched <c>{</c> or <c>}</c>.</exception>
    public static AttributeValueTemplate Parse(string value, SourceSpan location)
    {
        List<AvtPart> parts = new List<AvtPart>();
        StringBuilder literal = new StringBuilder();
        int i = 0;
        int length = value.Length;

        while (i < length)
        {
            char c = value[i];

            if (c == '{')
            {
                if (i + 1 < length && value[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                // Flush any literal accumulated so far
                if (literal.Length > 0)
                {
                    parts.Add(new AvtLiteral(literal.ToString()));
                    literal.Clear();
                }

                // Scan up to the matching unescaped '}', skipping string literals so that
                // a '}' inside a quoted XPath string is not treated as a close.
                int exprStart = i + 1;
                int cursor = exprStart;
                char? inString = null;
                while (cursor < length)
                {
                    char ec = value[cursor];
                    if (inString is char q)
                    {
                        if (ec == q)
                        {
                            // Doubled quote inside a string is an escaped quote, not an end.
                            if (cursor + 1 < length && value[cursor + 1] == q)
                            {
                                cursor += 2;
                                continue;
                            }
                            inString = null;
                        }

                        cursor++;
                        continue;
                    }

                    if (ec == '\'' || ec == '"')
                    {
                        inString = ec;
                        cursor++;
                        continue;
                    }

                    if (ec == '}')
                    {
                        break;
                    }

                    cursor++;
                }

                if (cursor >= length)
                {
                    throw Xtse0370(location, "Unmatched '{' in attribute value template");
                }

                string exprText = value.Substring(exprStart, cursor - exprStart);
                parts.Add(new AvtExpression(exprText, location));
                i = cursor + 1;
                continue;
            }

            if (c == '}')
            {
                if (i + 1 < length && value[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }

                throw Xtse0370(location, "Unmatched '}' in attribute value template");
            }

            literal.Append(c);
            i++;
        }

        if (literal.Length > 0)
        {
            parts.Add(new AvtLiteral(literal.ToString()));
        }

        if (parts.Count == 0)
        {
            parts.Add(new AvtLiteral(string.Empty));
        }

        return new AttributeValueTemplate(parts);
    }

    private static XsltStaticException Xtse0370(SourceSpan location, string message)
    {
        return new XsltStaticException(new Diagnostic[]
        {
            new Diagnostic(DiagnosticSeverity.Error, XsltErrors.XTSE0370, message, location),
        });
    }
}

/// <summary>A single part of an <see cref="AttributeValueTemplate"/>.</summary>
internal abstract class AvtPart
{
}

/// <summary>Literal text segment of an AVT.</summary>
internal sealed class AvtLiteral : AvtPart
{
    /// <summary>Initializes with the literal text.</summary>
    public AvtLiteral(string text)
    {
        Text = text;
    }

    /// <summary>The literal text (braces already unescaped).</summary>
    public string Text { get; }
}

/// <summary>An embedded XPath expression in an AVT. The expression text is held verbatim.</summary>
internal sealed class AvtExpression : AvtPart
{
    /// <summary>Initializes with the expression text and location.</summary>
    public AvtExpression(string expressionText, SourceSpan location)
    {
        ExpressionText = expressionText;
        Location = location;
    }

    /// <summary>The raw XPath 2.0 expression text.</summary>
    public string ExpressionText { get; }

    /// <summary>Source location of the containing attribute.</summary>
    public SourceSpan Location { get; }
}
