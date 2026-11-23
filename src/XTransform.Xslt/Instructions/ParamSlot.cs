using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// Describes a parameter slot on a compiled template / function: name, local-variable slot
/// index, optional default <c>@select</c>, and the "required" flag. Populated by the
/// instruction compiler and consumed by <c>apply-templates</c> / <c>call-template</c>.
/// </summary>
internal sealed class ParamSlot
{
    /// <summary>Initializes a parameter slot.</summary>
    public ParamSlot(QName name, int slotIndex, ExpressionNode? defaultSelect, bool isRequired, SourceSpan location)
    {
        Name = name;
        SlotIndex = slotIndex;
        DefaultSelect = defaultSelect;
        IsRequired = isRequired;
        Location = location;
    }

    /// <summary>The parameter name.</summary>
    public QName Name { get; }

    /// <summary>Local-variable slot the caller writes the value into before entering the body.</summary>
    public int SlotIndex { get; }

    /// <summary>Default-value expression; <see langword="null"/> when none is declared.</summary>
    public ExpressionNode? DefaultSelect { get; }

    /// <summary>True when <c>required="yes"</c>.</summary>
    public bool IsRequired { get; }

    /// <summary>Source location.</summary>
    public SourceSpan Location { get; }
}
