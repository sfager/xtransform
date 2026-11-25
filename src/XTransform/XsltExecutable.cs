using System.Collections.Generic;
using System.Linq;
using XTransform.Xdm;
using XTransform.Xslt;

namespace XTransform;

/// <summary>A compiled stylesheet. Immutable and thread-safe: cache and share it.</summary>
public sealed class XsltExecutable
{
    private readonly CompiledStylesheet _compiled;
    private readonly IResourceResolver _resolver;

    internal XsltExecutable(CompiledStylesheet compiled, IResourceResolver resolver)
    {
        _compiled = compiled;
        _resolver = resolver;
        GlobalParameters = compiled.Declarations.GlobalVariables.Values
            .Where(v => v.IsParam)
            .Select(v => v.Name)
            .ToArray();
    }

    /// <summary>Creates a transformer for one run. Cheap; a transformer is not thread-safe.</summary>
    /// <returns>A new transformer.</returns>
    public XsltTransformer CreateTransformer() => new XsltTransformer(_compiled, _resolver);

    /// <summary>The names of the stylesheet's global <c>xsl:param</c> declarations.</summary>
    public IReadOnlyCollection<QName> GlobalParameters { get; }

    /// <summary>Serialization parameters from the unnamed <c>xsl:output</c>.</summary>
    public XsltOutputSettings OutputSettings => _compiled.PrincipalOutput;
}
