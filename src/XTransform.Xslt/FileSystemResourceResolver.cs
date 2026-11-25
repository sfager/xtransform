using System;
using System.IO;

namespace XTransform.Xslt;

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
