// Composite FunctionLibrary used by XSLT compilation:
//   1. XSLT-specific functions (fn:key, fn:document, fn:doc, fn:doc-available).
//   2. User-defined xsl:function signatures.
//   3. Fallback to the pure XPath BuiltInFunctionLibrary.
//
// User signatures are registered with a late-bound invoker: a mutable slot holds the
// compiled body which is populated during the second compile pass (after all signatures
// are known so inter-function references resolve).

using System.Collections.Generic;
using System.Collections.Immutable;
using XTransform.Xdm;
using XTransform.XPath;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

/// <summary>Composite function library used by XSLT compilation and runtime.</summary>
internal sealed class XsltFunctionLibrary : FunctionLibrary
{
    private readonly Dictionary<string, FunctionSignature> _xslt;
    private readonly Dictionary<string, UserFunctionSlot> _user;

    /// <summary>Initializes the library with the given XSLT-specific signatures.</summary>
    public XsltFunctionLibrary()
    {
        _xslt = new Dictionary<string, FunctionSignature>(System.StringComparer.Ordinal);
        _user = new Dictionary<string, UserFunctionSlot>(System.StringComparer.Ordinal);
        RegisterXsltFunctions();
    }

    /// <inheritdoc/>
    public override bool TryResolve(QName name, int arity, out FunctionSignature? signature)
    {
        string key = MakeKey(name.NamespaceUri, name.LocalName, arity);
        if (_xslt.TryGetValue(key, out FunctionSignature? xsl))
        {
            signature = xsl;
            return true;
        }

        if (_user.TryGetValue(key, out UserFunctionSlot? userSlot))
        {
            signature = userSlot.Signature;
            return true;
        }

        return BuiltInFunctionLibrary.Instance.TryResolve(name, arity, out signature);
    }

    /// <summary>Registers a user-defined function signature with a late-bound invoker.</summary>
    public UserFunctionSlot RegisterUser(QName name, int arity)
    {
        UserFunctionSlot slot = new UserFunctionSlot(name, arity);
        _user[MakeKey(name.NamespaceUri, name.LocalName, arity)] = slot;
        return slot;
    }

    // ── XSLT function registration (fn:key / fn:document / fn:doc / fn:doc-available) ──

    private void RegisterXsltFunctions()
    {
        Register("key", 2,
            SeqNodeStar(),
            new[] { SeqStringOne(), SeqItemStar() },
            KeyInvoker);

        Register("document", 1,
            SeqNodeStar(),
            new[] { SeqItemStar() },
            DocumentInvoker);

        Register("doc", 1,
            SeqNodeZeroOrOne(),
            new[] { SeqStringOne() },
            DocInvoker);

        Register("doc-available", 1,
            SeqBoolOne(),
            new[] { SeqStringOne() },
            DocAvailableInvoker);
    }

    private static IXdmIterator KeyInvoker(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState run = GetRun(context);
        string keyName = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        XdmSequence keyValues = args[1];

        // Resolve the key name against the stylesheet's static scope — for P3-11 we take it
        // as an unprefixed local name; proper QName resolution lands when xsl:key supports
        // prefixes-in-name.
        QName keyQName = new QName(string.Empty, string.Empty, keyName);

        if (!run.Stylesheet.Keys.TryGetValue(keyQName, out KeyDecl? keyDecl))
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        // Current document comes from the context item; fall back to the primary input.
        XdmNode? node = context.ContextItem as XdmNode;
        TinyTree? tree = node?.Handle.Tree;
        if (tree is null)
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        KeyIndex index = run.GetOrBuildKeyIndex(tree, keyQName, keyDecl, context);

        List<XdmItem> hits = new List<XdmItem>();
        foreach (XdmItem value in keyValues)
        {
            string v = value.GetStringValue();
            if (index.TryGetValue(v, out IReadOnlyList<XdmNode>? nodes) && nodes is not null)
            {
                foreach (XdmNode n in nodes) { hits.Add(n); }
            }
        }

        return XdmSequence.From(hits).GetIterator();
    }

    private static IXdmIterator DocumentInvoker(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState run = GetRun(context);
        List<XdmItem> result = new List<XdmItem>();
        foreach (XdmItem arg in args[0])
        {
            string uriStr = arg.GetStringValue();
            XdmNode? loaded = run.LoadDocument(uriStr);
            if (loaded is not null)
            {
                result.Add(loaded);
            }
        }

        return XdmSequence.From(result).GetIterator();
    }

    private static IXdmIterator DocInvoker(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState run = GetRun(context);
        string uriStr = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        XdmNode? loaded = run.LoadDocument(uriStr);
        if (loaded is null)
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        return new XTransform.Xdm.Iterators.SingleItemIterator(loaded);
    }

    private static IXdmIterator DocAvailableInvoker(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState run = GetRun(context);
        string uriStr = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        bool available;
        try
        {
            available = run.LoadDocument(uriStr) is not null;
        }
        catch
        {
            available = false;
        }

        return new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.Boolean(available));
    }

    private static XsltRunState GetRun(DynamicContext context)
    {
        if (context.Run.Tag is not XsltRunState run)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0640,
                "XSLT function invoked outside an XSLT run — missing XsltRunState on RunState.Tag",
                SourceSpan.None);
        }

        return run;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void Register(string localName, int arity, SequenceType returnType,
                          SequenceType[] parameters, FunctionInvoker invoker)
    {
        QName name = new QName("fn", BuiltInFunctionLibrary.FnNamespace, localName);
        ImmutableArray<SequenceType> paramTypes = parameters.ToImmutableArray();
        FunctionSignature sig = new FunctionSignature(name, arity, returnType, paramTypes, invoker);
        _xslt[MakeKey(BuiltInFunctionLibrary.FnNamespace, localName, arity)] = sig;
    }

    private static string MakeKey(string ns, string local, int arity)
        => "{" + ns + "}" + local + "#" + arity;

    private static SequenceType SeqStringOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ExactlyOne);

    private static SequenceType SeqBoolOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Boolean), Occurrence.ExactlyOne);

    private static SequenceType SeqItemStar() => SequenceType.AnySequence;

    private static SequenceType SeqNodeStar() =>
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrMore);

    private static SequenceType SeqNodeZeroOrOne() =>
        new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrOne);
}

/// <summary>Slot for a user-defined function. The body is populated during compile pass 2.</summary>
internal sealed class UserFunctionSlot
{
    private UserFunctionBody? _body;

    /// <summary>Initializes the slot with its signature placeholder.</summary>
    public UserFunctionSlot(QName name, int arity)
    {
        Name = name;
        Arity = arity;

        // Build a param-types list of item()*.
        ImmutableArray<SequenceType>.Builder pb =
            ImmutableArray.CreateBuilder<SequenceType>(arity);
        for (int i = 0; i < arity; i++)
        {
            pb.Add(SequenceType.AnySequence);
        }

        Signature = new FunctionSignature(name, arity, SequenceType.AnySequence,
            pb.MoveToImmutable(),
            (ctx, args) =>
            {
                if (_body is null)
                {
                    throw new XsltDynamicException(
                        XsltErrors.XTDE0640,
                        $"User function {name.ClarkName}#{arity} invoked before its body was compiled",
                        SourceSpan.None);
                }

                return _body.Invoke(ctx, args);
            });
    }

    /// <summary>Expanded function name.</summary>
    public QName Name { get; }

    /// <summary>Arity.</summary>
    public int Arity { get; }

    /// <summary>The compile-time signature used by the XPath compiler.</summary>
    public FunctionSignature Signature { get; }

    /// <summary>Populates the body (compile pass 2).</summary>
    public void SetBody(UserFunctionBody body) => _body = body;
}
