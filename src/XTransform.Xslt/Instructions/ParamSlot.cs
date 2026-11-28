// An xsl:param of a template: its name, default value, whether it is required, and its as type.
// Parameters are bound by name through XsltRunState.PushVariable rather than a numeric slot index.

using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>A compiled template <c>xsl:param</c>.</summary>
internal sealed class ParamSlot
{
    /// <summary>Initializes a parameter slot.</summary>
    public ParamSlot(
        QName name,
        BoundExpression? defaultSelect,
        bool isRequired,
        SourceSpan location,
        VariableValueBody? defaultBody = null,
        SequenceType? asType = null,
        bool isTunnel = false)
    {
        Name = name;
        DefaultSelect = defaultSelect;
        IsRequired = isRequired;
        Location = location;
        DefaultBody = defaultBody;
        AsType = asType;
        IsTunnel = isTunnel;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>Default-value expression; <see langword="null"/> when none is declared.</summary>
    public BoundExpression? DefaultSelect { get; }

    /// <summary>Default-value content (XSLT 2.0 §9.3) when there is no select; <see langword="null"/> when none is declared.</summary>
    public VariableValueBody? DefaultBody { get; }

    /// <summary>The required type from the <c>as</c> attribute, or null.</summary>
    public SequenceType? AsType { get; }

    /// <summary>True for <c>tunnel="yes"</c>: the value comes from the tunnel context (XSLT 2.0 §10.1.2).</summary>
    public bool IsTunnel { get; }

    /// <summary>True when <c>required="yes"</c>.</summary>
    public bool IsRequired { get; }

    /// <summary>True when the parameter has an explicit default (a select or content).</summary>
    public bool HasDefault => DefaultSelect is not null || DefaultBody is not null;

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }
}
