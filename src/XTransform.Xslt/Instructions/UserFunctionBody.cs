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

            ItemCollectorReceiver collector = new ItemCollectorReceiver();
            Body.Execute(context, collector, run);
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
/// items by reference; text events become <c>xs:string</c> atomics.
/// </summary>
internal sealed class ItemCollectorReceiver : IResultReceiver
{
    private readonly List<XdmItem> _items = new List<XdmItem>();

    public XdmSequence ToSequence() => XdmSequence.From(_items);

    public void StartDocument() { }
    public void EndDocument() { }
    public void StartElement(int nameCode, SourceSpan location) { }
    public void Namespace(string prefix, string uri) { }
    public void Attribute(int nameCode, string value, SourceSpan location) { }
    public void EndElement() { }
    public void Text(string text) => _items.Add(XdmAtomicValue.String(text));
    public void Comment(string text) { }
    public void ProcessingInstruction(string target, string data) { }
    public void Append(XdmItem item)
    {
        if (item is not null)
        {
            _items.Add(item);
        }
    }
}
