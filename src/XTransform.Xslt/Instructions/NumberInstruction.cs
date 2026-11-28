// XSLT 2.0 §12 — xsl:number. Produces a text node holding a formatted number (or a place marker such
// as "1.2.3"), computed from the value attribute or from the selected node's position in its tree.

using System;
using System.Collections.Generic;
using System.Globalization;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.XPath.Ast;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled <c>xsl:number</c>.</summary>
internal sealed class NumberInstruction : XsltInstruction
{
    /// <summary>Initializes an xsl:number instruction.</summary>
    public NumberInstruction(
        BoundExpression? value,
        BoundExpression? select,
        NumberLevel level,
        PatternAst? count,
        PatternAst? from,
        CompiledAvt? format,
        CompiledAvt? lang,
        CompiledAvt? letterValue,
        CompiledAvt? ordinal,
        CompiledAvt? groupingSeparator,
        CompiledAvt? groupingSize,
        SourceSpan location)
        : base(location)
    {
        Value = value;
        Select = select;
        Level = level;
        Count = count;
        From = from;
        Format = format;
        Lang = lang;
        LetterValue = letterValue;
        Ordinal = ordinal;
        GroupingSeparator = groupingSeparator;
        GroupingSize = groupingSize;
    }

    public BoundExpression? Value { get; }

    public BoundExpression? Select { get; }

    public NumberLevel Level { get; }

    public PatternAst? Count { get; }

    public PatternAst? From { get; }

    public CompiledAvt? Format { get; }

    public CompiledAvt? Lang { get; }

    public CompiledAvt? LetterValue { get; }

    public CompiledAvt? Ordinal { get; }

    public CompiledAvt? GroupingSeparator { get; }

    public CompiledAvt? GroupingSize { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        context.CheckCancellation();

        List<long> numbers = Value is not null
            ? FromValue(context, run)
            : FromPosition(context, run);

        string format = Format?.Stringify(context, run) ?? "1";
        string? lang = Lang?.Stringify(context, run);
        string? letterValue = LetterValue?.Stringify(context, run);
        string? ordinal = Ordinal?.Stringify(context, run);
        string? groupingSeparator = GroupingSeparator?.Stringify(context, run);
        int groupingSize = 0;
        string? sizeText = GroupingSize?.Stringify(context, run);
        if (sizeText is not null &&
            int.TryParse(sizeText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedSize))
        {
            groupingSize = parsedSize;
        }

        string text = NumberFormatter.Format(numbers, format, lang, letterValue, ordinal, groupingSeparator, groupingSize);
        if (text.Length > 0)
        {
            receiver.EmitTextNode(text);
        }
    }

    // XSLT 2.0 §12.1: each atomized item V becomes xs:integer(round(number(V))); NaN, infinity and
    // negative results are the dynamic error XTDE0980.
    private List<long> FromValue(DynamicContext context, XsltRunState run)
    {
        List<long> numbers = new List<long>();
        IXdmIterator iter = Value!.Iterate(context, run);
        XdmItem? item;
        while ((item = iter.Next()) is not null)
        {
            string lexical = item.GetStringValue().Trim();
            double d;
            if (!double.TryParse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                d = lexical == "INF" ? double.PositiveInfinity : lexical == "-INF" ? double.NegativeInfinity : double.NaN;
            }

            double rounded = Math.Floor(d + 0.5);
            if (double.IsNaN(rounded) || double.IsInfinity(rounded) || rounded < 0 || rounded > long.MaxValue)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE0980,
                    $"xsl:number value '{lexical}' cannot be converted to a non-negative integer",
                    Location);
            }

            numbers.Add((long)rounded);
        }

        return numbers;
    }

    private List<long> FromPosition(DynamicContext context, XsltRunState run)
    {
        XdmNode selected;
        if (Select is not null)
        {
            IXdmIterator iter = Select.Iterate(context, run);
            XdmItem? first = iter.Next();
            if (first is not XdmNode node || iter.Next() is not null)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTTE1000,
                    "The select attribute of xsl:number must yield a single node",
                    Location);
            }

            selected = node;
        }
        else if (context.ContextItem is XdmNode contextNode)
        {
            selected = contextNode;
        }
        else
        {
            throw new XsltDynamicException(
                XsltErrors.XTTE0990,
                "xsl:number has no value or select attribute and the context item is not a node",
                Location);
        }

        return NumberingCounter.Compute(Level, selected, Count, From, context);
    }
}
