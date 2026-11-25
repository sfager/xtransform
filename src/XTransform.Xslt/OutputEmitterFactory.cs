// Picks the serializer for an xsl:output definition: method="xml" → XmlEmitter,
// method="text" → TextEmitter. html / xhtml and extension methods arrive in phase 4.

using System;
using System.IO;
using XTransform.Serialization;
using XTransform.Xdm;

namespace XTransform.Xslt;

/// <summary>Creates the emitter selected by <see cref="XsltOutputSettings.Method"/>.</summary>
internal static class OutputEmitterFactory
{
    /// <summary>Creates an emitter that encodes to <paramref name="stream"/> (the stream stays open).</summary>
    public static IResultReceiver Create(XsltOutputSettings settings, Stream stream, NamePool pool)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
#pragma warning restore CA1510

        switch (settings.Method)
        {
            case "xml":
                return XmlEmitter.Create(stream, pool, settings.ToXmlOptions());
            case "text":
                return TextEmitter.Create(stream, settings.ToTextOptions());
            default:
                throw Unsupported(settings.Method);
        }
    }

    /// <summary>Creates an emitter over <paramref name="writer"/>; the caller owns the writer's encoding.</summary>
    public static IResultReceiver Create(XsltOutputSettings settings, TextWriter writer, NamePool pool)
    {
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }
#pragma warning restore CA1510

        switch (settings.Method)
        {
            case "xml":
                return new XmlEmitter(writer, pool, settings.ToXmlOptions());
            case "text":
                return new TextEmitter(writer);
            default:
                throw Unsupported(settings.Method);
        }
    }

    private static NotSupportedException Unsupported(string method) =>
        new NotSupportedException($"Output method '{method}' is not supported yet (html / xhtml arrive in phase 4)");
}
