using System;
using System.IO;

namespace XTransform;

/// <summary>Supplies destinations for secondary result documents (<c>xsl:result-document</c>).</summary>
public interface IResultDocumentHandler
{
    /// <summary>Destination for a result document; <paramref name="href"/> is already resolved against the base output URI.</summary>
    /// <param name="href">The absolute URI of the result document.</param>
    /// <returns>A writable stream.</returns>
    public Stream Open(Uri href);

    /// <summary>Called when the result document has been written.</summary>
    /// <param name="href">The URI passed to <see cref="Open"/>.</param>
    /// <param name="stream">The stream returned by <see cref="Open"/>.</param>
    public void Close(Uri href, Stream stream);
}
