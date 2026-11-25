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

    /// <summary>
    /// Registers a .NET extension function. Its arity is the number of
    /// <paramref name="parameterTypes"/>; arguments reach <paramref name="invoke"/> already
    /// converted to those types (F&amp;O §3.1.5).
    /// </summary>
    public void RegisterExtension(
        QName name,
        IReadOnlyList<SequenceType> parameterTypes,
        SequenceType returnType,
        System.Func<IReadOnlyList<XdmSequence>, XdmSequence> invoke)
    {
        int arity = parameterTypes.Count;
        FunctionSignature signature = new FunctionSignature(
            name,
            arity,
            returnType,
            System.Collections.Immutable.ImmutableArray.CreateRange(parameterTypes),
            (context, args) => invoke(args).GetIterator());
        _xslt[MakeKey(name.NamespaceUri, name.LocalName, arity)] = signature;
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

        Register("key", 3,
            SeqNodeStar(),
            new[] { SeqStringOne(), SeqItemStar(), new SequenceType(ItemType.AnyNode, Occurrence.ExactlyOne) },
            Key3Invoker);

        Register("document", 1,
            SeqNodeStar(),
            new[] { SeqItemStar() },
            DocumentInvoker);

        Register("doc", 1,
            SeqNodeZeroOrOne(),
            new[] { SeqStringOne() },
            DocInvoker);

        Register("current", 0,
            new SequenceType(ItemType.AnyItem, Occurrence.ExactlyOne),
            System.Array.Empty<SequenceType>(),
            CurrentInvoker);

        Register("regex-group", 1,
            SeqStringOne(),
            new[] { new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne) },
            RegexGroupInvoker);

        Register("doc-available", 1,
            SeqBoolOne(),
            new[] { SeqStringOne() },
            DocAvailableInvoker);
    }

    private static IXdmIterator KeyInvoker(DynamicContext context, XdmSequence[] args) =>
        KeyLookup(context, args, top: null);

    // fn:key($name, $value, $top): like key() but searches the tree rooted at $top (XSLT 2.0 §16.4).
    private static IXdmIterator Key3Invoker(DynamicContext context, XdmSequence[] args)
    {
        if (args[2].FirstOrNull() is not XdmNode top)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1270,
                "The third argument of key() must be a single node",
                SourceSpan.None);
        }

        return KeyLookup(context, args, top);
    }

    private static IXdmIterator KeyLookup(DynamicContext context, XdmSequence[] args, XdmNode? top)
    {
        XsltRunState run = GetRun(context);
        string keyName = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        XdmSequence keyValues = args[1];

        if (!TryFindKey(run, keyName, out QName keyQName, out IReadOnlyList<KeyDecl>? keyDecls))
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        // The tree searched is the one containing the context item, or the one rooted at the third argument.
        XdmNode? node = top ?? context.ContextItem as XdmNode;
        TinyTree? tree = node?.Handle.Tree;
        if (tree is null)
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        KeyIndex index = run.GetOrBuildKeyIndex(tree, keyQName, keyDecls!, context);

        List<XdmNode> hits = new List<XdmNode>();
        HashSet<XdmNode> seen = new HashSet<XdmNode>();
        foreach (XdmItem value in keyValues)
        {
            string v = value.GetStringValue();
            if (index.TryGetValue(v, out IReadOnlyList<XdmNode>? nodes) && nodes is not null)
            {
                foreach (XdmNode n in nodes)
                {
                    if (seen.Add(n))
                    {
                        hits.Add(n);
                    }
                }
            }
        }

        // The result is in document order without duplicates (§16.4).
        hits.Sort((a, b) => a.CompareDocumentOrder(b));
        return XdmSequence.From(hits.ConvertAll(n => (XdmItem)n)).GetIterator();
    }

    // The key name is a QName resolved with the namespaces in scope of the call; the function has no static
    // context here, so a prefixed name is matched on prefix and local name against the declarations.
    private static bool TryFindKey(XsltRunState run, string keyName, out QName key, out IReadOnlyList<KeyDecl>? decls)
    {
        int colon = keyName.IndexOf(':');
        string prefix = colon < 0 ? string.Empty : keyName.Substring(0, colon);
        string local = colon < 0 ? keyName : keyName.Substring(colon + 1);
        foreach (KeyValuePair<QName, IReadOnlyList<KeyDecl>> entry in run.Stylesheet.Keys)
        {
            if (string.Equals(entry.Key.LocalName, local, System.StringComparison.Ordinal) &&
                string.Equals(entry.Key.Prefix, prefix, System.StringComparison.Ordinal))
            {
                key = entry.Key;
                decls = entry.Value;
                return true;
            }
        }

        key = default;
        decls = null;
        return false;
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

    // XSLT 2.0 §15.2: regex-group(N) returns the string captured by group N of the match being processed
    // by xsl:matching-substring; group 0 is the whole match. The result is the zero-length string when
    // there is no such group, the group did not take part in the match, or the call is outside a
    // matching-substring (including inside xsl:non-matching-substring).
    private static IXdmIterator RegexGroupInvoker(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState run = GetRun(context);
        long n = ((XdmAtomicValue)args[0].FirstOrNull()!).AsInt64();
        string value = string.Empty;
        System.Text.RegularExpressions.Match? match = run.CurrentRegexMatch;
        if (match is not null && n >= 0 && n < match.Groups.Count)
        {
            System.Text.RegularExpressions.Group group = match.Groups[(int)n];
            if (group.Success)
            {
                value = group.Value;
            }
        }

        return new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(value));
    }

    // XSLT 2.0 §16.1: fn:current() returns the current item — the item being processed by the
    // innermost template rule or xsl:for-each. Predicates and path steps do not change it.
    private static IXdmIterator CurrentInvoker(DynamicContext context, XdmSequence[] args)
    {
        XdmItem? current = context.CurrentItem;
        if (current is null)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1360,
                "fn:current() was called but there is no current item",
                SourceSpan.None);
        }

        return new XTransform.Xdm.Iterators.SingleItemIterator(current);
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
