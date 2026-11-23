using XTransform.Xdm;
using XTransform.XPath;

namespace XTransform.Xslt.Instructions;

/// <summary>
/// A compiled attribute on a literal result element. Its value is a <see cref="CompiledAvt"/>
/// stringified against the current context.
/// </summary>
internal sealed class LiteralAttributeInstruction : XsltInstruction
{
    /// <summary>Initializes a literal attribute.</summary>
    public LiteralAttributeInstruction(QName name, CompiledAvt value, SourceSpan location)
        : base(location)
    {
        Name = name;
        Value = value;
    }

    /// <summary>The expanded attribute name.</summary>
    public QName Name { get; }

    /// <summary>The attribute value template.</summary>
    public CompiledAvt Value { get; }

    /// <inheritdoc/>
    public override void Execute(DynamicContext context, IResultReceiver receiver, XsltRunState run)
    {
        int nameCode = run.NamePool.Allocate(Name.NamespaceUri, Name.LocalName);
        receiver.Attribute(nameCode, Value.Stringify(context), Location);
    }
}
