using System;
using System.IO;

namespace XTransform.Xslt;

/// <summary>
/// Resolves absolute URIs to byte streams. Used by the stylesheet linker for
/// <c>xsl:include</c> / <c>xsl:import</c>, and later by document and unparsed-text loaders.
/// Implementations control security (which schemes / paths are permitted).
/// </summary>
/// <remarks>
/// This mirrors the public API surface declared in <c>docs/interfaces.md</c>; the type is
/// currently <c>internal</c> and will be exposed through <c>XTransform</c> in P3-15.
/// </remarks>
internal interface IResourceResolver
{
    /// <summary>
    /// Opens the resource identified by <paramref name="uri"/>. Returns <see langword="null"/>
    /// when the resource does not exist; the caller translates that into the appropriate
    /// static error. Throw only for genuine I/O failures.
    /// </summary>
    public Stream? Open(Uri uri);
}

/// <summary>
/// The default <see cref="IResourceResolver"/>: opens <c>file:</c> URIs from the local
/// filesystem and denies every other scheme by returning <see langword="null"/>.
/// </summary>
internal sealed class FileSystemResourceResolver : IResourceResolver
{
    /// <summary>Shared singleton instance.</summary>
    public static FileSystemResourceResolver Instance { get; } = new FileSystemResourceResolver();

    private FileSystemResourceResolver() { }

    /// <inheritdoc/>
    public Stream? Open(Uri uri)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }
#pragma warning restore CA1510

        if (!uri.IsAbsoluteUri || !uri.IsFile)
        {
            return null;
        }

        string path = uri.LocalPath;
        if (!File.Exists(path))
        {
            return null;
        }

        return File.OpenRead(path);
    }
}
