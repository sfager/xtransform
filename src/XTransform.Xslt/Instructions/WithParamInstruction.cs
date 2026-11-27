using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Compiled <c>xsl:with-param</c>. Evaluates to a (name → sequence) binding consumed by a
/// matching <c>xsl:param</c> in the invoked template or function.
/// </summary>
internal sealed class WithParamInstruction
{
    /// <summary>Initializes a with-param binding.</summary>
    public WithParamInstruction(
        QName name,
        BoundExpression? select,
        SourceSpan location,
        VariableValueBody? body = null,
        SequenceType? asType = null,
        bool isTunnel = false)
    {
        Name = name;
        Select = select;
        Location = location;
        Body = body;
        AsType = asType;
        IsTunnel = isTunnel;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>The compiled <c>@select</c> expression. <see langword="null"/> when a body produces the value.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The content that produces the value when there is no <c>select</c> (XSLT 2.0 §9.3), or null.</summary>
    public VariableValueBody? Body { get; }

    /// <summary>The required type from the <c>as</c> attribute, or null.</summary>
    public SequenceType? AsType { get; }

    /// <summary>True for <c>tunnel="yes"</c>: the value goes into the tunnel context instead of binding a parameter directly.</summary>
    public bool IsTunnel { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }

    private XdmSequence? _fixedValue;

    /// <summary>
    /// A copy whose value has been computed now, in the context of the calling instruction. xsl:apply-templates evaluates its
    /// xsl:with-param values once in its own focus, not in the focus of each template it then invokes (XSLT 2.0 §6.5).
    /// </summary>
    public WithParamInstruction Bind(DynamicContext context, XsltRunState run)
    {
        WithParamInstruction bound = new WithParamInstruction(Name, null, Location, null, null, IsTunnel);
        bound._fixedValue = Evaluate(context, run);
        return bound;
    }

    /// <summary>Evaluates the with-param binding in the given context.</summary>
    public XdmSequence Evaluate(DynamicContext context, XsltRunState run)
    {
        if (_fixedValue is not null)
        {
            return _fixedValue;
        }

        XdmSequence value = Select is not null
            ? Select.Evaluate(context, run)
            : Body is not null
                ? Body.Evaluate(context, run)
                : AsType is null ? XdmSequence.Of(XdmAtomicValue.String(string.Empty)) : XdmSequence.Empty;

        // XSLT 2.0 §9.2: the value is converted to the as type of the with-param (XTTE0590).
        if (AsType is not null)
        {
            value = SequenceTypes.Convert(value, AsType, XsltErrors.XTTE0590, Location, $"The value of with-param ${Name.LocalName}");
        }

        return value;
    }
}
