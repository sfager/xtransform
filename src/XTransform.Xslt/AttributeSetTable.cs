using System.Collections.Generic;
using XTransform.Xdm;
using XTransform.Xslt.Instructions;

namespace XTransform.Xslt;

/// <summary>One <c>xsl:attribute-set</c> element and the import precedence of the module that declares it.</summary>
internal sealed class AttributeSetDecl
{
    /// <summary>Initializes a declaration.</summary>
    public AttributeSetDecl(StylesheetElement element, int precedence)
    {
        Element = element;
        Precedence = precedence;
    }

    /// <summary>The <c>xsl:attribute-set</c> element.</summary>
    public StylesheetElement Element { get; }

    /// <summary>Import precedence of the declaring module; higher wins.</summary>
    public int Precedence { get; }
}

/// <summary>
/// The attribute sets of one name, merged (XSLT 2.0 §10.2): the declarations in ascending import precedence and document order, so
/// that of two attributes with the same name the later one wins. <see cref="Body"/> is filled in once the declarations are compiled.
/// </summary>
internal sealed class AttributeSetSlot
{
    private readonly List<AttributeSetDecl> _declarations = new List<AttributeSetDecl>();

    /// <summary>Initializes a slot.</summary>
    public AttributeSetSlot(QName name)
    {
        Name = name;
    }

    /// <summary>The expanded name of the set.</summary>
    public QName Name { get; }

    /// <summary>The declarations, lowest precedence first and in document order among equals.</summary>
    public IReadOnlyList<AttributeSetDecl> Declarations => _declarations;

    /// <summary>The compiled attributes: those of the sets it uses, then its own; null until compiled.</summary>
    public XsltInstruction? Body { get; set; }

    /// <summary>Adds a declaration, keeping the order by precedence (stable for equal precedence).</summary>
    public void Add(AttributeSetDecl declaration)
    {
        int at = _declarations.Count;
        while (at > 0 && _declarations[at - 1].Precedence > declaration.Precedence)
        {
            at--;
        }

        _declarations.Insert(at, declaration);
    }
}

/// <summary>The <c>xsl:attribute-set</c> declarations of a stylesheet, by name.</summary>
internal sealed class AttributeSetTable
{
    private readonly Dictionary<QName, AttributeSetSlot> _slots = new Dictionary<QName, AttributeSetSlot>();

    /// <summary>An empty table.</summary>
    public static AttributeSetTable Empty { get; } = new AttributeSetTable();

    /// <summary>The sets, in no particular order.</summary>
    public IEnumerable<AttributeSetSlot> Slots => _slots.Values;

    /// <summary>Adds a declaration under <paramref name="name"/>.</summary>
    public void Add(QName name, StylesheetElement element, int precedence)
    {
        if (!_slots.TryGetValue(name, out AttributeSetSlot? slot))
        {
            slot = new AttributeSetSlot(name);
            _slots[name] = slot;
        }

        slot.Add(new AttributeSetDecl(element, precedence));
    }

    /// <summary>Finds the set called <paramref name="name"/>.</summary>
    public bool TryGet(QName name, out AttributeSetSlot slot)
    {
        bool found = _slots.TryGetValue(name, out AttributeSetSlot? value);
        slot = value!;
        return found;
    }
}
