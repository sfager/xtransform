// A compiled xsl:function body plus its parameter names. Invoked by XsltFunctionLibrary's
// late-bound invoker. Values are captured via an ItemCollectorReceiver so the function can
// return a sequence of items; the common case is a body consisting of xsl:sequence or an
// XPath-valued xsl:value-of.

using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

internal sealed class UserFunctionBody
{
    public UserFunctionBody(IReadOnlyList<QName> paramNames, XsltInstruction body)
    {
        ParamNames = paramNames;
        Body = body;
    }

    /// <summary>Parameter names in declaration order.</summary>
    public IReadOnlyList<QName> ParamNames { get; }

    /// <summary>The compiled body instructions.</summary>
    public XsltInstruction Body { get; }

    /// <summary>
    /// Invokes the function with <paramref name="args"/> in slot order. Pushes each argument
    /// as a local variable binding, runs the body against an <see cref="ItemCollectorReceiver"/>,
    /// and returns the collected sequence.
    /// </summary>
    public IXdmIterator Invoke(DynamicContext context, XdmSequence[] args)
    {
        XsltRunState? run = context.Run.Tag as XsltRunState;
        if (run is null)
        {
            throw new XsltDynamicException(
                XsltErrors.XTDE0640,
                "xsl:function invoked outside an XSLT run — missing XsltRunState on RunState.Tag",
                SourceSpan.None);
        }

        int marker = run.VariableScopeMarker;
        try
        {
            for (int i = 0; i < ParamNames.Count; i++)
            {
                run.PushVariable(ParamNames[i], i < args.Length ? args[i] : XdmSequence.Empty);
            }

            // XSLT 2.0 §10.3: the focus is undefined within a function body.
            ItemCollectorReceiver collector = new ItemCollectorReceiver(run.NamePool);
            Body.Execute(context.WithoutFocus(), collector, run);
            return collector.ToSequence().GetIterator();
        }
        finally
        {
            run.RestoreVariablesTo(marker);
        }
    }
}

/// <summary>
/// Collects items emitted during xsl:function body execution. <see cref="Append"/> records
/// items by reference; top-level text events become <c>xs:string</c> atomics; elements, comments and
/// processing instructions that the body constructs are built into nodes (one item each).
/// </summary>
internal sealed class ItemCollectorReceiver : IResultReceiver
{
    private readonly List<XdmItem> _items = new List<XdmItem>();
    private readonly NamePool _pool;
    private TreeBuilder? _current;
    private int _depth;

    public ItemCollectorReceiver(NamePool pool)
    {
        _pool = pool;
    }

    public XdmSequence ToSequence() => XdmSequence.From(_items);

    public void StartDocument() { }

    public void EndDocument() { }

    public void StartElement(int nameCode, SourceSpan location)
    {
        _current ??= new TreeBuilder(_pool);
        _current.StartElement(nameCode, location);
        _depth++;
    }

    public void Namespace(string prefix, string uri)
    {
        if (_current is not null && _depth > 0)
        {
            _current.Namespace(prefix, uri);
        }
    }

    public void Attribute(int nameCode, string value, SourceSpan location)
    {
        if (_current is not null && _depth > 0)
        {
            _current.Attribute(nameCode, value, location);
        }
    }

    public void EndElement()
    {
        if (_current is null)
        {
            return;
        }

        _current.EndElement();
        _depth--;
        if (_depth == 0)
        {
            _items.Add(FirstChild(_current.Build()));
            _current = null;
        }
    }

    public void Text(string text)
    {
        if (_depth > 0)
        {
            _current!.Text(text);
        }
        else
        {
            _items.Add(XdmAtomicValue.String(text));
        }
    }

    public void Comment(string text)
    {
        if (_depth > 0)
        {
            _current!.Comment(text);
            return;
        }

        TreeBuilder builder = new TreeBuilder(_pool);
        builder.Comment(text);
        _items.Add(FirstChild(builder.Build()));
    }

    public void ProcessingInstruction(string target, string data)
    {
        if (_depth > 0)
        {
            _current!.ProcessingInstruction(target, data);
            return;
        }

        TreeBuilder builder = new TreeBuilder(_pool);
        builder.ProcessingInstruction(target, data);
        _items.Add(FirstChild(builder.Build()));
    }

    public void Append(XdmItem item)
    {
        if (item is null)
        {
            return;
        }

        if (_depth > 0)
        {
            _current!.Append(item);
        }
        else
        {
            _items.Add(item);
        }
    }

    private static XdmNode FirstChild(XdmNode document)
    {
        foreach (XdmNode child in document.Axis(Axis.Child))
        {
            return child;
        }

        return document;
    }
}
