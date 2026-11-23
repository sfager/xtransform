using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// A compiled <c>xsl:template</c> body: its parameter list, local-slot count, and the
/// instruction tree. One instance is shared between every <see cref="TemplateRule"/> that
/// a match-template produces (unions share), and used by named templates for
/// <c>xsl:call-template</c>.
/// </summary>
internal sealed class CompiledTemplate
{
    /// <summary>Initializes a compiled template body.</summary>
    public CompiledTemplate(
        TemplateDecl source,
        IReadOnlyList<ParamSlot> parameters,
        int slotCount,
        XsltInstruction body)
    {
        Source = source;
        Parameters = parameters;
        SlotCount = slotCount;
        Body = body;
    }

    /// <summary>The source template declaration this was compiled from.</summary>
    public TemplateDecl Source { get; }

    /// <summary>The template's <c>xsl:param</c> slots in declaration order.</summary>
    public IReadOnlyList<ParamSlot> Parameters { get; }

    /// <summary>Total number of local-variable slots the body requires (parameters + locals).</summary>
    public int SlotCount { get; }

    /// <summary>The compiled body instruction tree.</summary>
    public XsltInstruction Body { get; }
}
