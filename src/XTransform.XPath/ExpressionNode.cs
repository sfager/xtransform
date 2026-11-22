// XPath 2.0 §2.2 — an expression is evaluated in a static context and a dynamic context;
// a compiled ExpressionNode captures the static analysis and provides pull evaluation.

using System;
using System.Numerics;
using System.Text;
using XTransform.Xdm;

namespace XTransform.XPath;

/// <summary>
/// A compiled, immutable XPath expression node. Thread-safe — holds no run state.
/// Subclasses (one per AST shape) implement the hot path <see cref="Iterate"/>.
/// </summary>
internal abstract class ExpressionNode
{
    /// <summary>
    /// Initializes the base fields shared by all expression node subclasses.
    /// </summary>
    /// <param name="location">Source location of the expression this node was compiled from.</param>
    /// <param name="staticType">The inferred static <see cref="SequenceType"/>.</param>
    /// <param name="dependencies">Which pieces of the dynamic context this expression reads.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="staticType"/> is <see langword="null"/>.</exception>
    protected ExpressionNode(SourceSpan location, SequenceType staticType, ExpressionDependencies dependencies)
    {
        Location = location;
#pragma warning disable CA1510 // ArgumentNullException.ThrowIfNull not available on netstandard2.0
        StaticType = staticType ?? throw new ArgumentNullException(nameof(staticType));
#pragma warning restore CA1510
        Dependencies = dependencies;
    }

    /// <summary>Source location of the expression this node was compiled from.</summary>
    public SourceSpan Location { get; }

    /// <summary>Inferred static <see cref="SequenceType"/> — the compiler sets this.</summary>
    public SequenceType StaticType { get; }

    /// <summary>Which pieces of the dynamic context this expression reads.</summary>
    public ExpressionDependencies Dependencies { get; }

    /// <summary>
    /// Returns a debug string representation of this node and its subtree.
    /// The default implementation returns the class name without any subtree.
    /// Subclasses override this to include their payload and children.
    /// Format: "NodeName(summary)\n  Child1\n  Child2\n..."
    /// </summary>
    /// <returns>A deterministic, human-readable rendering of the node tree.</returns>
    public virtual string ToDebugString() => GetType().Name;

    // ── Shared helpers for subclasses ─────────────────────────────────────────

    /// <summary>
    /// Appends <paramref name="childDebug"/> indented by <paramref name="depth"/> * 2 spaces
    /// to <paramref name="sb"/>. Each line of <paramref name="childDebug"/> is prefixed.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="childDebug">The child's <see cref="ToDebugString"/> output.</param>
    /// <param name="depth">Indent depth (each level = 2 spaces).</param>
    protected static void AppendIndented(StringBuilder sb, string childDebug, int depth)
    {
        string indent = new string(' ', depth * 2);
        foreach (string line in childDebug.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 0)
            {
                sb.Append(indent).AppendLine(trimmed);
            }
        }
    }

    /// <summary>
    /// Formats a <see cref="QName"/> as "prefix:local" when a prefix is present,
    /// or "{uri}local" when there is a namespace URI but no prefix, or just "local".
    /// </summary>
    /// <param name="name">The name to format.</param>
    /// <returns>A human-readable string representation.</returns>
    protected static string FormatQName(QName name)
    {
        if (name.Prefix.Length > 0)
        {
            return name.Prefix + ":" + name.LocalName;
        }

        if (name.NamespaceUri.Length > 0)
        {
            return "{" + name.NamespaceUri + "}" + name.LocalName;
        }

        return name.LocalName;
    }

    /// <summary>
    /// Pull evaluation. Must be lazy wherever the result can be consumed partially.
    /// </summary>
    /// <param name="context">The current dynamic context.</param>
    /// <returns>An iterator over the result sequence.</returns>
    public abstract IXdmIterator Iterate(DynamicContext context);

    /// <summary>
    /// Single-item evaluation. Default pulls one item from <see cref="Iterate"/> and throws
    /// <c>XPTY0004</c> if a second item is produced, else returns the item (null for empty).
    /// Override when the single-item form is cheaper than iterating.
    /// </summary>
    /// <param name="context">The current dynamic context.</param>
    /// <returns>The single item, or <see langword="null"/> for the empty sequence.</returns>
    /// <exception cref="XPathDynamicException">
    /// Thrown with error code <see cref="XPathErrors.XPTY0004"/> when the sequence contains more than one item.
    /// </exception>
    public virtual XdmItem? EvaluateItem(DynamicContext context)
    {
        IXdmIterator iter = Iterate(context);
        XdmItem? first = iter.Next();
        if (first is null)
        {
            return null;
        }

        XdmItem? second = iter.Next();
        if (second is not null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Sequence of more than one item is not allowed here",
                Location);
        }

        return first;
    }

    /// <summary>
    /// fn:boolean / effective boolean value (F&amp;O §15.1.1). Default iterates and stops
    /// early. Override when a cheaper path exists (e.g. literal, and / or).
    /// </summary>
    /// <param name="context">The current dynamic context.</param>
    /// <returns>The effective boolean value of the sequence.</returns>
    /// <exception cref="XPathDynamicException">
    /// Thrown with error code <see cref="XPathErrors.XPTY0004"/> when EBV is undefined for the sequence.
    /// </exception>
    public virtual bool EffectiveBooleanValue(DynamicContext context)
    {
        // F&O §15.1.1
        IXdmIterator iter = Iterate(context);
        XdmItem? first = iter.Next();

        if (first is null)
        {
            return false;  // empty sequence → false
        }

        if (first.IsNode)
        {
            return true;   // any node → true
        }

        // First item is atomic. If a second item exists, EBV is undefined → XPTY0004.
        XdmItem? second = iter.Next();
        if (second is not null)
        {
            throw new XPathDynamicException(
                XPathErrors.XPTY0004,
                "Effective boolean value is only defined for an empty sequence, a single node, or a single atomic of a Boolean/string/numeric type",
                Location);
        }

        return EffectiveBooleanOfAtomic((XdmAtomicValue)first);
    }

    /// <summary>
    /// Computes the effective boolean value of a single atomic value per F&amp;O §15.1.1.
    /// </summary>
    /// <param name="value">The singleton atomic value.</param>
    /// <returns>The effective boolean value.</returns>
    /// <exception cref="XPathDynamicException">
    /// Thrown with <see cref="XPathErrors.XPTY0004"/> when EBV is not defined for the atomic type.
    /// </exception>
    private static bool EffectiveBooleanOfAtomic(XdmAtomicValue value)
    {
        // F&O §15.1.1 — ebv on a singleton atomic:
        //   xs:boolean          → the value
        //   xs:string / xs:anyURI / xs:untypedAtomic → zero-length string → false
        //   xs:integer / xs:decimal / xs:float / xs:double → zero or NaN → false, else true
        //   anything else → XPTY0004
        if (value.TypeCode == XdmTypeCode.Boolean)
        {
            return value.AsBoolean();
        }

        XdmTypeCode primitive = CastTable.PrimitiveTypeOf(value.TypeCode);

        if (primitive == XdmTypeCode.String
            || value.TypeCode == XdmTypeCode.UntypedAtomic
            || value.TypeCode == XdmTypeCode.AnyUri)
        {
            return value.GetStringValue().Length > 0;
        }

        if (IsNumericType(value.TypeCode))
        {
            return NumericIsTruthy(value);
        }

        throw new XPathDynamicException(
            XPathErrors.XPTY0004,
            $"Effective boolean value is not defined for an atomic of type {value.TypeCode}",
            SourceSpan.None);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the type code belongs to the numeric hierarchy.
    /// </summary>
    /// <param name="t">The type code to test.</param>
    /// <returns><see langword="true"/> for xs:decimal, xs:double, xs:float and all xs:integer subtypes.</returns>
    private static bool IsNumericType(XdmTypeCode t) =>
        t == XdmTypeCode.Decimal || t == XdmTypeCode.Double || t == XdmTypeCode.Float
        || CastTable.IsSubtypeOf(t, XdmTypeCode.Integer)
        || CastTable.IsSubtypeOf(t, XdmTypeCode.Decimal);

    /// <summary>
    /// Returns the numeric truthiness of the given atomic value per F&amp;O §15.1.1.
    /// Only called when <see cref="IsNumericType"/> returns <see langword="true"/>.
    /// </summary>
    /// <param name="v">The atomic numeric value.</param>
    /// <returns><see langword="false"/> for zero or NaN; <see langword="true"/> otherwise.</returns>
    private static bool NumericIsTruthy(XdmAtomicValue v)
    {
        // F&O §15.1.1 — numeric EBV: zero or NaN → false
        switch (v.TypeCode)
        {
            case XdmTypeCode.Double:
                double d = v.AsDouble();
                return !double.IsNaN(d) && d != 0.0;

            case XdmTypeCode.Float:
                float f = v.AsFloat();
                return !float.IsNaN(f) && f != 0.0f;

            case XdmTypeCode.Decimal:
                return v.AsDecimal().Value != 0m;

            default:
                // Integer family — AsBigInteger is always safe.
                return v.AsBigInteger() != BigInteger.Zero;
        }
    }
}
