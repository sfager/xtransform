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
        // The key name is a lexical QName, so key() is bound to the namespaces in scope at the call (XSLT 2.0 §16.4).
        RegisterBound("key", 2,
            SeqNodeStar(),
            new[] { SeqStringOne(), SeqItemStar() },
            ns => (context, args) => KeyLookup(context, args, top: null, ns));

        RegisterBound("key", 3,
            SeqNodeStar(),
            new[] { SeqStringOne(), SeqItemStar(), new SequenceType(ItemType.AnyNode, Occurrence.ExactlyOne) },
            ns => (context, args) => Key3Invoker(context, args, ns));

        // The three document functions resolve a relative URI against the static base URI at the call.
        RegisterBound("document", 1,
            SeqNodeStar(),
            new[] { SeqItemStar() },
            ns => (context, args) => DocumentInvoker(context, args, StaticBase(ns, context)));

        RegisterBound("document", 2,
            SeqNodeStar(),
            new[] { SeqItemStar(), new SequenceType(ItemType.AnyNode, Occurrence.ExactlyOne) },
            ns => (context, args) => DocumentInvoker(context, args, StaticBase(ns, context)));

        RegisterBound("doc", 1,
            SeqNodeZeroOrOne(),
            new[] { SeqStringZeroOrOne() },
            ns => (context, args) => DocInvoker(context, args, StaticBase(ns, context)));

        Register("current", 0,
            new SequenceType(ItemType.AnyItem, Occurrence.ExactlyOne),
            System.Array.Empty<SequenceType>(),
            CurrentInvoker);

        Register("regex-group", 1,
            SeqStringOne(),
            new[] { new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne) },
            RegexGroupInvoker);

        // XSLT 2.0 §14.1, §14.2: the group being processed by xsl:for-each-group; empty outside one.
        Register("current-group", 0, new SequenceType(ItemType.AnyItem, Occurrence.ZeroOrMore), System.Array.Empty<SequenceType>(),
            (context, args) => GetRun(context).CurrentGroup is { } group
                ? XdmSequence.From(group).GetIterator()
                : XTransform.Xdm.Iterators.EmptyIterator.Instance);
        Register("current-grouping-key", 0, new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrOne), System.Array.Empty<SequenceType>(),
            (context, args) => GetRun(context).CurrentGroupingKey is { } key
                ? new XTransform.Xdm.Iterators.SingleItemIterator(key)
                : XTransform.Xdm.Iterators.EmptyIterator.Instance);

        RegisterBound("doc-available", 1,
            SeqBoolOne(),
            new[] { SeqStringOne() },
            ns => (context, args) => DocAvailableInvoker(context, args, StaticBase(ns, context)));

        Register("generate-id", 0, SeqStringOne(), System.Array.Empty<SequenceType>(), SystemFunctions.GenerateId0);
        Register("generate-id", 1, SeqStringOne(),
            new[] { new SequenceType(ItemType.AnyNode, Occurrence.ZeroOrOne) },
            SystemFunctions.GenerateId1);

        Register("unparsed-text", 1, SeqStringZeroOrOne(), new[] { SeqStringZeroOrOne() }, SystemFunctions.UnparsedText);
        Register("unparsed-text", 2, SeqStringZeroOrOne(), new[] { SeqStringZeroOrOne(), SeqStringOne() }, SystemFunctions.UnparsedText);
        Register("unparsed-text-available", 1, SeqBoolOne(), new[] { SeqStringZeroOrOne() }, SystemFunctions.UnparsedTextAvailable);
        Register("unparsed-text-available", 2, SeqBoolOne(), new[] { SeqStringZeroOrOne(), SeqStringOne() }, SystemFunctions.UnparsedTextAvailable);

        // XSLT 2.0 §16.6.4: the unparsed entities declared by the DTD of the document that holds the context node.
        Register("unparsed-entity-uri", 1, SeqStringOne(), new[] { SeqStringOne() },
            (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(
                UnparsedEntityOf(context, args[0], XsltErrors.XTDE1370)?.SystemUri ?? string.Empty)));
        Register("unparsed-entity-public-id", 1, SeqStringOne(), new[] { SeqStringOne() },
            (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(
                UnparsedEntityOf(context, args[0], XsltErrors.XTDE1380)?.PublicId ?? string.Empty)));

        // fn:current-output-uri() is the empty sequence when the principal result has no URI.
        Register("current-output-uri", 0, SeqAnyUriZeroOrOne(), System.Array.Empty<SequenceType>(),
            (context, args) => XTransform.Xdm.Iterators.EmptyIterator.Instance);

        // XSLT 2.0 §16.4 format-number: the 2-argument form uses the unnamed decimal format; the 3-argument form
        // reads a lexical QName, so it is bound to the namespaces in scope at the call.
        Register("format-number", 2, SeqStringOne(), new[] { SeqAnyAtomicZeroOrOne(), SeqStringOne() },
            (context, args) => FormatNumber(context, args, null));
        RegisterBound("format-number", 3, SeqStringOne(), new[] { SeqAnyAtomicZeroOrOne(), SeqStringOne(), SeqStringOne() },
            ns => (context, args) => FormatNumber(context, args, ns));

        // XSLT 2.0 §16.5 format-dateTime / format-date / format-time: 2- and 5-argument forms; an empty value
        // gives the empty sequence.
        RegisterDateFormat("format-dateTime", XdmTypeCode.DateTime);
        RegisterDateFormat("format-date", XdmTypeCode.Date);
        RegisterDateFormat("format-time", XdmTypeCode.Time);

        // The static base URI depends on where the call is: xml:base on the stylesheet changes it (XSLT 2.0 §3.2).
        RegisterBound("static-base-uri", 0, SeqAnyUriZeroOrOne(), System.Array.Empty<SequenceType>(),
            ns => (context, args) => StaticBase(ns, context) is { } baseUri
                ? new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.AnyUri(baseUri))
                : XTransform.Xdm.Iterators.EmptyIterator.Instance);
        RegisterBound("resolve-uri", 1, SeqAnyUriZeroOrOne(), new[] { SeqStringZeroOrOne() },
            ns => (context, args) => StaticBase(ns, context) is { } baseUri
                ? XTransform.XPath.Functions.UriFunctions.Resolve(args[0], baseUri)
                : XTransform.XPath.Functions.UriFunctions.FnResolveUri1(context, args));

        // Functions that read a lexical QName argument are bound to the namespaces in scope at the call.
        RegisterBound("system-property", 1, SeqStringOne(), new[] { SeqStringOne() },
            ns => (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(
                SystemFunctions.SystemProperty(args[0].FirstOrNull()?.GetStringValue() ?? string.Empty, ns))));
        RegisterBound("element-available", 1, SeqBoolOne(), new[] { SeqStringOne() },
            ns => (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.Boolean(
                SystemFunctions.ElementAvailable(args[0].FirstOrNull()?.GetStringValue() ?? string.Empty, ns))));
        RegisterBound("type-available", 1, SeqBoolOne(), new[] { SeqStringOne() },
            ns => (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.Boolean(
                SystemFunctions.TypeAvailable(args[0].FirstOrNull()?.GetStringValue() ?? string.Empty, ns))));
        RegisterBound("function-available", 1, SeqBoolOne(), new[] { SeqStringOne() },
            ns => (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.Boolean(
                FunctionAvailable(args[0].FirstOrNull()?.GetStringValue() ?? string.Empty, -1, ns))));
        RegisterBound("function-available", 2, SeqBoolOne(), new[] { SeqStringOne(), SeqIntegerOne() },
            ns => (context, args) => new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.Boolean(
                FunctionAvailable(
                    args[0].FirstOrNull()?.GetStringValue() ?? string.Empty,
                    (int)((XdmAtomicValue)args[1].FirstOrNull()!).AsInt64(),
                    ns))));
    }

    private static UnparsedEntity? UnparsedEntityOf(DynamicContext context, XdmSequence nameArg, QName errorCode)
    {
        if (context.ContextItem is not XdmNode node || node.Handle.Tree.GetKind(0) != XdmNodeKind.Document)
        {
            throw new XsltDynamicException(errorCode, "The context item must be a node in a tree rooted at a document node", SourceSpan.None);
        }

        IReadOnlyDictionary<string, UnparsedEntity>? entities = node.Handle.Tree.UnparsedEntities;
        string name = nameArg.FirstOrNull()?.GetStringValue() ?? string.Empty;
        return entities is not null && entities.TryGetValue(name, out UnparsedEntity? found) ? found : null;
    }

    private static string? StaticBase(IStaticNamespaceResolver ns, DynamicContext context) =>
        (ns as NamespaceBindingsResolver)?.StaticBaseUri ?? context.Run.StaticBaseUri;

    // XSLT 2.0 §16.6.1: true when a function of that name (and arity, when given) could be called here.
    private bool FunctionAvailable(string lexical, int arity, IStaticNamespaceResolver ns)
    {
        if (!SystemFunctions.TryResolveName(lexical, ns, out string uri, out string local))
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1400,
                $"function-available: '{lexical}' is not a lexical QName whose prefix is bound",
                SourceSpan.None);
        }

        // An unprefixed name is in the default function namespace.
        if (lexical.IndexOf(':') < 0)
        {
            uri = ns.DefaultFunctionNamespace;
        }

        QName name = new QName(uri, local);
        for (int a = arity < 0 ? 0 : arity; a <= (arity < 0 ? 10 : arity); a++)
        {
            if (TryResolve(name, a, out _))
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterBound(string localName, int arity, SequenceType returnType, SequenceType[] parameters,
                               System.Func<IStaticNamespaceResolver, FunctionInvoker> bind)
    {
        QName name = new QName("fn", BuiltInFunctionLibrary.FnNamespace, localName);
        FunctionSignature sig = new FunctionSignature(
            name, arity, returnType, parameters.ToImmutableArray(),
            static (_, _) => throw new System.InvalidOperationException("Function needs its static context"))
        {
            BindStaticContext = bind,
        };
        _xslt[MakeKey(BuiltInFunctionLibrary.FnNamespace, localName, arity)] = sig;
    }

    // fn:key($name, $value, $top): like key() but searches the tree rooted at $top (XSLT 2.0 §16.4).
    private static IXdmIterator Key3Invoker(DynamicContext context, XdmSequence[] args, IStaticNamespaceResolver ns)
    {
        if (args[2].FirstOrNull() is not XdmNode top)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE1270,
                "The third argument of key() must be a single node",
                SourceSpan.None);
        }

        return KeyLookup(context, args, top, ns);
    }

    // ns is the namespace context of the call; a null ns (key() inside a pattern) matches the name leniently and returns
    // nothing for a key that is not declared.
    internal static IXdmIterator KeyLookup(DynamicContext context, XdmSequence[] args, XdmNode? top, IStaticNamespaceResolver? ns = null)
    {
        XsltRunState run = GetRun(context);
        string keyName = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        XdmSequence keyValues = args[1];

        QName keyQName;
        IReadOnlyList<KeyDecl>? keyDecls;
        if (ns is not null)
        {
            // XSLT 2.0 §16.4 (XTDE1260): the name must be a lexical QName whose prefix is bound and that names an xsl:key.
            string lexical = keyName.Trim();
            if (!LexicalNames.IsQName(lexical)
                || !SystemFunctions.TryResolveName(lexical, ns, out string uri, out string local)
                || !TryFindKey(run, uri, local, out keyQName, out keyDecls))
            {
                throw new XsltDynamicException(XsltErrors.XTDE1260, $"'{keyName}' is not the name of an xsl:key declaration", SourceSpan.None);
            }
        }
        else if (!TryFindKey(run, keyName, out keyQName, out keyDecls))
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        // The tree searched is the one containing the context item, or the one rooted at the third argument. XTDE1270: it
        // must exist and be rooted at a document node.
        XdmNode? node = top ?? context.ContextItem as XdmNode;
        TinyTree? tree = node?.Handle.Tree;
        if (tree is null || tree.GetKind(0) != XdmNodeKind.Document)
        {
            throw new XsltDynamicException(XsltErrors.XTDE1270, "key() needs a node in a tree whose root is a document node", SourceSpan.None);
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
    // The same, with the name already resolved to a namespace and a local name.
    private static bool TryFindKey(XsltRunState run, string uri, string local, out QName key, out IReadOnlyList<KeyDecl>? decls)
    {
        foreach (KeyValuePair<QName, IReadOnlyList<KeyDecl>> entry in run.Stylesheet.Keys)
        {
            if (string.Equals(entry.Key.LocalName, local, System.StringComparison.Ordinal) &&
                string.Equals(entry.Key.NamespaceUri, uri, System.StringComparison.Ordinal))
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

    private static IXdmIterator DocumentInvoker(DynamicContext context, XdmSequence[] args, string? staticBase)
    {
        XsltRunState run = GetRun(context);
        XdmNode? baseNode = args.Length > 1 ? args[1].FirstOrNull() as XdmNode : null;
        List<XdmNode> found = new List<XdmNode>();
        foreach (XdmItem arg in args[0])
        {
            // A node argument is resolved against its own base URI, an atomic value against the stylesheet's (§16.1).
            string? baseUri = baseNode is not null ? baseNode.BaseUri : arg is XdmNode argNode ? argNode.BaseUri : staticBase;
            baseUri ??= staticBase;
            XdmNode? loaded = run.LoadDocument(arg.GetStringValue(), baseUri);
            if (loaded is not null && !found.Contains(loaded))
            {
                found.Add(loaded);
            }
        }

        // The result is in document order without duplicates.
        found.Sort((a, b) => a.CompareDocumentOrder(b));
        return XdmSequence.From(found.ConvertAll(n => (XdmItem)n)).GetIterator();
    }

    private static IXdmIterator DocInvoker(DynamicContext context, XdmSequence[] args, string? staticBase)
    {
        XsltRunState run = GetRun(context);
        if (args[0].FirstOrNull() is not { } uriItem)
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        XdmNode? loaded = run.LoadDocument(uriItem.GetStringValue(), staticBase);
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

    private static bool IsNcName(string text) =>
        text.Length > 0 && XdmAtomicValue.TryParse(text, XdmTypeCode.NcName, out _);

    // A lexical QName: an NCName, optionally preceded by an NCName and a colon.
    private static bool IsLexicalQName(string lexical)
    {
        int colon = lexical.IndexOf(':');
        return colon < 0
            ? IsNcName(lexical)
            : IsNcName(lexical.Substring(0, colon)) && IsNcName(lexical.Substring(colon + 1));
    }

    private static XTransform.Xdm.Iterators.SingleItemIterator FormatNumber(DynamicContext context, XdmSequence[] args, IStaticNamespaceResolver? ns)
    {
        XsltRunState run = GetRun(context);
        DecimalFormatTable formats = run.Stylesheet.Declarations.DecimalFormats;
        DecimalFormat format = formats.Unnamed;
        if (ns is not null)
        {
            string lexical = args[2].FirstOrNull()?.GetStringValue() ?? string.Empty;
            QName name;
            if (!SystemFunctions.TryResolveName(lexical, ns, out string uri, out string local) ||
                !IsLexicalQName(lexical))
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE1280,
                    $"format-number: '{lexical}' is not a valid decimal format name",
                    SourceSpan.None);
            }

            name = new QName(uri, local);
            if (!formats.TryGetNamed(name, out DecimalFormat? named) || named is null)
            {
                throw new XsltDynamicException(
                    XsltErrors.XTDE1280,
                    $"format-number: there is no xsl:decimal-format named '{lexical}'",
                    SourceSpan.None);
            }

            format = named;
        }

        // An empty value is NaN; xs:untypedAtomic is cast to xs:double (F&O §6.2.1).
        XdmAtomicValue? value = args[0].FirstOrNull() as XdmAtomicValue;
        if (value is not null && value.TypeCode == XdmTypeCode.UntypedAtomic)
        {
            value = CastTable.Cast(value, XdmTypeCode.Double, SourceSpan.None);
        }

        if (value is not null && !XTransform.XPath.Operators.NumericPromotion.IsNumeric(value.TypeCode))
        {
            throw new XsltDynamicException(
                XPathErrors.XPTY0004,
                "format-number: the first argument must be numeric",
                SourceSpan.None);
        }

        string picture = args[1].FirstOrNull()?.GetStringValue() ?? string.Empty;
        string text = NumberPictureFormatter.Format(format, value, picture, SourceSpan.None);
        return new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(text));
    }

    private void RegisterDateFormat(string localName, XdmTypeCode valueType)
    {
        SequenceType value = new SequenceType(ItemType.Atomic(valueType), Occurrence.ZeroOrOne);
        SequenceType optional = SeqStringZeroOrOne();
        SequenceType resultType = SeqStringZeroOrOne();
        Register(localName, 2, resultType, new[] { value, SeqStringOne() }, FormatDateTimeInvoker);
        Register(localName, 5, resultType, new[] { value, SeqStringOne(), optional, optional, optional }, FormatDateTimeInvoker);
    }

    private static IXdmIterator FormatDateTimeInvoker(DynamicContext context, XdmSequence[] args)
    {
        if (args[0].FirstOrNull() is not XdmAtomicValue value)
        {
            return XTransform.Xdm.Iterators.EmptyIterator.Instance;
        }

        string picture = args[1].FirstOrNull()?.GetStringValue() ?? string.Empty;
        string? language = args.Length > 2 ? args[2].FirstOrNull()?.GetStringValue() : null;
        string? calendar = args.Length > 3 ? args[3].FirstOrNull()?.GetStringValue() : null;
        string? country = args.Length > 4 ? args[4].FirstOrNull()?.GetStringValue() : null;
        string text = DateTimeFormatter.Format(value.AsDateTime(), picture, language, calendar, country, SourceSpan.None);
        return new XTransform.Xdm.Iterators.SingleItemIterator(XdmAtomicValue.String(text));
    }

    private static XTransform.Xdm.Iterators.SingleItemIterator DocAvailableInvoker(DynamicContext context, XdmSequence[] args, string? staticBase)
    {
        XsltRunState run = GetRun(context);
        string uriStr = args[0].FirstOrNull()?.GetStringValue() ?? string.Empty;
        bool available;
        try
        {
            available = run.LoadDocument(uriStr, staticBase) is not null;
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

    private static SequenceType SeqAnyAtomicZeroOrOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyAtomicType), Occurrence.ZeroOrOne);

    private static SequenceType SeqStringZeroOrOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.String), Occurrence.ZeroOrOne);

    private static SequenceType SeqAnyUriZeroOrOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.AnyUri), Occurrence.ZeroOrOne);

    private static SequenceType SeqIntegerOne() =>
        new SequenceType(ItemType.Atomic(XdmTypeCode.Integer), Occurrence.ExactlyOne);

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

                // The current group, grouping key, template rule and tunnel parameters are empty, and the current mode is the default mode, inside a stylesheet function (XSLT 2.0 §14.1, §6.7, §10.1.2, erratum XT.E19).
                if (ctx.Run.Tag is not XsltRunState run || (run.CurrentGroup is null && run.CurrentGroupingKey is null && run.CurrentRule is null && run.TunnelParameters.Count == 0 && run.CurrentMode == DeclarationCollector.DefaultMode))
                {
                    return _body.Invoke(ctx, args);
                }

                IReadOnlyDictionary<QName, XdmSequence> savedTunnel = run.TunnelParameters;
                run.TunnelParameters = XsltRunState.EmptyTunnel;
                TemplateRule? savedRule = run.CurrentRule;
                run.CurrentRule = null;
                QName savedMode = run.CurrentMode;
                run.CurrentMode = DeclarationCollector.DefaultMode;
                IReadOnlyList<XdmItem>? savedGroup = run.CurrentGroup;
                XdmAtomicValue? savedKey = run.CurrentGroupingKey;
                run.CurrentGroup = null;
                run.CurrentGroupingKey = null;
                try
                {
                    return _body.Invoke(ctx, args);
                }
                finally
                {
                    run.CurrentRule = savedRule;
                    run.CurrentMode = savedMode;
                    run.TunnelParameters = savedTunnel;
                    run.CurrentGroup = savedGroup;
                    run.CurrentGroupingKey = savedKey;
                }
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
