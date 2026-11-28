// XSLT 2.0 §16 — generate-id, system-property, element-available, type-available,
// unparsed-text and unparsed-text-available.

using System;
using System.IO;
using System.Text;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath;

namespace XTransform.Xslt;

internal static class SystemFunctions
{
    private const string XslNamespace = "http://www.w3.org/1999/XSL/Transform";

    // ── generate-id (§16.6.4) ────────────────────────────────────────────────

    /// <summary>A string unique per node within a run: tree id, node kind and index.</summary>
    internal static string GenerateId(XdmNode node)
    {
        NodeHandle h = node.Handle;
        string kind = h.IsAttribute ? "a" : h.IsNamespace ? "s" : "n";
        return "d" + h.Tree.TreeId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + kind + h.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static IXdmIterator GenerateId0(DynamicContext context, XdmSequence[] args)
    {
        if (context.ContextItem is not XdmNode node)
        {
            throw new XsltDynamicException(
                XPathErrors.XPTY0004,
                "generate-id() requires the context item to be a node",
                SourceSpan.None);
        }

        return new SingleItemIterator(XdmAtomicValue.String(GenerateId(node)));
    }

    internal static IXdmIterator GenerateId1(DynamicContext context, XdmSequence[] args) =>
        new SingleItemIterator(XdmAtomicValue.String(
            args[0].FirstOrNull() is XdmNode node ? GenerateId(node) : string.Empty));

    // ── Name resolution shared by the *-available functions and system-property ──

    /// <summary>Resolves a lexical QName with the namespaces in scope at the call; false if it is not a lexical QName or its prefix is unbound.</summary>
    internal static bool TryResolveName(string lexical, IStaticNamespaceResolver ns, out string uri, out string local)
    {
        // A name that is not a lexical QName cannot be resolved (XTDE1390, XTDE1400, XTDE1425, XTDE1440, XTDE1260).
        lexical = lexical.Trim();
        if (!LexicalNames.IsQName(lexical))
        {
            uri = string.Empty;
            local = lexical;
            return false;
        }

        int colon = lexical.IndexOf(':');
        local = colon < 0 ? lexical : lexical.Substring(colon + 1);
        if (colon < 0)
        {
            uri = string.Empty;
            return true;
        }

        string? bound = ns.LookupNamespace(lexical.Substring(0, colon));
        uri = bound ?? string.Empty;
        return bound is not null;
    }

    // ── system-property (§16.6.3) ────────────────────────────────────────────

    internal static string SystemProperty(string lexical, IStaticNamespaceResolver ns)
    {
        if (!TryResolveName(lexical, ns, out string uri, out string local))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1390,
                $"system-property: '{lexical}' is not a lexical QName whose prefix is bound",
                SourceSpan.None);
        }

        if (!string.Equals(uri, XslNamespace, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return local switch
        {
            "version" => "2.0",
            "vendor" => "XTransform",
            "vendor-url" => "https://github.com/sfager/xtransform",
            "product-name" => "XTransform",
            "product-version" => "0.3.0",
            "is-schema-aware" => "no",
            "supports-serialization" => "yes",
            "supports-backwards-compatibility" => "no",
            "supports-namespace-axis" => "no",
            _ => string.Empty,
        };
    }

    // ── element-available (§16.6.2), type-available (§16.6.5) ────────────────

    internal static bool ElementAvailable(string lexical, IStaticNamespaceResolver ns)
    {
        if (!TryResolveName(lexical, ns, out string uri, out string local))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1440,
                $"element-available: '{lexical}' is not a lexical QName whose prefix is bound",
                SourceSpan.None);
        }

        // Only xsl:* instructions are known; no extension instructions are supported.
        return string.Equals(uri, XslNamespace, StringComparison.Ordinal) && XsltElementNames.IsKnown(local);
    }

    internal static bool TypeAvailable(string lexical, IStaticNamespaceResolver ns)
    {
        if (!TryResolveName(lexical, ns, out string uri, out string local))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1425,
                $"type-available: '{lexical}' is not a lexical QName whose prefix is bound",
                SourceSpan.None);
        }

        // Schema-unaware: only the built-in XML Schema types exist.
        if (!string.Equals(uri, BuiltInFunctionLibrary.XsNamespace, StringComparison.Ordinal))
        {
            return false;
        }

        return CastTable.TryGetTypeCode(local, out _) ||
               local is "anyType" or "anySimpleType" or "anyAtomicType" or "untyped";
    }

    // ── unparsed-text (§16.2) ────────────────────────────────────────────────

    internal static IXdmIterator UnparsedText(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? uriItem = args[0].FirstOrNull();
        if (uriItem is null)
        {
            return EmptyIterator.Instance;
        }

        string? encoding = args.Length > 1 ? args[1].FirstOrNull()?.GetStringValue() : null;
        string text = ReadText(GetRun(context), context.Run.StaticBaseUri, uriItem.GetStringValue(), encoding, throwOnFailure: true)!;
        return new SingleItemIterator(XdmAtomicValue.String(text));
    }

    internal static IXdmIterator UnparsedTextAvailable(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? uriItem = args[0].FirstOrNull();
        string? encoding = args.Length > 1 ? args[1].FirstOrNull()?.GetStringValue() : null;
        bool ok = uriItem is not null &&
                  ReadText(GetRun(context), context.Run.StaticBaseUri, uriItem.GetStringValue(), encoding, throwOnFailure: false) is not null;
        return new SingleItemIterator(XdmAtomicValue.Boolean(ok));
    }

    private static string? ReadText(XsltRunState run, string? staticBase, string uriString, string? encodingName, bool throwOnFailure)
    {
        string? failure = null;
        string? result = null;
        try
        {
            string baseUri = staticBase ?? string.Empty;
            Uri? uri;
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out uri) &&
                !(baseUri.Length > 0 && Uri.TryCreate(new Uri(baseUri), uriString, out uri)))
            {
                failure = $"'{uriString}' is not an absolute URI and no base URI is available";
            }
            else if (uri!.Fragment.Length > 0)
            {
                failure = $"'{uriString}' contains a fragment identifier";
            }
            else
            {
                Encoding? encoding = encodingName is null ? null : TryGetEncoding(encodingName);
                if (encodingName is not null && encoding is null)
                {
                    failure = $"unknown encoding '{encodingName}'";
                }
                else
                {
                    IResourceResolver resolver = run.Resolver ?? FileSystemResourceResolver.Instance;
                    using Stream? stream = resolver.Open(uri);
                    if (stream is null)
                    {
                        failure = $"'{uriString}' could not be read";
                    }
                    else
                    {
                        result = Decode(stream, encoding, out failure);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or UriFormatException)
        {
            failure = ex.Message;
        }

        if (failure is not null)
        {
            if (throwOnFailure)
            {
                throw new XsltDynamicException(XsltErrors.FOUT1170, "unparsed-text: " + failure, SourceSpan.None);
            }

            return null;
        }

        return result;
    }

    private static Encoding? TryGetEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Decodes strictly (invalid bytes are an error). With no encoding given a byte-order mark is honoured and
    // UTF-8 is assumed otherwise (F&O §14.2).
    private static string? Decode(Stream stream, Encoding? encoding, out string? failure)
    {
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        int skip = 0;
        if (encoding is null)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                skip = 3;
                encoding = new UTF8Encoding(false, true);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                skip = 2;
                encoding = new UnicodeEncoding(true, false, true);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                skip = 2;
                encoding = new UnicodeEncoding(false, false, true);
            }
            else
            {
                encoding = new UTF8Encoding(false, true);
            }
        }
        else
        {
            encoding = Encoding.GetEncoding(encoding.WebName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }

        string text;
        try
        {
            text = encoding.GetString(bytes, skip, bytes.Length - skip);
        }
        catch (DecoderFallbackException)
        {
            failure = "the resource is not valid in its encoding";
            return null;
        }

        // The result must consist of XML characters only.
        foreach (char c in text)
        {
            if ((c < 0x20 && c != '\t' && c != '\n' && c != '\r') || c == '￾' || c == '￿')
            {
                failure = "the resource contains characters that are not allowed in XML";
                return null;
            }
        }

        failure = null;
        return text;
    }

    private static XsltRunState GetRun(DynamicContext context) =>
        context.Run.Tag as XsltRunState
        ?? throw new InvalidOperationException("No XSLT run state is attached to this evaluation");
}
