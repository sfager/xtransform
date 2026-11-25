// XSLT 2.0 §15.1 — xsl:analyze-string: splits a string into matching and non-matching substrings
// and processes each with the corresponding body. The context item is the substring (an xs:string),
// the position and size count all the substrings, and regex-group(N) reads the current match.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Functions;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:analyze-string</c>.</summary>
internal sealed class AnalyzeStringInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:analyze-string instruction.</summary>
    public AnalyzeStringInstruction(
        BoundExpression select,
        CompiledAvt regex,
        CompiledAvt? flags,
        XsltInstruction? matching,
        XsltInstruction? nonMatching,
        SourceSpan location)
        : base(location)
    {
        Select = select;
        Regex = regex;
        Flags = flags;
        Matching = matching;
        NonMatching = nonMatching;
    }

    /// <summary>The string to analyse (<c>@select</c>).</summary>
    public BoundExpression Select { get; }

    /// <summary>The regular expression (<c>@regex</c>, an attribute value template).</summary>
    public CompiledAvt Regex { get; }

    /// <summary>The flags (<c>@flags</c>), or null.</summary>
    public CompiledAvt? Flags { get; }

    /// <summary>The <c>xsl:matching-substring</c> body, or null.</summary>
    public XsltInstruction? Matching { get; }

    /// <summary>The <c>xsl:non-matching-substring</c> body, or null.</summary>
    public XsltInstruction? NonMatching { get; }

    private sealed class Part
    {
        public Part(string text, Match? match)
        {
            Text = text;
            Match = match;
        }

        public string Text { get; }

        public Match? Match { get; }
    }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        string input = EvaluateInput(context, run);
        Regex regex = CompileRegex(Regex.Stringify(context, run), Flags?.Stringify(context, run) ?? string.Empty, Location);

        // XTDE1150: a regex that can match the empty string would split the input into empty matches.
        if (regex.IsMatch(string.Empty))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1150,
                "The regular expression of xsl:analyze-string matches a zero-length string",
                Location);
        }

        List<Part> parts = Split(input, regex);
        Match? previous = run.CurrentRegexMatch;
        try
        {
            for (int i = 0; i < parts.Count; i++)
            {
                context.CheckCancellation();
                Part part = parts[i];
                XsltInstruction? body = part.Match is null ? NonMatching : Matching;
                if (body is null)
                {
                    continue;
                }

                // regex-group() sees the match only inside the matching-substring body.
                run.CurrentRegexMatch = part.Match;
                DynamicContext child = context.WithCurrentItem(XdmAtomicValue.String(part.Text), i + 1, parts.Count);
                int marker = run.VariableScopeMarker;
                try
                {
                    body.Execute(child, receiver, run);
                }
                finally
                {
                    run.RestoreVariablesTo(marker);
                }
            }
        }
        finally
        {
            run.CurrentRegexMatch = previous;
        }
    }

    /// <summary>
    /// Compiles <paramref name="pattern"/> and <paramref name="flags"/> with the XPath 2.0 dialect, mapping the
    /// regular-expression errors to the xsl:analyze-string codes XTDE1140 and XTDE1145.
    /// </summary>
    internal static Regex CompileRegex(string pattern, string flags, SourceSpan location)
    {
        try
        {
            return RegexFunctions.GetOrCompile(pattern, flags, location);
        }
        catch (XPathDynamicException ex) when (ex.ErrorCode.Equals(XPathErrors.FORX0002))
        {
            throw new XsltDynamicException(XsltErrors.XTDE1140, ex.Message, location);
        }
        catch (XPathDynamicException ex) when (ex.ErrorCode.Equals(XPathErrors.FORX0001))
        {
            throw new XsltDynamicException(XsltErrors.XTDE1145, ex.Message, location);
        }
    }

    // The sequence of substrings: matches and the (non-empty) text between them, in order.
    private static List<Part> Split(string input, Regex regex)
    {
        List<Part> parts = new List<Part>();
        int last = 0;
        foreach (Match m in regex.Matches(input))
        {
            if (m.Index > last)
            {
                parts.Add(new Part(input.Substring(last, m.Index - last), null));
            }

            parts.Add(new Part(m.Value, m));
            last = m.Index + m.Length;
        }

        if (last < input.Length)
        {
            parts.Add(new Part(input.Substring(last), null));
        }

        return parts;
    }

    // XSLT 2.0 §15.1: select must evaluate to a single xs:string (function conversion: a node or an
    // xs:untypedAtomic is atomized, xs:anyURI is promoted); anything else is the type error XPTY0004.
    private string EvaluateInput(DynamicContext context, XsltRunState run)
    {
        IXdmIterator iter = Select.Iterate(context, run);
        XdmItem? item = iter.Next();
        if (item is null || iter.Next() is not null)
        {
            throw new XsltDynamicException(
                XPathErrors.XPTY0004,
                "The select attribute of xsl:analyze-string must be a single xs:string",
                Location);
        }

        if (item is XdmAtomicValue atomic &&
            !ItemType.Atomic(XdmTypeCode.String).Matches(atomic) &&
            atomic.TypeCode != XdmTypeCode.UntypedAtomic &&
            atomic.TypeCode != XdmTypeCode.AnyUri)
        {
            throw new XsltDynamicException(
                XPathErrors.XPTY0004,
                $"The select attribute of xsl:analyze-string must be an xs:string, not {atomic.TypeCode}",
                Location);
        }

        return item.GetStringValue();
    }
}
