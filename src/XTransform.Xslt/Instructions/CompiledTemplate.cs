using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// A compiled <c>xsl:template</c> body: its parameter list and the instruction tree.
/// Variable storage lives on <see cref="XsltRunState"/>'s scope stack, so there is no
/// per-template slot count to track.
/// </summary>
internal sealed class CompiledTemplate
{
    /// <summary>Initializes a compiled template body.</summary>
    public CompiledTemplate(
        TemplateDecl source,
        IReadOnlyList<ParamSlot> parameters,
        XsltInstruction body,
        SequenceType? resultType = null)
    {
        Source = source;
        Parameters = parameters;
        Body = body;
        ResultType = resultType;
    }

    /// <summary>The source template declaration this was compiled from.</summary>
    public TemplateDecl Source { get; }

    /// <summary>The template's <c>xsl:param</c> declarations in order.</summary>
    public IReadOnlyList<ParamSlot> Parameters { get; }

    /// <summary>The required type of the result from the template's <c>as</c> attribute, or null.</summary>
    public SequenceType? ResultType { get; }

    /// <summary>The compiled body instruction tree.</summary>
    public XsltInstruction Body { get; }
}
