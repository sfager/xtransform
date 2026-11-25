using System.Collections.Generic;
using XTransform.Xdm;

namespace XTransform;

/// <summary>A .NET function callable from XPath expressions in a stylesheet.</summary>
public interface IExtensionFunction
{
    /// <summary>The expanded function name; the namespace must be non-empty.</summary>
    public QName Name { get; }

    /// <summary>Declared argument types; their count is the function's arity.</summary>
    public IReadOnlyList<SequenceType> ArgumentTypes { get; }

    /// <summary>Declared result type.</summary>
    public SequenceType ResultType { get; }

    /// <summary>Invokes the function with arguments already converted to <see cref="ArgumentTypes"/>.</summary>
    /// <param name="arguments">One sequence per declared argument.</param>
    /// <returns>The result sequence.</returns>
    public XdmSequence Invoke(IReadOnlyList<XdmSequence> arguments);
}
