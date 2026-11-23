using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Describes a parameter slot on a compiled template / function: name, optional default
/// <c>@select</c>, and the "required" flag. Populated by the instruction compiler and
/// consumed by <c>apply-templates</c> / <c>call-template</c>; the runtime binds values via
/// <see cref="XsltRunState.PushVariable"/> rather than a numeric slot index.
/// </summary>
internal sealed class ParamSlot
{
    /// <summary>Initializes a parameter slot.</summary>
    public ParamSlot(QName name, BoundExpression? defaultSelect, bool isRequired, SourceSpan location)
    {
        Name = name;
        DefaultSelect = defaultSelect;
        IsRequired = isRequired;
        Location = location;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>Default-value expression; <see langword="null"/> when none is declared.</summary>
    public BoundExpression? DefaultSelect { get; }

    /// <summary>True when <c>required="yes"</c>.</summary>
    public bool IsRequired { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }
}
