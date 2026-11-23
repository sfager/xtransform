// XSLT 2.0 §3.12 — the static context for use-when expressions.
// Only system-property, function-available and element-available may be called; no user
// variables, no context item, no focus.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;
using XTransform.Xdm.Iterators;
using XTransform.XPath;

namespace XTransform.Xslt;

/// <summary>
/// A function library that resolves exactly the three XSLT functions permitted in
/// <c>[xsl:]use-when</c> expressions and falls back to <see cref="BuiltInFunctionLibrary"/>
/// for pure XPath built-ins (e.g. <c>fn:true</c>, <c>fn:false</c>, string functions).
/// </summary>
internal sealed class UseWhenFunctionLibrary : FunctionLibrary
{
    /// <summary>Shared singleton instance.</summary>
    public static UseWhenFunctionLibrary Instance { get; } = new UseWhenFunctionLibrary();

    private readonly Dictionary<string, FunctionSignature> _xslt;

    private UseWhenFunctionLibrary()
    {
        _xslt = new Dictionary<string, FunctionSignature>(System.StringComparer.Ordinal);
        Register("system-property", 1, SeqStringOne(),
                 new[] { SeqStringOne() },
                 (ctx, args) => new SingleItemIterator(XdmAtomicValue.String(SystemProperty(args[0]))));
        Register("function-available", 1, SeqBoolOne(),
                 new[] { SeqStringOne() },
                 (ctx, args) => new SingleItemIterator(XdmAtomicValue.Boolean(FunctionAvailable(args[0], arity: -1))));
        Register("function-available", 2, SeqBoolOne(),
                 new[] { SeqStringOne(), SeqIntOne() },
                 (ctx, args) => new SingleItemIterator(XdmAtomicValue.Boolean(
                     FunctionAvailable(args[0], arity: (int)((XdmAtomicValue)args[1].FirstOrNull()!).AsInt64()))));
        Register("element-available", 1, SeqBoolOne(),
                 new[] { SeqStringOne() },
                 (ctx, args) => new SingleItemIterator(XdmAtomicValue.Boolean(ElementAvailable(args[0]))));
    }

    /// <inheritdoc/>
    public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
    {
        if (string.Equals(name.NamespaceUri, BuiltInFunctionLibrary.FnNamespace, System.StringComparison.Ordinal))
        {
            string key = name.LocalName + "#" + arity;
            if (_xslt.TryGetValue(key, out FunctionSignature? xsl))
            {
                signature = xsl;
                return true;
            }
        }

        // Fall back to pure XPath built-ins (fn:true, fn:false, fn:not, string functions …).
        return BuiltInFunctionLibrary.Instance.TryResolve(name, arity, out signature);
    }

    // ── system-property ──────────────────────────────────────────────────────

    // XSLT 2.0 §16.6.3 — unknown properties return an empty string.
    private static string SystemProperty(XdmSequence arg)
    {
        string name = arg.FirstOrNull()?.GetStringValue() ?? string.Empty;
        return name switch
        {
            "xsl:version" => "2.0",
            "xsl:vendor" => "XTransform",
            "xsl:vendor-url" => "https://github.com/sfager/xtransform",
            "xsl:product-name" => "XTransform",
            "xsl:product-version" => "0.3.0",
            "xsl:is-schema-aware" => "no",
            "xsl:supports-serialization" => "yes",
            "xsl:supports-backwards-compatibility" => "no",
            "xsl:supports-namespace-axis" => "no",
            _ => string.Empty,
        };
    }

    // ── function-available ───────────────────────────────────────────────────

    // XSLT 2.0 §16.6.1 — reports whether the named function could be bound here.
    // For the use-when static context we only recognise the three allowed XSLT functions
    // and anything the built-in XPath library exposes.
    private static bool FunctionAvailable(XdmSequence nameArg, int arity)
    {
        string qname = nameArg.FirstOrNull()?.GetStringValue() ?? string.Empty;
        if (qname.Length == 0)
        {
            return false;
        }

        // The spec says the function name is interpreted using the in-scope namespaces. We
        // don't have a resolver here; the caller is expected to pass a fully-qualified name
        // or use the fn: prefix. For P3-02, we accept unprefixed names as fn: and otherwise
        // require an explicit prefix that we resolve against known XSLT/fn namespaces.
        string ns;
        string local;
        int colon = qname.IndexOf(':');
        if (colon < 0)
        {
            ns = BuiltInFunctionLibrary.FnNamespace;
            local = qname;
        }
        else
        {
            string prefix = qname.Substring(0, colon);
            local = qname.Substring(colon + 1);
            ns = prefix switch
            {
                "fn" => BuiltInFunctionLibrary.FnNamespace,
                "xs" => BuiltInFunctionLibrary.XsNamespace,
                _ => string.Empty,
            };
        }

        QName fnName = new QName(ns, local);

        if (arity < 0)
        {
            // Try a reasonable range of arities 0..5 and report true if any match.
            for (int a = 0; a <= 5; a++)
            {
                if (UseWhenFunctionLibrary.Instance.TryResolve(fnName, a, out _))
                {
                    return true;
                }
            }

            return false;
        }

        return UseWhenFunctionLibrary.Instance.TryResolve(fnName, arity, out _);
    }

    // ── element-available ────────────────────────────────────────────────────

    // XSLT 2.0 §16.6.2 — true when the lexical QName would resolve to an instruction
    // that this processor supports.
    private static bool ElementAvailable(XdmSequence nameArg)
    {
        string qname = nameArg.FirstOrNull()?.GetStringValue() ?? string.Empty;
        if (qname.Length == 0)
        {
            return false;
        }

        int colon = qname.IndexOf(':');
        string local = colon < 0 ? qname : qname.Substring(colon + 1);

        // For P3-02 we only know about xsl:* elements; literal result elements are always
        // considered available (§16.6.2). Without a namespace resolver we approximate by
        // checking the local name against the XSLT 2.0 element registry.
        return XsltElementNames.IsKnown(local);
    }

    // ── Registration helpers ─────────────────────────────────────────────────

    private void Register(string localName, int arity, SequenceType returnType,
                          SequenceType[] parameters, FunctionInvoker invoker)
    {
        QName name = new QName("fn", BuiltInFunctionLibrary.FnNamespace, localName);
        ImmutableArray<SequenceType> paramTypes = parameters.ToImmutableArray();
        _xslt[localName + "#" + arity] = new FunctionSignature(name, arity, returnType, paramTypes, invoker);
    }

    private static SequenceType SeqStringOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ExactlyOne);

    private static SequenceType SeqBoolOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    private static SequenceType SeqIntOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);
}
