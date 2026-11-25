using System;
using System.IO;

namespace XTransform;

/// <summary>
/// Resolves absolute URIs to byte streams. Used for <c>xsl:include</c> / <c>xsl:import</c>,
/// <c>fn:doc</c> / <c>fn:document</c> and, later, <c>unparsed-text</c>. Implementations
/// control security: which schemes and paths are permitted.
/// </summary>
public interface IResourceResolver
{
    /// <summary>
    /// Opens the resource identified by <paramref name="uri"/>. Returns <see langword="null"/>
    /// when the resource does not exist; the caller translates that into the appropriate
    /// spec error. Throw only for genuine I/O failures.
    /// </summary>
    /// <param name="uri">The absolute URI to open.</param>
    /// <returns>The resource stream (the caller disposes it), or <see langword="null"/>.</returns>
    public Stream? Open(Uri uri);
}
