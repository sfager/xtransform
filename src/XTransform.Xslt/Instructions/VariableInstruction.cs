// XSLT 2.0 §9 — xsl:variable. At runtime: evaluate the select expression, push the
// result onto the current variable scope. The compiler restores the scope marker when the
// enclosing element ends, so the binding has lexical extent from the declaration through
// the last sibling of its parent.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>Compiled local <c>xsl:variable</c>.</summary>
internal sealed class VariableInstruction : XsltInstruction
{
    /// <summary>Initializes a local xsl:variable.</summary>
    public VariableInstruction(QName name, BoundExpression? select, VariableValueBody? body, SourceSpan location, SequenceType? asType = null)
        : base(location)
    {
        Name = name;
        Select = select;
        Body = body;
        AsType = asType;
    }

    /// <summary>The variable's expanded name.</summary>
    public QName Name { get; }

    /// <summary>The compiled select expression, or null when the value comes from <see cref="Body"/> or is the empty string.</summary>
    public BoundExpression? Select { get; }

    /// <summary>The required type from the <c>as</c> attribute, or null.</summary>
    public SequenceType? AsType { get; }

    /// <summary>The compiled content when there is no select attribute, or null.</summary>
    public VariableValueBody? Body { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        // §9.3: select, else the content (the compiler supplies the zero-length string for an empty untyped variable).
        XdmSequence value = Select is not null
            ? Select.Evaluate(context, run)
            : Body is not null ? Body.Evaluate(context, run) : XdmSequence.Empty;
        // XSLT 2.0 §9.1: the value is converted to the as type; failure is XTTE0570.
        if (AsType is not null)
        {
            value = SequenceTypes.Convert(value, AsType, XsltErrors.XTTE0570, Location, $"The value of variable ${Name.LocalName}");
        }

        run.PushVariable(Name, value);
    }
}
